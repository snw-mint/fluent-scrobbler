using System;
using System.Threading.Tasks;
using Windows.Media.Control;
using Microsoft.UI.Xaml;
using FluentScrobbler.Services.Media;

namespace FluentScrobbler.Services
{
    public record NowPlayingInfo(string Track, string Artist, string Album);

    public enum ScrobbleStatus
    {
        Idle,
        Listening,
        Sent,
        Error
    }

    public record ScrobbleStatusInfo(ScrobbleStatus Status, string? Track = null, string? Artist = null, string? Album = null);

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
        private bool _isProcessing;
        private string _lastScrobbledSignature = string.Empty;

        public event EventHandler? TrackScrobbled;
        public event EventHandler<NowPlayingInfo?>? NowPlayingChanged;
        public event EventHandler<ScrobbleStatusInfo>? StatusChanged;

        public NowPlayingInfo? CurrentTrack { get; private set; }
        public ScrobbleStatusInfo CurrentStatus { get; private set; } = new(ScrobbleStatus.Idle);

        public void Start()
        {
            if (_timer != null) return;
            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromSeconds(2);
            _timer.Tick += Timer_Tick;
            _timer.Start();
        }

        private void SetStatus(ScrobbleStatus status, string? track = null, string? artist = null, string? album = null)
        {
            var newStatus = new ScrobbleStatusInfo(status, track, artist, album);
            if (CurrentStatus != newStatus)
            {
                CurrentStatus = newStatus;
                StatusChanged?.Invoke(this, newStatus);
            }
        }

        private async void Timer_Tick(object? sender, object e)
        {
            if (_isProcessing) return;
            _isProcessing = true;

            try
            {
                if (!_lastFmService.IsLoggedIn())
                {
                    SetStatus(ScrobbleStatus.Idle);
                    return;
                }

                var manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
                var sessions = manager?.GetSessions();
                GlobalSystemMediaTransportControlsSession? allowedSession = null;

                if (sessions != null)
                {
                    foreach (var s in sessions)
                    {
                        if (_windowsMediaService.IsSourceAllowed(s.SourceAppUserModelId))
                        {
                            allowedSession = s;
                            break;
                        }
                    }
                }

                if (allowedSession == null)
                {
                    await CheckTrackEndedAsync();
                    ResetStateWithoutScrobble();
                    return;
                }

                string appId = allowedSession.SourceAppUserModelId;
                var playbackInfo = allowedSession.GetPlaybackInfo();
                bool isCurrentlyPlaying = playbackInfo != null && playbackInfo.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;

                if (!isCurrentlyPlaying)
                {
                    _isPlaying = false;
                    SetStatus(ScrobbleStatus.Idle);
                    return;
                }

                var props = await allowedSession.TryGetMediaPropertiesAsync();
                if (props == null || string.IsNullOrWhiteSpace(props.Title))
                {
                    await CheckTrackEndedAsync();
                    ResetStateWithoutScrobble();
                    return;
                }

                string title = props.Title.Trim();
                string rawArtist = !string.IsNullOrWhiteSpace(props.Artist) ? props.Artist.Trim() : (props.AlbumArtist?.Trim() ?? string.Empty);
                string artist = rawArtist;
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

                    CurrentTrack = new NowPlayingInfo(title, artist, album);
                    NowPlayingChanged?.Invoke(this, CurrentTrack);
                    SetStatus(ScrobbleStatus.Listening, _currentTrack, _currentArtist, _currentAlbum);

                    await _lastFmService.UpdateNowPlayingAsync(_currentTrack, _currentArtist, _currentAlbum);
                }
                else
                {
                    _isPlaying = true;
                    _elapsedSeconds += 2;

                    int minLength = _windowsMediaService.GetMinimumTrackLengthSeconds();
                    int maxSeconds = _windowsMediaService.GetMaximumTimeThresholdSeconds();
                    if (!_hasScrobbledCurrentTrack && _elapsedSeconds >= minLength)
                    {
                        if (_elapsedSeconds >= maxSeconds || _elapsedSeconds >= 30)
                        {
                            await ExecuteScrobbleAsync();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogService.LogError("[Scrobbler Service Error] Background processing failed", ex);
            }
            finally
            {
                _isProcessing = false;
            }
        }

        private async Task ExecuteScrobbleAsync()
        {
            if (_hasScrobbledCurrentTrack || string.IsNullOrEmpty(_currentTrack)) return;

            string signature = $"{_currentArtist}|{_currentTrack}|{_trackStartTime}";
            if (_lastScrobbledSignature == signature)
            {
                _hasScrobbledCurrentTrack = true;
                return;
            }

            _hasScrobbledCurrentTrack = true;
            _lastScrobbledSignature = signature;

            bool success = false;
            try
            {
                success = await _lastFmService.ScrobbleTrackAsync(_currentTrack, _currentArtist, _currentAlbum, _trackStartTime);
            }
            catch (Exception ex) when (ex is System.Net.Http.HttpRequestException || ex is TaskCanceledException)
            {
                LogService.LogError("[Network Error] Scrobble failed due to network, queueing offline.", ex);
            }
            catch (Exception ex)
            {
                LogService.LogError("[API Error] Scrobble failed.", ex);
            }

            if (success)
            {
                SetStatus(ScrobbleStatus.Sent, _currentTrack, _currentArtist, _currentAlbum);
                TrackScrobbled?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                SetStatus(ScrobbleStatus.Error, _currentTrack, _currentArtist, _currentAlbum);
                await OfflineCacheService.Instance.AddScrobbleAsync(_currentTrack, _currentArtist, _currentAlbum, _trackStartTime);
                await OfflineCacheWorker.Instance.TriggerOfflineModeAsync();
            }
        }

        private async Task CheckTrackEndedAsync()
        {
            if (_isPlaying && !_hasScrobbledCurrentTrack && !string.IsNullOrEmpty(_currentTrack) && _elapsedSeconds >= 30)
            {
                await ExecuteScrobbleAsync();
            }
        }

        private void ResetStateWithoutScrobble()
        {
            bool wasPlaying = !string.IsNullOrEmpty(_currentTrack);
            _currentTrack = string.Empty;
            _currentArtist = string.Empty;
            _currentAlbum = string.Empty;
            _hasScrobbledCurrentTrack = true;
            _elapsedSeconds = 0;
            _isPlaying = false;

            SetStatus(ScrobbleStatus.Idle);

            if (wasPlaying)
            {
                CurrentTrack = null;
                NowPlayingChanged?.Invoke(this, null);
            }
        }
    }
}