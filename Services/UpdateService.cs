using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace FluentScrobbler.Services
{
    public class UpdateService
    {
        private static readonly Lazy<UpdateService> _instance = new(() => new UpdateService());
        public static UpdateService Instance => _instance.Value;

        private readonly HttpClient _httpClient;
        private const string ReleasesApiUrl = "https://api.github.com/repos/snw-mint/fluent-scrobbler/releases";
        public const string DefaultReleasesUrl = "https://github.com/snw-mint/fluent-scrobbler/releases";
        public const string StoreProductUrl = "ms-windows-store://pdp/?ProductId=9N5RMD87SPVM";
        private const string LastCheckKey = "LastUpdateCheckUtc";
        private static readonly TimeSpan Cooldown = TimeSpan.FromHours(24);

        public bool IsUpdateAvailable { get; private set; }
        public string LatestVersion { get; private set; } = string.Empty;
        public string ReleaseUrl { get; private set; } = AppInfoService.IsPackaged ? StoreProductUrl : DefaultReleasesUrl;
        public DateTime? LastCheckTime { get; private set; }

        public event EventHandler? UpdateStatusChanged;

        private bool _hasNotified = false;

        private UpdateService()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "FluentScrobbler");
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");
            _httpClient.Timeout = TimeSpan.FromSeconds(10);
            LoadLastCheckTime();
        }

        private void LoadLastCheckTime()
        {
            try
            {
                string? saved = SettingsService.GetSetting(LastCheckKey);
                if (!string.IsNullOrEmpty(saved) && DateTime.TryParse(saved, null, System.Globalization.DateTimeStyles.RoundtripKind, out var dt))
                {
                    LastCheckTime = dt;
                }
            }
            catch (Exception ex)
            {
                LogService.LogError("[UpdateService] Failed to load last update check time", ex);
            }
        }

        public async Task CheckForUpdatesAsync(bool force = false)
        {
            if (!force && LastCheckTime.HasValue)
            {
                if (DateTime.UtcNow - LastCheckTime.Value < Cooldown)
                {
                    LogService.LogInfo($"[UpdateService] Skipping update check (cooldown active, last check: {LastCheckTime.Value:u})");
                    return;
                }
            }

            try
            {
                LogService.LogInfo($"[UpdateService] Checking for updates (force: {force})...");
                using var response = await _httpClient.GetAsync(ReleasesApiUrl);
                if (!response.IsSuccessStatusCode)
                {
                    LogService.LogWarning($"[UpdateService] GitHub releases API returned status {response.StatusCode}");
                    return;
                }

                string json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                string bestTagName = string.Empty;
                string bestHtmlUrl = string.Empty;

                if (root.ValueKind == JsonValueKind.Array)
                {
                    foreach (var release in root.EnumerateArray())
                    {
                        if (release.TryGetProperty("draft", out var draftProp) && draftProp.GetBoolean())
                        {
                            continue;
                        }

                        string tag = release.TryGetProperty("tag_name", out var tagProp) ? tagProp.GetString() ?? string.Empty : string.Empty;
                        if (string.IsNullOrEmpty(tag) && release.TryGetProperty("name", out var nameProp))
                        {
                            tag = nameProp.GetString() ?? string.Empty;
                        }

                        string url = release.TryGetProperty("html_url", out var urlProp) ? urlProp.GetString() ?? string.Empty : string.Empty;

                        if (string.IsNullOrEmpty(bestTagName) || IsNewerVersion(tag, bestTagName))
                        {
                            bestTagName = tag;
                            bestHtmlUrl = url;
                        }
                    }
                }
                else if (root.ValueKind == JsonValueKind.Object)
                {
                    bestTagName = root.TryGetProperty("tag_name", out var tagProp) ? tagProp.GetString() ?? string.Empty : string.Empty;
                    if (string.IsNullOrEmpty(bestTagName) && root.TryGetProperty("name", out var nameProp))
                    {
                        bestTagName = nameProp.GetString() ?? string.Empty;
                    }
                    bestHtmlUrl = root.TryGetProperty("html_url", out var urlProp) ? urlProp.GetString() ?? string.Empty : string.Empty;
                }

                if (!string.IsNullOrEmpty(bestHtmlUrl))
                {
                    ReleaseUrl = AppInfoService.IsPackaged ? StoreProductUrl : bestHtmlUrl;
                }

                LastCheckTime = DateTime.UtcNow;
                SettingsService.SetSetting(LastCheckKey, LastCheckTime.Value.ToString("o"));

                string currentVersion = AppInfoService.Version;
                if (!string.IsNullOrEmpty(bestTagName) && IsNewerVersion(bestTagName, currentVersion))
                {
                    IsUpdateAvailable = true;
                    LatestVersion = bestTagName.StartsWith("v", StringComparison.OrdinalIgnoreCase) ? bestTagName : $"v{bestTagName}";
                    LogService.LogInfo($"[UpdateService] New update available: {LatestVersion} (current: v{currentVersion})");
                    UpdateStatusChanged?.Invoke(this, EventArgs.Empty);

                    if (!_hasNotified)
                    {
                        _hasNotified = true;
                        NotificationService.ShowUpdateAvailableNotification(LatestVersion, ReleaseUrl);
                    }
                }
                else
                {
                    IsUpdateAvailable = false;
                    LogService.LogInfo($"[UpdateService] App is up to date (current: v{currentVersion}, latest: {bestTagName})");
                    UpdateStatusChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            catch (Exception ex)
            {
                LogService.LogError("[UpdateService] Failed to check for updates", ex);
            }
        }

        public static bool IsNewerVersion(string latestStr, string currentStr)
        {
            if (string.IsNullOrWhiteSpace(latestStr) || string.IsNullOrWhiteSpace(currentStr))
                return false;

            string cleanLatest = latestStr.Trim().TrimStart('v', 'V');
            string cleanCurrent = currentStr.Trim().TrimStart('v', 'V');

            int dashIndex = cleanLatest.IndexOf('-');
            if (dashIndex > 0) cleanLatest = cleanLatest.Substring(0, dashIndex);

            dashIndex = cleanCurrent.IndexOf('-');
            if (dashIndex > 0) cleanCurrent = cleanCurrent.Substring(0, dashIndex);

            if (Version.TryParse(cleanLatest, out var latestVer) && Version.TryParse(cleanCurrent, out var currentVer))
            {
                return latestVer > currentVer;
            }

            var latestParts = cleanLatest.Split('.');
            var currentParts = cleanCurrent.Split('.');
            int maxLen = Math.Max(latestParts.Length, currentParts.Length);

            for (int i = 0; i < maxLen; i++)
            {
                int l = i < latestParts.Length && int.TryParse(latestParts[i], out int lv) ? lv : 0;
                int c = i < currentParts.Length && int.TryParse(currentParts[i], out int cv) ? cv : 0;
                if (l > c) return true;
                if (l < c) return false;
            }

            return false;
        }
    }
}
