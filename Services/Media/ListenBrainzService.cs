using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace FluentScrobbler.Services.Media
{
    public class ListenBrainzService
    {
        private readonly HttpClient _httpClient;
        private static readonly ConcurrentDictionary<string, string?> _artCache = new();
        private static readonly ConcurrentDictionary<string, Task<string?>> _inFlightTasks = new();

        public ListenBrainzService()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "FluentScrobbler/1.0 (contact@snw-mint.app)");
        }

        public Task<string?> GetAlbumCoverUrlAsync(string? albumName, string artistName, string? trackTitle = null)
        {
            if (string.IsNullOrWhiteSpace(artistName)) return Task.FromResult<string?>(null);

            string cacheKey = $"{artistName.Trim().ToLowerInvariant()}|{albumName?.Trim().ToLowerInvariant()}|{trackTitle?.Trim().ToLowerInvariant()}";
            if (_artCache.TryGetValue(cacheKey, out var cachedUrl))
            {
                return Task.FromResult(cachedUrl);
            }

            return _inFlightTasks.GetOrAdd(cacheKey, async key =>
            {
                string? resolvedUrl = await FetchAlbumCoverUrlAsync(albumName, artistName, trackTitle);
                _artCache[key] = resolvedUrl;
                _inFlightTasks.TryRemove(key, out _);
                return resolvedUrl;
            });
        }

        private async Task<string?> FetchAlbumCoverUrlAsync(string? albumName, string artistName, string? trackTitle)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(albumName))
                {
                    string cleanAlbum = EscapeLucene(albumName);
                    string cleanArtist = EscapeLucene(artistName);

                    string rgQuery = Uri.EscapeDataString($"releasegroup:\"{cleanAlbum}\" AND artist:\"{cleanArtist}\" AND primarytype:Album");
                    string rgSearchUrl = $"https://musicbrainz.org/ws/2/release-group/?query={rgQuery}&fmt=json&limit=1";
                    string? rgCover = await TryGetCoverFromReleaseGroupSearchAsync(rgSearchUrl);
                    if (!string.IsNullOrEmpty(rgCover)) return rgCover;

                    string releaseQuery = Uri.EscapeDataString($"release:\"{cleanAlbum}\" AND artist:\"{cleanArtist}\" AND status:official");
                    string releaseSearchUrl = $"https://musicbrainz.org/ws/2/release/?query={releaseQuery}&fmt=json&limit=1";
                    string? releaseCover = await TryGetCoverFromReleaseSearchAsync(releaseSearchUrl);
                    if (!string.IsNullOrEmpty(releaseCover)) return releaseCover;
                }

                if (!string.IsNullOrWhiteSpace(trackTitle))
                {
                    string cleanTrack = EscapeLucene(trackTitle);
                    string cleanArtist = EscapeLucene(artistName);
                    string recQuery = Uri.EscapeDataString($"recording:\"{cleanTrack}\" AND artist:\"{cleanArtist}\"");
                    string recSearchUrl = $"https://musicbrainz.org/ws/2/recording/?query={recQuery}&fmt=json&limit=1";
                    string? recCover = await TryGetCoverFromRecordingSearchAsync(recSearchUrl);
                    if (!string.IsNullOrEmpty(recCover)) return recCover;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ListenBrainzService] Erro: {ex.Message}");
            }

            return null;
        }

        private async Task<string?> TryGetCoverFromReleaseGroupSearchAsync(string searchUrl)
        {
            var response = await _httpClient.GetAsync(searchUrl);
            if (response.IsSuccessStatusCode)
            {
                string json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("release-groups", out var rgs) && rgs.ValueKind == JsonValueKind.Array && rgs.GetArrayLength() > 0)
                {
                    var firstRg = rgs[0];
                    if (firstRg.TryGetProperty("id", out var idProp) && idProp.GetString() is string rgid)
                    {
                        return await FetchCaaImageAsync($"https://coverartarchive.org/release-group/{rgid}");
                    }
                }
            }
            return null;
        }

        private async Task<string?> TryGetCoverFromReleaseSearchAsync(string searchUrl)
        {
            var response = await _httpClient.GetAsync(searchUrl);
            if (response.IsSuccessStatusCode)
            {
                string json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("releases", out var releases) && releases.ValueKind == JsonValueKind.Array && releases.GetArrayLength() > 0)
                {
                    var firstRel = releases[0];
                    if (firstRel.TryGetProperty("id", out var idProp) && idProp.GetString() is string mbid)
                    {
                        return await FetchCaaImageAsync($"https://coverartarchive.org/release/{mbid}");
                    }
                }
            }
            return null;
        }

        private async Task<string?> TryGetCoverFromRecordingSearchAsync(string searchUrl)
        {
            var response = await _httpClient.GetAsync(searchUrl);
            if (response.IsSuccessStatusCode)
            {
                string json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("recordings", out var recs) && recs.ValueKind == JsonValueKind.Array && recs.GetArrayLength() > 0)
                {
                    var firstRec = recs[0];
                    if (firstRec.TryGetProperty("releases", out var recReleases) && recReleases.ValueKind == JsonValueKind.Array && recReleases.GetArrayLength() > 0)
                    {
                        foreach (var rel in recReleases.EnumerateArray())
                        {
                            if (rel.TryGetProperty("id", out var idProp) && idProp.GetString() is string mbid)
                            {
                                string? cover = await FetchCaaImageAsync($"https://coverartarchive.org/release/{mbid}");
                                if (!string.IsNullOrEmpty(cover)) return cover;
                            }
                        }
                    }
                }
            }
            return null;
        }

        private async Task<string?> FetchCaaImageAsync(string caaUrl)
        {
            try
            {
                var caaResponse = await _httpClient.GetAsync(caaUrl);
                if (caaResponse.IsSuccessStatusCode)
                {
                    string caaJson = await caaResponse.Content.ReadAsStringAsync();
                    using var caaDoc = JsonDocument.Parse(caaJson);
                    if (caaDoc.RootElement.TryGetProperty("images", out var images) && images.ValueKind == JsonValueKind.Array && images.GetArrayLength() > 0)
                    {
                        foreach (var img in images.EnumerateArray())
                        {
                            bool isFront = img.TryGetProperty("front", out var frontProp) && frontProp.GetBoolean();
                            if (isFront && img.TryGetProperty("image", out var frontImgUrl) && !string.IsNullOrEmpty(frontImgUrl.GetString()))
                            {
                                return frontImgUrl.GetString();
                            }
                        }

                        var firstImg = images[0];
                        if (firstImg.TryGetProperty("image", out var imgUrl) && !string.IsNullOrEmpty(imgUrl.GetString()))
                        {
                            return imgUrl.GetString();
                        }
                    }
                }
            }
            catch
            {
            }
            return null;
        }

        private static string EscapeLucene(string input)
        {
            return Regex.Replace(input, @"[""\\]", @"\$0");
        }
    }
}