using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace FluentScrobbler.Services.Media
{
    public class DeezerService
    {
        private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(10) };
        private const string BaseUrl = "https://api.deezer.com";
        private const string PlaceholderHash = "d41d8cd98f00b204e9800998ecf8427e";

        private static bool IsPlaceholder(string? url)
        {
            if (string.IsNullOrWhiteSpace(url)) return true;
            return url.Contains(PlaceholderHash, StringComparison.OrdinalIgnoreCase);
        }

        private static string Clean(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return string.Empty;
            return s.Replace("\"", "").Replace("'", "").Trim();
        }

        private static string StripExtra(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return string.Empty;
            var r = Regex.Replace(s, @"\s*[\(\[](remaster|deluxe|bonus|version|edition|mono|stereo|anniversary|feat|ft)[\s\S]*?[\)\]]", "", RegexOptions.IgnoreCase);
            r = Regex.Replace(r, @"\s*-\s*(remaster|deluxe|bonus|version|edition)[\s\S]*$", "", RegexOptions.IgnoreCase);
            return r.Trim();
        }

        public async Task<string?> GetAlbumArtUrlAsync(string artist, string album)
        {
            if (string.IsNullOrWhiteSpace(artist) && string.IsNullOrWhiteSpace(album)) return null;

            var art = await SearchAlbumCoreAsync(artist, album);
            if (!string.IsNullOrWhiteSpace(art)) return art;

            var cleanAlbum = StripExtra(album);
            if (!string.Equals(cleanAlbum, album, StringComparison.OrdinalIgnoreCase))
            {
                art = await SearchAlbumCoreAsync(artist, cleanAlbum);
                if (!string.IsNullOrWhiteSpace(art)) return art;
            }

            return null;
        }

        public async Task<string?> GetTrackArtUrlAsync(string artist, string track, string? album = null)
        {
            if (string.IsNullOrWhiteSpace(artist) && string.IsNullOrWhiteSpace(track)) return null;

            var art = await SearchTrackCoreAsync(artist, track, album);
            if (!string.IsNullOrWhiteSpace(art)) return art;

            var cleanTrack = StripExtra(track);
            if (!string.Equals(cleanTrack, track, StringComparison.OrdinalIgnoreCase))
            {
                art = await SearchTrackCoreAsync(artist, cleanTrack, album);
                if (!string.IsNullOrWhiteSpace(art)) return art;
            }

            return null;
        }

        private async Task<string?> SearchAlbumCoreAsync(string artist, string album)
        {
            try
            {
                var q = $"{Clean(album)} {Clean(artist)}".Trim();
                if (string.IsNullOrEmpty(q)) return null;

                var url = $"{BaseUrl}/search/album?q={Uri.EscapeDataString(q)}";
                using var res = await _http.GetAsync(url);
                if (!res.IsSuccessStatusCode) return null;

                var json = await res.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
                    return null;

                var cands = new List<(int score, string cover)>();
                var targetArtist = artist.Trim();
                var targetAlbum = album.Trim();

                foreach (var el in data.EnumerateArray())
                {
                    string? cov = null;
                    if (el.TryGetProperty("cover_medium", out var cmProp))
                        cov = cmProp.GetString();
                    if (string.IsNullOrWhiteSpace(cov) && el.TryGetProperty("cover", out var cProp))
                        cov = cProp.GetString();

                    if (IsPlaceholder(cov) || string.IsNullOrWhiteSpace(cov)) continue;

                    var itemTitle = el.TryGetProperty("title", out var tProp) ? tProp.GetString() ?? "" : "";
                    var itemArtist = el.TryGetProperty("artist", out var aProp) && aProp.TryGetProperty("name", out var nProp) ? nProp.GetString() ?? "" : "";

                    bool matchArt = string.Equals(itemArtist.Trim(), targetArtist, StringComparison.OrdinalIgnoreCase);
                    bool matchAlb = string.Equals(itemTitle.Trim(), targetAlbum, StringComparison.OrdinalIgnoreCase);

                    if (matchArt && matchAlb)
                    {
                        return cov;
                    }

                    int sc = 0;
                    if (matchArt) sc += 5;
                    else if (itemArtist.Contains(targetArtist, StringComparison.OrdinalIgnoreCase) || targetArtist.Contains(itemArtist, StringComparison.OrdinalIgnoreCase)) sc += 2;

                    if (matchAlb) sc += 5;
                    else if (itemTitle.Contains(targetAlbum, StringComparison.OrdinalIgnoreCase) || targetAlbum.Contains(itemTitle, StringComparison.OrdinalIgnoreCase)) sc += 2;

                    cands.Add((sc, cov));
                }

                if (cands.Count > 0)
                {
                    cands.Sort((a, b) => b.score.CompareTo(a.score));
                    return cands[0].cover;
                }
            }
            catch
            {
            }
            return null;
        }

        private async Task<string?> SearchTrackCoreAsync(string artist, string track, string? album)
        {
            try
            {
                var q = $"{Clean(track)} {Clean(artist)}".Trim();
                if (string.IsNullOrEmpty(q)) return null;

                var url = $"{BaseUrl}/search/track?q={Uri.EscapeDataString(q)}";
                using var res = await _http.GetAsync(url);
                if (!res.IsSuccessStatusCode) return null;

                var json = await res.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
                    return null;

                var cands = new List<(int score, string cover)>();
                var targetArtist = artist.Trim();
                var targetTrack = track.Trim();
                var targetAlbum = album?.Trim() ?? string.Empty;

                foreach (var el in data.EnumerateArray())
                {
                    string? cov = null;
                    string albTitle = "";
                    if (el.TryGetProperty("album", out var albProp))
                    {
                        if (albProp.TryGetProperty("cover_medium", out var cmProp))
                            cov = cmProp.GetString();
                        if (string.IsNullOrWhiteSpace(cov) && albProp.TryGetProperty("cover", out var cProp))
                            cov = cProp.GetString();
                        if (albProp.TryGetProperty("title", out var atProp))
                            albTitle = atProp.GetString() ?? "";
                    }

                    if (string.IsNullOrWhiteSpace(cov) && el.TryGetProperty("cover_medium", out var tcmProp))
                        cov = tcmProp.GetString();

                    if (IsPlaceholder(cov) || string.IsNullOrWhiteSpace(cov)) continue;

                    var itemTitle = el.TryGetProperty("title", out var tProp) ? tProp.GetString() ?? "" : "";
                    var itemArtist = el.TryGetProperty("artist", out var aProp) && aProp.TryGetProperty("name", out var nProp) ? nProp.GetString() ?? "" : "";

                    bool matchArt = string.Equals(itemArtist.Trim(), targetArtist, StringComparison.OrdinalIgnoreCase);
                    bool matchTrk = string.Equals(itemTitle.Trim(), targetTrack, StringComparison.OrdinalIgnoreCase);

                    if (matchArt && matchTrk)
                    {
                        if (string.IsNullOrEmpty(targetAlbum) || string.Equals(albTitle.Trim(), targetAlbum, StringComparison.OrdinalIgnoreCase))
                        {
                            return cov;
                        }
                    }

                    int sc = 0;
                    if (matchArt) sc += 4;
                    else if (itemArtist.Contains(targetArtist, StringComparison.OrdinalIgnoreCase)) sc += 1;

                    if (matchTrk) sc += 4;
                    else if (itemTitle.Contains(targetTrack, StringComparison.OrdinalIgnoreCase)) sc += 1;

                    if (!string.IsNullOrEmpty(targetAlbum) && string.Equals(albTitle.Trim(), targetAlbum, StringComparison.OrdinalIgnoreCase))
                        sc += 3;

                    cands.Add((sc, cov));
                }

                if (cands.Count > 0)
                {
                    cands.Sort((a, b) => b.score.CompareTo(a.score));
                    return cands[0].cover;
                }
            }
            catch
            {
            }
            return null;
        }
    }
}
