using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Timer = System.Timers.Timer;
using ElapsedEventArgs = System.Timers.ElapsedEventArgs;
using FluentScrobbler.Models;

namespace FluentScrobbler.Services
{
    public class LegacyPlayerWatcher : ILegacyPlayerWatcher
    {
        private static LegacyPlayerWatcher? _instance;
        public static LegacyPlayerWatcher Instance => _instance ??= new LegacyPlayerWatcher();

        // Win32 Interop Constants
        private const string WinampClassName = "Winamp v1.x";
        private const uint WM_USER = 0x0400;
        private const int IPC_ISPLAYING = 104;

        // P/Invoke Signatures
        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        // Regex patterns for metadata cleaning
        private static readonly Regex PlayerSuffixRegex = new(
            @"\s*[-–—]\s*(?:Winamp(?:\s+[\d\.]+)?|AIMP(?:\s+[\d\.]+)?)\s*$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled
        );

        private static readonly Regex TrackNumberPrefixRegex = new(
            @"^\s*\d{1,4}\s*[\.\-–—]\s*",
            RegexOptions.Compiled
        );

        private static readonly Regex ArtistTrackSeparatorRegex = new(
            @"\s+[-–—]\s+",
            RegexOptions.Compiled
        );

        private readonly object _stateLock = new();
        private Timer? _pollingTimer;
        private bool _isRunning;
        private LegacyTrackInfo? _lastTrack;

        public event EventHandler<LegacyTrackInfo>? TrackChanged;

        public bool IsRunning
        {
            get
            {
                lock (_stateLock)
                {
                    return _isRunning;
                }
            }
        }

        public LegacyTrackInfo? CurrentTrack
        {
            get
            {
                lock (_stateLock)
                {
                    return _lastTrack;
                }
            }
        }

        public void Start() => SetMonitoringState(true);

        public void Stop() => SetMonitoringState(false);

        public void SetMonitoringState(bool enabled)
        {
            lock (_stateLock)
            {
                if (_isRunning == enabled) return;

                _isRunning = enabled;

                if (_isRunning)
                {
                    _pollingTimer = new Timer(1500)
                    {
                        AutoReset = false
                    };
                    _pollingTimer.Elapsed += OnTimerElapsed;
                    _pollingTimer.Start();

                    // Perform an immediate initial check
                    CheckPlayerState();
                }
                else
                {
                    if (_pollingTimer != null)
                    {
                        _pollingTimer.Stop();
                        _pollingTimer.Elapsed -= OnTimerElapsed;
                        _pollingTimer.Dispose();
                        _pollingTimer = null;
                    }

                    if (_lastTrack != null && _lastTrack.State != LegacyPlaybackState.NotRunning)
                    {
                        var stoppedTrack = new LegacyTrackInfo
                        {
                            Artist = _lastTrack.Artist,
                            Title = _lastTrack.Title,
                            State = LegacyPlaybackState.NotRunning
                        };
                        _lastTrack = stoppedTrack;
                        TrackChanged?.Invoke(this, stoppedTrack);
                    }
                }
            }
        }

        private void OnTimerElapsed(object? sender, ElapsedEventArgs e)
        {
            lock (_stateLock)
            {
                if (!_isRunning) return;

                try
                {
                    CheckPlayerState();
                }
                catch (Exception ex)
                {
                    LogService.LogError("[LegacyPlayerWatcher] Error during polling cycle", ex);
                }
                finally
                {
                    if (_isRunning && _pollingTimer != null)
                    {
                        _pollingTimer.Start();
                    }
                }
            }
        }

        public void CheckPlayerState()
        {
            IntPtr hWnd = FindWindow(WinampClassName, null);

            LegacyTrackInfo currentInfo;

            if (hWnd == IntPtr.Zero)
            {
                currentInfo = new LegacyTrackInfo
                {
                    State = LegacyPlaybackState.NotRunning
                };
            }
            else
            {
                int playStatus = SendMessage(hWnd, WM_USER, IntPtr.Zero, (IntPtr)IPC_ISPLAYING).ToInt32();

                LegacyPlaybackState state = playStatus switch
                {
                    1 => LegacyPlaybackState.Playing,
                    3 => LegacyPlaybackState.Paused,
                    _ => LegacyPlaybackState.Stopped
                };

                string rawTitle = GetWindowTitle(hWnd);
                var (artist, title) = ParseAndCleanTitle(rawTitle);

                currentInfo = new LegacyTrackInfo
                {
                    Artist = artist,
                    Title = title,
                    State = state
                };
            }

            // Deduplication: only trigger if track info or playback state has changed
            if (!Equals(_lastTrack, currentInfo))
            {
                _lastTrack = currentInfo;
                TrackChanged?.Invoke(this, currentInfo);
            }
        }

        public static (string Artist, string Title) ParseAndCleanTitle(string? rawTitle)
        {
            if (string.IsNullOrWhiteSpace(rawTitle))
            {
                return (string.Empty, string.Empty);
            }

            string cleaned = rawTitle.Trim();

            // 1. Remove player suffixes like "- Winamp" or "- AIMP"
            cleaned = PlayerSuffixRegex.Replace(cleaned, string.Empty).Trim();

            // Guard against player-only title headers when no track is playing
            if (string.Equals(cleaned, "Winamp", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(cleaned, "AIMP", StringComparison.OrdinalIgnoreCase))
            {
                return (string.Empty, string.Empty);
            }

            // 2. Remove leading track numbering (e.g. "01. ", "1. ", "01 - ")
            cleaned = TrackNumberPrefixRegex.Replace(cleaned, string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(cleaned))
            {
                return (string.Empty, string.Empty);
            }

            // 3. Separate standard "Artist - Title" format
            var parts = ArtistTrackSeparatorRegex.Split(cleaned);
            if (parts.Length >= 2)
            {
                string artist = parts[0].Trim();
                string title = string.Join(" - ", parts.Skip(1)).Trim();
                return (artist, title);
            }

            // Fallback if no separator is found
            return (string.Empty, cleaned);
        }

        private static string GetWindowTitle(IntPtr hWnd)
        {
            int length = GetWindowTextLength(hWnd);
            if (length <= 0) return string.Empty;

            var sb = new StringBuilder(length + 1);
            GetWindowText(hWnd, sb, sb.Capacity);
            return sb.ToString();
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
