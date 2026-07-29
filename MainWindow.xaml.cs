using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using FluentScrobbler.Services;

namespace FluentScrobbler
{
    public sealed partial class MainWindow : Window
    {
        public static new MainWindow? Current { get; private set; }

        public MainWindow()
        {
            Current = this;
            this.InitializeComponent();
            this.ExtendsContentIntoTitleBar = true;
            this.SetTitleBar(AppTitleBar);
            this.Title = "Fluent Scrobbler";
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