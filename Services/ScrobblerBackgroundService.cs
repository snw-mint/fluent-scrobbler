using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Windows.Media.Control;
using FluentScrobbler.Models;
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

        private readonly IScrobbleProvider[] _providers = new IScrobbleProvider[] { new LastFmService() };
        private readonly MediaArtResolver _mediaArtResolver = new();
        private readonly WindowsMediaService _windowsMediaService = new();
        private readonly ILegacyPlayerWatcher _legacyPlayerWatcher = LegacyPlayerWatcher.Instance;
        private readonly SemaphoreSlim _scrobbleLock = new(1, 1);
        private readonly SemaphoreSlim _sessionLock = new(1, 1);
        private static readonly ConcurrentDictionary<string, DateTimeOffset> _scrobbledTracksHistory = new(StringComparer.OrdinalIgnoreCase);
        private readonly System.Collections.Generic.HashSet<string> _notifiedNewInstances = new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, GlobalSystemMediaTransportControlsSessionPlaybackStatus> _lastPlaybackStatus = new(StringComparer.OrdinalIgnoreCase);

        private CancellationTokenSource? _cts;
        private GlobalSystemMediaTransportControlsSessionManager? _sessionMgr;
        private string? _activeSessionId;

        private string _currentTrack = string.Empty;
        private string _currentArtist = string.Empty;
        private string _currentAlbum = string.Empty;
        private string _currentAppId = string.Empty;
        private GlobalSystemMediaTransportControlsSession? _currentSession;
        private long _trackStartTime;
        private TimeSpan? _currentTrackDuration;
        private double _listenedSeconds;
        private DateTimeOffset? _lastPlayTickTime;
        private bool _hasScrobbledCurrentTrack;
        private bool _nowPlayingSent;
        private DateTimeOffset _lastNowPlayingAttempt = DateTimeOffset.MinValue;
        private bool _isPlaying;
        private string _lastScrobbledSignature = string.Empty;
        private TimeSpan? _lastTimelinePosition;

        public event EventHandler? TrackScrobbled;
        public event EventHandler<NowPlayingInfo?>? NowPlayingChanged;
        public event EventHandler<ScrobbleStatusInfo>? StatusChanged;
        public event EventHandler<string>? NewSourceDetected;

        public NowPlayingInfo? CurrentTrack { get; private set; }
        public ScrobbleStatusInfo CurrentStatus { get; private set; } = new(ScrobbleStatus.Idle);

        public void ForgetSource(string appId)
        {
            if (string.IsNullOrWhiteSpace(appId)) return;
            lock (_notifiedNewInstances)
            {
                _notifiedNewInstances.Remove(appId);
            }

            if (string.Equals(_activeSessionId, appId, StringComparison.OrdinalIgnoreCase))
            {
                _activeSessionId = null;
            }

            if (string.Equals(_currentAppId, appId, StringComparison.OrdinalIgnoreCase))
            {
                _currentAppId = string.Empty;
                _currentTrackDuration = null;
                _lastPlayTickTime = null;
                _listenedSeconds = 0;
                _isPlaying = false;
                CurrentTrack = null;
                NowPlayingChanged?.Invoke(this, null);
                _ = ClearDiscordPresenceAsync();
                SetStatus(ScrobbleStatus.Idle);
            }

            try
            {
                if (_sessionMgr != null)
                {
                    var sessions = _sessionMgr.GetSessions();
                    var match = sessions?.FirstOrDefault(s => string.Equals(s.SourceAppUserModelId, appId, StringComparison.OrdinalIgnoreCase));
                    if (match != null)
                    {
                        var pb = match.GetPlaybackInfo();
                        if (pb != null)
                        {
                            _lastPlaybackStatus[appId] = pb.PlaybackStatus;
                            return;
                        }
                    }
                }
            }
            catch
            {
            }

            _lastPlaybackStatus.TryRemove(appId, out _);
        }

        public void Start()
        {
            if (_cts != null) return;
            _cts = new CancellationTokenSource();

            _legacyPlayerWatcher.TrackChanged += OnLegacyTrackChanged;
            if (SettingsService.IsLegacyPlayersEnabled())
            {
                _legacyPlayerWatcher.Start();
            }

            _ = EnsureSessionManagerAsync();
            _ = RunLoopAsync(_cts.Token);
        }

        private void OnLegacyTrackChanged(object? sender, LegacyTrackInfo info)
        {
            if (info.State == LegacyPlaybackState.Playing && !string.IsNullOrWhiteSpace(info.SourceApp))
            {
                RegisterLegacySourceIfNew(info.SourceApp, info.DisplayName);
            }
            _ = ProcessSessionUpdateAsync();
        }

        private async Task EnsureSessionManagerAsync()
        {
            if (_sessionMgr != null) return;

            try
            {
                var mgr = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
                if (mgr != null)
                {
                    mgr.CurrentSessionChanged += OnCurrentSessionChanged;
                    mgr.SessionsChanged += OnSessionsChanged;
                    _sessionMgr = mgr;
                }
            }
            catch
            {
                _sessionMgr = null;
            }
        }

        private void UnsubscribeSessionManager()
        {
            if (_sessionMgr != null)
            {
                try
                {
                    _sessionMgr.CurrentSessionChanged -= OnCurrentSessionChanged;
                    _sessionMgr.SessionsChanged -= OnSessionsChanged;
                }
                catch
                {
                }
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

        private static bool IsRecentlyScrobbled(string artist, string track, int cooldownSeconds = 10)
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
                bool anyLoggedIn = false;
                for (int i = 0; i < _providers.Length; i++)
                {
                    if (_providers[i].IsLoggedIn())
                    {
                        anyLoggedIn = true;
                        break;
                    }
                }

                if (!anyLoggedIn)
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

                await EnsureSessionManagerAsync();
                if (_sessionMgr == null) return;

                IReadOnlyList<GlobalSystemMediaTransportControlsSession>? sessions = null;
                try
                {
                    sessions = _sessionMgr.GetSessions();
                }
                catch (Exception ex)
                {
                    LogService.LogError("[SMTC Error] Failed to get sessions, resetting session manager", ex);
                    UnsubscribeSessionManager();
                    return;
                }
                var playingSessions = new List<GlobalSystemMediaTransportControlsSession>();
                if (sessions != null)
                {
                    var knownSources = _windowsMediaService.GetKnownSources();
                    foreach (var s in sessions)
                    {
                        string sAppId = s.SourceAppUserModelId;
                        if (string.IsNullOrWhiteSpace(sAppId)) continue;

                        GlobalSystemMediaTransportControlsSessionPlaybackInfo? pb = null;
                        try
                        {
                            pb = s.GetPlaybackInfo();
                        }
                        catch
                        {
                        }

                        var currentStatus = pb?.PlaybackStatus;
                        bool hasPrevStatus = _lastPlaybackStatus.TryGetValue(sAppId, out var prevStatus);
                        if (currentStatus.HasValue)
                        {
                            _lastPlaybackStatus[sAppId] = currentStatus.Value;
                        }

                        bool isPlaying = currentStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
                        bool userGavePlay = isPlaying && (!hasPrevStatus || prevStatus != GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing);

                        if (isPlaying && !knownSources.Any(k => string.Equals(k, sAppId, StringComparison.OrdinalIgnoreCase)))
                        {
                            bool shouldRegister = false;
                            lock (_notifiedNewInstances)
                            {
                                if (userGavePlay && !_notifiedNewInstances.Contains(sAppId))
                                {
                                    _notifiedNewInstances.Add(sAppId);
                                    shouldRegister = true;
                                }
                            }

                            if (shouldRegister)
                            {
                                string displayName = WindowsMediaService.FormatAppDisplayName(sAppId);
                                _windowsMediaService.RegisterKnownSource(sAppId, displayName);
                                NotificationService.ShowNewInstanceNotification(displayName);
                                NewSourceDetected?.Invoke(this, displayName);
                            }
                        }

                        if (_windowsMediaService.IsSourceAllowed(sAppId) && isPlaying)
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
                    if (_legacyPlayerWatcher.IsRunning && _legacyPlayerWatcher.CurrentTrack is { State: LegacyPlaybackState.Playing } legacyTrack && legacyTrack.IsValid)
                    {
                        string legacyAppId = !string.IsNullOrWhiteSpace(legacyTrack.SourceApp) ? legacyTrack.SourceApp : "winamp.exe";
                        RegisterLegacySourceIfNew(legacyAppId, legacyTrack.DisplayName);

                        if (!_windowsMediaService.IsSourceAllowed(legacyAppId))
                        {
                            await SetIdleStateAsync();
                            return;
                        }

                        await ProcessTrackAsync(legacyTrack.Title, legacyTrack.Artist, string.Empty, legacyAppId, null, legacyTrack.Position, legacyTrack.Duration);
                        return;
                    }

                    await SetIdleStateAsync();
                    return;
                }

                var props = await allowedSession.TryGetMediaPropertiesAsync();
                if (props == null || string.IsNullOrWhiteSpace(props.Title))
                {
                    if (_legacyPlayerWatcher.IsRunning && _legacyPlayerWatcher.CurrentTrack is { State: LegacyPlaybackState.Playing } legacyTrack && legacyTrack.IsValid)
                    {
                        string legacyAppId = !string.IsNullOrWhiteSpace(legacyTrack.SourceApp) ? legacyTrack.SourceApp : "winamp.exe";
                        RegisterLegacySourceIfNew(legacyAppId, legacyTrack.DisplayName);

                        if (!_windowsMediaService.IsSourceAllowed(legacyAppId))
                        {
                            await SetIdleStateAsync();
                            return;
                        }

                        await ProcessTrackAsync(legacyTrack.Title, legacyTrack.Artist, string.Empty, legacyAppId, null, legacyTrack.Position, legacyTrack.Duration);
                        return;
                    }

                    await SetIdleStateAsync();
                    return;
                }

                string smtcAppId = allowedSession.SourceAppUserModelId;
                string smtcArtist = !string.IsNullOrWhiteSpace(props.Artist) ? props.Artist.Trim() : (props.AlbumArtist?.Trim() ?? string.Empty);
                string smtcAlbum = props.AlbumTitle?.Trim() ?? string.Empty;
                (smtcArtist, smtcAlbum) = WindowsMediaService.SplitAppleMusicArtistAlbum(smtcAppId, smtcArtist, smtcAlbum);

                await ProcessTrackAsync(props.Title, smtcArtist, smtcAlbum, smtcAppId, allowedSession);
            }
            catch (Exception ex)
            {
                LogService.LogError("[Scrobbler Service Error] Background processing failed", ex);
            }
        }

        private void RegisterLegacySourceIfNew(string appId, string? displayName)
        {
            try
            {
                var knownSources = _windowsMediaService.GetKnownSources();
                bool shouldNotify = false;
                lock (_notifiedNewInstances)
                {
                    if (!knownSources.Any(k => string.Equals(k, appId, StringComparison.OrdinalIgnoreCase)) && !_notifiedNewInstances.Contains(appId))
                    {
                        _notifiedNewInstances.Add(appId);
                        shouldNotify = true;
                    }
                }

                if (shouldNotify)
                {
                    string name = !string.IsNullOrWhiteSpace(displayName)
                        ? displayName
                        : WindowsMediaService.FormatAppDisplayName(appId);
                    _windowsMediaService.RegisterKnownSource(appId, name);
                    NotificationService.ShowNewInstanceNotification(name);
                    NewSourceDetected?.Invoke(this, name);
                }
            }
            catch (Exception ex)
            {
                LogService.LogError("[Legacy Source Registration] Failed to register source", ex);
            }
        }

        private async Task ProcessTrackAsync(string rawTitle, string rawArtist, string rawAlbum, string appId, GlobalSystemMediaTransportControlsSession? session, TimeSpan? legacyPosition = null, TimeSpan? legacyDuration = null)
        {
            if (!_windowsMediaService.IsSourceAllowed(appId))
            {
                await SetIdleStateAsync();
                return;
            }

            _currentSession = session;
            string title = rawTitle.Trim();
            string artist = rawArtist;
            string album = rawAlbum.Trim();

            if (_windowsMediaService.IsCleanTrackTitlesEnabled())
            {
                title = WindowsMediaService.CleanTrackTitle(title);
            }

            if (_windowsMediaService.IsPrimaryArtistOnlyEnabled())
            {
                artist = WindowsMediaService.FormatPrimaryArtist(artist);
            }

            TimeSpan? currentPosition = null;
            TimeSpan? trackDuration = null;
            if (session != null)
            {
                try
                {
                    var tl = session.GetTimelineProperties();
                    if (tl != null)
                    {
                        currentPosition = tl.Position;
                        if (tl.EndTime > TimeSpan.Zero)
                        {
                            trackDuration = tl.EndTime;
                        }
                    }
                }
                catch
                {
                }
            }
            else
            {
                currentPosition = legacyPosition;
                trackDuration = legacyDuration;
            }

            if (title != _currentTrack || artist != _currentArtist)
            {
                _currentTrack = title;
                _currentArtist = artist;
                _currentAlbum = album;
                _currentAppId = appId;
                _trackStartTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                _listenedSeconds = 0;
                _lastPlayTickTime = DateTimeOffset.UtcNow;
                _hasScrobbledCurrentTrack = false;
                _isPlaying = true;
                _lastTimelinePosition = currentPosition;
                _currentTrackDuration = trackDuration;

                CurrentTrack = new NowPlayingInfo(title, artist, album);
                NowPlayingChanged?.Invoke(this, CurrentTrack);
                SetStatus(ScrobbleStatus.Listening, _currentTrack, _currentArtist, _currentAlbum);

                _nowPlayingSent = false;
                _lastNowPlayingAttempt = DateTimeOffset.MinValue;
                await TrySendNowPlayingAsync();
                _ = UpdateDiscordPresenceAsync(_currentTrack, _currentArtist, _currentAlbum, _trackStartTime);
            }
            else
            {
                _isPlaying = true;
                bool isTrackRepeated = false;
                if (currentPosition.HasValue && _lastTimelinePosition.HasValue)
                {
                    if (_lastTimelinePosition.Value >= TimeSpan.FromSeconds(30) && currentPosition.Value < TimeSpan.FromSeconds(5))
                    {
                        isTrackRepeated = true;
                    }
                }
                else if (_currentTrackDuration.HasValue && _currentTrackDuration.Value.TotalSeconds >= 30)
                {
                    if (_listenedSeconds >= _currentTrackDuration.Value.TotalSeconds)
                    {
                        isTrackRepeated = true;
                    }
                }

                if (isTrackRepeated)
                {
                    LogService.LogInfo($"[Scrobbler] Repeat detected for '{_currentArtist} - {_currentTrack}'. Starting new scrobble cycle.");

                    _trackStartTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                    _listenedSeconds = 0;
                    _lastPlayTickTime = DateTimeOffset.UtcNow;
                    _hasScrobbledCurrentTrack = false;
                    _lastTimelinePosition = currentPosition;

                    _nowPlayingSent = false;
                    _lastNowPlayingAttempt = DateTimeOffset.MinValue;
                    await TrySendNowPlayingAsync();
                    _ = UpdateDiscordPresenceAsync(_currentTrack, _currentArtist, _currentAlbum, _trackStartTime);
                }
                else
                {
                    DateTimeOffset now = DateTimeOffset.UtcNow;
                    if (_lastPlayTickTime.HasValue)
                    {
                        double delta = (now - _lastPlayTickTime.Value).TotalSeconds;
                        if (delta > 0)
                        {
                            _listenedSeconds += Math.Min(delta, 5);
                        }
                    }
                    _lastPlayTickTime = now;

                    _lastTimelinePosition = currentPosition;
                    if (trackDuration.HasValue && trackDuration.Value > TimeSpan.Zero)
                    {
                        _currentTrackDuration = trackDuration;
                    }

                    if (!_windowsMediaService.IsSendNowPlayingEnabled())
                    {
                        _nowPlayingSent = false;
                        _lastNowPlayingAttempt = DateTimeOffset.MinValue;
                    }
                    else if (!_nowPlayingSent)
                    {
                        await TrySendNowPlayingAsync();
                    }

                    if (CurrentTrack == null)
                    {
                        CurrentTrack = new NowPlayingInfo(_currentTrack, _currentArtist, _currentAlbum);
                        NowPlayingChanged?.Invoke(this, CurrentTrack);
                        SetStatus(ScrobbleStatus.Listening, _currentTrack, _currentArtist, _currentAlbum);
                        _ = UpdateDiscordPresenceAsync(_currentTrack, _currentArtist, _currentAlbum, _trackStartTime);
                    }

                    if (!_hasScrobbledCurrentTrack)
                    {
                        int percentage = _windowsMediaService.GetScrobblePercentageThreshold();
                        int minLength = _windowsMediaService.GetMinimumTrackLengthSeconds();
                        int maxWaitSeconds = _windowsMediaService.GetMaximumTimeThresholdSeconds();

                        if (_currentTrackDuration.HasValue && _currentTrackDuration.Value.TotalSeconds >= Math.Min(30, maxWaitSeconds))
                        {
                            double totalDurationSeconds = _currentTrackDuration.Value.TotalSeconds;
                            double percentageSeconds = totalDurationSeconds * (percentage / 100.0);
                            double triggerSeconds = Math.Min(percentageSeconds, maxWaitSeconds);

                            bool shouldScrobble = _listenedSeconds >= triggerSeconds && _listenedSeconds >= Math.Min(minLength, maxWaitSeconds);

                            if (shouldScrobble)
                            {
                                await ExecuteScrobbleAsync();
                            }
                        }
                        else if (!_currentTrackDuration.HasValue)
                        {
                            int unknownDurationThreshold = Math.Max(Math.Min(30, maxWaitSeconds), Math.Min(minLength, maxWaitSeconds));
                            if (_listenedSeconds >= unknownDurationThreshold)
                            {
                                await ExecuteScrobbleAsync();
                            }
                        }
                    }
                }
            }
        }

        private async Task TrySendNowPlayingAsync()
        {
            if (_nowPlayingSent || string.IsNullOrEmpty(_currentTrack) || !_windowsMediaService.IsSendNowPlayingEnabled())
            {
                return;
            }

            if (DateTimeOffset.UtcNow - _lastNowPlayingAttempt < TimeSpan.FromSeconds(10))
            {
                return;
            }

            _lastNowPlayingAttempt = DateTimeOffset.UtcNow;
            for (int i = 0; i < _providers.Length; i++)
            {
                if (!_providers[i].IsLoggedIn()) continue;
                try
                {
                    if (await _providers[i].UpdateNowPlayingAsync(_currentTrack, _currentArtist, _currentAlbum))
                    {
                        _nowPlayingSent = true;
                    }
                }
                catch (Exception ex)
                {
                    LogService.LogError("[Scrobbler] Now playing update failed", ex);
                }
            }
        }

        private async Task SetIdleStateAsync()
        {
            _nowPlayingSent = false;
            _currentSession = null;
            _lastTimelinePosition = null;
            _lastPlayTickTime = null;
            bool wasPlaying = _isPlaying || CurrentTrack != null;
            _isPlaying = false;
            if (wasPlaying)
            {
                CurrentTrack = null;
                NowPlayingChanged?.Invoke(this, null);
                _ = ClearDiscordPresenceAsync();
            }
            SetStatus(ScrobbleStatus.Idle);
            await Task.CompletedTask;
        }

        private async Task ExecuteScrobbleAsync()
        {
            if (_hasScrobbledCurrentTrack || string.IsNullOrWhiteSpace(_currentTrack) || string.IsNullOrWhiteSpace(_currentArtist)) return;

            if (!await _scrobbleLock.WaitAsync(0)) return;

            try
            {
                if (_hasScrobbledCurrentTrack || string.IsNullOrEmpty(_currentTrack)) return;

                if (IsRecentlyScrobbled(_currentArtist, _currentTrack, 10))
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
                    for (int i = 0; i < _providers.Length; i++)
                    {
                        if (_providers[i].IsLoggedIn())
                        {
                            bool pSuccess = await _providers[i].ScrobbleTrackAsync(_currentTrack, _currentArtist, _currentAlbum, _trackStartTime);
                            if (pSuccess) success = true;
                        }
                    }
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

        private void ResetStateWithoutScrobble()
        {
            _nowPlayingSent = false;
            bool wasPlaying = !string.IsNullOrEmpty(_currentTrack);
            _currentTrack = string.Empty;
            _currentArtist = string.Empty;
            _currentAlbum = string.Empty;
            _currentSession = null;
            _activeSessionId = null;
            _lastTimelinePosition = null;
            _currentTrackDuration = null;
            _lastPlayTickTime = null;
            _hasScrobbledCurrentTrack = true;
            _listenedSeconds = 0;
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
