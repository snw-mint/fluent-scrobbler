using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Storage.Pickers;
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
        private System.Threading.CancellationTokenSource? _authPollCts;

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
            StopAuthPolling();
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

        private void StopAuthPolling()
        {
            _authPollCts?.Cancel();
            _authPollCts?.Dispose();
            _authPollCts = null;
        }

        private void StartAuthPolling(string token)
        {
            StopAuthPolling();
            _authPollCts = new System.Threading.CancellationTokenSource();
            var ct = _authPollCts.Token;

            _ = Task.Run(async () =>
            {
                while (!ct.IsCancellationRequested)
                {
                    try
                    {
                        await Task.Delay(2000, ct);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }

                    if (ct.IsCancellationRequested) break;

                    string? sessionKey = await _lastFmService.FetchSessionKeyAsync(token);
                    if (!string.IsNullOrEmpty(sessionKey))
                    {
                        _currentAuthToken = null;
                        _dataLoaded = false;
                        var (username, _) = _lastFmService.GetUserSession();
                        NotificationService.ShowAuthSuccessNotification(username ?? "User");

                        this.DispatcherQueue?.TryEnqueue(async () =>
                        {
                            await LoadAccountStateAsync();
                        });
                        break;
                    }
                }
            }, ct);
        }

        private async Task TryCompleteAuthAsync()
        {
            if (string.IsNullOrEmpty(_currentAuthToken)) return;

            string? sessionKey = await _lastFmService.FetchSessionKeyAsync(_currentAuthToken);
            if (!string.IsNullOrEmpty(sessionKey))
            {
                StopAuthPolling();
                _currentAuthToken = null;
                _dataLoaded = false;
                var (username, _) = _lastFmService.GetUserSession();
                NotificationService.ShowAuthSuccessNotification(username ?? "User");
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
                                var bmp = new BitmapImage { DecodePixelWidth = 160, UriSource = new Uri(imageUrl) };
                                UserAvatarImage.Source = bmp;
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

            if (ExportConfigButton != null) ExportConfigButton.IsEnabled = isLoggedIn;
            if (ImportConfigButton != null) ImportConfigButton.IsEnabled = isLoggedIn;
            if (ConfigBackupDescription != null)
            {
                ConfigBackupDescription.Text = isLoggedIn
                    ? "Export your settings to a JSON file or import existing preferences."
                    : "Log in to your account to export or import your application settings.";
            }
        }

        private async void OnActionButtonClick(object sender, RoutedEventArgs e)
        {
            if (_lastFmService.IsLoggedIn())
            {
                StopAuthPolling();
                _lastFmService.ClearUserSession();
                _currentAuthToken = null;
                _dataLoaded = false;
                await LoadAccountStateAsync();
            }
            else
            {
                try
                {
                    ActionButton.IsEnabled = false;
                    ActionButtonIcon.Visibility = Visibility.Collapsed;
                    ActionButtonRing.Visibility = Visibility.Visible;
                    ActionButtonRing.IsActive = true;

                    if (!string.IsNullOrEmpty(_currentAuthToken))
                    {
                        string? k = await _lastFmService.FetchSessionKeyAsync(_currentAuthToken);
                        if (!string.IsNullOrEmpty(k))
                        {
                            StopAuthPolling();
                            _currentAuthToken = null;
                            _dataLoaded = false;
                            var (u, _) = _lastFmService.GetUserSession();
                            NotificationService.ShowAuthSuccessNotification(u ?? "User");
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
                        StartAuthPolling(_currentAuthToken);
                    }
                    else
                    {
                        AccountSubtitleText.Text = "Network error: Failed to request token. Try again.";
                        ActionButtonText.Text = "Retry Login";
                        _currentAuthToken = null;
                    }
                }
                finally
                {
                    ActionButton.IsEnabled = true;
                    ActionButtonRing.IsActive = false;
                    ActionButtonRing.Visibility = Visibility.Collapsed;
                    ActionButtonIcon.Visibility = Visibility.Visible;
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
            MainContentPanel.HorizontalAlignment = HorizontalAlignment.Center;
            MainContentPanel.Width = Math.Max(0, (e.NewSize.Width - 64) * 0.9);
        }

        private IntPtr GetWindowHandle()
        {
            if (MainWindow.Current != null)
            {
                return WinRT.Interop.WindowNative.GetWindowHandle(MainWindow.Current);
            }
            return IntPtr.Zero;
        }

        private async void ExportConfigButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var savePicker = new FileSavePicker();
                var hwnd = GetWindowHandle();
                if (hwnd != IntPtr.Zero)
                {
                    WinRT.Interop.InitializeWithWindow.Initialize(savePicker, hwnd);
                }

                savePicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
                savePicker.FileTypeChoices.Add("JSON File", new List<string> { ".json" });
                savePicker.SuggestedFileName = $"FluentScrobbler-Settings-{DateTime.Now:yyyyMMdd}.json";

                var file = await savePicker.PickSaveFileAsync();
                if (file != null)
                {
                    await SettingsService.ExportSettingsAsync(file.Path);

                    var dialog = new ContentDialog
                    {
                        Title = "Settings Exported",
                        Content = $"Your settings were successfully exported to:\n{file.Path}",
                        CloseButtonText = "OK",
                        DefaultButton = ContentDialogButton.Close,
                        XamlRoot = this.XamlRoot
                    };
                    await dialog.ShowAsync();
                }
            }
            catch (Exception ex)
            {
                LogService.LogError("[Export Settings Error]", ex);
                var errorDialog = new ContentDialog
                {
                    Title = "Export Failed",
                    Content = $"Could not export settings: {ex.Message}",
                    CloseButtonText = "OK",
                    DefaultButton = ContentDialogButton.Close,
                    XamlRoot = this.XamlRoot
                };
                await errorDialog.ShowAsync();
            }
        }

        private async void ImportConfigButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var openPicker = new FileOpenPicker();
                var hwnd = GetWindowHandle();
                if (hwnd != IntPtr.Zero)
                {
                    WinRT.Interop.InitializeWithWindow.Initialize(openPicker, hwnd);
                }

                openPicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
                openPicker.FileTypeFilter.Add(".json");

                var file = await openPicker.PickSingleFileAsync();
                if (file != null)
                {
                    var confirmDialog = new ContentDialog
                    {
                        Title = "Import Settings?",
                        Content = "Importing settings will overwrite your current application preferences. Do you wish to continue?",
                        PrimaryButtonText = "Import",
                        CloseButtonText = "Cancel",
                        DefaultButton = ContentDialogButton.Close,
                        XamlRoot = this.XamlRoot
                    };

                    var result = await confirmDialog.ShowAsync();
                    if (result == ContentDialogResult.Primary)
                    {
                        bool imported = await SettingsService.ImportSettingsAsync(file.Path);
                        if (imported)
                        {
                            var successDialog = new ContentDialog
                            {
                                Title = "Settings Imported",
                                Content = "Settings were imported successfully! You may need to restart the application for all changes to take full effect.",
                                CloseButtonText = "OK",
                                DefaultButton = ContentDialogButton.Close,
                                XamlRoot = this.XamlRoot
                            };
                            await successDialog.ShowAsync();
                        }
                        else
                        {
                            var failDialog = new ContentDialog
                            {
                                Title = "Import Failed",
                                Content = "The selected file is not a valid Fluent Scrobbler settings backup or is corrupted.",
                                CloseButtonText = "OK",
                                DefaultButton = ContentDialogButton.Close,
                                XamlRoot = this.XamlRoot
                            };
                            await failDialog.ShowAsync();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogService.LogError("[Import Settings Error]", ex);
                var errorDialog = new ContentDialog
                {
                    Title = "Import Failed",
                    Content = $"Could not import settings: {ex.Message}",
                    CloseButtonText = "OK",
                    DefaultButton = ContentDialogButton.Close,
                    XamlRoot = this.XamlRoot
                };
                await errorDialog.ShowAsync();
            }
        }
    }
}
