using System;

namespace FluentScrobbler.Models
{
    public class ScrobbleTrack
    {
        public string Name { get; set; } = string.Empty;
        public string Artist { get; set; } = string.Empty;
        public string Album { get; set; } = string.Empty;
        public string AlbumArtUrl { get; set; } = string.Empty;
        public bool IsLoved { get; set; }
        public bool IsNowPlaying { get; set; }
        public DateTimeOffset? PlayedAt { get; set; }
    }
}