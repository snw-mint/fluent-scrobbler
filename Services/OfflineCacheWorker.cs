using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using System.Threading;
using Windows.Networking.Connectivity;

namespace FluentScrobbler.Services
{
    public class OfflineCacheWorker
    {
        private static readonly Lazy<OfflineCacheWorker> _instance = new(() => new OfflineCacheWorker());
        public static OfflineCacheWorker Instance => _instance.Value;
        private readonly OfflineCacheService _cacheService = OfflineCacheService.Instance;
        private readonly LastFmService _lastFmService = new();
        private System.Timers.Timer? _timer;
        private readonly SemaphoreSlim _processLock = new SemaphoreSlim(1, 1);
        private int _currentBackoffLevel = 0;
        private readonly int[] _backoffIntervals = { 15, 60, 180, 600 };
        private volatile bool _retryRequested;
        private int _networkChangeVersion;
        private bool _offlineMode;
        public bool OfflineMode
        {
            get => _offlineMode;
            private set
            {
                if (_offlineMode != value)
                {
                    _offlineMode = value;
                    OfflineModeChanged?.Invoke(this, _offlineMode);
                }
            }
        }

        public event EventHandler<bool>? OfflineModeChanged;
        public event EventHandler<int>? CacheCountChanged;
        public event EventHandler? CacheSynced;

        private OfflineCacheWorker()
        {
            NetworkInformation.NetworkStatusChanged += NetworkInformation_NetworkStatusChanged;
            NetworkChange.NetworkAddressChanged += NetworkChange_NetworkAddressChanged;
        }

        public void Start()
        {
            if (_timer != null) return;

            _timer = new System.Timers.Timer();
            SetTimerInterval();
            _timer.Elapsed += Timer_Elapsed;
            _timer.Start();

            _ = InitialBootSyncAsync();
        }

        private async Task InitialBootSyncAsync()
        {
            await Task.Delay(3000);
            await ProcessCacheAsync();
        }

        private void SetTimerInterval()
        {
            if (_timer != null)
            {
                int intervalSeconds = _backoffIntervals[_currentBackoffLevel];
                _timer.Interval = TimeSpan.FromSeconds(intervalSeconds).TotalMilliseconds;
            }
        }

        private void NetworkChange_NetworkAddressChanged(object? sender, EventArgs e)
        {
            HandleNetworkChange();
        }

        private void NetworkInformation_NetworkStatusChanged(object sender)
        {
            HandleNetworkChange();
        }

        private async void HandleNetworkChange()
        {
            int version = Interlocked.Increment(ref _networkChangeVersion);
            await Task.Delay(2500);
            if (version != Volatile.Read(ref _networkChangeVersion)) return;

            if (IsInternetAvailable())
            {
                _currentBackoffLevel = 0;
                SetTimerInterval();
                _retryRequested = true;
                await ProcessCacheAsync(waitForLock: true);
            }
        }

        private static bool IsInternetAvailable()
        {
            try
            {
                var profile = NetworkInformation.GetInternetConnectionProfile();
                if (profile != null)
                {
                    var level = profile.GetNetworkConnectivityLevel();
                    if (level == NetworkConnectivityLevel.InternetAccess || level == NetworkConnectivityLevel.ConstrainedInternetAccess)
                    {
                        return true;
                    }
                }
                return NetworkInterface.GetIsNetworkAvailable();
            }
            catch
            {
                return true;
            }
        }

        private async void Timer_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
        {
            await ProcessCacheAsync();
        }

        public async Task ForceSyncAsync()
        {
            _currentBackoffLevel = 0;
            SetTimerInterval();
            _retryRequested = true;
            await ProcessCacheAsync(waitForLock: true);
        }

        public async Task TriggerOfflineModeAsync()
        {
            OfflineMode = true;
            _currentBackoffLevel = 0;
            SetTimerInterval();
            _timer?.Stop();
            _timer?.Start();
            await UpdateCacheCountAsync();
        }

        public void NotifyNetworkSuccess()
        {
            if (_offlineMode || _currentBackoffLevel > 0)
            {
                _currentBackoffLevel = 0;
                SetTimerInterval();
                _retryRequested = true;
                _ = ProcessCacheAsync();
            }
        }

        public async Task UpdateCacheCountAsync()
        {
            int count = await _cacheService.GetPendingCountAsync();
            CacheCountChanged?.Invoke(this, count);

            if (count == 0 && OfflineMode)
            {
                OfflineMode = false;
                _currentBackoffLevel = 0;
                SetTimerInterval();
            }
        }

        private async Task ProcessCacheAsync(bool waitForLock = false)
        {
            if (waitForLock)
            {
                await _processLock.WaitAsync();
            }
            else if (!_processLock.Wait(0))
            {
                _retryRequested = true;
                return;
            }

            bool anyBatchSynced = false;

            try
            {
                while (true)
                {
                    _retryRequested = false;

                    long cutoff = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 1209600;
                    await _cacheService.PurgeExpiredScrobblesAsync(cutoff);

                    int count = await _cacheService.GetPendingCountAsync();
                    CacheCountChanged?.Invoke(this, count);

                    if (count == 0)
                    {
                        OfflineMode = false;
                        _currentBackoffLevel = 0;
                        SetTimerInterval();
                        break;
                    }

                    OfflineMode = true;

                    var pendingScrobbles = await _cacheService.GetPendingScrobblesAsync(50);
                    if (!pendingScrobbles.Any()) break;

                    var uniqueEntries = new List<FluentScrobbler.Models.ScrobbleEntry>();
                    var invalidOrDuplicateIds = new List<int>();
                    var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    foreach (var s in pendingScrobbles)
                    {
                        if (string.IsNullOrWhiteSpace(s.Artist) || string.IsNullOrWhiteSpace(s.Track) || s.Timestamp < cutoff)
                        {
                            invalidOrDuplicateIds.Add(s.Id);
                            continue;
                        }

                        string key = $"{s.Artist.Trim().ToLowerInvariant()}|{s.Track.Trim().ToLowerInvariant()}|{s.Timestamp / 60}";
                        if (seen.Add(key))
                        {
                            uniqueEntries.Add(s);
                        }
                        else
                        {
                            invalidOrDuplicateIds.Add(s.Id);
                        }
                    }

                    if (invalidOrDuplicateIds.Count > 0)
                    {
                        await _cacheService.RemoveScrobblesAsync(invalidOrDuplicateIds);
                    }

                    if (uniqueEntries.Count == 0)
                    {
                        await UpdateCacheCountAsync();
                        continue;
                    }

                    var batchResult = await _lastFmService.ScrobbleBatchAsync(uniqueEntries);

                    if (batchResult == BatchScrobbleResult.Success)
                    {
                        anyBatchSynced = true;
                        var ids = uniqueEntries.Select(s => s.Id).ToList();
                        await _cacheService.RemoveScrobblesAsync(ids);

                        _currentBackoffLevel = 0;
                        SetTimerInterval();

                        await UpdateCacheCountAsync();

                        int remaining = await _cacheService.GetPendingCountAsync();
                        if (remaining > 0)
                        {
                            await Task.Delay(1000);
                            continue;
                        }
                        break;
                    }
                    else if (batchResult == BatchScrobbleResult.PermanentError)
                    {
                        LogService.LogWarning("[OfflineWorker] Permanent error encountered in batch. Discarding invalid entries to unblock queue.");
                        var ids = uniqueEntries.Select(s => s.Id).ToList();
                        await _cacheService.RemoveScrobblesAsync(ids);
                        await UpdateCacheCountAsync();
                        continue;
                    }
                    else
                    {
                        if (_retryRequested)
                        {
                            _currentBackoffLevel = 0;
                            SetTimerInterval();
                            continue;
                        }
                        IncreaseBackoff();
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                LogService.LogError("[OfflineWorker Error] Failed to process cache", ex);
                IncreaseBackoff();
            }
            finally
            {
                _processLock.Release();
            }

            if (anyBatchSynced)
            {
                CacheSynced?.Invoke(this, EventArgs.Empty);
            }
        }

        private void IncreaseBackoff()
        {
            if (_currentBackoffLevel < _backoffIntervals.Length - 1)
            {
                _currentBackoffLevel++;
                SetTimerInterval();
            }
        }
    }
}
