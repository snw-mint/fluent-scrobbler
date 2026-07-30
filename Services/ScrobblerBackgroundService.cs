using System;
using System.Threading.Tasks;
using Windows.Media.Control;
using Microsoft.UI.Xaml;
using FluentScrobbler.Services.Media;

namespace FluentScrobbler.Services
{
    public class ScrobblerBackgroundService
    {
        private static ScrobblerBackgroundService? _instance;
        public static ScrobblerBackgroundService Instance => _instance ??= new ScrobblerBackgroundService();

        private readonly LastFmService _lastFmService = new();
        private readonly WindowsMediaService _windowsMediaService = new();
        private DispatcherTimer? _timer;

        private string _currentTrack = string.Empty;
        private string _currentArtist = string.Empty;
        private string _currentAlbum = string.Empty;
        private string _currentAppId = string.Empty;
        private long _trackStartTime;
        private int _elapsedSeconds;
        private bool _hasScrobbledCurrentTrack;
        private bool _isPlaying;

        public void Start()
        {
            if (_timer != null) return;

            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromSeconds(2);
            _timer.Tick += Timer_Tick;
            _timer.Start();
        }

        private async void Timer_Tick(object? sender, object e)
        {
            if (!_lastFmService.IsLoggedIn()) return;

            try
            {
                var manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
                var session = manager?.GetCurrentSession();

                if (session == null)
                {
                    await CheckTrackEndedAsync();
                    return;
                }

                string appId = session.SourceAppUserModelId;
                if (!_windowsMediaService.IsSourceAllowed(appId))
                {
                    await CheckTrackEndedAsync();
                    return;
                }

                var playbackInfo = session.GetPlaybackInfo();
                bool isCurrentlyPlaying = playbackInfo != null && playbackInfo.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;

                if (!isCurrentlyPlaying)
                {
                    _isPlaying = false;
                    return;
                }

                var props = await session.TryGetMediaPropertiesAsync();
                if (props == null || string.IsNullOrWhiteSpace(props.Title))
                {
                    await CheckTrackEndedAsync();
                    return;
                }

                string title = props.Title.Trim();
                string artist = props.Artist?.Trim() ?? string.Empty;
                string album = props.AlbumTitle?.Trim() ?? string.Empty;

                if (_windowsMediaService.IsPrimaryArtistOnlyEnabled())
                {
                    artist = WindowsMediaService.FormatPrimaryArtist(artist);
                }

                if (title != _currentTrack || artist != _currentArtist)
                {
                    await CheckTrackEndedAsync();

                    _currentTrack = title;
                    _currentArtist = artist;
                    _currentAlbum = album;
                    _currentAppId = appId;
                    _trackStartTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                    _elapsedSeconds = 0;
                    _hasScrobbledCurrentTrack = false;
                    _isPlaying = true;

                    await _lastFmService.UpdateNowPlayingAsync(_currentTrack, _currentArtist, _currentAlbum);
                }
                else
                {
                    _isPlaying = true;
                    _elapsedSeconds += 2;

                    int minLength = _windowsMediaService.GetMinimumTrackLengthSeconds();
                    int thresholdPct = _windowsMediaService.GetScrobblePercentageThreshold();
                    int maxSeconds = _windowsMediaService.GetMaximumTimeThresholdSeconds();

                    if (!_hasScrobbledCurrentTrack && _elapsedSeconds >= minLength)
                    {
                        if (_elapsedSeconds >= maxSeconds || _elapsedSeconds >= 120)
                        {
                            _hasScrobbledCurrentTrack = true;
                            await _lastFmService.ScrobbleTrackAsync(_currentTrack, _currentArtist, _currentAlbum, _trackStartTime);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ScrobblerBackgroundService] Erro: {ex.Message}");
            }
        }

        private async Task CheckTrackEndedAsync()
        {
            if (_isPlaying && !_hasScrobbledCurrentTrack && !string.IsNullOrEmpty(_currentTrack) && _elapsedSeconds >= 30)
            {
                _hasScrobbledCurrentTrack = true;
                await _lastFmService.ScrobbleTrackAsync(_currentTrack, _currentArtist, _currentAlbum, _trackStartTime);
            }

            _currentTrack = string.Empty;
            _currentArtist = string.Empty;
            _currentAlbum = string.Empty;
            _isPlaying = false;
        }
    }
}
