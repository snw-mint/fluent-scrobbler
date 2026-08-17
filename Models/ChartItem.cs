namespace FluentScrobbler.Models
{
    public class ChartItem
    {
        public int Rank { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Subtitle { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = string.Empty;
        public int PlayCount { get; set; }

        public string RankText => $"#{Rank}";
        public string PlayCountText => $"{PlayCount} plays";
    }
}
