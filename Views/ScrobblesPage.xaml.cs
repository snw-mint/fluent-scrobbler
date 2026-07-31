using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using FluentIcons.WinUI;
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

        private DispatcherTimer? _nowPlayingTimer;
        private string _lastNowPlayingTrack = string.Empty;
        private string _lastNowPlayingArtist = string.Empty;
        private CancellationTokenSource? _cts;

        private static readonly SemaphoreSlim _artLoadSemaphore = new(3, 3);

        public ScrobblesPage()
        {
            this.InitializeComponent();
            this.Loaded += ScrobblesPage_Loaded;
            this.Unloaded += ScrobblesPage_Unloaded;
        }

        private async void ScrobblesPage_Loaded(object sender, RoutedEventArgs e)
        {
            _cts = new CancellationTokenSource();
            await LoadDataAsync(_cts.Token);
            if (!_cts.IsCancellationRequested)
            {
                StartNowPlayingTimer();
            }
        }

        private void ScrobblesPage_Unloaded(object sender, RoutedEventArgs e)
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
            _nowPlayingTimer?.Stop();
            _nowPlayingTimer = null;
        }

        private void StartNowPlayingTimer()
        {
            _nowPlayingTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            _nowPlayingTimer.Tick += async (s, e) =>
            {
                if (_cts == null || _cts.IsCancellationRequested) return;
                await RefreshNowPlayingAsync(_cts.Token);
            };
            _nowPlayingTimer.Start();
        }

        private async Task LoadDataAsync(CancellationToken ct)
        {
            if (!_lastFmService.IsLoggedIn()) return;

            var (username, _) = _lastFmService.GetUserSession();
            if (string.IsNullOrEmpty(username)) return;

            var recentTracks = await _lastFmService.GetRecentTracksAsync(username, limit: 6);
            if (ct.IsCancellationRequested) return;
            if (recentTracks == null || recentTracks.Count == 0) return;

            var nowPlaying = recentTracks.FirstOrDefault(t => t.IsNowPlaying);
            if (!ct.IsCancellationRequested)
                await ApplyNowPlayingAsync(nowPlaying?.Artist, nowPlaying?.Album, nowPlaying?.Name, nowPlaying?.AlbumArtUrl, ct);

            if (ct.IsCancellationRequested) return;

            if (nowPlaying != null)
            {
                _lastNowPlayingTrack = nowPlaying.Name;
                _lastNowPlayingArtist = nowPlaying.Artist;
            }

            var historyTracks = recentTracks.Where(t => !t.IsNowPlaying).Take(5).ToList();

            var historyItems = historyTracks.Select(t => new ScrobbleItem
            {
                TrackName = t.Name,
                ArtistName = t.Artist,
                AlbumName = t.Album,
                CoverUrl = string.Empty,
                Timestamp = t.PlayedAt?.LocalDateTime ?? DateTime.Now,
                IsNowPlaying = false,
                IsFavorite = t.IsLoved
            }).ToList();

            if (ct.IsCancellationRequested) return;

            Scrobbles.Clear();
            foreach (var item in historyItems)
            {
                Scrobbles.Add(item);
            }

            RenderScrobblesList();
            _ = LoadArtProgressivelyAsync(historyItems, historyTracks.Select(t => (t.Artist, (string?)t.Album, t.Name, (string?)t.AlbumArtUrl)).ToList(), ct);
        }

        private void RenderScrobblesList()
        {
            if (ScrobblesItemsContainer == null) return;
            ScrobblesItemsContainer.Children.Clear();

            foreach (var item in Scrobbles)
            {
                var cardBorder = CreateScrobbleCardUI(item);
                ScrobblesItemsContainer.Children.Add(cardBorder);
            }
        }

        private Border CreateScrobbleCardUI(ScrobbleItem item)
        {
            var grid = new Grid { ColumnSpacing = 16 };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Album Art Container
            var artBorder = new Border
            {
                Width = 52,
                Height = 52,
                CornerRadius = new CornerRadius(6),
                Background = (Brush)Application.Current.Resources["LayerFillColorDefaultBrush"]
            };

            var artGrid = new Grid();
            
            // Usando nome totalmente qualificado para evitar ambiguidade entre WinUI e FluentIcons
            var icon = new FluentIcons.WinUI.SymbolIcon
            {
                Symbol = FluentIcons.Common.Symbol.MusicNote1,
                FontSize = 24,
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            artGrid.Children.Add(icon);

            var img = new Image
            {
                Width = 52,
                Height = 52,
                Stretch = Stretch.UniformToFill
            };

            if (!string.IsNullOrEmpty(item.CoverUrl))
            {
                try
                {
                    img.Source = new BitmapImage(new Uri(item.CoverUrl));
                }
                catch { }
            }

            item.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(ScrobbleItem.CoverUrl) && !string.IsNullOrEmpty(item.CoverUrl))
                {
                    this.DispatcherQueue?.TryEnqueue(() =>
                    {
                        try
                        {
                            img.Source = new BitmapImage(new Uri(item.CoverUrl));
                        }
                        catch { }
                    });
                }
            };

            artGrid.Children.Add(img);
            artBorder.Child = artGrid;
            Grid.SetColumn(artBorder, 0);
            grid.Children.Add(artBorder);

            // Track Details Stack
            var infoStack = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                Spacing = 2
            };

            var titleText = new TextBlock
            {
                Text = item.TrackName,
                Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"]
            };
            infoStack.Children.Add(titleText);

            var subStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 4
            };

            var artistText = new TextBlock
            {
                Text = item.ArtistName,
                Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
            };

            var dotText = new TextBlock
            {
                Text = "•",
                Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
            };

            var albumText = new TextBlock
            {
                Text = item.AlbumName,
                Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
            };

            subStack.Children.Add(artistText);
            subStack.Children.Add(dotText);
            subStack.Children.Add(albumText);
            infoStack.Children.Add(subStack);

            Grid.SetColumn(infoStack, 1);
            grid.Children.Add(infoStack);

            // Timestamp
            var timeText = new TextBlock
            {
                Text = item.TimeFormatted,
                VerticalAlignment = VerticalAlignment.Center,
                Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                Margin = new Thickness(8, 0, 8, 0)
            };
            Grid.SetColumn(timeText, 2);
            grid.Children.Add(timeText);

            // Card Container
            var outerBorder = new Border
            {
                Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
                BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(14),
                Margin = new Thickness(0, 0, 0, 8),
                Child = grid
            };

            return outerBorder;
        }

        private async Task LoadArtProgressivelyAsync(
            List<ScrobbleItem> items,
            List<(string Artist, string? Album, string Name, string? AlbumArtUrl)> trackInfos,
            CancellationToken ct)
        {
            var tasks = new List<Task>();
            var dq = this.DispatcherQueue;

            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                var (artist, album, name, artUrl) = trackInfos[i];

                tasks.Add(Task.Run(async () =>
                {
                    await _artLoadSemaphore.WaitAsync(ct).ConfigureAwait(false);
                    try
                    {
                        if (ct.IsCancellationRequested) return;
                        string? resolved = await _mediaArtResolver.ResolveAlbumArtAsync(artist, album ?? string.Empty, name, artUrl);
                        if (!ct.IsCancellationRequested && !string.IsNullOrEmpty(resolved) && dq != null)
                        {
                            dq.TryEnqueue(() =>
                            {
                                if (!ct.IsCancellationRequested)
                                    item.CoverUrl = resolved;
                            });
                        }
                    }
                    catch (OperationCanceledException) { }
                    finally
                    {
                        _artLoadSemaphore.Release();
                    }
                }, ct));
            }

            try
            {
                await Task.WhenAll(tasks);
            }
            catch (OperationCanceledException) { }
        }

        private async Task RefreshNowPlayingAsync(CancellationToken ct)
        {
            if (ct.IsCancellationRequested) return;
            if (!_lastFmService.IsLoggedIn()) return;

            var (username, _) = _lastFmService.GetUserSession();
            if (string.IsNullOrEmpty(username)) return;

            var recentTracks = await _lastFmService.GetRecentTracksAsync(username, limit: 6);
            if (ct.IsCancellationRequested) return;
            if (recentTracks == null) return;

            var historyTracks = recentTracks.Where(t => !t.IsNowPlaying).Take(5).ToList();
            if (historyTracks.Count > 0 && Scrobbles.Count > 0)
            {
                var latestHistory = historyTracks[0];
                var currentLatest = Scrobbles[0];
                if (latestHistory.Name != currentLatest.TrackName || latestHistory.PlayedAt?.LocalDateTime != currentLatest.Timestamp)
                {
                    UpdateHistoryList(historyTracks, ct);
                }
            }
            else if (historyTracks.Count > 0 && Scrobbles.Count == 0)
            {
                UpdateHistoryList(historyTracks, ct);
            }

            if (ct.IsCancellationRequested) return;

            var nowPlaying = recentTracks.FirstOrDefault(t => t.IsNowPlaying);

            string newTrack = nowPlaying?.Name ?? string.Empty;
            string newArtist = nowPlaying?.Artist ?? string.Empty;

            if (nowPlaying == null)
            {
                var winMedia = await _windowsMediaService.GetCurrentWindowsMediaAsync();
                if (ct.IsCancellationRequested) return;
                if (winMedia.HasValue)
                {
                    var (winTitle, winArtist, winAlbum, _) = winMedia.Value;
                    newTrack = winTitle;
                    newArtist = winArtist;

                    if (newTrack == _lastNowPlayingTrack && newArtist == _lastNowPlayingArtist) return;

                    _lastNowPlayingTrack = newTrack;
                    _lastNowPlayingArtist = newArtist;
                    await ApplyNowPlayingAsync(winArtist, winAlbum, winTitle, null, ct);
                    return;
                }

                if (_lastNowPlayingTrack != string.Empty)
                {
                    _lastNowPlayingTrack = string.Empty;
                    _lastNowPlayingArtist = string.Empty;
                    if (!ct.IsCancellationRequested)
                    {
                        NowPlayingPanel.Visibility = Visibility.Collapsed;
                    }
                }
                return;
            }

            if (newTrack == _lastNowPlayingTrack && newArtist == _lastNowPlayingArtist) return;

            _lastNowPlayingTrack = newTrack;
            _lastNowPlayingArtist = newArtist;
            await ApplyNowPlayingAsync(nowPlaying.Artist, nowPlaying.Album, nowPlaying.Name, nowPlaying.AlbumArtUrl, ct);
        }

        private void UpdateHistoryList(List<ScrobbleTrack> historyTracks, CancellationToken ct)
        {
            if (ct.IsCancellationRequested) return;

            var historyItems = historyTracks.Select(t => new ScrobbleItem
            {
                TrackName = t.Name,
                ArtistName = t.Artist,
                AlbumName = t.Album,
                CoverUrl = string.Empty,
                Timestamp = t.PlayedAt?.LocalDateTime ?? DateTime.Now,
                IsNowPlaying = false,
                IsFavorite = t.IsLoved
            }).ToList();

            Scrobbles.Clear();

            foreach (var item in historyItems)
            {
                Scrobbles.Add(item);
            }

            RenderScrobblesList();
            _ = LoadArtProgressivelyAsync(historyItems, historyTracks.Select(t => (t.Artist, (string?)t.Album, t.Name, (string?)t.AlbumArtUrl)).ToList(), ct);
        }

        private async Task ApplyNowPlayingAsync(string? artist, string? album, string? track, string? lastFmArtUrl, CancellationToken ct)
        {
            if (ct.IsCancellationRequested) return;

            if (string.IsNullOrEmpty(track))
            {
                var winMedia = await _windowsMediaService.GetCurrentWindowsMediaAsync();
                if (ct.IsCancellationRequested) return;
                if (winMedia.HasValue)
                {
                    var (winTitle, winArtist, winAlbum, _) = winMedia.Value;
                    artist = winArtist;
                    album = winAlbum;
                    track = winTitle;
                }
                else
                {
                    NowPlayingPanel.Visibility = Visibility.Collapsed;
                    return;
                }
            }

            if (ct.IsCancellationRequested) return;

            NowPlayingPanel.Visibility = Visibility.Visible;
            NowPlayingTrack.Text = track;
            string artistAlbumStr = string.IsNullOrEmpty(album) ? (artist ?? string.Empty) : $"{artist} • {album}";
            NowPlayingArtist.Text = artistAlbumStr;

            NowPlayingAlbumArtImage.Visibility = Visibility.Collapsed;
            NowPlayingFallbackIcon.Visibility = Visibility.Visible;

            string? coverUrl = await _mediaArtResolver.ResolveAlbumArtAsync(artist ?? string.Empty, album ?? string.Empty, track, lastFmArtUrl);
            if (ct.IsCancellationRequested) return;

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
        }
    }
}