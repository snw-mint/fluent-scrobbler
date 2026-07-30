using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace FluentScrobbler.Services.Media
{
    public class ListenBrainzService
    {
        private readonly HttpClient _httpClient;
        private static readonly ConcurrentDictionary<string, string?> _memoryCache = new();
        private static readonly ConcurrentDictionary<string, Task<string?>> _inFlightTasks = new();
        private static readonly SemaphoreSlim _mbRateLimiter = new(1, 1);
        private static readonly SemaphoreSlim _diskCacheLock = new(1, 1);

        private static readonly string DiskCachePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FluentScrobbler",
            "art_cache.json"
        );

        private static bool _diskCacheLoaded = false;
        private static Dictionary<string, DiskCacheEntry> _diskCache = new();

        private record DiskCacheEntry(string? Url, long ExpiresAtUnix);

        public ListenBrainzService()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "FluentScrobbler/1.0 (contact@snw-mint.app)");
            _httpClient.Timeout = TimeSpan.FromSeconds(10);
            EnsureDiskCacheLoaded();
        }

        private static void EnsureDiskCacheLoaded()
        {
            if (_diskCacheLoaded) return;
            _diskCacheLoaded = true;

            try
            {
                if (!File.Exists(DiskCachePath)) return;

                string json = File.ReadAllText(DiskCachePath);
                var loaded = JsonSerializer.Deserialize<Dictionary<string, DiskCacheEntry>>(json);
                if (loaded == null) return;

                long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                foreach (var kv in loaded)
                {
                    if (kv.Value.ExpiresAtUnix > now)
                        _diskCache[kv.Key] = kv.Value;
                }
            }
            catch
            {
            }
        }

        private static async Task PersistDiskCacheAsync()
        {
            await _diskCacheLock.WaitAsync();
            try
            {
                string dir = Path.GetDirectoryName(DiskCachePath)!;
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                await File.WriteAllTextAsync(DiskCachePath, JsonSerializer.Serialize(_diskCache));
            }
            catch
            {
            }
            finally
            {
                _diskCacheLock.Release();
            }
        }

        public Task<string?> GetAlbumCoverUrlAsync(string? albumName, string artistName, string? trackTitle = null)
        {
            if (string.IsNullOrWhiteSpace(artistName)) return Task.FromResult<string?>(null);

            string cacheKey = $"{artistName.Trim().ToLowerInvariant()}|{albumName?.Trim().ToLowerInvariant() ?? trackTitle?.Trim().ToLowerInvariant()}";

            if (_memoryCache.TryGetValue(cacheKey, out var memHit))
                return Task.FromResult(memHit);

            if (_diskCache.TryGetValue(cacheKey, out var diskHit))
            {
                _memoryCache[cacheKey] = diskHit.Url;
                return Task.FromResult(diskHit.Url);
            }

            return _inFlightTasks.GetOrAdd(cacheKey, async key =>
            {
                string? resolved = await FetchWithFallbackAsync(albumName, artistName, trackTitle);
                _memoryCache[key] = resolved;
                _diskCache[key] = new DiskCacheEntry(resolved, DateTimeOffset.UtcNow.AddHours(24).ToUnixTimeSeconds());
                _ = PersistDiskCacheAsync();
                _inFlightTasks.TryRemove(key, out _);
                return resolved;
            });
        }

        private async Task<string?> FetchWithFallbackAsync(string? albumName, string artistName, string? trackTitle)
        {
            if (!string.IsNullOrWhiteSpace(albumName))
            {
                string? caaUrl = await TryCoverArtArchiveAsync(albumName, artistName);
                if (!string.IsNullOrEmpty(caaUrl)) return caaUrl;

                string? itunesUrl = await TryItunesAsync(artistName, albumName);
                if (!string.IsNullOrEmpty(itunesUrl)) return itunesUrl;
            }
            else if (!string.IsNullOrWhiteSpace(trackTitle))
            {
                string? itunesUrl = await TryItunesTrackAsync(artistName, trackTitle);
                if (!string.IsNullOrEmpty(itunesUrl)) return itunesUrl;
            }

            return null;
        }

        private async Task<string?> TryCoverArtArchiveAsync(string albumName, string artistName)
        {
            try
            {
                string clean = EscapeLucene(albumName);
                string cleanArtist = EscapeLucene(artistName);
                string query = Uri.EscapeDataString($"releasegroup:\"{clean}\" AND artist:\"{cleanArtist}\" AND primarytype:Album");
                string searchUrl = $"https://musicbrainz.org/ws/2/release-group/?query={query}&fmt=json&limit=3";

                string? json = await ExecuteMbThrottledAsync(searchUrl);
                if (string.IsNullOrEmpty(json)) return null;

                using var doc = JsonDocument.Parse(json);
                if (!doc.RootElement.TryGetProperty("release-groups", out var rgs) || rgs.ValueKind != JsonValueKind.Array) return null;

                foreach (var rg in rgs.EnumerateArray())
                {
                    if (!rg.TryGetProperty("id", out var idProp) || idProp.GetString() is not string rgId) continue;

                    string? cover = await FetchCaaFrontAsync($"https://coverartarchive.org/release-group/{rgId}");
                    if (!string.IsNullOrEmpty(cover)) return cover;
                }
            }
            catch
            {
            }
            return null;
        }

        private async Task<string?> FetchCaaFrontAsync(string caaUrl)
        {
            try
            {
                var response = await _httpClient.GetAsync(caaUrl);
                if (!response.IsSuccessStatusCode) return null;

                string json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                if (!doc.RootElement.TryGetProperty("images", out var images) || images.ValueKind != JsonValueKind.Array) return null;

                foreach (var img in images.EnumerateArray())
                {
                    bool isFront = img.TryGetProperty("front", out var f) && f.GetBoolean();
                    if (!isFront) continue;

                    if (img.TryGetProperty("thumbnails", out var thumbs))
                    {
                        foreach (var size in new[] { "500", "250" })
                        {
                            if (thumbs.TryGetProperty(size, out var t) && t.GetString() is string tu && !string.IsNullOrEmpty(tu))
                                return tu;
                        }
                    }

                    if (img.TryGetProperty("image", out var imgUrl) && imgUrl.GetString() is string u)
                        return u;
                }
            }
            catch
            {
            }
            return null;
        }

        private async Task<string?> TryItunesAsync(string artistName, string albumName)
        {
            try
            {
                string term = Uri.EscapeDataString($"{artistName} {albumName}");
                string url = $"https://itunes.apple.com/search?term={term}&media=music&entity=album&limit=3";
                string? json = await _httpClient.GetStringAsync(url);
                return ParseItunesArtwork(json);
            }
            catch
            {
            }
            return null;
        }

        private async Task<string?> TryItunesTrackAsync(string artistName, string trackTitle)
        {
            try
            {
                string term = Uri.EscapeDataString($"{artistName} {trackTitle}");
                string url = $"https://itunes.apple.com/search?term={term}&media=music&entity=song&limit=3";
                string? json = await _httpClient.GetStringAsync(url);
                return ParseItunesArtwork(json);
            }
            catch
            {
            }
            return null;
        }

        private static string? ParseItunesArtwork(string? json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (!doc.RootElement.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array) return null;

                foreach (var result in results.EnumerateArray())
                {
                    if (result.TryGetProperty("artworkUrl100", out var art) && art.GetString() is string raw && !string.IsNullOrEmpty(raw))
                    {
                        return raw.Replace("100x100bb", "600x600bb");
                    }
                }
            }
            catch
            {
            }
            return null;
        }

        private async Task<string?> ExecuteMbThrottledAsync(string url)
        {
            await _mbRateLimiter.WaitAsync();
            try
            {
                await Task.Delay(1100);
                var response = await _httpClient.GetAsync(url);
                return response.IsSuccessStatusCode ? await response.Content.ReadAsStringAsync() : null;
            }
            finally
            {
                _mbRateLimiter.Release();
            }
        }

        private static string EscapeLucene(string input)
        {
            return Regex.Replace(input, @"[""\\ ]", m => m.Value == " " ? " " : "\\" + m.Value);
        }
    }
}