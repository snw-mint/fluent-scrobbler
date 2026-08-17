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

        private string _lastNowPlayingTrack = string.Empty;
        private string _lastNowPlayingArtist = string.Empty;
        private CancellationTokenSource? _cts;

        private static readonly SemaphoreSlim _artLoadSemaphore = new(3, 3);
        private static readonly List<ScrobbleItem> _cachedScrobbles = new();

        public ScrobblesPage()
        {
            this.InitializeComponent();
            this.Loaded += ScrobblesPage_Loaded;
            this.Unloaded += ScrobblesPage_Unloaded;
        }

        private async void ScrobblesPage_Loaded(object sender, RoutedEventArgs e)
        {
            _cts = new CancellationTokenSource();

            ScrobblerBackgroundService.Instance.TrackScrobbled += OnTrackScrobbledInBackground;
            ScrobblerBackgroundService.Instance.NowPlayingChanged += OnNowPlayingChanged;

            if (Scrobbles.Count == 0 && _cachedScrobbles.Count > 0)
            {
                foreach (var item in _cachedScrobbles)
                {
                    Scrobbles.Add(item);
                }
                RenderScrobblesList();
            }

            await LoadDataAsync(_cts.Token, showLoading: Scrobbles.Count == 0, forceRefresh: Scrobbles.Count == 0);

            var currentTrack = ScrobblerBackgroundService.Instance.CurrentTrack;
            if (currentTrack != null && _cts != null && !_cts.IsCancellationRequested)
            {
                _lastNowPlayingTrack = currentTrack.Track;
                _lastNowPlayingArtist = currentTrack.Artist;
                await ApplyNowPlayingAsync(currentTrack.Artist, currentTrack.Album, currentTrack.Track, null, _cts.Token);
            }
            else
            {
                SetNowPlayingIdle();
            }
        }

        private void SetNowPlayingIdle()
        {
            _lastNowPlayingTrack = string.Empty;
            _lastNowPlayingArtist = string.Empty;

            NowPlayingActiveContainer.Visibility = Visibility.Collapsed;
            NowPlayingIdleContainer.Visibility = Visibility.Visible;
            NowPlayingAlbumArtImage.Visibility = Visibility.Collapsed;
            NowPlayingFallbackIcon.Visibility = Visibility.Collapsed;
            NowPlayingIdleIcon.Visibility = Visibility.Visible;
        }

        private void ScrobblesPage_Unloaded(object sender, RoutedEventArgs e)
        {
            ScrobblerBackgroundService.Instance.TrackScrobbled -= OnTrackScrobbledInBackground;
            ScrobblerBackgroundService.Instance.NowPlayingChanged -= OnNowPlayingChanged;
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }

        private void OnTrackScrobbledInBackground(object? sender, EventArgs e)
        {
            this.DispatcherQueue?.TryEnqueue(async () =>
            {
                if (_cts != null && !_cts.IsCancellationRequested)
                {
                    await LoadDataAsync(_cts.Token, showLoading: false, forceRefresh: true);
                }
            });
        }

        private void OnNowPlayingChanged(object? sender, NowPlayingInfo? info)
        {
            this.DispatcherQueue?.TryEnqueue(async () =>
            {
                if (_cts == null || _cts.IsCancellationRequested) return;

                if (info == null)
                {
                    SetNowPlayingIdle();
                    return;
                }

                if (info.Track == _lastNowPlayingTrack && info.Artist == _lastNowPlayingArtist && NowPlayingActiveContainer.Visibility == Visibility.Visible) return;

                _lastNowPlayingTrack = info.Track;
                _lastNowPlayingArtist = info.Artist;
                await ApplyNowPlayingAsync(info.Artist, info.Album, info.Track, null, _cts.Token);
            });
        }

        private async Task LoadDataAsync(CancellationToken ct, bool showLoading = false, bool forceRefresh = false)
        {
            if (!_lastFmService.IsLoggedIn()) return;

            var (username, _) = _lastFmService.GetUserSession();
            if (string.IsNullOrEmpty(username)) return;

            if (showLoading)
            {
                LoadingContainer.Visibility = Visibility.Visible;
                LoadingRing.IsActive = true;
            }

            var recentTracks = await _lastFmService.GetRecentTracksAsync(username, limit: 16, forceRefresh: forceRefresh);

            if (showLoading)
            {
                LoadingContainer.Visibility = Visibility.Collapsed;
                LoadingRing.IsActive = false;
            }

            if (ct.IsCancellationRequested) return;
            if (recentTracks == null || recentTracks.Count == 0)
            {
                if (Scrobbles.Count == 0 && _cachedScrobbles.Count > 0)
                {
                    Scrobbles.Clear();
                    foreach (var item in _cachedScrobbles)
                    {
                        Scrobbles.Add(item);
                    }
                    RenderScrobblesList();
                }
                return;
            }

            var historyTracks = recentTracks.Where(t => !t.IsNowPlaying).Take(15).ToList();

            var historyItems = historyTracks.Select(t => {
                var cached = _cachedScrobbles.FirstOrDefault(c => c.TrackName == t.Name && c.ArtistName == t.Artist && !string.IsNullOrEmpty(c.CoverUrl));
                return new ScrobbleItem
                {
                    TrackName = t.Name,
                    ArtistName = t.Artist,
                    AlbumName = t.Album,
                    CoverUrl = cached?.CoverUrl ?? string.Empty,
                    Timestamp = t.PlayedAt?.LocalDateTime ?? DateTime.Now,
                    IsNowPlaying = false,
                    IsFavorite = t.IsLoved
                };
            }).ToList();

            if (ct.IsCancellationRequested) return;

            Scrobbles.Clear();
            foreach (var item in historyItems)
            {
                Scrobbles.Add(item);
            }

            _cachedScrobbles.Clear();
            _cachedScrobbles.AddRange(Scrobbles);

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

            var artBorder = (Border)Microsoft.UI.Xaml.Markup.XamlReader.Load(@"
<Border xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
        Width=""52"" Height=""52"" CornerRadius=""6""
        Background=""{ThemeResource LayerFillColorDefaultBrush}"" />");

            var artGrid = new Grid();
            var icon = new FluentIcons.WinUI.SymbolIcon
            {
                Symbol = FluentIcons.Common.Symbol.MusicNote1,
                FontSize = 24,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            artGrid.Children.Add(icon);

            var img = new Image
            {
                Width = 52,
                Height = 52,
                Stretch = Stretch.UniformToFill,
                Visibility = Visibility.Collapsed
            };

            Action updateImageAction = () =>
            {
                if (!string.IsNullOrEmpty(item.CoverUrl))
                {
                    try
                    {
                        img.Source = new BitmapImage(new Uri(item.CoverUrl));
                        img.Visibility = Visibility.Visible;
                        icon.Visibility = Visibility.Collapsed;
                    }
                    catch
                    {
                        img.Visibility = Visibility.Collapsed;
                        icon.Visibility = Visibility.Visible;
                    }
                }
            };

            updateImageAction();

            item.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(ScrobbleItem.CoverUrl))
                {
                    this.DispatcherQueue?.TryEnqueue(() => updateImageAction());
                }
            };

            artGrid.Children.Add(img);
            artBorder.Child = artGrid;
            Grid.SetColumn(artBorder, 0);
            grid.Children.Add(artBorder);

            var infoStack = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                Spacing = 2
            };

            var titleText = new TextBlock
            {
                Text = item.TrackName,
                Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"],
                TextTrimming = TextTrimming.CharacterEllipsis
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
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            subStack.Children.Add(artistText);

            if (!string.IsNullOrWhiteSpace(item.AlbumName))
            {
                var dotText = new TextBlock
                {
                    Text = "•",
                    Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"]
                };

                var albumText = new TextBlock
                {
                    Text = item.AlbumName,
                    Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                    TextTrimming = TextTrimming.CharacterEllipsis
                };

                subStack.Children.Add(dotText);
                subStack.Children.Add(albumText);
            }

            infoStack.Children.Add(subStack);

            Grid.SetColumn(infoStack, 1);
            grid.Children.Add(infoStack);

            var timeText = new TextBlock
            {
                Text = item.TimeFormatted,
                VerticalAlignment = VerticalAlignment.Center,
                Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                Margin = new Thickness(8, 0, 8, 0)
            };
            Grid.SetColumn(timeText, 2);
            grid.Children.Add(timeText);

            var outerBorder = (Border)Microsoft.UI.Xaml.Markup.XamlReader.Load(@"
<Border xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
        Background=""{ThemeResource CardBackgroundFillColorDefaultBrush}""
        BorderBrush=""{ThemeResource CardStrokeColorDefaultBrush}""
        BorderThickness=""1""
        CornerRadius=""8""
        Padding=""14""
        Margin=""0,0,0,8"" />");

            outerBorder.Child = grid;
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

            if (!ct.IsCancellationRequested)
            {
                this.DispatcherQueue?.TryEnqueue(() =>
                {
                    _cachedScrobbles.Clear();
                    _cachedScrobbles.AddRange(Scrobbles);
                });
            }
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
                SetNowPlayingIdle();
                return;
            }

            if (ct.IsCancellationRequested) return;

            NowPlayingIdleContainer.Visibility = Visibility.Collapsed;
            NowPlayingActiveContainer.Visibility = Visibility.Visible;
            NowPlayingIdleIcon.Visibility = Visibility.Collapsed;
            NowPlayingTrack.Text = track;

            string artistAlbumStr = string.IsNullOrWhiteSpace(album) 
                ? (artist ?? string.Empty) 
                : $"{artist} • {album}";

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

        private void ScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (MainContentPanel == null) return;
            const double maxContentWidth = 1400;
            const double horizontalPadding = 64;
            if (e.NewSize.Width > maxContentWidth + horizontalPadding)
            {
                MainContentPanel.HorizontalAlignment = HorizontalAlignment.Center;
                MainContentPanel.Width = maxContentWidth;
            }
            else
            {
                MainContentPanel.HorizontalAlignment = HorizontalAlignment.Stretch;
                MainContentPanel.Width = double.NaN;
            }
        }
    }
}