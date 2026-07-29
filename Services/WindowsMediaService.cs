using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.Media.Control;

namespace FluentScrobbler.Services
{
    public class SourceAppInfo
    {
        public string AppId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public bool IsAllowed { get; set; } = true;
    }

    public class WindowsMediaService
    {
        private const string LocalSettingsAllowedKey = "AllowedScrobbleSources";
        private const string LocalSettingsKnownKey = "KnownScrobbleSources";
        private const string PrimaryArtistOnlyKey = "UsePrimaryArtistOnly";
        private const string MinTrackLengthKey = "MinimumTrackLengthSeconds";
        private const string SendNowPlayingKey = "SendNowPlayingNotifications";
        private const string PercentageThresholdKey = "ScrobblePercentageThreshold";
        private const string MaxTimeThresholdKey = "MaximumTimeThresholdSeconds";

        private static readonly string SettingsFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FluentScrobbler",
            "media_settings.json"
        );

        private static readonly object FileLock = new object();

        private static Dictionary<string, string> LoadSettingsFromFile()
        {
            lock (FileLock)
            {
                try
                {
                    if (File.Exists(SettingsFilePath))
                    {
                        string json = File.ReadAllText(SettingsFilePath);
                        var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                        if (dict != null) return dict;
                    }
                }
                catch
                {
                }
                return new Dictionary<string, string>();
            }
        }

        private static void SaveSettingsToFile(Dictionary<string, string> dict)
        {
            lock (FileLock)
            {
                try
                {
                    string dir = Path.GetDirectoryName(SettingsFilePath)!;
                    if (!Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }
                    string json = JsonSerializer.Serialize(dict);
                    File.WriteAllText(SettingsFilePath, json);
                }
                catch
                {
                }
            }
        }

        public bool IsPrimaryArtistOnlyEnabled()
        {
            var dict = LoadSettingsFromFile();
            if (dict.TryGetValue(PrimaryArtistOnlyKey, out string? val) && bool.TryParse(val, out bool result))
            {
                return result;
            }
            return false;
        }

        public void SetPrimaryArtistOnlyEnabled(bool enabled)
        {
            var dict = LoadSettingsFromFile();
            dict[PrimaryArtistOnlyKey] = enabled.ToString();
            SaveSettingsToFile(dict);
        }

        public int GetMinimumTrackLengthSeconds()
        {
            var dict = LoadSettingsFromFile();
            if (dict.TryGetValue(MinTrackLengthKey, out string? val) && int.TryParse(val, out int result))
            {
                return Math.Clamp(result, 10, 60);
            }
            return 10;
        }

        public void SetMinimumTrackLengthSeconds(int seconds)
        {
            var dict = LoadSettingsFromFile();
            dict[MinTrackLengthKey] = seconds.ToString();
            SaveSettingsToFile(dict);
        }

        public bool IsSendNowPlayingEnabled()
        {
            return true;
        }

        public void SetSendNowPlayingEnabled(bool enabled)
        {
            var dict = LoadSettingsFromFile();
            dict[SendNowPlayingKey] = enabled.ToString();
            SaveSettingsToFile(dict);
        }

        public int GetScrobblePercentageThreshold()
        {
            var dict = LoadSettingsFromFile();
            if (dict.TryGetValue(PercentageThresholdKey, out string? val) && int.TryParse(val, out int result))
            {
                return Math.Clamp(result, 10, 100);
            }
            return 50;
        }

        public void SetScrobblePercentageThreshold(int percentage)
        {
            var dict = LoadSettingsFromFile();
            dict[PercentageThresholdKey] = percentage.ToString();
            SaveSettingsToFile(dict);
        }

        public int GetMaximumTimeThresholdSeconds()
        {
            var dict = LoadSettingsFromFile();
            if (dict.TryGetValue(MaxTimeThresholdKey, out string? val) && int.TryParse(val, out int result))
            {
                return Math.Clamp(result, 60, 600);
            }
            return 240;
        }

        public void SetMaximumTimeThresholdSeconds(int seconds)
        {
            var dict = LoadSettingsFromFile();
            dict[MaxTimeThresholdKey] = seconds.ToString();
            SaveSettingsToFile(dict);
        }

        public static string FormatPrimaryArtist(string artist)
        {
            if (string.IsNullOrWhiteSpace(artist)) return artist;

            string[] delimiters = new[] { " feat. ", " feat ", " ft. ", " ft ", " featuring ", " Feat. ", " Feat ", " Ft. ", " Ft ", " Featuring ", " & ", ", ", " x ", " X " };
            foreach (var delim in delimiters)
            {
                int idx = artist.IndexOf(delim, StringComparison.OrdinalIgnoreCase);
                if (idx > 0)
                {
                    artist = artist.Substring(0, idx);
                }
            }

            int bracketIdx = artist.IndexOf("(feat", StringComparison.OrdinalIgnoreCase);
            if (bracketIdx > 0)
            {
                artist = artist.Substring(0, bracketIdx);
            }

            return artist.Trim();
        }

        public async Task<List<SourceAppInfo>> GetDetectedSourcesAsync()
        {
            var knownSources = GetStoredList(LocalSettingsKnownKey);
            var allowedSources = GetStoredList(LocalSettingsAllowedKey);

            try
            {
                var manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
                if (manager != null)
                {
                    var sessions = manager.GetSessions();
                    if (sessions != null)
                    {
                        foreach (var session in sessions)
                        {
                            try
                            {
                                string appId = session.SourceAppUserModelId;
                                if (!string.IsNullOrWhiteSpace(appId) && !knownSources.Contains(appId))
                                {
                                    knownSources.Add(appId);
                                    if (!allowedSources.Contains(appId))
                                    {
                                        allowedSources.Add(appId);
                                    }
                                }
                            }
                            catch
                            {
                            }
                        }
                    }
                    SaveStoredList(LocalSettingsKnownKey, knownSources);
                    SaveStoredList(LocalSettingsAllowedKey, allowedSources);
                }
            }
            catch
            {
            }

            var result = new List<SourceAppInfo>();
            foreach (var appId in knownSources)
            {
                result.Add(new SourceAppInfo
                {
                    AppId = appId,
                    DisplayName = FormatAppDisplayName(appId),
                    IsAllowed = allowedSources.Contains(appId)
                });
            }

            return result;
        }

        public void SetSourceAllowed(string appId, bool isAllowed)
        {
            var allowedSources = GetStoredList(LocalSettingsAllowedKey);
            if (isAllowed)
            {
                if (!allowedSources.Contains(appId))
                {
                    allowedSources.Add(appId);
                }
            }
            else
            {
                allowedSources.Remove(appId);
            }
            SaveStoredList(LocalSettingsAllowedKey, allowedSources);
        }

        public bool IsSourceAllowed(string appId)
        {
            var allowedSources = GetStoredList(LocalSettingsAllowedKey);
            return allowedSources.Contains(appId);
        }

        private static List<string> GetStoredList(string key)
        {
            var dict = LoadSettingsFromFile();
            if (dict.TryGetValue(key, out string? raw) && !string.IsNullOrWhiteSpace(raw))
            {
                return raw.Split('|', StringSplitOptions.RemoveEmptyEntries).ToList();
            }
            return new List<string>();
        }

        private static void SaveStoredList(string key, List<string> list)
        {
            var dict = LoadSettingsFromFile();
            dict[key] = string.Join('|', list.Distinct());
            SaveSettingsToFile(dict);
        }

        public static string FormatAppDisplayName(string appId)
        {
            if (string.IsNullOrWhiteSpace(appId)) return "Unknown Application";
            string lower = appId.ToLowerInvariant();
            if (lower.Contains("spotify")) return "Spotify";
            if (lower.Contains("chrome")) return "Google Chrome";
            if (lower.Contains("msedge") || lower.Contains("edge")) return "Microsoft Edge";
            if (lower.Contains("firefox")) return "Mozilla Firefox";
            if (lower.Contains("applemusic") || lower.Contains("apple.music")) return "Apple Music";
            if (lower.Contains("vlc")) return "VLC Media Player";
            if (lower.Contains("foobar")) return "foobar2000";
            if (lower.Contains("wmplayer") || lower.Contains("mediaplayer")) return "Windows Media Player";

            string name = Path.GetFileNameWithoutExtension(appId);
            if (name.Contains("!"))
            {
                var parts = name.Split('!');
                name = parts[parts.Length - 1];
            }
            return name;
        }
    }
}
