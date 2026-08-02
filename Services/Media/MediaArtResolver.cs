using System;
using System.Threading.Tasks;

namespace FluentScrobbler.Services.Media
{
    public class MediaArtResolver
    {
        private readonly ListenBrainzService _listenBrainzService = new();
        private readonly LastFmService _lastFmService = new();

        public async Task<string?> ResolveAlbumArtAsync(string artist, string album, string? trackTitle = null, string? lastFmArtUrl = null)
        {
            if (!string.IsNullOrWhiteSpace(lastFmArtUrl) &&
                !lastFmArtUrl.Contains("2a96cbd8b46e442fc41c2b86b821562f", StringComparison.OrdinalIgnoreCase))
            {
                return lastFmArtUrl;
            }

            if (!string.IsNullOrWhiteSpace(artist))
            {
                if (!string.IsNullOrWhiteSpace(trackTitle))
                {
                    string? trackArt = await _lastFmService.GetTrackArtFromLastFmAsync(artist, trackTitle);
                    if (!string.IsNullOrWhiteSpace(trackArt))
                    {
                        return trackArt;
                    }
                }

                if (!string.IsNullOrWhiteSpace(album))
                {
                    string? lastFmApiArt = await _lastFmService.GetAlbumArtFromLastFmAsync(artist, album);
                    if (!string.IsNullOrWhiteSpace(lastFmApiArt))
                    {
                        return lastFmApiArt;
                    }

                    string? musicBrainzArt = await _listenBrainzService.GetAlbumCoverUrlAsync(album, artist);
                    if (!string.IsNullOrWhiteSpace(musicBrainzArt))
                    {
                        return musicBrainzArt;
                    }
                }
                else if (!string.IsNullOrWhiteSpace(trackTitle))
                {
                    string? lastFmApiArt = await _lastFmService.GetAlbumArtFromLastFmAsync(artist, trackTitle);
                    if (!string.IsNullOrWhiteSpace(lastFmApiArt))
                    {
                        return lastFmApiArt;
                    }

                    string? musicBrainzArt = await _listenBrainzService.GetAlbumCoverUrlAsync(trackTitle, artist);
                    if (!string.IsNullOrWhiteSpace(musicBrainzArt))
                    {
                        return musicBrainzArt;
                    }
                }
            }

            return null;
        }
    }
}