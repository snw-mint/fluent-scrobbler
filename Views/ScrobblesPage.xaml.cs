using System;
using System.Collections.ObjectModel;
using FluentScrobbler.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FluentScrobbler.Views
{
    public sealed partial class ScrobblesPage : Page
    {
        public ObservableCollection<ScrobbleItem> Scrobbles { get; } = new();

        public ScrobblesPage()
        {
            this.InitializeComponent();
            LoadData();
        }

        private void LoadData()
        {
            Scrobbles.Clear();

            Scrobbles.Add(new ScrobbleItem
            {
                TrackName = "Get Lucky",
                ArtistName = "Daft Punk",
                AlbumName = "Random Access Memories",
                ScrobbleCount = 142,
                Timestamp = DateTime.Now.AddMinutes(-8),
                IsFavorite = true
            });

            Scrobbles.Add(new ScrobbleItem
            {
                TrackName = "Blinding Lights",
                ArtistName = "The Weeknd",
                AlbumName = "After Hours",
                ScrobbleCount = 98,
                Timestamp = DateTime.Now.AddMinutes(-24)
            });

            Scrobbles.Add(new ScrobbleItem
            {
                TrackName = "Midnight City",
                ArtistName = "M83",
                AlbumName = "Hurry Up, We're Dreaming",
                ScrobbleCount = 76,
                Timestamp = DateTime.Now.AddHours(-2)
            });

            Scrobbles.Add(new ScrobbleItem
            {
                TrackName = "Resonance",
                ArtistName = "HOME",
                AlbumName = "Odyssey",
                ScrobbleCount = 210,
                Timestamp = DateTime.Now.AddHours(-5),
                IsFavorite = true
            });

            Scrobbles.Add(new ScrobbleItem
            {
                TrackName = "Instant Crush",
                ArtistName = "Daft Punk feat. Julian Casablancas",
                AlbumName = "Random Access Memories",
                ScrobbleCount = 85,
                Timestamp = DateTime.Now.AddHours(-8)
            });

            ScrobblesListView.ItemsSource = Scrobbles;
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            LoadData();
        }

        private void MenuFavorite_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Content is FluentIcons.WinUI.SymbolIcon icon)
            {
                icon.IconVariant = icon.IconVariant == FluentIcons.Common.IconVariant.Filled
                    ? FluentIcons.Common.IconVariant.Regular
                    : FluentIcons.Common.IconVariant.Filled;
            }
        }

        private void MenuEdit_Click(object sender, RoutedEventArgs e)
        {
        }

        private void MenuDelete_Click(object sender, RoutedEventArgs e)
        {
        }

        private void MenuBlock_Click(object sender, RoutedEventArgs e)
        {
        }

        private void MenuOpenInfo_Click(object sender, RoutedEventArgs e)
        {
        }
    }
}
