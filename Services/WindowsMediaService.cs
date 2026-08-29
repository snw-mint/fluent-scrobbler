using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
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
        private const string CleanTrackTitlesKey = "CleanTrackTitles";
        private const string MinTrackLengthKey = "MinimumTrackLengthSeconds";
        private const string SendNowPlayingKey = "SendNowPlayingNotifications";
        private const string PercentageThresholdKey = "ScrobblePercentageThreshold";
        private const string MaxTimeThresholdKey = "MaximumTimeThresholdSeconds";

        private static readonly string SettingsFilePath = Path.Combine(
            AppInfoService.AppDataPath,
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
                        var dict = JsonSerializer.Deserialize(json, AppJsonContext.Default.DictionaryStringString);
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
                    string json = JsonSerializer.Serialize(dict, AppJsonContext.Default.DictionaryStringString);
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

        public bool IsCleanTrackTitlesEnabled()
        {
            var dict = LoadSettingsFromFile();
            if (dict.TryGetValue(CleanTrackTitlesKey, out string? val) && bool.TryParse(val, out bool res))
            {
                return res;
            }
            return false;
        }

        public void SetCleanTrackTitlesEnabled(bool enabled)
        {
            var dict = LoadSettingsFromFile();
            dict[CleanTrackTitlesKey] = enabled.ToString();
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

        private static readonly Regex PrimaryArtistRegex = new(
            @"\s*[\(\[](?:feat\.?|ft\.?|featuring|with|and|e|y|et|&|,)\s+.*[\)\]]|\s+(?:feat\.?|ft\.?|featuring|with|and|e|y|et|&|,|x)\s+.*$|\s*[,;/\\|&].*$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled
        );

        public static string FormatPrimaryArtist(string artist)
        {
            if (string.IsNullOrWhiteSpace(artist)) return artist;

            string cleaned = PrimaryArtistRegex.Replace(artist, string.Empty).Trim();
            return string.IsNullOrWhiteSpace(cleaned) ? artist.Trim() : cleaned;
        }

        private static readonly Regex CleanTrackTitleRegex = new(
            @"\s*[\(\[]\s*(?:(?:\d{4}\s*[-–—/]?\s*)?(?:digital\s+)?remaster(?:ed)?(?:\s+(?:version|\d{4}))?|remaster(?:ed)?(?:\s+\d{4})?|7[""”']*(?:\s*(?:edit|mix|version|single))?|radio\s+(?:edit|mix|version)|single\s+version)\s*[\)\]]|\s*[-–—]\s*(?:(?:\d{4}\s*[-–—/]?\s*)?(?:digital\s+)?remaster(?:ed)?(?:\s+(?:version|\d{4}))?|remaster(?:ed)?(?:\s+\d{4})?|7[""”']*(?:\s*(?:edit|mix|version|single))?|radio\s+(?:edit|mix|version)|single\s+version)\s*$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled
        );

        public static string CleanTrackTitle(string title)
        {
            if (string.IsNullOrWhiteSpace(title)) return title;

            string cleaned = CleanTrackTitleRegex.Replace(title, string.Empty).Trim();
            return string.IsNullOrWhiteSpace(cleaned) ? title.Trim() : cleaned;
        }

        public List<string> GetKnownSources()
        {
            return GetStoredList(LocalSettingsKnownKey);
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
                                }
                            }
                            catch
                            {
                            }
                        }
                    }
                    SaveStoredList(LocalSettingsKnownKey, knownSources);
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

        public async Task<(string Title, string Artist, string Album, string SourceApp)?> GetCurrentWindowsMediaAsync()
        {
            try
            {
                var manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
                var sessions = manager?.GetSessions();
                if (sessions == null) return null;

                foreach (var session in sessions)
                {
                    try
                    {
                        string appId = session.SourceAppUserModelId;
                        if (!IsSourceAllowed(appId)) continue;

                        var playbackInfo = session.GetPlaybackInfo();
                        if (playbackInfo == null || playbackInfo.PlaybackStatus != GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
                            continue;

                        var mediaProperties = await session.TryGetMediaPropertiesAsync();
                        if (mediaProperties != null && !string.IsNullOrWhiteSpace(mediaProperties.Title))
                        {
                            string sourceName = FormatAppDisplayName(appId);
                            string artist = !string.IsNullOrWhiteSpace(mediaProperties.Artist) ? mediaProperties.Artist.Trim() : (mediaProperties.AlbumArtist?.Trim() ?? string.Empty);
                            if (IsPrimaryArtistOnlyEnabled())
                            {
                                artist = FormatPrimaryArtist(artist);
                            }
                            string title = mediaProperties.Title.Trim();
                            if (IsCleanTrackTitlesEnabled())
                            {
                                title = CleanTrackTitle(title);
                            }
                            return (title, artist, mediaProperties.AlbumTitle ?? string.Empty, sourceName);
                        }
                    }
                    catch (Exception ex)
                    {
                        LogService.LogError("[Media Detection Error] Exception processing media session", ex);
                    }
                }
            }
            catch (Exception ex)
            {
                LogService.LogError("[Media Detection Error] Exception requesting media manager", ex);
            }
            return null;
        }

        public async Task<Windows.Storage.Streams.IRandomAccessStreamWithContentType?> GetCurrentWindowsMediaThumbnailAsync()
        {
            try
            {
                var manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
                var currentSession = manager?.GetCurrentSession();
                if (currentSession != null)
                {
                    var mediaProperties = await currentSession.TryGetMediaPropertiesAsync();
                    if (mediaProperties?.Thumbnail != null)
                    {
                        return await mediaProperties.Thumbnail.OpenReadAsync();
                    }
                }
            }
            catch
            {
            }
            return null;
        }
    }
}
