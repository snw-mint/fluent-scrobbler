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
        private const string ReleasesApiUrl = "https://api.github.com/repos/snw-mint/fluent-scrobbler/releases/latest";
        public const string DefaultReleasesUrl = "https://github.com/snw-mint/fluent-scrobbler/releases";

        public bool IsUpdateAvailable { get; private set; }
        public string LatestVersion { get; private set; } = string.Empty;
        public string ReleaseUrl { get; private set; } = DefaultReleasesUrl;

        public event EventHandler? UpdateStatusChanged;

        private bool _hasNotified = false;

        private UpdateService()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "FluentScrobbler");
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");
            _httpClient.Timeout = TimeSpan.FromSeconds(10);
        }

        public async Task CheckForUpdatesAsync()
        {
            try
            {
                using var response = await _httpClient.GetAsync(ReleasesApiUrl);
                if (!response.IsSuccessStatusCode) return;

                string json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                string tagName = root.TryGetProperty("tag_name", out var tagProp) ? tagProp.GetString() ?? string.Empty : string.Empty;
                if (string.IsNullOrEmpty(tagName) && root.TryGetProperty("name", out var nameProp))
                {
                    tagName = nameProp.GetString() ?? string.Empty;
                }

                string htmlUrl = root.TryGetProperty("html_url", out var urlProp) ? urlProp.GetString() ?? string.Empty : string.Empty;

                if (!string.IsNullOrEmpty(htmlUrl))
                {
                    ReleaseUrl = htmlUrl;
                }

                string currentVersion = AppInfoService.Version;
                if (IsNewerVersion(tagName, currentVersion))
                {
                    IsUpdateAvailable = true;
                    LatestVersion = tagName.StartsWith("v", StringComparison.OrdinalIgnoreCase) ? tagName : $"v{tagName}";
                    UpdateStatusChanged?.Invoke(this, EventArgs.Empty);

                    if (!_hasNotified)
                    {
                        _hasNotified = true;
                        NotificationService.ShowUpdateAvailableNotification(LatestVersion, ReleaseUrl);
                    }
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
