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

        public ElementTheme CurrentTheme { get; private set; } = ElementTheme.Light;
        public Windows.UI.Color CurrentAccentColor { get; private set; } = Windows.UI.Color.FromArgb(255, 0, 120, 212);
        public bool IsAcrylic { get; private set; } = false;
        public bool IsManualColor { get; private set; } = true;

        public MainWindow()
        {
            Current = this;
            this.InitializeComponent();
            this.ExtendsContentIntoTitleBar = true;
            this.SetTitleBar(AppTitleBar);
            this.Title = "Fluent Scrobbler";
        }

        public void SetAppTheme(ElementTheme theme)
        {
            CurrentTheme = theme;
            if (this.Content is FrameworkElement root)
            {
                root.RequestedTheme = theme;
            }
        }

        public void SetSystemBackdrop(bool isAcrylic)
        {
            IsAcrylic = isAcrylic;
            if (isAcrylic)
            {
                this.SystemBackdrop = new DesktopAcrylicBackdrop();
            }
            else
            {
                this.SystemBackdrop = new MicaBackdrop();
            }
        }

        public void SetColorMode(bool isManual)
        {
            IsManualColor = isManual;
        }

        public void SetAccentColor(Windows.UI.Color color)
        {
            CurrentAccentColor = color;
            var brush = new SolidColorBrush(color);

            if (this.Content is FrameworkElement root)
            {
                root.Resources["SystemAccentColor"] = color;
                root.Resources["SystemAccentColorLight1"] = color;
                root.Resources["SystemAccentColorLight2"] = color;
                root.Resources["SystemAccentColorDark1"] = color;
                root.Resources["SystemAccentColorDark2"] = color;
                root.Resources["AccentFillColorDefaultBrush"] = brush;
                root.Resources["AccentFillColorSecondaryBrush"] = brush;
                root.Resources["AccentFillColorTertiaryBrush"] = brush;
                root.Resources["AccentTextFillColorPrimaryBrush"] = brush;
            }

            Application.Current.Resources["SystemAccentColor"] = color;
            Application.Current.Resources["AccentFillColorDefaultBrush"] = brush;
            Application.Current.Resources["AccentFillColorSecondaryBrush"] = brush;
            Application.Current.Resources["AccentFillColorTertiaryBrush"] = brush;
            Application.Current.Resources["AccentTextFillColorPrimaryBrush"] = brush;
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
                    if (tag != "AboutPage" && tag != "ProPage")
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
                    "FavoritesPage" => typeof(FavoritesPage),
                    "ChartsPage" => typeof(ChartsPage),
                    "SettingsPage" => typeof(SettingsPage),
                    "AccountPage" => typeof(AccountPage),
                    "ProPage" => typeof(ProPage),
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