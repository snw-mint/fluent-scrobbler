using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.Networking.Connectivity;

namespace FluentScrobbler.Services
{
    public class OfflineCacheWorker
    {
        private static OfflineCacheWorker? _instance;
        public static OfflineCacheWorker Instance => _instance ??= new OfflineCacheWorker();
        private readonly OfflineCacheService _cacheService = OfflineCacheService.Instance;
        private readonly LastFmService _lastFmService = new();
        private System.Timers.Timer? _timer;
        private bool _isProcessing = false;
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
            await ProcessCacheAsync();
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

        private async Task ProcessCacheAsync()
        {
            if (_isProcessing) return;
            _isProcessing = true;

            try
            {
                int count = await _cacheService.GetPendingCountAsync();
                CacheCountChanged?.Invoke(this, count);

                if (count == 0)
                {
                    OfflineMode = false;
                    _currentBackoffLevel = 0;
                    SetTimerInterval();
                    return;
                }

                OfflineMode = true;

                
                var profile = NetworkInformation.GetInternetConnectionProfile();
                if (profile == null || profile.GetNetworkConnectivityLevel() != NetworkConnectivityLevel.InternetAccess)
                {
                    
                    IncreaseBackoff();
                    return;
                }

                
                var pendingScrobbles = await _cacheService.GetPendingScrobblesAsync(50);
                if (!pendingScrobbles.Any()) return;

                bool batchSuccess = await _lastFmService.ScrobbleBatchAsync(pendingScrobbles);
                
                if (batchSuccess)
                {
                    var ids = pendingScrobbles.Select(s => s.Id).ToList();
                    await _cacheService.RemoveScrobblesAsync(ids);
                    
                    _currentBackoffLevel = 0;
                    SetTimerInterval();
                    
                    await UpdateCacheCountAsync();

                    
                    int remaining = await _cacheService.GetPendingCountAsync();
                    if (remaining > 0)
                    {
                        
                        await Task.Delay(1000);
                        _isProcessing = false;
                        await ProcessCacheAsync();
                        return; 
                    }
                }
                else
                {
                    IncreaseBackoff();
                }
            }
            catch (Exception ex)
            {
                LogService.LogError("[OfflineWorker Error] Failed to process cache", ex);
                IncreaseBackoff();
            }
            finally
            {
                _isProcessing = false;
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
