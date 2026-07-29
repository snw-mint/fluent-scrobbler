using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.System;
using FluentScrobbler.Models;

namespace FluentScrobbler.Services
{
    public class LastFmService
    {
        private const string DefaultApiKey = "YOUR_API_KEY_HERE";
        private const string DefaultApiSecret = "YOUR_API_SECRET_HERE";
        private const string BaseUrl = "https://ws.audioscrobbler.com/2.0/";

        private readonly string ApiKey;
        private readonly string ApiSecret;

        private static readonly string SettingsFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FluentScrobbler",
            "settings.json"
        );

        private static readonly string LocalSecretsFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FluentScrobbler",
            "secrets.json"
        );

        private readonly HttpClient _httpClient;

        public LastFmService()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "FluentScrobbler-WindowsApp/1.0");

            var secrets = LoadSecrets();
            ApiKey = secrets.TryGetValue("ApiKey", out var k) && !string.IsNullOrEmpty(k) ? k : DefaultApiKey;
            ApiSecret = secrets.TryGetValue("ApiSecret", out var s) && !string.IsNullOrEmpty(s) ? s : DefaultApiSecret;
        }

        private static Dictionary<string, string> LoadSecrets()
        {
            try
            {
                string baseDirSecrets = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "secrets.json");
                if (File.Exists(baseDirSecrets))
                {
                    string json = File.ReadAllText(baseDirSecrets);
                    return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
                }
                if (File.Exists("secrets.json"))
                {
                    string json = File.ReadAllText("secrets.json");
                    return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
                }
                if (File.Exists(LocalSecretsFilePath))
                {
                    string json = File.ReadAllText(LocalSecretsFilePath);
                    return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
                }
            }
            catch
            {
            }
            return new Dictionary<string, string>();
        }

        private static Dictionary<string, string> LoadSettingsFromFile()
        {
            try
            {
                if (File.Exists(SettingsFilePath))
                {
                    string json = File.ReadAllText(SettingsFilePath);
                    return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
                }
            }
            catch
            {
            }
            return new Dictionary<string, string>();
        }

        private static void SaveSettingsToFile(Dictionary<string, string> settings)
        {
            try
            {
                string? dir = Path.GetDirectoryName(SettingsFilePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                string json = JsonSerializer.Serialize(settings);
                File.WriteAllText(SettingsFilePath, json);
            }
            catch
            {
            }
        }

        private static string? GetSetting(string key)
        {
            try
            {
                var localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
                return localSettings.Values[key]?.ToString();
            }
            catch
            {
                var settings = LoadSettingsFromFile();
                return settings.TryGetValue(key, out var val) ? val : null;
            }
        }

        private static void SetSetting(string key, string value)
        {
            try
            {
                var localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
                localSettings.Values[key] = value;
            }
            catch
            {
                var settings = LoadSettingsFromFile();
                settings[key] = value;
                SaveSettingsToFile(settings);
            }
        }

        private static void RemoveSetting(string key)
        {
            try
            {
                var localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
                localSettings.Values.Remove(key);
            }
            catch
            {
                var settings = LoadSettingsFromFile();
                if (settings.Remove(key))
                {
                    SaveSettingsToFile(settings);
                }
            }
        }

        private string GenerateApiSignature(Dictionary<string, string> parameters, string apiSecret)
        {
            var sortedParams = parameters.OrderBy(p => p.Key, StringComparer.Ordinal);
            var builder = new StringBuilder();

            foreach (var kvp in sortedParams)
            {
                builder.Append(kvp.Key);
                builder.Append(kvp.Value);
            }

            builder.Append(apiSecret);

            using var md5 = MD5.Create();
            byte[] inputBytes = Encoding.UTF8.GetBytes(builder.ToString());
            byte[] hashBytes = md5.ComputeHash(inputBytes);

            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        }

        public bool IsLoggedIn()
        {
            string? sessionKey = GetSetting("LastFmSessionKey");
            return !string.IsNullOrEmpty(sessionKey);
        }

        public (string? Username, string? SessionKey) GetUserSession()
        {
            string? username = GetSetting("LastFmUsername");
            string? sessionKey = GetSetting("LastFmSessionKey");
            return (username, sessionKey);
        }

        public void SaveUserSession(string username, string sessionKey)
        {
            SetSetting("LastFmUsername", username);
            SetSetting("LastFmSessionKey", sessionKey);
        }

        public void ClearUserSession()
        {
            RemoveSetting("LastFmUsername");
            RemoveSetting("LastFmSessionKey");
        }

        public async Task<string?> RequestAuthTokenAsync()
        {
            try
            {
                string url = $"{BaseUrl}?method=auth.getToken&api_key={ApiKey}&format=json";
                var response = await _httpClient.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);

                    if (doc.RootElement.TryGetProperty("token", out var tokenElement))
                    {
                        return tokenElement.GetString();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Erro ao obter token: {ex.Message}");
            }

            return null;
        }

        public async Task OpenAuthPageInBrowserAsync(string token)
        {
            string authUrl = $"https://www.last.fm/api/auth/?api_key={ApiKey}&token={token}";

            if (Uri.TryCreate(authUrl, UriKind.Absolute, out Uri? targetUri) && targetUri != null)
            {
                await Launcher.LaunchUriAsync(targetUri);
            }
        }

        public async Task<string?> FetchSessionKeyAsync(string token)
        {
            try
            {
                var paramsForSig = new Dictionary<string, string>
                {
                    { "api_key", ApiKey },
                    { "method", "auth.getSession" },
                    { "token", token }
                };

                string apiSig = GenerateApiSignature(paramsForSig, ApiSecret);

                string url = $"{BaseUrl}?method=auth.getSession&api_key={ApiKey}&token={token}&api_sig={apiSig}&format=json";
                var response = await _httpClient.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);

                    if (doc.RootElement.TryGetProperty("session", out var session))
                    {
                        string sessionKey = session.GetProperty("key").GetString()!;
                        string username = session.GetProperty("name").GetString()!;

                        SaveUserSession(username, sessionKey);
                        return sessionKey;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Erro ao obter sessão: {ex.Message}");
            }

            return null;
        }

        public async Task<(string Username, string ImageUrl, int ScrobbleCount)?> GetUserInfoAsync(string username)
        {
            try
            {
                string url = $"{BaseUrl}?method=user.getinfo&user={Uri.EscapeDataString(username)}&api_key={ApiKey}&format=json";
                var response = await _httpClient.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);

                    if (doc.RootElement.TryGetProperty("user", out var user))
                    {
                        string name = user.GetProperty("name").GetString() ?? username;
                        int playcount = 0;

                        if (user.TryGetProperty("playcount", out var pcElement) && int.TryParse(pcElement.GetString(), out int pc))
                        {
                            playcount = pc;
                        }

                        string imageUrl = string.Empty;
                        if (user.TryGetProperty("image", out var images) && images.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var img in images.EnumerateArray())
                            {
                                if (img.TryGetProperty("size", out var size) && size.GetString() == "large")
                                {
                                    imageUrl = img.GetProperty("#text").GetString() ?? string.Empty;
                                }
                            }
                        }

                        return (name, imageUrl, playcount);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Erro ao obter info do usuario: {ex.Message}");
            }

            return null;
        }

        public async Task<List<ScrobbleTrack>> GetRecentTracksAsync(string username, int limit = 50)
        {
            var tracks = new List<ScrobbleTrack>();

            try
            {
                string requestUrl = $"{BaseUrl}?method=user.getrecenttracks&user={Uri.EscapeDataString(username)}&api_key={ApiKey}&format=json&limit={limit}";

                HttpResponseMessage response = await _httpClient.GetAsync(requestUrl);

                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Erro ao buscar scrobbles: {ex.Message}");
            }

            return tracks;
        }

        public async Task<bool> ToggleLoveTrackAsync(string track, string artist, bool love, string sessionKey)
        {
            await Task.Delay(100);
            return true;
        }
    }
}