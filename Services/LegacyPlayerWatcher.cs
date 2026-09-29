using System;
using System.Diagnostics;
using System.IO;
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

        private const string WinampClassName = "Winamp v1.x";
        private const uint WM_USER = 0x0400;
        private const int IPC_ISPLAYING = 104;
        private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
        private const int IPC_GETLISTPOS = 125;
        private const int IPC_GETPLAYLISTTITLE = 212;
        private const int IPC_GETPLAYLISTTITLEW = 213;
        private const uint PROCESS_VM_READ = 0x0010;
        private const string ScrollSeparator = " *** ";

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint processAccess, bool bInheritHandle, int processId);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool QueryFullProcessImageName(IntPtr hProcess, int dwFlags, StringBuilder lpExeName, ref int lpdwSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int nSize, out IntPtr lpNumberOfBytesRead);

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
                        _lastTrack = null;
                        TrackChanged?.Invoke(this, stoppedTrack);
                    }
                    else
                    {
                        _lastTrack = null;
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
                GetWindowThreadProcessId(hWnd, out uint pid);
                int processId = (int)pid;
                var (technicalId, displayName, binaryPath) = ResolveProcessMetadata(processId);

                int playStatus = SendMessage(hWnd, WM_USER, IntPtr.Zero, (IntPtr)IPC_ISPLAYING).ToInt32();

                LegacyPlaybackState state = playStatus switch
                {
                    1 => LegacyPlaybackState.Playing,
                    3 => LegacyPlaybackState.Paused,
                    _ => LegacyPlaybackState.Stopped
                };

                string? ipcTitle = TryGetWinampPlaylistTitle(hWnd, processId);
                string rawTitle = ipcTitle ?? NormalizeScrollingTitle(GetWindowTitle(hWnd));
                var (artist, title) = ParseAndCleanTitle(rawTitle, stripTrackNumber: ipcTitle == null);

                currentInfo = new LegacyTrackInfo
                {
                    Artist = artist,
                    Title = title,
                    State = state,
                    ProcessId = processId,
                    ProcessName = technicalId,
                    DisplayName = displayName,
                    BinaryPath = binaryPath,
                    SourceApp = LegacyTrackInfo.NormalizeSourceApp(technicalId)
                };
            }

            if (!Equals(_lastTrack, currentInfo))
            {
                _lastTrack = currentInfo;
                TrackChanged?.Invoke(this, currentInfo);
            }
        }

        private static (string TechnicalId, string DisplayName, string BinaryPath) ResolveProcessMetadata(int processId)
        {
            string binaryPath = string.Empty;
            string technicalId = string.Empty;
            string displayName = string.Empty;

            if (processId <= 0)
            {
                return (string.Empty, string.Empty, string.Empty);
            }

            try
            {
                IntPtr hProcess = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, processId);
                if (hProcess != IntPtr.Zero)
                {
                    try
                    {
                        var sb = new StringBuilder(1024);
                        int size = sb.Capacity;
                        if (QueryFullProcessImageName(hProcess, 0, sb, ref size))
                        {
                            binaryPath = sb.ToString();
                        }
                    }
                    finally
                    {
                        CloseHandle(hProcess);
                    }
                }

                if (string.IsNullOrEmpty(binaryPath))
                {
                    try
                    {
                        using var proc = Process.GetProcessById(processId);
                        binaryPath = proc.MainModule?.FileName ?? string.Empty;
                    }
                    catch
                    {
                    }
                }

                if (!string.IsNullOrEmpty(binaryPath))
                {
                    technicalId = Path.GetFileName(binaryPath).ToLowerInvariant();
                }
                else
                {
                    try
                    {
                        using var proc = Process.GetProcessById(processId);
                        technicalId = (proc.ProcessName + ".exe").ToLowerInvariant();
                    }
                    catch
                    {
                        technicalId = "legacyplayer.exe";
                    }
                }

                if (!string.IsNullOrEmpty(binaryPath) && File.Exists(binaryPath))
                {
                    try
                    {
                        var versionInfo = FileVersionInfo.GetVersionInfo(binaryPath);
                        if (!string.IsNullOrWhiteSpace(versionInfo.FileDescription))
                        {
                            displayName = versionInfo.FileDescription.Trim();
                        }
                        else if (!string.IsNullOrWhiteSpace(versionInfo.ProductName))
                        {
                            displayName = versionInfo.ProductName.Trim();
                        }
                    }
                    catch
                    {
                    }
                }

                if (string.IsNullOrWhiteSpace(displayName))
                {
                    string rawName = Path.GetFileNameWithoutExtension(technicalId);
                    if (!string.IsNullOrEmpty(rawName))
                    {
                        displayName = char.ToUpperInvariant(rawName[0]) + (rawName.Length > 1 ? rawName.Substring(1) : string.Empty);
                    }
                    else
                    {
                        displayName = "Legacy Player";
                    }
                }
            }
            catch (Exception ex)
            {
                LogService.LogWarning($"[LegacyPlayerWatcher] Error resolving metadata for PID {processId}: {ex.Message}");
                technicalId = string.IsNullOrEmpty(technicalId) ? "legacyplayer.exe" : technicalId;
                displayName = string.IsNullOrEmpty(displayName) ? "Legacy Player" : displayName;
            }

            return (technicalId, displayName, binaryPath);
        }

        public static (string Artist, string Title) ParseAndCleanTitle(string? rawTitle, bool stripTrackNumber = true)
        {
            if (string.IsNullOrWhiteSpace(rawTitle))
            {
                return (string.Empty, string.Empty);
            }

            string cleaned = rawTitle.Trim();

            cleaned = PlayerSuffixRegex.Replace(cleaned, string.Empty).Trim();

            if (string.Equals(cleaned, "Winamp", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(cleaned, "AIMP", StringComparison.OrdinalIgnoreCase))
            {
                return (string.Empty, string.Empty);
            }

            if (stripTrackNumber)

            {
                cleaned = TrackNumberPrefixRegex.Replace(cleaned, string.Empty).Trim();
            }

            if (string.IsNullOrWhiteSpace(cleaned))
            {
                return (string.Empty, string.Empty);
            }

            var parts = ArtistTrackSeparatorRegex.Split(cleaned);
            if (parts.Length >= 2)
            {
                string artist = parts[0].Trim();
                string title = string.Join(" - ", parts.Skip(1)).Trim();
                return (artist, title);
            }

            return (string.Empty, cleaned);
        }

        private static string? TryGetWinampPlaylistTitle(IntPtr hWnd, int processId)
        {
            int pos = SendMessage(hWnd, WM_USER, IntPtr.Zero, (IntPtr)IPC_GETLISTPOS).ToInt32();
            if (pos < 0) return null;

            IntPtr remoteW = SendMessage(hWnd, WM_USER, (IntPtr)pos, (IntPtr)IPC_GETPLAYLISTTITLEW);
            IntPtr remoteA = remoteW == IntPtr.Zero
                ? SendMessage(hWnd, WM_USER, (IntPtr)pos, (IntPtr)IPC_GETPLAYLISTTITLE)
                : IntPtr.Zero;

            IntPtr remote = remoteW != IntPtr.Zero ? remoteW : remoteA;
            if (remote == IntPtr.Zero) return null;

            IntPtr hProcess = OpenProcess(PROCESS_VM_READ, false, processId);
            if (hProcess == IntPtr.Zero) return null;

            try
            {
                foreach (int size in new[] { 1024, 256 })
                {
                    var buffer = new byte[size];
                    if (!ReadProcessMemory(hProcess, remote, buffer, size, out _)) continue;

                    string text = remoteW != IntPtr.Zero
                        ? Encoding.Unicode.GetString(buffer)
                        : Encoding.Default.GetString(buffer);

                    int nul = text.IndexOf('\0');
                    text = nul >= 0 ? text[..nul] : text;
                    return string.IsNullOrWhiteSpace(text) ? null : text;
                }
                return null;
            }
            finally
            {
                CloseHandle(hProcess);
            }
        }

        private static string NormalizeScrollingTitle(string rawTitle)
        {
            if (string.IsNullOrEmpty(rawTitle)) return rawTitle;

            string doubled = rawTitle + rawTitle;
            int idx = doubled.IndexOf(ScrollSeparator, StringComparison.Ordinal);
            if (idx < 0) return rawTitle;

            int length = rawTitle.Length - ScrollSeparator.Length;
            return length > 0 ? doubled.Substring(idx + ScrollSeparator.Length, length) : rawTitle;
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
