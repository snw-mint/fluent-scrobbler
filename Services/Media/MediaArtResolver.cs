using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace Fluent Scrobbler.Services.Media
{
    public class MediaArtResolver
    {
        private readonly ListenBrainzService _listenBrainzService = new();
        private readonly LastFmService _lastFmService = new();
        private static readonly ConcurrentDictionary<string, string> _artCache = new(StringComparer.OrdinalIgnoreCase);

        public async Task<string?> ResolveAlbumArtAsync(string artist, string album, string? trackTitle = null, string? lastFmArtUrl = null)
        {
            string cacheKey = $"{artist}|{album}|{trackTitle}";

            if (!string.IsNullOrWhiteSpace(lastFmArtUrl) &&
                !lastFmArtUrl.Contains("2a96cbd8b46e442fc41c2b86b821562f", StringComparison.OrdinalIgnoreCase))
            {
                _artCache[cacheKey] = lastFmArtUrl;
                return lastFmArtUrl;
            }

            if (_artCache.TryGetValue(cacheKey, out var cachedUrl) && !string.IsNullOrEmpty(cachedUrl))
            {
                return cachedUrl;
            }

            if (!string.IsNullOrWhiteSpace(artist))
            {
                if (!string.IsNullOrWhiteSpace(trackTitle))
                {
                    string? trackArt = await _lastFmService.GetTrackArtFromLastFmAsync(artist, trackTitle);
                    if (!string.IsNullOrWhiteSpace(trackArt))
                    {
                        _artCache[cacheKey] = trackArt;
                        return trackArt;
                    }
                }

                if (!string.IsNullOrWhiteSpace(album))
                {
                    string? lastFmApiArt = await _lastFmService.GetAlbumArtFromLastFmAsync(artist, album);
                    if (!string.IsNullOrWhiteSpace(lastFmApiArt))
                    {
                        _artCache[cacheKey] = lastFmApiArt;
                        return lastFmApiArt;
                    }

                    string? musicBrainzArt = await _listenBrainzService.GetAlbumCoverUrlAsync(album, artist);
                    if (!string.IsNullOrWhiteSpace(musicBrainzArt))
                    {
                        _artCache[cacheKey] = musicBrainzArt;
                        return musicBrainzArt;
                    }
                }
                else if (!string.IsNullOrWhiteSpace(trackTitle))
                {
                    string? lastFmApiArt = await _lastFmService.GetAlbumArtFromLastFmAsync(artist, trackTitle);
                    if (!string.IsNullOrWhiteSpace(lastFmApiArt))
                    {
                        _artCache[cacheKey] = lastFmApiArt;
                        return lastFmApiArt;
                    }

                    string? musicBrainzArt = await _listenBrainzService.GetAlbumCoverUrlAsync(trackTitle, artist);
                    if (!string.IsNullOrWhiteSpace(musicBrainzArt))
                    {
                        _artCache[cacheKey] = musicBrainzArt;
                        return musicBrainzArt;
                    }
                }
            }

            return null;
        }
    }
}
