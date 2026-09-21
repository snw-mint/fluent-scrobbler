using System;

namespace FluentScrobbler.Models
{
    public enum LegacyPlaybackState
    {
        NotRunning,
        Stopped,
        Paused,
        Playing
    }

    public class LegacyTrackInfo : IEquatable<LegacyTrackInfo>
    {
        public string Artist { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public LegacyPlaybackState State { get; set; } = LegacyPlaybackState.NotRunning;

        public bool IsValid => !string.IsNullOrWhiteSpace(Artist) && !string.IsNullOrWhiteSpace(Title);

        public bool IsPlaying => State == LegacyPlaybackState.Playing && IsValid;

        public bool Equals(LegacyTrackInfo? other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;

            return string.Equals(Artist?.Trim(), other.Artist?.Trim(), StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(Title?.Trim(), other.Title?.Trim(), StringComparison.OrdinalIgnoreCase) &&
                   State == other.State;
        }

        public override bool Equals(object? obj) => Equals(obj as LegacyTrackInfo);

        public override int GetHashCode()
        {
            return HashCode.Combine(
                Artist?.Trim().ToLowerInvariant() ?? string.Empty,
                Title?.Trim().ToLowerInvariant() ?? string.Empty,
                State);
        }

        public override string ToString() => $"{Artist} - {Title} [{State}]";
    }
}
