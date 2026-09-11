using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace FluentScrobbler.Services.Media
{
    public class MediaArtResolver
    {
        private readonly DeezerService _deezer = new();
        private readonly CoverCacheService _cache = CoverCacheService.Instance;
        private readonly ListenBrainzService _mb = new();
        private static readonly ConcurrentDictionary<string, (string LocalUri, string RemoteUrl)> _mem = new(StringComparer.OrdinalIgnoreCase);

        public async Task<string?> ResolveAlbumArtAsync(string artist, string album, string? trackTitle = null, string? lastFmArtUrl = null)
        {
            var res = await ResolveArtCoreAsync(artist, album, trackTitle);
            return res.LocalUri;
        }

        public async Task<string?> ResolveRemoteArtUrlAsync(string artist, string album, string? trackTitle = null)
        {
            var res = await ResolveArtCoreAsync(artist, album, trackTitle);
            return res.RemoteUrl;
        }

        private async Task<(string? LocalUri, string? RemoteUrl)> ResolveArtCoreAsync(string artist, string album, string? trackTitle)
        {
            if (string.IsNullOrWhiteSpace(artist)) return (null, null);

            var key = $"{artist}|{album}|{trackTitle}".ToLowerInvariant();

            if (_mem.TryGetValue(key, out var m) && !string.IsNullOrEmpty(m.LocalUri))
            {
                return m;
            }

            var cached = await _cache.GetCachedAsync(key);
            if (!string.IsNullOrEmpty(cached.LocalUri))
            {
                _mem[key] = (cached.LocalUri, cached.RemoteUrl ?? string.Empty);
                return cached;
            }

            string? remote = null;

            if (!string.IsNullOrWhiteSpace(album))
            {
                remote = await _deezer.GetAlbumArtUrlAsync(artist, album);
            }

            if (string.IsNullOrWhiteSpace(remote) && !string.IsNullOrWhiteSpace(trackTitle))
            {
                remote = await _deezer.GetTrackArtUrlAsync(artist, trackTitle, album);
            }

            if (string.IsNullOrWhiteSpace(remote) && !string.IsNullOrWhiteSpace(album))
            {
                remote = await _mb.GetAlbumCoverUrlAsync(album, artist);
            }

            if (string.IsNullOrWhiteSpace(remote) && !string.IsNullOrWhiteSpace(trackTitle))
            {
                remote = await _mb.GetAlbumCoverUrlAsync(trackTitle, artist);
            }

            if (!string.IsNullOrWhiteSpace(remote))
            {
                var saved = await _cache.SaveAndCacheAsync(key, remote);
                if (!string.IsNullOrEmpty(saved.LocalUri))
                {
                    _mem[key] = (saved.LocalUri, saved.RemoteUrl ?? remote);
                    return saved;
                }
                return (remote, remote);
            }

            return (null, null);
        }
    }
}