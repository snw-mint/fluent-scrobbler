using System.Collections.ObjectModel;
using FluentScrobbler.Models;
using Microsoft.UI.Xaml.Controls;

namespace FluentScrobbler
{
    public sealed partial class ChartsPage : Page
    {
        public ObservableCollection<ChartItem> TopArtists { get; } = new();
        public ObservableCollection<ChartItem> TopAlbums { get; } = new();
        public ObservableCollection<ChartItem> TopTracks { get; } = new();

        private bool _isInitialized = false;

        public ChartsPage()
        {
            this.InitializeComponent();
            _isInitialized = true;
            LoadChartsData();
        }

        private void LoadChartsData()
        {
            if (TopArtistsList == null || TopAlbumsList == null || TopTracksList == null)
                return;

            TopArtists.Clear();
            TopAlbums.Clear();
            TopTracks.Clear();
            TopArtists.Add(new ChartItem { Rank = 1, Title = "Daft Punk", PlayCount = 340 });
            TopArtists.Add(new ChartItem { Rank = 2, Title = "The Weeknd", PlayCount = 285 });
            TopArtists.Add(new ChartItem { Rank = 3, Title = "Tame Impala", PlayCount = 210 });
            TopArtists.Add(new ChartItem { Rank = 4, Title = "Gorillaz", PlayCount = 195 });
            TopArtists.Add(new ChartItem { Rank = 5, Title = "Kavinsky", PlayCount = 160 });
            TopAlbums.Add(new ChartItem { Rank = 1, Title = "Random Access Memories", Subtitle = "Daft Punk", PlayCount = 180 });
            TopAlbums.Add(new ChartItem { Rank = 2, Title = "After Hours", Subtitle = "The Weeknd", PlayCount = 145 });
            TopAlbums.Add(new ChartItem { Rank = 3, Title = "Currents", Subtitle = "Tame Impala", PlayCount = 130 });
            TopAlbums.Add(new ChartItem { Rank = 4, Title = "Demon Days", Subtitle = "Gorillaz", PlayCount = 115 });
            TopAlbums.Add(new ChartItem { Rank = 5, Title = "OutRun", Subtitle = "Kavinsky", PlayCount = 95 });
            TopTracks.Add(new ChartItem { Rank = 1, Title = "Get Lucky", Subtitle = "Daft Punk", PlayCount = 142 });
            TopTracks.Add(new ChartItem { Rank = 2, Title = "Starboy", Subtitle = "The Weeknd", PlayCount = 120 });
            TopTracks.Add(new ChartItem { Rank = 3, Title = "The Less I Know The Better", Subtitle = "Tame Impala", PlayCount = 108 });
            TopTracks.Add(new ChartItem { Rank = 4, Title = "Instant Crush", Subtitle = "Daft Punk", PlayCount = 94 });
            TopTracks.Add(new ChartItem { Rank = 5, Title = "Nightcall", Subtitle = "Kavinsky", PlayCount = 88 });

            TopArtistsList.ItemsSource = TopArtists;
            TopAlbumsList.ItemsSource = TopAlbums;
            TopTracksList.ItemsSource = TopTracks;
        }

        private void TimeFilterCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitialized)
            {
                LoadChartsData();
            }
        }
    }
}
