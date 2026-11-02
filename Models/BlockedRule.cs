using System;

namespace FluentScrobbler.Models
{
    public enum BlockedRuleTarget
    {
        Track,
        Artist
    }

    public sealed class BlockedRule
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public BlockedRuleTarget Target { get; set; }
        public string Pattern { get; set; } = string.Empty;
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    }
}

