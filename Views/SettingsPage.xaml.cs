using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
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

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            if (e.Parameter is string param && param == "ExpandSourceFiltering")
            {
                isSourceFilteringExpanded = true;
            }
        }

        private void SettingsPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (isSourceFilteringExpanded)
            {
                SourceFilteringAccordion.Visibility = Visibility.Visible;
                SourceFilteringChevron.Symbol = FluentIcons.Common.Symbol.ChevronDown;
            }

            ThemeModeComboBox.SelectionChanged -= ThemeMode_SelectionChanged;

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

            ThemeModeComboBox.SelectionChanged += ThemeMode_SelectionChanged;

            var mediaService = new WindowsMediaService();
            bool isPrimaryArtistOnly = mediaService.IsPrimaryArtistOnlyEnabled();
            UsePrimaryArtistOnlyToggle.Toggled -= UsePrimaryArtistOnlyToggle_Toggled;
            UsePrimaryArtistOnlyToggle.IsOn = isPrimaryArtistOnly;
            UsePrimaryArtistOnlyStatusText.Text = isPrimaryArtistOnly ? "On" : "Off";
            UsePrimaryArtistOnlyToggle.Toggled += UsePrimaryArtistOnlyToggle_Toggled;

            bool isStartupEnabled = StartupService.IsStartupEnabled();
            StartOnStartupToggle.Toggled -= StartOnStartupToggle_Toggled;
            StartOnStartupToggle.IsOn = isStartupEnabled;
            StartOnStartupStatusText.Text = isStartupEnabled ? "On" : "Off";
            StartOnStartupToggle.Toggled += StartOnStartupToggle_Toggled;

            bool isStartMinimized = StartupService.IsStartMinimizedToTrayEnabled();
            StartMinimizedToTrayToggle.Toggled -= StartMinimizedToTrayToggle_Toggled;
            StartMinimizedToTrayToggle.IsOn = isStartMinimized;
            StartMinimizedToTrayStatusText.Text = isStartMinimized ? "On" : "Off";
            StartMinimizedToTrayToggle.Toggled += StartMinimizedToTrayToggle_Toggled;

            LoadSourceApplications();
        }

        private void SettingsPage_Unloaded(object sender, RoutedEventArgs e)
        {
            ThemeModeComboBox.SelectionChanged -= ThemeMode_SelectionChanged;
            UsePrimaryArtistOnlyToggle.Toggled -= UsePrimaryArtistOnlyToggle_Toggled;
            StartOnStartupToggle.Toggled -= StartOnStartupToggle_Toggled;
            StartMinimizedToTrayToggle.Toggled -= StartMinimizedToTrayToggle_Toggled;
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
                        Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"]
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
                        Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"]
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

        private void UsePrimaryArtistOnlyToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (UsePrimaryArtistOnlyStatusText != null && UsePrimaryArtistOnlyToggle != null)
            {
                bool isOn = UsePrimaryArtistOnlyToggle.IsOn;
                UsePrimaryArtistOnlyStatusText.Text = isOn ? "On" : "Off";
                var mediaService = new WindowsMediaService();
                mediaService.SetPrimaryArtistOnlyEnabled(isOn);
            }
        }

        private void StartOnStartupToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (StartOnStartupStatusText != null && StartOnStartupToggle != null)
            {
                bool isOn = StartOnStartupToggle.IsOn;
                StartOnStartupStatusText.Text = isOn ? "On" : "Off";
                StartupService.SetStartup(isOn);
            }
        }

        private void StartMinimizedToTrayToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (StartMinimizedToTrayStatusText != null && StartMinimizedToTrayToggle != null)
            {
                bool isOn = StartMinimizedToTrayToggle.IsOn;
                StartMinimizedToTrayStatusText.Text = isOn ? "On" : "Off";
                StartupService.SetStartMinimizedToTrayEnabled(isOn);
            }
        }

        private void ScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (MainContentPanel == null) return;
            const double maxContentWidth = 1400;
            const double horizontalPadding = 64;
            if (e.NewSize.Width > maxContentWidth + horizontalPadding)
            {
                MainContentPanel.HorizontalAlignment = HorizontalAlignment.Center;
                MainContentPanel.Width = maxContentWidth;
            }
            else
            {
                MainContentPanel.HorizontalAlignment = HorizontalAlignment.Stretch;
                MainContentPanel.Width = double.NaN;
            }
        }
    }
}
