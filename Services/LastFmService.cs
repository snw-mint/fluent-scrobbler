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
                    return JsonSerializer.Deserialize(json, AppJsonContext.Default.DictionaryStringString) ?? new Dictionary<string, string>();
                }
                if (File.Exists("secrets.json"))
                {
                    string json = File.ReadAllText("secrets.json");
                    return JsonSerializer.Deserialize(json, AppJsonContext.Default.DictionaryStringString) ?? new Dictionary<string, string>();
                }
                if (File.Exists(LocalSecretsFilePath))
                {
                    string json = File.ReadAllText(LocalSecretsFilePath);
                    return JsonSerializer.Deserialize(json, AppJsonContext.Default.DictionaryStringString) ?? new Dictionary<string, string>();
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
                    return JsonSerializer.Deserialize(json, AppJsonContext.Default.DictionaryStringString) ?? new Dictionary<string, string>();
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
                string json = JsonSerializer.Serialize(settings, AppJsonContext.Default.DictionaryStringString);
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

        public async Task OpenAuthPageInBrowserAsync(string? token = null)
        {
            string authUrl = string.IsNullOrEmpty(token)
                ? $"https://www.last.fm/api/auth/?api_key={ApiKey}"
                : $"https://www.last.fm/api/auth/?api_key={ApiKey}&token={token}";

            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = authUrl,
                    UseShellExecute = true
                });
            }
            catch
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "cmd",
                        Arguments = $"/c start \"\" \"{authUrl.Replace("&", "^&")}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    });
                }
                catch
                {
                }
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

        public async Task<List<ScrobbleTrack>> GetRecentTracksAsync(string username, int limit = 50, long? fromTimestamp = null)
        {
            var tracks = new List<ScrobbleTrack>();

            try
            {
                string requestUrl = $"{BaseUrl}?method=user.getrecenttracks&user={Uri.EscapeDataString(username)}&api_key={ApiKey}&format=json&limit={limit}&extended=1";
                if (fromTimestamp.HasValue)
                {
                    requestUrl += $"&from={fromTimestamp.Value}";
                }

                HttpResponseMessage response = await _httpClient.GetAsync(requestUrl);

                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);

                    if (doc.RootElement.TryGetProperty("recenttracks", out var recentTracksElement) &&
                        recentTracksElement.TryGetProperty("track", out var trackElement))
                    {
                        var trackList = new List<JsonElement>();
                        if (trackElement.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var el in trackElement.EnumerateArray())
                            {
                                trackList.Add(el);
                            }
                        }
                        else if (trackElement.ValueKind == JsonValueKind.Object)
                        {
                            trackList.Add(trackElement);
                        }

                        foreach (var item in trackList)
                        {
                            var track = new ScrobbleTrack();
                            if (item.TryGetProperty("name", out var nameProp))
                            {
                                track.Name = nameProp.GetString() ?? string.Empty;
                            }

                            if (item.TryGetProperty("artist", out var artistProp))
                            {
                                if (artistProp.ValueKind == JsonValueKind.Object)
                                {
                                    if (artistProp.TryGetProperty("name", out var artistName))
                                        track.Artist = artistName.GetString() ?? string.Empty;
                                    else if (artistProp.TryGetProperty("#text", out var artistText))
                                        track.Artist = artistText.GetString() ?? string.Empty;
                                }
                                else if (artistProp.ValueKind == JsonValueKind.String)
                                {
                                    track.Artist = artistProp.GetString() ?? string.Empty;
                                }
                            }

                            if (item.TryGetProperty("album", out var albumProp))
                            {
                                if (albumProp.ValueKind == JsonValueKind.Object && albumProp.TryGetProperty("#text", out var albumText))
                                {
                                    track.Album = albumText.GetString() ?? string.Empty;
                                }
                                else if (albumProp.ValueKind == JsonValueKind.String)
                                {
                                    track.Album = albumProp.GetString() ?? string.Empty;
                                }
                            }

                            if (item.TryGetProperty("image", out var imagesProp) && imagesProp.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var img in imagesProp.EnumerateArray())
                                {
                                    if (img.TryGetProperty("size", out var sizeProp) && sizeProp.GetString() == "large")
                                    {
                                        if (img.TryGetProperty("#text", out var imgText))
                                            track.AlbumArtUrl = imgText.GetString() ?? string.Empty;
                                    }
                                }
                            }

                            bool hasNowPlayingAttr = item.TryGetProperty("@attr", out var attrProp) &&
                                                     attrProp.ValueKind == JsonValueKind.Object &&
                                                     attrProp.TryGetProperty("nowplaying", out var nowPlayingProp) &&
                                                     (nowPlayingProp.ValueKind == JsonValueKind.True ||
                                                      (nowPlayingProp.ValueKind == JsonValueKind.String &&
                                                       (string.Equals(nowPlayingProp.GetString(), "true", StringComparison.OrdinalIgnoreCase) ||
                                                        nowPlayingProp.GetString() == "1")));

                            bool isFirstTrackWithoutDate = !item.TryGetProperty("date", out _) && tracks.Count == 0;

                            if (hasNowPlayingAttr || isFirstTrackWithoutDate)
                            {
                                track.IsNowPlaying = true;
                            }

                            if (item.TryGetProperty("loved", out var lovedProp))
                            {
                                track.IsLoved = lovedProp.GetString() == "1";
                            }

                            if (item.TryGetProperty("date", out var dateProp) &&
                                dateProp.TryGetProperty("uts", out var utsProp) &&
                                long.TryParse(utsProp.GetString(), out long uts))
                            {
                                track.PlayedAt = DateTimeOffset.FromUnixTimeSeconds(uts);
                            }

                            tracks.Add(track);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Erro ao buscar scrobbles: {ex.Message}");
            }

            return tracks;
        }

        public async Task<List<string>> GetArtistTopTagsAsync(string artist, int count = 5)
        {
            var tags = new List<string>();
            try
            {
                string url = $"{BaseUrl}?method=artist.gettoptags&artist={Uri.EscapeDataString(artist)}&api_key={ApiKey}&format=json";
                var response = await _httpClient.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("toptags", out var topTags) &&
                        topTags.TryGetProperty("tag", out var tagArray) &&
                        tagArray.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var tag in tagArray.EnumerateArray())
                        {
                            if (tag.TryGetProperty("name", out var tagName))
                            {
                                string name = tagName.GetString() ?? string.Empty;
                                if (!string.IsNullOrWhiteSpace(name))
                                {
                                    tags.Add(name.ToLowerInvariant());
                                }
                            }
                            if (tags.Count >= count) break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Erro ao buscar tags do artista: {ex.Message}");
            }
            return tags;
        }

        public async Task<bool> UpdateNowPlayingAsync(string track, string artist, string album = "")
        {
            var (_, sessionKey) = GetUserSession();
            if (string.IsNullOrEmpty(sessionKey)) return false;

            try
            {
                var parameters = new Dictionary<string, string>
                {
                    { "api_key", ApiKey },
                    { "artist", artist },
                    { "method", "track.updateNowPlaying" },
                    { "sk", sessionKey },
                    { "track", track }
                };

                if (!string.IsNullOrWhiteSpace(album))
                {
                    parameters["album"] = album;
                }

                string apiSig = GenerateApiSignature(parameters, ApiSecret);
                parameters["api_sig"] = apiSig;
                parameters["format"] = "json";

                var content = new FormUrlEncodedContent(parameters);
                var response = await _httpClient.PostAsync(BaseUrl, content);

                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync();
                    return !json.Contains("error");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Erro ao atualizar Now Playing: {ex.Message}");
            }
            return false;
        }

        public async Task<bool> ScrobbleTrackAsync(string track, string artist, string album = "", long? timestamp = null)
        {
            var (_, sessionKey) = GetUserSession();
            if (string.IsNullOrEmpty(sessionKey)) return false;

            try
            {
                long uts = timestamp ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                var parameters = new Dictionary<string, string>
                {
                    { "api_key", ApiKey },
                    { "artist", artist },
                    { "method", "track.scrobble" },
                    { "sk", sessionKey },
                    { "timestamp", uts.ToString() },
                    { "track", track }
                };

                if (!string.IsNullOrWhiteSpace(album))
                {
                    parameters["album"] = album;
                }

                string apiSig = GenerateApiSignature(parameters, ApiSecret);
                parameters["api_sig"] = apiSig;
                parameters["format"] = "json";

                var content = new FormUrlEncodedContent(parameters);
                var response = await _httpClient.PostAsync(BaseUrl, content);

                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync();
                    return !json.Contains("error");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Erro ao enviar scrobble: {ex.Message}");
            }
            return false;
        }

        public async Task<bool> ToggleLoveTrackAsync(string track, string artist, bool love, string sessionKey)
        {
            await Task.Delay(100);
            return true;
        }
    }
}