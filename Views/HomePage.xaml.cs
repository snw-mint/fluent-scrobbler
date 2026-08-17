using System;
using System.Collections.Generic;
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
    public sealed partial class HomePage : Page
    {
        private readonly LastFmService _lastFmService = new();
        private readonly MediaArtResolver _mediaArtResolver = new();
        private readonly WindowsMediaService _windowsMediaService = new();

        private static readonly List<ScrobbleItem> _cachedRecentScrobbles = new();

        private static string? _cachedTitle;
        private static string? _cachedSubtitle;
        private static string? _cachedScrobblesToday;
        private static string? _cachedScrobblesTodaySub;
        private static string? _cachedMinutesToday;
        private static string? _cachedMinutesTodaySub;
        private static string? _cachedVibeToday;
        private static string? _cachedVibeTodaySub;
        private static string? _cachedTotalScrobbles;
        private static string? _cachedTotalScrobblesSub;

        private static bool _cachedNowPlayingVisible;
        private static string? _cachedNowPlayingTrack;
        private static string? _cachedNowPlayingArtistAlbum;
        private static string? _cachedNowPlayingArtUrl;

        private string _lastNowPlayingTrack = string.Empty;
        private string _lastNowPlayingArtist = string.Empty;
        private static bool _dashboardLoaded;

        private static bool _cachedOfflineBannerOpen;
        private static InfoBarSeverity _cachedOfflineSeverity = InfoBarSeverity.Warning;
        private static string _cachedOfflineTitle = "Working Offline";
        private static string _cachedOfflineMessage = "Connection lost. Your scrobbles are being saved locally and will sync automatically once you are back online.";
        private static string? _cachedOfflineButtonContent;
        private static string? _cachedOfflineButtonTag;

        public HomePage()
        {
            this.InitializeComponent();
            ApplyCachedState();
            this.Loaded += HomePage_Loaded;
            this.Unloaded += HomePage_Unloaded;
        }

        private void ApplyCachedState()
        {
            if (_cachedTitle != null) DashboardTitleText.Text = _cachedTitle;
            if (_cachedSubtitle != null) DashboardSubtitleText.Text = _cachedSubtitle;

            if (_cachedOfflineBannerOpen)
            {
                OfflineStatusInfoBar.Severity = _cachedOfflineSeverity;
                OfflineStatusInfoBar.Title = _cachedOfflineTitle;
                OfflineStatusInfoBar.Message = _cachedOfflineMessage;
                if (_cachedOfflineButtonContent != null) ForceSyncButton.Content = _cachedOfflineButtonContent;
                if (_cachedOfflineButtonTag != null) ForceSyncButton.Tag = _cachedOfflineButtonTag;
                OfflineStatusInfoBar.Visibility = Visibility.Visible;
                OfflineStatusInfoBar.IsOpen = true;
            }
            else
            {
                OfflineStatusInfoBar.IsOpen = false;
                OfflineStatusInfoBar.Visibility = Visibility.Collapsed;
            }
            if (_cachedScrobblesToday != null) ScrobblesTodayText.Text = _cachedScrobblesToday;
            if (_cachedScrobblesTodaySub != null) ScrobblesTodaySubtext.Text = _cachedScrobblesTodaySub;
            if (_cachedMinutesToday != null) MinutesTodayText.Text = _cachedMinutesToday;
            if (_cachedMinutesTodaySub != null) MinutesTodaySubtext.Text = _cachedMinutesTodaySub;
            if (_cachedVibeToday != null) TodayVibeText.Text = _cachedVibeToday;
            if (_cachedVibeTodaySub != null) TodayVibeSubtext.Text = _cachedVibeTodaySub;
            if (_cachedTotalScrobbles != null) ScrobblesTotalText.Text = _cachedTotalScrobbles;
            if (_cachedTotalScrobblesSub != null) ScrobblesTotalSubtext.Text = _cachedTotalScrobblesSub;

            if (_cachedNowPlayingVisible)
            {
                NowPlayingActiveContainer.Visibility = Visibility.Visible;
                NowPlayingIdleContainer.Visibility = Visibility.Collapsed;
                NowPlayingIdleIcon.Visibility = Visibility.Collapsed;
                if (_cachedNowPlayingTrack != null) NowPlayingTrackText.Text = _cachedNowPlayingTrack;
                if (_cachedNowPlayingArtistAlbum != null) NowPlayingArtistAlbumText.Text = _cachedNowPlayingArtistAlbum;
                if (!string.IsNullOrEmpty(_cachedNowPlayingArtUrl))
                {
                    try
                    {
                        NowPlayingAlbumArtImage.Source = new BitmapImage(new Uri(_cachedNowPlayingArtUrl));
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
            }
            else
            {
                SetNowPlayingIdle();
            }

            if (_cachedRecentScrobbles.Count > 0)
            {
                RenderRecentScrobbles(_cachedRecentScrobbles);
            }
        }

        private void SetNowPlayingIdle()
        {
            _lastNowPlayingTrack = string.Empty;
            _lastNowPlayingArtist = string.Empty;
            _cachedNowPlayingVisible = false;
            _cachedNowPlayingTrack = null;
            _cachedNowPlayingArtistAlbum = null;
            _cachedNowPlayingArtUrl = null;

            NowPlayingActiveContainer.Visibility = Visibility.Collapsed;
            NowPlayingIdleContainer.Visibility = Visibility.Visible;
            NowPlayingAlbumArtImage.Visibility = Visibility.Collapsed;
            NowPlayingFallbackIcon.Visibility = Visibility.Collapsed;
            NowPlayingIdleIcon.Visibility = Visibility.Visible;
        }

        private void CacheCurrentState()
        {
            _cachedTitle = DashboardTitleText.Text;
            _cachedSubtitle = DashboardSubtitleText.Text;
            _cachedScrobblesToday = ScrobblesTodayText.Text;
            _cachedScrobblesTodaySub = ScrobblesTodaySubtext.Text;
            _cachedMinutesToday = MinutesTodayText.Text;
            _cachedMinutesTodaySub = MinutesTodaySubtext.Text;
            _cachedVibeToday = TodayVibeText.Text;
            _cachedVibeTodaySub = TodayVibeSubtext.Text;
            _cachedTotalScrobbles = ScrobblesTotalText.Text;
            _cachedTotalScrobblesSub = ScrobblesTotalSubtext.Text;
        }

        private async void HomePage_Loaded(object sender, RoutedEventArgs e)
        {
            ScrobblerBackgroundService.Instance.TrackScrobbled += OnTrackScrobbled;
            ScrobblerBackgroundService.Instance.NowPlayingChanged += OnNowPlayingChanged;
            System.Net.NetworkInformation.NetworkChange.NetworkAddressChanged += OnNetworkAddressChanged;

            if (!System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable())
            {
                SetOfflineStatus(isOffline: true);
            }

            if (!_dashboardLoaded)
            {
                await LoadDashboardDataAsync();
                _dashboardLoaded = true;
            }
            else
            {
                ApplyCachedState();
            }

            var currentTrack = ScrobblerBackgroundService.Instance.CurrentTrack;
            if (currentTrack != null)
            {
                if (_lastNowPlayingTrack != currentTrack.Track || _lastNowPlayingArtist != currentTrack.Artist || NowPlayingActiveContainer.Visibility != Visibility.Visible)
                {
                    _lastNowPlayingTrack = currentTrack.Track;
                    _lastNowPlayingArtist = currentTrack.Artist;
                    await ApplyNowPlayingAsync(currentTrack.Artist, currentTrack.Album, currentTrack.Track, null);
                }
            }
            else
            {
                SetNowPlayingIdle();
            }

            OfflineCacheWorker.Instance.OfflineModeChanged += OnOfflineModeChanged;
            OfflineCacheWorker.Instance.CacheCountChanged += OnCacheCountChanged;
            
            
            SetOfflineStatus(OfflineCacheWorker.Instance.OfflineMode, await OfflineCacheService.Instance.GetPendingCountAsync());
        }

        private void HomePage_Unloaded(object sender, RoutedEventArgs e)
        {
            ScrobblerBackgroundService.Instance.TrackScrobbled -= OnTrackScrobbled;
            ScrobblerBackgroundService.Instance.NowPlayingChanged -= OnNowPlayingChanged;
            System.Net.NetworkInformation.NetworkChange.NetworkAddressChanged -= OnNetworkAddressChanged;
            OfflineCacheWorker.Instance.OfflineModeChanged -= OnOfflineModeChanged;
            OfflineCacheWorker.Instance.CacheCountChanged -= OnCacheCountChanged;
        }

        private async void OnOfflineModeChanged(object? sender, bool isOffline)
        {
            int count = await OfflineCacheService.Instance.GetPendingCountAsync();
            this.DispatcherQueue?.TryEnqueue(() =>
            {
                SetOfflineStatus(isOffline, count);
            });
        }

        private void OnCacheCountChanged(object? sender, int count)
        {
            this.DispatcherQueue?.TryEnqueue(() =>
            {
                SetOfflineStatus(OfflineCacheWorker.Instance.OfflineMode, count);
            });
        }

        private void OnNetworkAddressChanged(object? sender, EventArgs e)
        {
            this.DispatcherQueue?.TryEnqueue(async () =>
            {
                bool isAvailable = System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable();
                if (!isAvailable)
                {
                    SetOfflineStatus(isOffline: true);
                }
                else
                {
                    _dashboardLoaded = false;
                    await LoadDashboardDataAsync();
                    _dashboardLoaded = true;
                }
            });
        }

        private async void OnNowPlayingChanged(object? sender, NowPlayingInfo? info)
        {
            this.DispatcherQueue?.TryEnqueue(async () =>
            {
                if (info == null)
                {
                    SetNowPlayingIdle();
                    return;
                }

                if (info.Track == _lastNowPlayingTrack && info.Artist == _lastNowPlayingArtist && NowPlayingActiveContainer.Visibility == Visibility.Visible) return;

                _lastNowPlayingTrack = info.Track;
                _lastNowPlayingArtist = info.Artist;
                await ApplyNowPlayingAsync(info.Artist, info.Album, info.Track, null);
            });
        }

        private async void OnTrackScrobbled(object? sender, EventArgs e)
        {
            this.DispatcherQueue?.TryEnqueue(async () =>
            {
                _dashboardLoaded = false;
                await LoadDashboardDataAsync();
                _dashboardLoaded = true;
            });
        }

        private async Task ApplyNowPlayingAsync(string artist, string album, string track, string? lastFmArtUrl)
        {
            NowPlayingIdleContainer.Visibility = Visibility.Collapsed;
            NowPlayingActiveContainer.Visibility = Visibility.Visible;
            NowPlayingIdleIcon.Visibility = Visibility.Collapsed;
            NowPlayingTrackText.Text = track;
            string artistAlbumStr = string.IsNullOrEmpty(album) ? artist : $"{artist} • {album}";
            NowPlayingArtistAlbumText.Text = artistAlbumStr;

            NowPlayingAlbumArtImage.Visibility = Visibility.Collapsed;
            NowPlayingFallbackIcon.Visibility = Visibility.Visible;

            string? artUrl = await _mediaArtResolver.ResolveAlbumArtAsync(artist, album, track, lastFmArtUrl);
            if (!string.IsNullOrEmpty(artUrl))
            {
                try
                {
                    NowPlayingAlbumArtImage.Source = new BitmapImage(new Uri(artUrl));
                    NowPlayingAlbumArtImage.Visibility = Visibility.Visible;
                    NowPlayingFallbackIcon.Visibility = Visibility.Collapsed;
                }
                catch
                {
                    NowPlayingAlbumArtImage.Visibility = Visibility.Collapsed;
                    NowPlayingFallbackIcon.Visibility = Visibility.Visible;
                }
            }

            _cachedNowPlayingVisible = true;
            _cachedNowPlayingTrack = track;
            _cachedNowPlayingArtistAlbum = artistAlbumStr;
            _cachedNowPlayingArtUrl = artUrl;
        }

        private async Task LoadDashboardDataAsync()
        {
            int hour = DateTime.Now.Hour;
            string greeting = hour switch
            {
                >= 5 and < 12 => "Good morning",
                >= 12 and < 18 => "Good afternoon",
                _ => "Good evening"
            };

            string displayName = "User";

            if (!System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable())
            {
                SetOfflineStatus(isOffline: true);
                DashboardTitleText.Text = $"{greeting}, {displayName}";
                DashboardSubtitleText.Text = "Welcome back! Here is a summary of your activity today.";
                CacheCurrentState();
                return;
            }

            try
            {
                if (_lastFmService.IsLoggedIn())
                {
                    var (username, _) = _lastFmService.GetUserSession();
                    if (!string.IsNullOrEmpty(username))
                    {
                        displayName = username;
                        var userInfoTask = _lastFmService.GetUserInfoAsync(username);

                        DateTime todayMidnight = DateTime.Today;
                        long todayUts = new DateTimeOffset(todayMidnight).ToUnixTimeSeconds();
                        var tracksTodayTask = _lastFmService.GetRecentTracksAsync(username, limit: 200, fromTimestamp: todayUts);
                        var recentHistoryTask = _lastFmService.GetRecentTracksAsync(username, limit: 5);

                        await Task.WhenAll(userInfoTask, tracksTodayTask, recentHistoryTask);

                        var userInfo = await userInfoTask;
                        var tracksToday = await tracksTodayTask;
                        var recentHistory = await recentHistoryTask;

                        var historyTracks = recentHistory.Where(t => !t.IsNowPlaying).Take(3).ToList();
                        var historyItems = historyTracks.Select(t => {
                            var cached = _cachedRecentScrobbles.FirstOrDefault(c => c.TrackName == t.Name && c.ArtistName == t.Artist && !string.IsNullOrEmpty(c.CoverUrl));
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

                        _cachedRecentScrobbles.Clear();
                        _cachedRecentScrobbles.AddRange(historyItems);
                        RenderRecentScrobbles(historyItems);
                        _ = LoadArtProgressivelyAsync(historyItems, historyTracks.Select(t => (t.Artist, (string?)t.Album, t.Name, (string?)t.AlbumArtUrl)).ToList());

                        if (userInfo.HasValue)
                        {
                            if (!string.IsNullOrEmpty(userInfo.Value.DisplayName))
                            {
                                displayName = userInfo.Value.DisplayName;
                            }
                            else if (!string.IsNullOrEmpty(userInfo.Value.Username))
                            {
                                displayName = userInfo.Value.Username;
                            }
                            ScrobblesTotalText.Text = $"{userInfo.Value.ScrobbleCount:N0}";
                            ScrobblesTotalSubtext.Text = "Lifetime scrobbles";
                        }

                        int scrobblesTodayCount = tracksToday.Count;
                        ScrobblesTodayText.Text = $"{scrobblesTodayCount:N0}";
                        ScrobblesTodaySubtext.Text = scrobblesTodayCount == 1 ? "1 track scrobbled" : $"{scrobblesTodayCount:N0} tracks scrobbled";

                        int minutesToday = (int)Math.Round(scrobblesTodayCount * 3.5);
                        if (minutesToday >= 60)
                        {
                            int hrs = minutesToday / 60;
                            int mins = minutesToday % 60;
                            MinutesTodayText.Text = mins > 0 ? $"{hrs}h {mins}m" : $"{hrs}h";
                        }
                        else
                        {
                            MinutesTodayText.Text = $"{minutesToday} mins";
                        }
                        MinutesTodaySubtext.Text = "Estimated listening time";

                        if (scrobblesTodayCount == 0)
                        {
                            TodayVibeText.Text = "Quiet Day";
                            TodayVibeSubtext.Text = "No scrobbles yet today";
                        }
                        else
                        {
                            var topArtistsToday = tracksToday
                                .Where(t => !string.IsNullOrWhiteSpace(t.Artist))
                                .GroupBy(t => t.Artist)
                                .OrderByDescending(g => g.Count())
                                .Take(5)
                                .ToList();

                            var tagScores = new System.Collections.Generic.Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                            var ignoredTags = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase)
                            {
                                "seen live", "favourites", "favorites", "spotify", "american", "british",
                                "male vocalists", "female vocalists", "albums i own", "check out"
                            };

                            foreach (var group in topArtistsToday)
                            {
                                string artistName = group.Key;
                                int playCount = group.Count();

                                var artistTags = await _lastFmService.GetArtistTopTagsAsync(artistName, count: 5);
                                foreach (var tag in artistTags)
                                {
                                    if (ignoredTags.Contains(tag)) continue;

                                    if (tagScores.ContainsKey(tag))
                                    {
                                        tagScores[tag] += playCount;
                                    }
                                    else
                                    {
                                        tagScores[tag] = playCount;
                                    }
                                }
                            }

                            var topTagEntry = tagScores.OrderByDescending(kv => kv.Value).FirstOrDefault();
                            if (!string.IsNullOrEmpty(topTagEntry.Key))
                            {
                                string formattedTag = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(topTagEntry.Key);
                                TodayVibeText.Text = formattedTag;
                                TodayVibeSubtext.Text = "Top genre today";
                            }
                            else
                            {
                                TodayVibeText.Text = "Eclectic";
                                TodayVibeSubtext.Text = "Based on today's tracks";
                            }
                        }
                    }
                }

                SetOfflineStatus(isOffline: false);
            }
            catch (Exception)
            {
                SetOfflineStatus(isOffline: true);
            }

            DashboardTitleText.Text = $"{greeting}, {displayName}";
            DashboardSubtitleText.Text = "Welcome back! Here is a summary of your activity today.";
            CacheCurrentState();
        }

        private void QuickAction_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string tag)
            {
                Type? pageType = tag switch
                {
                    "HomePage" => typeof(HomePage),
                    "ScrobblesPage" => typeof(ScrobblesPage),
                    "SettingsPage" => typeof(SettingsPage),
                    "AccountPage" => typeof(AccountPage),
                    "AboutPage" => typeof(AboutPage),
                    _ => null
                };

                if (pageType != null && Frame != null)
                {
                    Frame.Navigate(pageType);
                }
            }
        }

        private async void ExternalLink_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string url && !string.IsNullOrEmpty(url))
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = url,
                        UseShellExecute = true
                    });
                }
                catch
                {
                    try
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = "cmd",
                            Arguments = $"/c start \"\" \"{url.Replace("&", "^&")}\"",
                            UseShellExecute = false,
                            CreateNoWindow = true
                        });
                    }
                    catch
                    {
                    }
                }
            }
        }

        public void SetOfflineStatus(bool isOffline, int pendingCount = 0, bool isAuthError = false, string? customMessage = null)
        {
            if (!isOffline && !isAuthError && pendingCount == 0)
            {
                OfflineStatusInfoBar.IsOpen = false;
                OfflineStatusInfoBar.Visibility = Visibility.Collapsed;
                _cachedOfflineBannerOpen = false;
                return;
            }

            if (isAuthError)
            {
                OfflineStatusInfoBar.Severity = InfoBarSeverity.Error;
                OfflineStatusInfoBar.Title = "Authentication Error";
                OfflineStatusInfoBar.Message = customMessage ?? "Unable to submit scrobbles. Your Last.fm session may have expired. Please re-authenticate your account.";
                ForceSyncButton.Content = "Go to Account";
                ForceSyncButton.Tag = "AccountPage";
            }
            else if (pendingCount > 0)
            {
                OfflineStatusInfoBar.Severity = InfoBarSeverity.Warning;
                OfflineStatusInfoBar.Title = "Unsaved Scrobbles";
                string countText = pendingCount == 1 ? "1 scrobble" : $"{pendingCount} scrobbles";
                OfflineStatusInfoBar.Message = customMessage ?? $"You have {countText} stored in offline cache. Click below to try sending them now.";
                ForceSyncButton.Content = "Sync Now";
                ForceSyncButton.Tag = "Sync";
            }
            else
            {
                OfflineStatusInfoBar.Severity = InfoBarSeverity.Warning;
                OfflineStatusInfoBar.Title = "Working Offline";
                OfflineStatusInfoBar.Message = customMessage ?? "Connection lost. Your scrobbles are being saved locally and will sync automatically once you are back online.";
                ForceSyncButton.Content = "Force Sync";
                ForceSyncButton.Tag = "Sync";
            }

            OfflineStatusInfoBar.Visibility = Visibility.Visible;
            OfflineStatusInfoBar.IsOpen = true;
            _cachedOfflineBannerOpen = true;
            _cachedOfflineSeverity = OfflineStatusInfoBar.Severity;
            _cachedOfflineTitle = OfflineStatusInfoBar.Title;
            _cachedOfflineMessage = OfflineStatusInfoBar.Message;
            _cachedOfflineButtonContent = ForceSyncButton.Content as string;
            _cachedOfflineButtonTag = ForceSyncButton.Tag as string;
        }

        private async void ForceSyncButton_Click(object sender, RoutedEventArgs e)
        {
            if (ForceSyncButton.Tag is string tag && tag == "AccountPage")
            {
                Frame?.Navigate(typeof(AccountPage));
                return;
            }

            ForceSyncButton.IsEnabled = false;
            await OfflineCacheWorker.Instance.ForceSyncAsync();
            ForceSyncButton.IsEnabled = true;
        }

        private void RenderRecentScrobbles(List<ScrobbleItem> items)
        {
            if (RecentScrobblesContainer == null) return;
            RecentScrobblesContainer.Children.Clear();

            if (items.Count == 0)
            {
                NoRecentScrobblesText.Visibility = Visibility.Visible;
                RecentScrobblesContainer.Children.Add(NoRecentScrobblesText);
                return;
            }

            NoRecentScrobblesText.Visibility = Visibility.Collapsed;
            foreach (var item in items)
            {
                var card = CreateScrobbleCardUI(item);
                RecentScrobblesContainer.Children.Add(card);
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
                Stretch = Microsoft.UI.Xaml.Media.Stretch.UniformToFill,
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
            List<(string Artist, string? Album, string Name, string? AlbumArtUrl)> trackInfos)
        {
            var dq = this.DispatcherQueue;
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                var (artist, album, name, artUrl) = trackInfos[i];
                if (!string.IsNullOrEmpty(item.CoverUrl)) continue;

                _ = Task.Run(async () =>
                {
                    try
                    {
                        string? resolved = await _mediaArtResolver.ResolveAlbumArtAsync(artist, album ?? string.Empty, name, artUrl);
                        if (!string.IsNullOrEmpty(resolved) && dq != null)
                        {
                            dq.TryEnqueue(() =>
                            {
                                item.CoverUrl = resolved;
                            });
                        }
                    }
                    catch { }
                });
            }
        }

        private void ScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (MainContentPanel == null) return;
            double width = e.NewSize.Width;

            if (width > 3200)
            {
                MainContentPanel.HorizontalAlignment = HorizontalAlignment.Center;
                MainContentPanel.Width = 1100;
            }
            else if (width > 2200)
            {
                MainContentPanel.HorizontalAlignment = HorizontalAlignment.Center;
                MainContentPanel.Width = 1180;
            }
            else if (width > 1464)
            {
                MainContentPanel.HorizontalAlignment = HorizontalAlignment.Center;
                MainContentPanel.Width = 1260;
            }
            else
            {
                MainContentPanel.HorizontalAlignment = HorizontalAlignment.Stretch;
                MainContentPanel.Width = double.NaN;
            }
        }
    }
}
