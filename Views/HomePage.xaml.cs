using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using FluentScrobbler.Services;
using FluentScrobbler.Services.Media;

namespace FluentScrobbler.Views
{
    public sealed partial class HomePage : Page
    {
        private readonly LastFmService _lastFmService = new();
        private readonly MediaArtResolver _mediaArtResolver = new();
        private readonly WindowsMediaService _windowsMediaService = new();

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

        private DispatcherTimer? _nowPlayingTimer;
        private string _lastNowPlayingTrack = string.Empty;
        private string _lastNowPlayingArtist = string.Empty;

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
                NowPlayingCard.Visibility = Visibility.Visible;
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
            }
            else
            {
                NowPlayingCard.Visibility = Visibility.Collapsed;
            }
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
            await LoadDashboardDataAsync();
            StartNowPlayingTimer();
        }

        private void HomePage_Unloaded(object sender, RoutedEventArgs e)
        {
            _nowPlayingTimer?.Stop();
            _nowPlayingTimer = null;
        }

        private void StartNowPlayingTimer()
        {
            _nowPlayingTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            _nowPlayingTimer.Tick += async (s, e) => await RefreshNowPlayingAsync();
            _nowPlayingTimer.Start();
        }

        private async Task RefreshNowPlayingAsync()
        {
            if (!_lastFmService.IsLoggedIn()) return;

            var (username, _) = _lastFmService.GetUserSession();
            if (string.IsNullOrEmpty(username)) return;

            var recentTracks = await _lastFmService.GetRecentTracksAsync(username, limit: 3);
            var nowPlaying = recentTracks?.FirstOrDefault(t => t.IsNowPlaying);

            string newTrack = nowPlaying?.Name ?? string.Empty;
            string newArtist = nowPlaying?.Artist ?? string.Empty;

            if (nowPlaying == null)
            {
                var winMedia = await _windowsMediaService.GetCurrentWindowsMediaAsync();
                if (winMedia.HasValue)
                {
                    var (winTitle, winArtist, winAlbum, winSource) = winMedia.Value;
                    newTrack = winTitle;
                    newArtist = winArtist;

                    if (newTrack == _lastNowPlayingTrack && newArtist == _lastNowPlayingArtist) return;

                    _lastNowPlayingTrack = newTrack;
                    _lastNowPlayingArtist = newArtist;
                    await ApplyNowPlayingAsync(winArtist, winAlbum, winTitle, null);
                    return;
                }

                if (_lastNowPlayingTrack != string.Empty)
                {
                    _lastNowPlayingTrack = string.Empty;
                    _lastNowPlayingArtist = string.Empty;
                    NowPlayingCard.Visibility = Visibility.Collapsed;
                    _cachedNowPlayingVisible = false;
                    _cachedNowPlayingTrack = null;
                    _cachedNowPlayingArtistAlbum = null;
                    _cachedNowPlayingArtUrl = null;
                }
                return;
            }

            if (newTrack == _lastNowPlayingTrack && newArtist == _lastNowPlayingArtist) return;

            _lastNowPlayingTrack = newTrack;
            _lastNowPlayingArtist = newArtist;
            await ApplyNowPlayingAsync(nowPlaying.Artist, nowPlaying.Album, nowPlaying.Name, nowPlaying.AlbumArtUrl);
        }

        private async Task ApplyNowPlayingAsync(string artist, string album, string track, string? lastFmArtUrl)
        {
            NowPlayingCard.Visibility = Visibility.Visible;
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
                    var recentTracksTask = _lastFmService.GetRecentTracksAsync(username, limit: 5);

                    await Task.WhenAll(userInfoTask, tracksTodayTask, recentTracksTask);

                    var userInfo = await userInfoTask;
                    var tracksToday = await tracksTodayTask;
                    var recentTracks = await recentTracksTask;

                    var nowPlayingTrack = recentTracks.FirstOrDefault(t => t.IsNowPlaying);
                    if (nowPlayingTrack != null)
                    {
                        _lastNowPlayingTrack = nowPlayingTrack.Name;
                        _lastNowPlayingArtist = nowPlayingTrack.Artist;
                        await ApplyNowPlayingAsync(nowPlayingTrack.Artist, nowPlayingTrack.Album, nowPlayingTrack.Name, nowPlayingTrack.AlbumArtUrl);
                    }
                    else
                    {
                        var winMedia = await _windowsMediaService.GetCurrentWindowsMediaAsync();
                        if (winMedia.HasValue)
                        {
                            var (winTitle, winArtist, winAlbum, winSource) = winMedia.Value;
                            _lastNowPlayingTrack = winTitle;
                            _lastNowPlayingArtist = winArtist;
                            await ApplyNowPlayingAsync(winArtist, winAlbum, winTitle, null);
                        }
                        else
                        {
                            NowPlayingCard.Visibility = Visibility.Collapsed;
                            _cachedNowPlayingVisible = false;
                            _cachedNowPlayingTrack = null;
                            _cachedNowPlayingArtistAlbum = null;
                            _cachedNowPlayingArtUrl = null;
                        }
                    }

                    if (userInfo.HasValue)
                    {
                        if (!string.IsNullOrEmpty(userInfo.Value.Username))
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
    }
}
