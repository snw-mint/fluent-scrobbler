using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FluentScrobbler
{
    public sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            this.InitializeComponent();
            this.ExtendsContentIntoTitleBar = true;
            this.SetTitleBar(AppTitleBar);
            this.Title = "Fluent Scrobbler";
        }

        private void NavView_Loaded(object sender, RoutedEventArgs e)
        {
            ContentFrame.Navigate(typeof(HomePage));
            if (NavView.MenuItems.Count > 0)
            {
                NavView.SelectedItem = NavView.MenuItems[0];
            }
        }

        private void NavView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
        {
            if (args.InvokedItemContainer is NavigationViewItem item)
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