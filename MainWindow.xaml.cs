using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using System.Windows.Input;
using H.NotifyIcon;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Graphics;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;
using FluentScrobbler.Services;
using FluentScrobbler.Views;

namespace FluentScrobbler
{
    public class TrayRelayCommand : ICommand
    {
        private readonly Action _execute;
        public TrayRelayCommand(Action execute) => _execute = execute;
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => _execute();
        public event EventHandler? CanExecuteChanged { add { } remove { } }
    }

    public sealed partial class MainWindow : Window
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        public static new MainWindow? Current { get; private set; }
        public ElementTheme CurrentTheme { get; private set; } = ElementTheme.Default;
        public Windows.UI.Color CurrentAccentColor { get; private set; }
        public bool IsManualColor { get; private set; } = false;
        private readonly TrayThemeService _trayThemeService = new();
        private ScrobbleStatusInfo _currentScrobbleStatus = new(ScrobbleStatus.Idle);
        private TaskbarIcon? AppTrayIcon;

        public MainWindow()
        {
            Current = this;
            this.InitializeComponent();
            AppVersionText.Text = AppInfoService.FormattedVersion;
            this.ExtendsContentIntoTitleBar = true;
            this.SetTitleBar(AppTitleBar);
            this.Title = "Fluent Scrobbler";

            var windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(windowHandle);
            var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);

            string iconPath = System.IO.Path.Combine(System.AppContext.BaseDirectory, "Assets", "AppIcon.ico");
            if (System.IO.File.Exists(iconPath))
            {
                appWindow.SetIcon(iconPath);
            }

            LoadSavedTheme();
            InitializeTrayTheme();

            ContentFrame.Navigated += ContentFrame_Navigated;
            this.Closed += MainWindow_Closed;

            this.RootGrid.KeyDown += async (s, e) =>
            {
                if (e.Key == Windows.System.VirtualKey.F12)
                {
                    await CapturarMarketing4KAsync();
                }
            };
        }

        public async Task CapturarMarketing4KAsync(string nomeArquivo = "FluentScrobbler_Marketing_4K.png")
        {
            IntPtr hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            SizeInt32 tamanhoOriginal = this.AppWindow.Size;

            int larguraBase = 1920;
            int alturaBase = 1080;
            int larguraFinal4K = 3840;
            int alturaFinal4K = 2160;

            try
            {
                SetWindowPos(hWnd, IntPtr.Zero, 0, 0, larguraBase, alturaBase, 0x0004 | 0x0010);

                this.RootGrid.Width = larguraBase;
                this.RootGrid.Height = alturaBase;

                await Task.Delay(300);
                this.RootGrid.UpdateLayout();

                var renderBitmap = new RenderTargetBitmap();
                await renderBitmap.RenderAsync(this.RootGrid, larguraFinal4K, alturaFinal4K);

                IBuffer pixelBuffer = await renderBitmap.GetPixelsAsync();
                byte[] pixels = pixelBuffer.ToArray();

                StorageFolder pastaImagens = KnownFolders.PicturesLibrary;
                StorageFile arquivo = await pastaImagens.CreateFileAsync(
                    nomeArquivo,
                    CreationCollisionOption.GenerateUniqueName
                );

                using (IRandomAccessStream stream = await arquivo.OpenAsync(FileAccessMode.ReadWrite))
                {
                    BitmapEncoder encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
                    encoder.SetPixelData(
                        BitmapPixelFormat.Bgra8,
                        BitmapAlphaMode.Premultiplied,
                        (uint)renderBitmap.PixelWidth,
                        (uint)renderBitmap.PixelHeight,
                        96,
                        96,
                        pixels);

                    await encoder.FlushAsync();
                }
            }
            finally
            {

                this.RootGrid.ClearValue(FrameworkElement.WidthProperty);
                this.RootGrid.ClearValue(FrameworkElement.HeightProperty);

                SetWindowPos(hWnd, IntPtr.Zero, 0, 0, tamanhoOriginal.Width, tamanhoOriginal.Height, 0x0004 | 0x0010);
                this.RootGrid.UpdateLayout();
            }
        }

        #region Tray Icon Logic

        private void InitializeTrayTheme()
        {
            AppTrayIcon = new TaskbarIcon
            {
                ToolTipText = "Fluent Scrobbler - Idle",
                LeftClickCommand = new TrayRelayCommand(OnTrayIconLeftClick)
            };

            var openItem = new MenuFlyoutItem { Text = "Open Fluent Scrobbler" };
            openItem.Click += OnOpenWindowClick;

            var exitItem = new MenuFlyoutItem { Text = "Exit" };
            exitItem.Click += OnExitAppClick;

            AppTrayIcon.ContextFlyout = new MenuFlyout
            {
                Items = { openItem, new MenuFlyoutSeparator(), exitItem }
            };

            AppTrayIcon.ForceCreate(enablesEfficiencyMode: false);

            UpdateTrayIcon();
            UpdateTrayToolTip();

            _trayThemeService.RegisterThemeChangeCallback(() =>
            {
                this.DispatcherQueue.TryEnqueue(() => UpdateTrayIcon());
            });

            ScrobblerBackgroundService.Instance.StatusChanged += (sender, statusInfo) =>
            {
                this.DispatcherQueue.TryEnqueue(() => UpdateScrobbleStatus(statusInfo));
            };

            OfflineCacheWorker.Instance.OfflineModeChanged += (sender, isOffline) =>
            {
                this.DispatcherQueue.TryEnqueue(() => { UpdateTrayIcon(); UpdateTrayToolTip(); });
            };

            OfflineCacheWorker.Instance.CacheCountChanged += (sender, count) =>
            {
                this.DispatcherQueue.TryEnqueue(() => { UpdateTrayIcon(); UpdateTrayToolTip(); });
            };
        }

        public void UpdateScrobbleStatus(ScrobbleStatusInfo statusInfo)
        {
            _currentScrobbleStatus = statusInfo;
            UpdateTrayIcon();
            UpdateTrayToolTip();
        }

        private void UpdateTrayIcon()
        {
            if (AppTrayIcon == null) return;

            var theme = _trayThemeService.GetCurrentTheme();
            string colorSuffix = theme == TrayTheme.Dark ? "white" : "black";
            bool isActive = _currentScrobbleStatus.Status != ScrobbleStatus.Idle;
            bool isError = _currentScrobbleStatus.Status == ScrobbleStatus.Error || OfflineCacheWorker.Instance.OfflineMode;

            string statePrefix = isError ? "error" : (isActive ? "active" : "idle");
            string relativePath = $"ms-appx:///Assets/Tray/tray-{statePrefix}-{colorSuffix}.ico";

            try
            {
                AppTrayIcon.IconSource = new BitmapImage(new Uri(relativePath));
            }
            catch (Exception ex)
            {
                LogService.LogError($"[Tray Icon] Failed to load tray icon: {relativePath}", ex);

                string fallbackPrefix = isActive ? "active" : "idle";
                string fallbackPath = $"ms-appx:///Assets/Tray/tray-{fallbackPrefix}-{colorSuffix}.ico";
                try
                {
                    AppTrayIcon.IconSource = new BitmapImage(new Uri(fallbackPath));
                }
                catch
                {
                }
            }
        }

        private async void UpdateTrayToolTip()
        {
            if (AppTrayIcon == null) return;

            string statusText = _currentScrobbleStatus.Status switch
            {
                ScrobbleStatus.Listening => "Listening",
                ScrobbleStatus.Sent => "Sent",
                ScrobbleStatus.Error => "Error",
                _ => "Idle"
            };

            if (OfflineCacheWorker.Instance.OfflineMode)
            {
                int pendingCount = await OfflineCacheService.Instance.GetPendingCountAsync();
                statusText = $"{pendingCount} pending scrobbles (Offline)";
            }

            if (_currentScrobbleStatus.Status == ScrobbleStatus.Idle ||
                (string.IsNullOrEmpty(_currentScrobbleStatus.Track) && string.IsNullOrEmpty(_currentScrobbleStatus.Artist)))
            {
                AppTrayIcon.ToolTipText = $"Fluent Scrobbler\n{statusText}";
            }
            else
            {
                string track = _currentScrobbleStatus.Track ?? string.Empty;
                string artist = _currentScrobbleStatus.Artist ?? string.Empty;

                string header = !string.IsNullOrEmpty(artist) && !string.IsNullOrEmpty(track)
                    ? $"{artist} · {track}"
                    : (!string.IsNullOrEmpty(track) ? track : artist);

                AppTrayIcon.ToolTipText = $"{header}\n{statusText}";
            }
        }

        private void OnTrayIconLeftClick()
        {
            this.AppWindow.Show();
            this.Activate();
            if (ContentFrame.CurrentSourcePageType != typeof(ScrobblesPage))
            {
                ContentFrame.Navigate(typeof(ScrobblesPage));
            }
            SetSelectedItemByTag("ScrobblesPage");
        }

        private void OnOpenWindowClick(object sender, RoutedEventArgs e)
        {
            this.AppWindow.Show();
            this.Activate();
        }

        private void OnExitAppClick(object sender, RoutedEventArgs e)
        {
            Application.Current.Exit();
        }

        #endregion

        private void ContentFrame_Navigated(object sender, Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
        {
            if (e.SourcePageType != null)
            {
                string? tag = e.SourcePageType.Name switch
                {
                    nameof(HomePage) => "HomePage",
                    nameof(ScrobblesPage) => "ScrobblesPage",
                    nameof(SettingsPage) => "SettingsPage",
                    nameof(AccountPage) => "AccountPage",
                    nameof(ContributePage) => "ContributePage",
                    nameof(AboutPage) => "AboutPage",
                    nameof(LicensesPage) => "AboutPage",
                    _ => null
                };

                if (tag != null)
                {
                    SetSelectedItemByTag(tag);
                }
            }
        }

        private void MainWindow_Closed(object sender, WindowEventArgs args)
        {
            args.Handled = true;
            this.AppWindow.Hide();
        }

        private const string AppThemeKey = "AppThemeMode";

        private void LoadSavedTheme()
        {
            string? saved = SettingsService.GetSetting(AppThemeKey);
            ElementTheme theme = saved switch
            {
                "Light" => ElementTheme.Light,
                "Dark" => ElementTheme.Dark,
                _ => ElementTheme.Default
            };
            SetAppTheme(theme, save: false);
        }

        public void SetAppTheme(ElementTheme theme, bool save = true)
        {
            CurrentTheme = theme;
            if (this.Content is FrameworkElement root)
            {
                root.RequestedTheme = theme;
                root.ActualThemeChanged -= Root_ActualThemeChanged;
                root.ActualThemeChanged += Root_ActualThemeChanged;
            }

            UpdateTitleBarTheme(theme);

            if (save)
            {
                SettingsService.SetSetting(AppThemeKey, theme.ToString());
            }
        }

        private void Root_ActualThemeChanged(FrameworkElement sender, object args)
        {
            UpdateTitleBarTheme(CurrentTheme);
        }

        private void UpdateTitleBarTheme(ElementTheme theme)
        {
            try
            {
                var titleBar = this.AppWindow?.TitleBar;
                if (titleBar == null) return;

                ElementTheme actualTheme = theme;
                if (actualTheme == ElementTheme.Default)
                {
                    if (this.Content is FrameworkElement root)
                    {
                        actualTheme = root.ActualTheme;
                    }
                    else
                    {
                        actualTheme = Application.Current.RequestedTheme == ApplicationTheme.Dark
                            ? ElementTheme.Dark
                            : ElementTheme.Light;
                    }
                }

                if (actualTheme == ElementTheme.Light)
                {
                    titleBar.ButtonForegroundColor = Microsoft.UI.Colors.Black;
                    titleBar.ButtonHoverForegroundColor = Microsoft.UI.Colors.Black;
                    titleBar.ButtonPressedForegroundColor = Microsoft.UI.Colors.Black;
                    titleBar.ButtonInactiveForegroundColor = Windows.UI.Color.FromArgb(255, 120, 120, 120);
                    titleBar.ButtonHoverBackgroundColor = Windows.UI.Color.FromArgb(20, 0, 0, 0);
                    titleBar.ButtonPressedBackgroundColor = Windows.UI.Color.FromArgb(30, 0, 0, 0);
                }
                else
                {
                    titleBar.ButtonForegroundColor = Microsoft.UI.Colors.White;
                    titleBar.ButtonHoverForegroundColor = Microsoft.UI.Colors.White;
                    titleBar.ButtonPressedForegroundColor = Microsoft.UI.Colors.White;
                    titleBar.ButtonInactiveForegroundColor = Windows.UI.Color.FromArgb(255, 150, 150, 150);
                    titleBar.ButtonHoverBackgroundColor = Windows.UI.Color.FromArgb(20, 255, 255, 255);
                    titleBar.ButtonPressedBackgroundColor = Windows.UI.Color.FromArgb(30, 255, 255, 255);
                }
            }
            catch
            {
            }
        }

        public void SetColorMode(bool isManual)
        {
            IsManualColor = isManual;
        }

        public void SetAccentColor(Windows.UI.Color color)
        {
            CurrentAccentColor = color;
            Windows.UI.Color light1 = LightenColor(color, 0.15f);
            Windows.UI.Color light2 = LightenColor(color, 0.30f);
            Windows.UI.Color light3 = LightenColor(color, 0.45f);
            Windows.UI.Color dark1 = DarkenColor(color, 0.15f);
            Windows.UI.Color dark2 = DarkenColor(color, 0.30f);
            Windows.UI.Color dark3 = DarkenColor(color, 0.45f);

            double luminance = (0.299 * color.R + 0.587 * color.G + 0.114 * color.B) / 255.0;
            Windows.UI.Color textOnAccentColor = luminance > 0.55 ? Microsoft.UI.Colors.Black : Microsoft.UI.Colors.White;

            UpdateResourceColor("SystemAccentColor", color);
            UpdateResourceColor("SystemAccentColorLight1", light1);
            UpdateResourceColor("SystemAccentColorLight2", light2);
            UpdateResourceColor("SystemAccentColorLight3", light3);
            UpdateResourceColor("SystemAccentColorDark1", dark1);
            UpdateResourceColor("SystemAccentColorDark2", dark2);
            UpdateResourceColor("SystemAccentColorDark3", dark3);
            UpdateResourceColor("TextOnAccentFillColorPrimary", textOnAccentColor);

            UpdateResourceBrush("AccentFillColorDefaultBrush", color);
            UpdateResourceBrush("AccentFillColorSecondaryBrush", light1);
            UpdateResourceBrush("AccentFillColorTertrush", light2);
            UpdateResourceBrush("AccentTextFillColorPrimaryBrush", color);
            UpdateResourceBrush("TextOnAccentFillColorPrimaryBrush", textOnAccentColor);
            UpdateResourceBrush("ToggleSwitchFillOn", color);
            UpdateResourceBrush("ToggleSwitchFillOnPointerOver", light1);
            UpdateResourceBrush("ToggleSwitchFillOnPressed", dark1);

            if (this.Content is FrameworkElement root)
            {
                root.Resources["SystemAccentColor"] = color;
                root.Resources["AccentFillColorDefaultBrush"] = Application.Current.Resources["AccentFillColorDefaultBrush"];
                root.Resources["AccentFillColorSecondaryBrush"] = Application.Current.Resources["AccentFillColorSecondaryBrush"];
                root.Resources["AccentFillColorTertiaryBrush"] = Application.Current.Resources["AccentFillColorTertiaryBrush"];
                root.Resources["TextOnAccentFillColorPrimaryBrush"] = Application.Current.Resources["TextOnAccentFillColorPrimaryBrush"];

                var current = root.RequestedTheme;
                root.RequestedTheme = ElementTheme.Default;
                root.RequestedTheme = current;
            }
        }

        private static void UpdateResourceColor(string key, Windows.UI.Color color)
        {
            Application.Current.Resources[key] = color;
        }

        private static void UpdateResourceBrush(string key, Windows.UI.Color color)
        {
            if (Application.Current.Resources[key] is SolidColorBrush brush)
            {
                brush.Color = color;
            }
            else
            {
                Application.Current.Resources[key] = new SolidColorBrush(color);
            }
        }

        private static Windows.UI.Color LightenColor(Windows.UI.Color color, float factor)
        {
            byte r = (byte)Math.Min(255, color.R + (255 - color.R) * factor);
            byte g = (byte)Math.Min(255, color.G + (255 - color.G) * factor);
            byte b = (byte)Math.Min(255, color.B + (255 - color.B) * factor);
            return Windows.UI.Color.FromArgb(color.A, r, g, b);
        }

        private static Windows.UI.Color DarkenColor(Windows.UI.Color color, float factor)
        {
            byte r = (byte)Math.Max(0, color.R * (1 - factor));
            byte g = (byte)Math.Max(0, color.G * (1 - factor));
            byte b = (byte)Math.Max(0, color.B * (1 - factor));
            return Windows.UI.Color.FromArgb(color.A, r, g, b);
        }

        public void UpdateNavigationState(bool isLoggedIn)
        {
            foreach (var item in NavView.MenuItems)
            {
                if (item is NavigationViewItem navItem)
                {
                    string tag = navItem.Tag?.ToString() ?? "";
                    if (tag != "AccountPage")
                    {
                        navItem.IsEnabled = isLoggedIn;
                    }
                }
            }

            foreach (var item in NavView.FooterMenuItems)
            {
                if (item is NavigationViewItem navItem)
                {
                    string tag = navItem.Tag?.ToString() ?? "";
                    if (tag != "AboutPage" && tag != "ContributePage")
                    {
                        navItem.IsEnabled = isLoggedIn;
                    }
                }
            }
        }

        private void NavView_Loaded(object sender, RoutedEventArgs e)
        {
            var service = new LastFmService();
            bool isLoggedIn = service.IsLoggedIn();
            UpdateNavigationState(isLoggedIn);

            ScrobblerBackgroundService.Instance.Start();
            OfflineCacheWorker.Instance.Start();

            UpdateService.Instance.UpdateStatusChanged += (s, e) =>
            {
                this.DispatcherQueue.TryEnqueue(UpdateStatusUi);
            };
            UpdateStatusUi();
            _ = UpdateService.Instance.CheckForUpdatesAsync();

            if (!isLoggedIn)
            {
                ContentFrame.Navigate(typeof(AccountPage));
                SetSelectedItemByTag("AccountPage");
            }
            else
            {
                ContentFrame.Navigate(typeof(HomePage));
                SetSelectedItemByTag("HomePage");
            }

            if (FeatureBadgeService.HasUnseenSettingsFeatures())
            {
                SettingsNavInfoBadge.Visibility = Visibility.Visible;
            }
        }

        public void UpdateSettingsBadge()
        {
            if (SettingsNavInfoBadge != null)
            {
                SettingsNavInfoBadge.Visibility = FeatureBadgeService.HasUnseenSettingsFeatures()
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
        }

        public void HideSettingsBadge()
        {
            if (SettingsNavInfoBadge != null)
            {
                SettingsNavInfoBadge.Visibility = Visibility.Collapsed;
            }
        }

        public void NavigateToHome()
        {
            ContentFrame.Navigate(typeof(HomePage));
            SetSelectedItemByTag("HomePage");
        }

        public void NavigateToSourceSettings()
        {
            ContentFrame.Navigate(typeof(SettingsPage), "ExpandSourceFiltering");
            SetSelectedItemByTag("SettingsPage");
        }

        private void SetSelectedItemByTag(string tag)
        {
            foreach (var item in NavView.MenuItems)
            {
                if (item is NavigationViewItem navItem && navItem.Tag?.ToString() == tag)
                {
                    NavView.SelectedItem = navItem;
                    return;
                }
            }

            foreach (var item in NavView.FooterMenuItems)
            {
                if (item is NavigationViewItem navItem && navItem.Tag?.ToString() == tag)
                {
                    NavView.SelectedItem = navItem;
                    return;
                }
            }
        }

        private void StatusButton_Click(object sender, RoutedEventArgs e)
        {
            if (ContentFrame.CurrentSourcePageType != typeof(AboutPage))
            {
                ContentFrame.Navigate(typeof(AboutPage));
            }
            SetSelectedItemByTag("AboutPage");
        }

        private void UpdateStatusUi()
        {
            if (UpdateService.Instance.IsUpdateAvailable)
            {
                StatusIcon.Symbol = FluentIcons.Common.Symbol.ArrowSync;
                StatusIcon.Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["AccentTextFillColorPrimaryBrush"];
                ToolTipService.SetToolTip(StatusButton, $"Update available: {UpdateService.Instance.LatestVersion}");
                StatusButton.Visibility = Visibility.Visible;
            }
            else
            {
                StatusButton.Visibility = Visibility.Collapsed;
            }
        }

        private void NavView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
        {
            if (args.InvokedItemContainer is NavigationViewItem item && item.IsEnabled)
            {
                Type? pageType = item.Tag?.ToString() switch
                {
                    "HomePage" => typeof(HomePage),
                    "ScrobblesPage" => typeof(ScrobblesPage),
                    "SettingsPage" => typeof(SettingsPage),
                    "AccountPage" => typeof(AccountPage),
                    "ContributePage" => typeof(ContributePage),
                    "AboutPage" => typeof(AboutPage),
                    _ => null
                };

                if (pageType != null && ContentFrame.CurrentSourcePageType != pageType)
                {
                    ContentFrame.Navigate(pageType);
                }
            }
        }
    }
}
