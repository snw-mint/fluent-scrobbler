using System;
using SQLite;

namespace Fluent Scrobbler.Models
{
    [Table("ScrobbleEntries")]
    public class ScrobbleEntry
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public string Track { get; set; } = string.Empty;

        public string Artist { get; set; } = string.Empty;

        public string Album { get; set; } = string.Empty;

        public long Timestamp { get; set; }

        public string Status { get; set; } = "Pending";
    }
}
