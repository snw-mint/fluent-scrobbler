using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using FluentScrobbler.Services;

namespace FluentScrobbler.Views
{
    public sealed partial class AccountPage : Page
    {
        private readonly LastFmService _lastFmService = new();
        private string? _currentAuthToken;
        private bool _dataLoaded;

        public AccountPage()
        {
            this.InitializeComponent();
            this.Loaded += AccountPage_Loaded;
            this.Unloaded += AccountPage_Unloaded;
        }

        private async void AccountPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (MainWindow.Current != null)
            {
                MainWindow.Current.Activated += Window_Activated;
            }
            if (!_dataLoaded)
            {
                await LoadAccountStateAsync();
            }

            OfflineCacheWorker.Instance.CacheCountChanged += OnCacheCountChanged;
            UpdateOfflineCacheStatus(await OfflineCacheService.Instance.GetPendingCountAsync());
        }

        private void AccountPage_Unloaded(object sender, RoutedEventArgs e)
        {
            if (MainWindow.Current != null)
            {
                MainWindow.Current.Activated -= Window_Activated;
            }
            OfflineCacheWorker.Instance.CacheCountChanged -= OnCacheCountChanged;
        }

        private void OnCacheCountChanged(object? sender, int count)
        {
            this.DispatcherQueue?.TryEnqueue(() =>
            {
                UpdateOfflineCacheStatus(count);
            });
        }

        private async void Window_Activated(object sender, WindowActivatedEventArgs args)
        {
            if (args.WindowActivationState != WindowActivationState.Deactivated && !string.IsNullOrEmpty(_currentAuthToken) && !_lastFmService.IsLoggedIn())
            {
                await TryCompleteAuthAsync();
            }
        }

        private async Task TryCompleteAuthAsync()
        {
            if (string.IsNullOrEmpty(_currentAuthToken)) return;

            string? sessionKey = await _lastFmService.FetchSessionKeyAsync(_currentAuthToken);
            if (!string.IsNullOrEmpty(sessionKey))
            {
                _currentAuthToken = null;
                _dataLoaded = false;
                await LoadAccountStateAsync();
            }
            else
            {
                AccountSubtitleText.Text = "Waiting for authorization in browser...";
                ActionButtonText.Text = "Complete Login";
            }
        }

        private async Task LoadAccountStateAsync()
        {
            bool isLoggedIn = _lastFmService.IsLoggedIn();
            MainWindow.Current?.UpdateNavigationState(isLoggedIn);

            if (isLoggedIn)
            {
                var (username, sessionKey) = _lastFmService.GetUserSession();
                AccountTitleText.Text = username ?? "Last.fm User";
                AccountSubtitleText.Text = "Connected to Last.fm";

                ActionButton.Style = (Style)Application.Current.Resources["DefaultButtonStyle"];
                ActionButtonText.Text = "Log out";
                ActionButtonIcon.Glyph = "\uE8A7";

                if (!string.IsNullOrEmpty(username))
                {
                    var userInfo = await _lastFmService.GetUserInfoAsync(username);
                    if (userInfo.HasValue)
                    {
                        var (name, displayName, imageUrl, scrobbleCount) = userInfo.Value;
                        AccountTitleText.Text = !string.IsNullOrEmpty(displayName) ? displayName : name;
                        AccountDetailsText.Text = $"{scrobbleCount:N0} scrobbles";
                        AccountDetailsText.Visibility = Visibility.Visible;

                        if (!string.IsNullOrEmpty(imageUrl))
                        {
                            try
                            {
                                UserAvatarImage.Source = new BitmapImage(new Uri(imageUrl));
                                UserAvatarImage.Visibility = Visibility.Visible;
                                UserAvatarIcon.Visibility = Visibility.Collapsed;
                            }
                            catch (Exception ex)
                            {
                                LogService.LogError("[Render/UI Exception] Failed to load avatar image bitmap", ex);
                            }
                        }
                    }
                }
                _dataLoaded = true;
            }
            else
            {
                AccountTitleText.Text = "Login to Lastfm Account";
                AccountSubtitleText.Text = "Not connected";
                AccountDetailsText.Visibility = Visibility.Collapsed;

                UserAvatarImage.Visibility = Visibility.Collapsed;
                UserAvatarIcon.Visibility = Visibility.Visible;

                ActionButton.Style = (Style)Application.Current.Resources["AccentButtonStyle"];
                ActionButtonText.Text = string.IsNullOrEmpty(_currentAuthToken) ? "Login" : "Complete Login";
                ActionButtonIcon.Glyph = "\uE8A7";
            }
        }

        private async void OnActionButtonClick(object sender, RoutedEventArgs e)
        {
            if (_lastFmService.IsLoggedIn())
            {
                _lastFmService.ClearUserSession();
                _currentAuthToken = null;
                _dataLoaded = false;
                await LoadAccountStateAsync();
            }
            else
            {
                if (!string.IsNullOrEmpty(_currentAuthToken))
                {
                    string? sessionKey = await _lastFmService.FetchSessionKeyAsync(_currentAuthToken);
                    if (!string.IsNullOrEmpty(sessionKey))
                    {
                        _currentAuthToken = null;
                        _dataLoaded = false;
                        await LoadAccountStateAsync();
                        return;
                    }
                }

                _currentAuthToken = await _lastFmService.RequestAuthTokenAsync();

                if (!string.IsNullOrEmpty(_currentAuthToken))
                {
                    AccountSubtitleText.Text = "Authorize in browser, then click Complete Login";
                    ActionButtonText.Text = "Complete Login";
                    await _lastFmService.OpenAuthPageInBrowserAsync(_currentAuthToken);
                }
                else
                {
                    AccountSubtitleText.Text = "Authorize in browser, then click Complete Login";
                    ActionButtonText.Text = "Complete Login";
                    await _lastFmService.OpenAuthPageInBrowserAsync(null);
                }
            }
        }

        public void UpdateOfflineCacheStatus(int pendingCount = 0)
        {
            if (OfflineCacheStatusDescription == null) return;

            if (pendingCount <= 0)
            {
                OfflineCacheStatusDescription.Text = "No scrobbles pending in offline cache.";
                if (SyncNowButton != null) SyncNowButton.IsEnabled = false;
            }
            else
            {
                string itemText = pendingCount == 1 ? "1 scrobble" : $"{pendingCount} scrobbles";
                OfflineCacheStatusDescription.Text = $"{itemText} saved locally awaiting internet connection.";
                if (SyncNowButton != null) SyncNowButton.IsEnabled = true;
            }
        }

        private async void SyncNowButton_Click(object sender, RoutedEventArgs e)
        {
            if (SyncNowButton == null) return;
            SyncNowButton.IsEnabled = false;
            await OfflineCacheWorker.Instance.ForceSyncAsync();
        }

        private async void ClearCacheButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ContentDialog
            {
                Title = "Clear Offline Cache?",
                Content = "Are you sure you want to permanently delete all locally saved scrobbles?",
                PrimaryButtonText = "Clear",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.XamlRoot
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                await OfflineCacheService.Instance.ClearCacheAsync();
                await OfflineCacheWorker.Instance.UpdateCacheCountAsync();
            }
        }

        private void ScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (MainContentPanel == null) return;
            if (e.NewSize.Width > 1324)
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
