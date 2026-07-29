using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using FluentScrobbler.Services;

namespace FluentScrobbler.Views
{
    public sealed partial class SettingsPage : Page
    {
        private bool isSourceFilteringExpanded = false;

        public SettingsPage()
        {
            this.InitializeComponent();
            this.Loaded += SettingsPage_Loaded;
            this.Unloaded += SettingsPage_Unloaded;
        }

        private void SettingsPage_Loaded(object sender, RoutedEventArgs e)
        {
            ThemeModeComboBox.SelectionChanged -= ThemeMode_SelectionChanged;
            PrimaryArtistToggle.Toggled -= PrimaryArtistToggle_Toggled;

            if (MainWindow.Current != null)
            {
                ThemeModeComboBox.SelectedIndex = MainWindow.Current.CurrentTheme switch
                {
                    ElementTheme.Light => 0,
                    ElementTheme.Dark => 1,
                    ElementTheme.Default => 2,
                    _ => 2
                };
            }

            var mediaService = new WindowsMediaService();
            PrimaryArtistToggle.IsOn = mediaService.IsPrimaryArtistOnlyEnabled();
            UpdatePrimaryArtistStatusText();

            ThemeModeComboBox.SelectionChanged += ThemeMode_SelectionChanged;
            PrimaryArtistToggle.Toggled += PrimaryArtistToggle_Toggled;

            LoadSourceApplications();
        }

        private void SettingsPage_Unloaded(object sender, RoutedEventArgs e)
        {
            ThemeModeComboBox.SelectionChanged -= ThemeMode_SelectionChanged;
            PrimaryArtistToggle.Toggled -= PrimaryArtistToggle_Toggled;
        }

        private void PrimaryArtistToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (PrimaryArtistToggle == null) return;
            UpdatePrimaryArtistStatusText();
            var mediaService = new WindowsMediaService();
            mediaService.SetPrimaryArtistOnlyEnabled(PrimaryArtistToggle.IsOn);
        }

        private void UpdatePrimaryArtistStatusText()
        {
            if (PrimaryArtistStatusText != null && PrimaryArtistToggle != null)
            {
                PrimaryArtistStatusText.Text = PrimaryArtistToggle.IsOn ? "On" : "Off";
            }
        }

        private void SourceFilteringHeader_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            isSourceFilteringExpanded = !isSourceFilteringExpanded;
            SourceFilteringAccordion.Visibility = isSourceFilteringExpanded ? Visibility.Visible : Visibility.Collapsed;
            SourceFilteringChevron.Symbol = isSourceFilteringExpanded ? FluentIcons.Common.Symbol.ChevronDown : FluentIcons.Common.Symbol.ChevronRight;
        }

        private async void LoadSourceApplications()
        {
            try
            {
                var mediaService = new WindowsMediaService();
                var sources = await mediaService.GetDetectedSourcesAsync();

                if (SourceAppsStackPanel == null) return;
                SourceAppsStackPanel.Children.Clear();

                if (sources == null || sources.Count == 0)
                {
                    var emptyText = new TextBlock
                    {
                        Text = "No media applications detected yet. Play media in any app to list it here.",
                        Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                        Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
                    };
                    SourceAppsStackPanel.Children.Add(emptyText);
                    return;
                }

                foreach (var app in sources)
                {
                    var textStack = new StackPanel
                    {
                        VerticalAlignment = VerticalAlignment.Center,
                        Spacing = 2,
                        Margin = new Thickness(4, 0, 0, 0)
                    };

                    var nameText = new TextBlock
                    {
                        Text = app.DisplayName,
                        Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"]
                    };
                    textStack.Children.Add(nameText);

                    var packageText = new TextBlock
                    {
                        Text = app.AppId,
                        Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                        Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
                    };
                    textStack.Children.Add(packageText);

                    var check = new CheckBox
                    {
                        IsChecked = app.IsAllowed,
                        Content = textStack,
                        VerticalAlignment = VerticalAlignment.Center,
                        Tag = app.AppId
                    };
                    check.Checked += SourceApp_CheckChanged;
                    check.Unchecked += SourceApp_CheckChanged;

                    SourceAppsStackPanel.Children.Add(check);
                }
            }
            catch
            {
            }
        }

        private void SourceApp_CheckChanged(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox check && check.Tag is string appId)
            {
                var mediaService = new WindowsMediaService();
                mediaService.SetSourceAllowed(appId, check.IsChecked == true);
            }
        }

        private void ThemeMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ThemeModeComboBox == null || MainWindow.Current == null) return;

            ElementTheme theme = ThemeModeComboBox.SelectedIndex switch
            {
                0 => ElementTheme.Light,
                1 => ElementTheme.Dark,
                2 => ElementTheme.Default,
                _ => ElementTheme.Default
            };

            MainWindow.Current.SetAppTheme(theme);
        }
    }
}
