using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Windows.Media.Control;
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
        private readonly MediaArtResolver _mediaArtResolver = new();
        private readonly WindowsMediaService _windowsMediaService = new();
        private readonly SemaphoreSlim _scrobbleLock = new(1, 1);
        private readonly SemaphoreSlim _sessionLock = new(1, 1);
        private static readonly ConcurrentDictionary<string, DateTimeOffset> _scrobbledTracksHistory = new(StringComparer.OrdinalIgnoreCase);
        private readonly System.Collections.Generic.HashSet<string> _notifiedNewInstances = new(StringComparer.OrdinalIgnoreCase);

        private CancellationTokenSource? _cts;
        private GlobalSystemMediaTransportControlsSessionManager? _sessionMgr;
        private string? _activeSessionId;

        private string _currentTrack = string.Empty;
        private string _currentArtist = string.Empty;
        private string _currentAlbum = string.Empty;
        private string _currentAppId = string.Empty;
        private GlobalSystemMediaTransportControlsSession? _currentSession;
        private long _trackStartTime;
        private int _elapsedSeconds;
        private bool _hasScrobbledCurrentTrack;
        private bool _isPlaying;
        private string _lastScrobbledSignature = string.Empty;

        public event EventHandler? TrackScrobbled;
        public event EventHandler<NowPlayingInfo?>? NowPlayingChanged;
        public event EventHandler<ScrobbleStatusInfo>? StatusChanged;
        public event EventHandler<string>? NewSourceDetected;

        public NowPlayingInfo? CurrentTrack { get; private set; }
        public ScrobbleStatusInfo CurrentStatus { get; private set; } = new(ScrobbleStatus.Idle);

        public void Start()
        {
            if (_cts != null) return;
            _cts = new CancellationTokenSource();
            _ = InitSessionManagerAsync();
            _ = RunLoopAsync(_cts.Token);
        }

        private async Task InitSessionManagerAsync()
        {
            try
            {
                _sessionMgr = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
                if (_sessionMgr != null)
                {
                    _sessionMgr.CurrentSessionChanged += OnCurrentSessionChanged;
                    _sessionMgr.SessionsChanged += OnSessionsChanged;
                }
            }
            catch
            {
                _sessionMgr = null;
            }
        }

        private void OnCurrentSessionChanged(GlobalSystemMediaTransportControlsSessionManager sender, CurrentSessionChangedEventArgs args)
        {
            _ = ProcessSessionUpdateAsync();
        }

        private void OnSessionsChanged(GlobalSystemMediaTransportControlsSessionManager sender, SessionsChangedEventArgs args)
        {
            _ = ProcessSessionUpdateAsync();
        }

        private async Task RunLoopAsync(CancellationToken token)
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
            while (!token.IsCancellationRequested)
            {
                await ProcessSessionUpdateAsync();

                try
                {
                    if (!await timer.WaitForNextTickAsync(token)) break;
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        private async Task ProcessSessionUpdateAsync()
        {
            await _sessionLock.WaitAsync();
            try
            {
                await TickAsync();
            }
            finally
            {
                _sessionLock.Release();
            }
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

        private static string GetTrackKey(string artist, string track) => $"{artist.Trim().ToLowerInvariant()}|{track.Trim().ToLowerInvariant()}";

        private static bool IsRecentlyScrobbled(string artist, string track, int cooldownSeconds = 60)
        {
            if (string.IsNullOrWhiteSpace(artist) || string.IsNullOrWhiteSpace(track)) return false;
            string key = GetTrackKey(artist, track);
            if (_scrobbledTracksHistory.TryGetValue(key, out var lastTime))
            {
                if (DateTimeOffset.UtcNow - lastTime < TimeSpan.FromSeconds(cooldownSeconds))
                {
                    return true;
                }
            }
            return false;
        }

        private async Task TickAsync()
        {
            try
            {
                if (!_lastFmService.IsLoggedIn())
                {
                    bool wasPlaying = _isPlaying || CurrentTrack != null;
                    _isPlaying = false;
                    if (wasPlaying)
                    {
                        CurrentTrack = null;
                        NowPlayingChanged?.Invoke(this, null);
                        _ = ClearDiscordPresenceAsync();
                    }
                    SetStatus(ScrobbleStatus.Idle);
                    return;
                }

                try
                {
                    if (_sessionMgr == null)
                    {
                        _sessionMgr = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
                        if (_sessionMgr != null)
                        {
                            _sessionMgr.CurrentSessionChanged += OnCurrentSessionChanged;
                            _sessionMgr.SessionsChanged += OnSessionsChanged;
                        }
                    }
                }
                catch
                {
                    _sessionMgr = null;
                }

                if (_sessionMgr == null) return;

                var sessions = _sessionMgr.GetSessions();
                if (sessions != null)
                {
                    var knownSources = _windowsMediaService.GetKnownSources();
                    foreach (var s in sessions)
                    {
                        string sAppId = s.SourceAppUserModelId;
                        if (!string.IsNullOrWhiteSpace(sAppId))
                        {
                            if (!knownSources.Contains(sAppId) && !_notifiedNewInstances.Contains(sAppId))
                            {
                                _notifiedNewInstances.Add(sAppId);
                                string displayName = WindowsMediaService.FormatAppDisplayName(sAppId);
                                NotificationService.ShowNewInstanceNotification(displayName);
                                NewSourceDetected?.Invoke(this, displayName);
                            }
                        }
                    }
                }

                var playingSessions = new List<GlobalSystemMediaTransportControlsSession>();
                if (sessions != null)
                {
                    foreach (var s in sessions)
                    {
                        string id = s.SourceAppUserModelId;
                        if (string.IsNullOrWhiteSpace(id)) continue;

                        if (!_windowsMediaService.IsSourceAllowed(id)) continue;

                        var pb = s.GetPlaybackInfo();
                        if (pb != null && pb.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
                        {
                            playingSessions.Add(s);
                        }
                    }
                }

                GlobalSystemMediaTransportControlsSession? allowedSession = null;

                if (playingSessions.Count == 0)
                {
                    _activeSessionId = null;
                    allowedSession = null;
                }
                else if (playingSessions.Count == 1)
                {
                    allowedSession = playingSessions[0];
                    _activeSessionId = allowedSession.SourceAppUserModelId;
                }
                else
                {
                    if (!string.IsNullOrEmpty(_activeSessionId))
                    {
                        allowedSession = playingSessions.FirstOrDefault(s => string.Equals(s.SourceAppUserModelId, _activeSessionId, StringComparison.OrdinalIgnoreCase));
                    }

                    if (allowedSession == null)
                    {
                        var curr = _sessionMgr.GetCurrentSession();
                        if (curr != null)
                        {
                            allowedSession = playingSessions.FirstOrDefault(s => string.Equals(s.SourceAppUserModelId, curr.SourceAppUserModelId, StringComparison.OrdinalIgnoreCase));
                        }

                        allowedSession ??= playingSessions[0];
                        _activeSessionId = allowedSession.SourceAppUserModelId;
                    }
                }

                if (allowedSession == null)
                {
                    _currentSession = null;
                    await CheckTrackEndedAsync();
                    bool wasPlaying = _isPlaying || CurrentTrack != null;
                    _isPlaying = false;
                    if (wasPlaying)
                    {
                        CurrentTrack = null;
                        NowPlayingChanged?.Invoke(this, null);
                        _ = ClearDiscordPresenceAsync();
                    }
                    SetStatus(ScrobbleStatus.Idle);
                    return;
                }

                var props = await allowedSession.TryGetMediaPropertiesAsync();
                if (props == null || string.IsNullOrWhiteSpace(props.Title))
                {
                    _currentSession = null;
                    await CheckTrackEndedAsync();
                    bool wasPlaying = _isPlaying || CurrentTrack != null;
                    _isPlaying = false;
                    if (wasPlaying)
                    {
                        CurrentTrack = null;
                        NowPlayingChanged?.Invoke(this, null);
                        _ = ClearDiscordPresenceAsync();
                    }
                    SetStatus(ScrobbleStatus.Idle);
                    return;
                }

                _currentSession = allowedSession;
                string appId = allowedSession.SourceAppUserModelId;

                string title = props.Title.Trim();
                string rawArtist = !string.IsNullOrWhiteSpace(props.Artist) ? props.Artist.Trim() : (props.AlbumArtist?.Trim() ?? string.Empty);
                string artist = rawArtist;
                string album = props.AlbumTitle?.Trim() ?? string.Empty;

                if (_windowsMediaService.IsCleanTrackTitlesEnabled())
                {
                    title = WindowsMediaService.CleanTrackTitle(title);
                }

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
                    _hasScrobbledCurrentTrack = IsRecentlyScrobbled(_currentArtist, _currentTrack, 60);
                    _isPlaying = true;

                    CurrentTrack = new NowPlayingInfo(title, artist, album);
                    NowPlayingChanged?.Invoke(this, CurrentTrack);
                    SetStatus(ScrobbleStatus.Listening, _currentTrack, _currentArtist, _currentAlbum);

                    await _lastFmService.UpdateNowPlayingAsync(_currentTrack, _currentArtist, _currentAlbum);
                    _ = UpdateDiscordPresenceAsync(_currentTrack, _currentArtist, _currentAlbum, _trackStartTime);
                }
                else
                {
                    _isPlaying = true;
                    if (CurrentTrack == null)
                    {
                        CurrentTrack = new NowPlayingInfo(_currentTrack, _currentArtist, _currentAlbum);
                        NowPlayingChanged?.Invoke(this, CurrentTrack);
                        SetStatus(ScrobbleStatus.Listening, _currentTrack, _currentArtist, _currentAlbum);
                        _ = UpdateDiscordPresenceAsync(_currentTrack, _currentArtist, _currentAlbum, _trackStartTime);
                    }
                    _elapsedSeconds = (int)(DateTimeOffset.UtcNow.ToUnixTimeSeconds() - _trackStartTime);

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
                _sessionMgr = null;
                LogService.LogError("[Scrobbler Service Error] Background processing failed", ex);
            }
        }

        private async Task ExecuteScrobbleAsync()
        {
            if (_hasScrobbledCurrentTrack || string.IsNullOrEmpty(_currentTrack)) return;

            if (!await _scrobbleLock.WaitAsync(0)) return;

            try
            {
                if (_hasScrobbledCurrentTrack || string.IsNullOrEmpty(_currentTrack)) return;

                if (IsRecentlyScrobbled(_currentArtist, _currentTrack, 60))
                {
                    _hasScrobbledCurrentTrack = true;
                    return;
                }

                string signature = $"{_currentArtist}|{_currentTrack}|{_trackStartTime}";
                if (_lastScrobbledSignature == signature)
                {
                    _hasScrobbledCurrentTrack = true;
                    return;
                }

                _hasScrobbledCurrentTrack = true;
                _lastScrobbledSignature = signature;
                string trackKey = GetTrackKey(_currentArtist, _currentTrack);
                _scrobbledTracksHistory[trackKey] = DateTimeOffset.UtcNow;

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
            finally
            {
                _scrobbleLock.Release();
            }
        }

        private async Task CheckTrackEndedAsync()
        {
            if (_isPlaying && !_hasScrobbledCurrentTrack && !string.IsNullOrEmpty(_currentTrack) && _elapsedSeconds >= 30)
            {
                if (!IsRecentlyScrobbled(_currentArtist, _currentTrack, 60))
                {
                    await ExecuteScrobbleAsync();
                }
                else
                {
                    _hasScrobbledCurrentTrack = true;
                }
            }
        }

        private void ResetStateWithoutScrobble()
        {
            bool wasPlaying = !string.IsNullOrEmpty(_currentTrack);
            _currentTrack = string.Empty;
            _currentArtist = string.Empty;
            _currentAlbum = string.Empty;
            _currentSession = null;
            _activeSessionId = null;
            _hasScrobbledCurrentTrack = true;
            _elapsedSeconds = 0;
            _isPlaying = false;

            SetStatus(ScrobbleStatus.Idle);

            if (wasPlaying)
            {
                CurrentTrack = null;
                NowPlayingChanged?.Invoke(this, null);
                _ = ClearDiscordPresenceAsync();
            }
        }

        public async Task SyncDiscordPresenceAsync()
        {
            LogService.LogInfo($"[Discord RPC] Sync requested: enabled={SettingsService.GetSetting("DiscordRichPresence")}, track={CurrentTrack?.Track}");
            if (SettingsService.GetSetting("DiscordRichPresence") == "true")
            {
                if (CurrentTrack != null)
                {
                    await UpdateDiscordPresenceAsync(CurrentTrack.Track, CurrentTrack.Artist, CurrentTrack.Album, _trackStartTime);
                }
                else
                {
                    LogService.LogInfo("[Discord RPC] Sync: no active track playing right now");
                }
            }
            else
            {
                await ClearDiscordPresenceAsync();
            }
        }

        private async Task UpdateDiscordPresenceAsync(string track, string artist, string album, long fallbackStart)
        {
            try
            {
                string? val = SettingsService.GetSetting("DiscordRichPresence");
                LogService.LogInfo($"[Discord RPC] UpdatePresence: setting={val}, track='{track}', artist='{artist}'");
                if (val != "true") return;

                long start = fallbackStart;
                long? end = null;

                if (_currentSession != null)
                {
                    try
                    {
                        var tl = _currentSession.GetTimelineProperties();
                        if (tl != null && tl.EndTime > TimeSpan.Zero)
                        {
                            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                            long pos = (long)tl.Position.TotalSeconds;
                            long total = (long)tl.EndTime.TotalSeconds;
                            if (total > pos)
                            {
                                start = now - pos;
                                end = start + total;
                            }
                        }
                    }
                    catch
                    {
                    }
                }

                string? art = await _mediaArtResolver.ResolveRemoteArtUrlAsync(artist, album, track);
                LogService.LogInfo($"[Discord RPC] Track art URL: '{art ?? "none"}', start={start}, end={end}");
                await DiscordRpcService.Instance.UpdateActivityAsync(track, artist, album, art, start, end);
            }
            catch (Exception ex)
            {
                LogService.LogError("[Discord RPC Error] Failed to update presence", ex);
            }
        }

        private async Task ClearDiscordPresenceAsync()
        {
            try
            {
                LogService.LogInfo("[Discord RPC] ClearPresence called");
                if (DiscordRpcService.Instance.IsConnected)
                {
                    await DiscordRpcService.Instance.ClearActivityAsync();
                }
            }
            catch (Exception ex)
            {
                LogService.LogError("[Discord RPC Error] Failed to clear presence", ex);
            }
        }
    }
}
