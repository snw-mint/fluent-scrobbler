using System;

namespace FluentScrobbler.Models
{
    public class ScrobbleItem
    {
        public string TrackName { get; set; } = string.Empty;
        public string ArtistName { get; set; } = string.Empty;
        public string AlbumName { get; set; } = string.Empty;
        public string CoverUrl { get; set; } = string.Empty;
        public int ScrobbleCount { get; set; }
        public DateTime Timestamp { get; set; }
        public bool IsNowPlaying { get; set; }
        public bool IsFavorite { get; set; }

        public string TimeFormatted => IsNowPlaying ? "Scrobbling Now..." : FormatTimeAgo(Timestamp);
        public string ScrobbleCountText => $"{ScrobbleCount} scrobbles";

        private static string FormatTimeAgo(DateTime dt)
        {
            var span = DateTime.Now - dt;
            if (span.TotalMinutes < 1) return "Just now";
            if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes}m ago";
            if (span.TotalHours < 24) return $"{(int)span.TotalHours}h ago";
            return dt.ToString("MMM dd, HH:mm");
        }
    }
}
