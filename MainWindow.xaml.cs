using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using FluentScrobbler.Services;
using FluentScrobbler.Views;

namespace FluentScrobbler
{
    public sealed partial class MainWindow : Window
    {
        public static new MainWindow? Current { get; private set; }
        public ElementTheme CurrentTheme { get; private set; } = ElementTheme.Default;
        public Windows.UI.Color CurrentAccentColor { get; private set; } = Windows.UI.Color.FromArgb(255, 0, 120, 212);
        public bool IsManualColor { get; private set; } = false;

        public MainWindow()
        {
            Current = this;
            this.InitializeComponent();
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

            SetAccentColor(Windows.UI.Color.FromArgb(255, 0, 120, 212));

            this.Closed += MainWindow_Closed;
        }

        private void MainWindow_Closed(object sender, WindowEventArgs args)
        {
            args.Handled = true;
            this.AppWindow.Hide();
        }

        public void SetAppTheme(ElementTheme theme)
        {
            CurrentTheme = theme;
            if (this.Content is FrameworkElement root)
            {
                root.RequestedTheme = theme;
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

            UpdateResourceColor("SystemAccentColor", color);
            UpdateResourceColor("SystemAccentColorLight1", light1);
            UpdateResourceColor("SystemAccentColorLight2", light2);
            UpdateResourceColor("SystemAccentColorLight3", light3);
            UpdateResourceColor("SystemAccentColorDark1", dark1);
            UpdateResourceColor("SystemAccentColorDark2", dark2);
            UpdateResourceColor("SystemAccentColorDark3", dark3);

            UpdateResourceBrush("AccentFillColorDefaultBrush", color);
            UpdateResourceBrush("AccentFillColorSecondaryBrush", light1);
            UpdateResourceBrush("AccentFillColorTertiaryBrush", light2);
            UpdateResourceBrush("AccentTextFillColorPrimaryBrush", color);
            UpdateResourceBrush("ToggleSwitchFillOn", color);
            UpdateResourceBrush("ToggleSwitchFillOnPointerOver", light1);
            UpdateResourceBrush("ToggleSwitchFillOnPressed", dark1);

            if (this.Content is FrameworkElement root)
            {
                root.Resources["SystemAccentColor"] = color;
                root.Resources["AccentFillColorDefaultBrush"] = Application.Current.Resources["AccentFillColorDefaultBrush"];
                root.Resources["AccentFillColorSecondaryBrush"] = Application.Current.Resources["AccentFillColorSecondaryBrush"];
                root.Resources["AccentFillColorTertiaryBrush"] = Application.Current.Resources["AccentFillColorTertiaryBrush"];

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
                    if (tag != "AboutPage")
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