using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using FluentScrobbler.Models;
using FluentScrobbler.Services;
using FluentScrobbler.Services.Media;

namespace FluentScrobbler.Views
{
    public sealed partial class ScrobblesPage : Page
    {
        private readonly LastFmService _lastFmService = new();
        private readonly MediaArtResolver _mediaArtResolver = new();
        private readonly WindowsMediaService _windowsMediaService = new();

        public ObservableCollection<ScrobbleItem> Scrobbles { get; } = new();

        private static readonly List<ScrobbleItem> _cachedHistory = new();
        private static bool _cachedNowPlayingVisible;
        private static string? _cachedNowPlayingTrack;
        private static string? _cachedNowPlayingArtist;
        private static string? _cachedNowPlayingCoverUrl;

        public ScrobblesPage()
        {
            this.InitializeComponent();
            ApplyCachedState();
            this.Loaded += ScrobblesPage_Loaded;
        }

        private void ApplyCachedState()
        {
            if (_cachedHistory.Count > 0)
            {
                Scrobbles.Clear();
                foreach (var item in _cachedHistory)
                {
                    Scrobbles.Add(item);
                }
                ScrobblesListView.ItemsSource = Scrobbles;
            }

            if (_cachedNowPlayingVisible)
            {
                NowPlayingPanel.Visibility = Visibility.Visible;
                if (_cachedNowPlayingTrack != null) NowPlayingTrack.Text = _cachedNowPlayingTrack;
                if (_cachedNowPlayingArtist != null) NowPlayingArtist.Text = _cachedNowPlayingArtist;

                if (!string.IsNullOrEmpty(_cachedNowPlayingCoverUrl))
                {
                    try
                    {
                        NowPlayingAlbumArtImage.Source = new BitmapImage(new Uri(_cachedNowPlayingCoverUrl));
                        NowPlayingAlbumArtImage.Visibility = Visibility.Visible;
                        NowPlayingFallbackIcon.Visibility = Visibility.Collapsed;
                    }
                    catch
                    {
                        NowPlayingAlbumArtImage.Visibility = Visibility.Collapsed;
                        NowPlayingFallbackIcon.Visibility = Visibility.Visible;
                    }
                }
            }
            else
            {
                NowPlayingPanel.Visibility = Visibility.Collapsed;
            }
        }

        private async void ScrobblesPage_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadDataAsync();
        }

        private async Task LoadDataAsync()
        {
            if (!_lastFmService.IsLoggedIn())
            {
                return;
            }

            var (username, _) = _lastFmService.GetUserSession();
            if (string.IsNullOrEmpty(username))
            {
                return;
            }

            var recentTracks = await _lastFmService.GetRecentTracksAsync(username, limit: 50);
            if (recentTracks == null || recentTracks.Count == 0)
            {
                return;
            }

            var nowPlaying = recentTracks.FirstOrDefault(t => t.IsNowPlaying);
            if (nowPlaying != null)
            {
                NowPlayingPanel.Visibility = Visibility.Visible;
                NowPlayingTrack.Text = nowPlaying.Name;
                string artistAlbumStr = string.IsNullOrEmpty(nowPlaying.Album)
                    ? nowPlaying.Artist
                    : $"{nowPlaying.Artist} • {nowPlaying.Album}";
                NowPlayingArtist.Text = artistAlbumStr;

                string? coverUrl = await _mediaArtResolver.ResolveAlbumArtAsync(nowPlaying.Artist, nowPlaying.Album, nowPlaying.Name, nowPlaying.AlbumArtUrl);
                if (!string.IsNullOrEmpty(coverUrl))
                {
                    try
                    {
                        NowPlayingAlbumArtImage.Source = new BitmapImage(new Uri(coverUrl));
                        NowPlayingAlbumArtImage.Visibility = Visibility.Visible;
                        NowPlayingFallbackIcon.Visibility = Visibility.Collapsed;
                    }
                    catch
                    {
                        NowPlayingAlbumArtImage.Visibility = Visibility.Collapsed;
                        NowPlayingFallbackIcon.Visibility = Visibility.Visible;
                    }
                }
                else
                {
                    NowPlayingAlbumArtImage.Visibility = Visibility.Collapsed;
                    NowPlayingFallbackIcon.Visibility = Visibility.Visible;
                }

                _cachedNowPlayingVisible = true;
                _cachedNowPlayingTrack = nowPlaying.Name;
                _cachedNowPlayingArtist = artistAlbumStr;
                _cachedNowPlayingCoverUrl = coverUrl;
            }
            else
            {
                var winMedia = await _windowsMediaService.GetCurrentWindowsMediaAsync();
                if (winMedia.HasValue)
                {
                    var (winTitle, winArtist, winAlbum, _) = winMedia.Value;
                    NowPlayingPanel.Visibility = Visibility.Visible;
                    NowPlayingTrack.Text = winTitle;
                    string artistAlbumStr = string.IsNullOrEmpty(winAlbum) ? winArtist : $"{winArtist} • {winAlbum}";
                    NowPlayingArtist.Text = artistAlbumStr;

                    string? coverUrl = await _mediaArtResolver.ResolveAlbumArtAsync(winArtist, winAlbum, winTitle);
                    if (!string.IsNullOrEmpty(coverUrl))
                    {
                        try
                        {
                            NowPlayingAlbumArtImage.Source = new BitmapImage(new Uri(coverUrl));
                            NowPlayingAlbumArtImage.Visibility = Visibility.Visible;
                            NowPlayingFallbackIcon.Visibility = Visibility.Collapsed;
                        }
                        catch
                        {
                            NowPlayingAlbumArtImage.Visibility = Visibility.Collapsed;
                            NowPlayingFallbackIcon.Visibility = Visibility.Visible;
                        }
                    }
                    else
                    {
                        NowPlayingAlbumArtImage.Visibility = Visibility.Collapsed;
                        NowPlayingFallbackIcon.Visibility = Visibility.Visible;
                    }

                    _cachedNowPlayingVisible = true;
                    _cachedNowPlayingTrack = winTitle;
                    _cachedNowPlayingArtist = artistAlbumStr;
                    _cachedNowPlayingCoverUrl = coverUrl;
                }
                else
                {
                    NowPlayingPanel.Visibility = Visibility.Collapsed;
                    _cachedNowPlayingVisible = false;
                    _cachedNowPlayingTrack = null;
                    _cachedNowPlayingArtist = null;
                    _cachedNowPlayingCoverUrl = null;
                }
            }

            var historyTracks = recentTracks.Where(t => !t.IsNowPlaying).Take(5).ToList();
            var historyTasks = historyTracks.Select(async t =>
            {
                string? resolvedCover = await _mediaArtResolver.ResolveAlbumArtAsync(t.Artist, t.Album, t.Name, t.AlbumArtUrl);
                return new ScrobbleItem
                {
                    TrackName = t.Name,
                    ArtistName = t.Artist,
                    AlbumName = t.Album,
                    CoverUrl = resolvedCover ?? string.Empty,
                    Timestamp = t.PlayedAt?.LocalDateTime ?? DateTime.Now,
                    IsNowPlaying = false,
                    IsFavorite = t.IsLoved
                };
            });

            var historyItems = await Task.WhenAll(historyTasks);

            Scrobbles.Clear();
            _cachedHistory.Clear();

            foreach (var item in historyItems)
            {
                Scrobbles.Add(item);
                _cachedHistory.Add(item);
            }

            ScrobblesListView.ItemsSource = Scrobbles;
        }
    }
}
