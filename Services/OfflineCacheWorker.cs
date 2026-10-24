using System;
using System.Collections.Generic;
using System.Linq;
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
        private readonly int[] _backoffIntervals = { 60, 300, 900, 3600 };
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

        private OfflineCacheWorker()
        {
            NetworkInformation.NetworkStatusChanged += NetworkInformation_NetworkStatusChanged;
        }

        public void Start()
        {
            if (_timer != null) return;

            _timer = new System.Timers.Timer();
            SetTimerInterval();
            _timer.Elapsed += Timer_Elapsed;
            _timer.Start();


            _ = ProcessCacheAsync();
        }

        private void SetTimerInterval()
        {
            if (_timer != null)
            {
                int intervalSeconds = _backoffIntervals[_currentBackoffLevel];
                _timer.Interval = TimeSpan.FromSeconds(intervalSeconds).TotalMilliseconds;
            }
        }

        private async void NetworkInformation_NetworkStatusChanged(object sender)
        {
            var profile = NetworkInformation.GetInternetConnectionProfile();
            if (profile != null && profile.GetNetworkConnectivityLevel() == NetworkConnectivityLevel.InternetAccess)
            {
                _currentBackoffLevel = 0;
                SetTimerInterval();
                await ProcessCacheAsync();
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
            await ProcessCacheAsync(waitForLock: true);
        }

        public async Task TriggerOfflineModeAsync()
        {
            OfflineMode = true;
            await UpdateCacheCountAsync();
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
                return;
            }

            try
            {
                while (true)
                {
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
