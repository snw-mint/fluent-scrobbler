using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace FluentScrobbler.Models
{
    public class ScrobbleItem : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public string TrackName { get; set; } = string.Empty;
        public string ArtistName { get; set; } = string.Empty;
        public string AlbumName { get; set; } = string.Empty;

        private string _coverUrl = string.Empty;
        public string CoverUrl
        {
            get => _coverUrl;
            set
            {
                if (_coverUrl != value)
                {
                    _coverUrl = value;
                    OnPropertyChanged();
                }
            }
        }

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