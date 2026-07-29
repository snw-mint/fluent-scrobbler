using System;
using System.Collections.ObjectModel;
using FluentScrobbler.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FluentScrobbler.Views
{
    public sealed partial class FavoritesPage : Page
    {
        public ObservableCollection<ScrobbleItem> FavoriteTracks { get; } = new();

        public FavoritesPage()
        {
            this.InitializeComponent();
            LoadFavorites();
        }

        private void LoadFavorites()
        {
            FavoriteTracks.Clear();

            FavoriteTracks.Add(new ScrobbleItem
            {
                TrackName = "Get Lucky",
                ArtistName = "Daft Punk",
                AlbumName = "Random Access Memories",
                ScrobbleCount = 142,
                Timestamp = DateTime.Now.AddMinutes(-8),
                IsFavorite = true
            });

            FavoriteTracks.Add(new ScrobbleItem
            {
                TrackName = "Resonance",
                ArtistName = "HOME",
                AlbumName = "Odyssey",
                ScrobbleCount = 210,
                Timestamp = DateTime.Now.AddHours(-5),
                IsFavorite = true
            });

            FavoriteTracks.Add(new ScrobbleItem
            {
                TrackName = "The Less I Know The Better",
                ArtistName = "Tame Impala",
                AlbumName = "Currents",
                ScrobbleCount = 108,
                Timestamp = DateTime.Now.AddDays(-1),
                IsFavorite = true
            });

            FavoritesListView.ItemsSource = FavoriteTracks;
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            LoadFavorites();
        }

        private void MenuRemoveFavorite_Click(object sender, RoutedEventArgs e)
        {
            // Placeholder remove favorite handler
        }

        private void MenuEdit_Click(object sender, RoutedEventArgs e)
        {
            // Placeholder edit handler
        }

        private void MenuDelete_Click(object sender, RoutedEventArgs e)
        {
            // Placeholder delete handler
        }
    }
}
