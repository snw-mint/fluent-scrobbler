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
        }

        private void AccountPage_Unloaded(object sender, RoutedEventArgs e)
        {
            if (MainWindow.Current != null)
            {
                MainWindow.Current.Activated -= Window_Activated;
            }
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
                        var (name, imageUrl, scrobbleCount) = userInfo.Value;
                        AccountTitleText.Text = name;
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
    }
}