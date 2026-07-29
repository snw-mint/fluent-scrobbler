using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
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
            ColorModeComboBox.SelectionChanged -= ColorMode_SelectionChanged;
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

                ColorModeComboBox.SelectedIndex = MainWindow.Current.IsManualColor ? 1 : 0;
                PaletteSection.Visibility = MainWindow.Current.IsManualColor ? Visibility.Visible : Visibility.Collapsed;
            }

            var mediaService = new WindowsMediaService();
            PrimaryArtistToggle.IsOn = mediaService.IsPrimaryArtistOnlyEnabled();
            UpdatePrimaryArtistStatusText();

            MinTrackLengthSlider.ValueChanged -= MinTrackLengthSlider_ValueChanged;
            int minSeconds = mediaService.GetMinimumTrackLengthSeconds();
            MinTrackLengthSlider.Value = minSeconds;
            UpdateMinTrackLengthText(minSeconds);
            MinTrackLengthSlider.ValueChanged += MinTrackLengthSlider_ValueChanged;

            NowPlayingToggle.Toggled -= NowPlayingToggle_Toggled;
            NowPlayingToggle.IsOn = mediaService.IsSendNowPlayingEnabled();
            UpdateNowPlayingStatusText();

            PercentageThresholdSlider.ValueChanged -= PercentageThresholdSlider_ValueChanged;
            int pct = mediaService.GetScrobblePercentageThreshold();
            PercentageThresholdSlider.Value = pct;
            UpdatePercentageThresholdText(pct);
            PercentageThresholdSlider.ValueChanged += PercentageThresholdSlider_ValueChanged;

            MaxTimeThresholdSlider.ValueChanged -= MaxTimeThresholdSlider_ValueChanged;
            int maxSeconds = mediaService.GetMaximumTimeThresholdSeconds();
            MaxTimeThresholdSlider.Value = maxSeconds;
            UpdateMaxTimeThresholdText(maxSeconds);
            MaxTimeThresholdSlider.ValueChanged += MaxTimeThresholdSlider_ValueChanged;

            ThemeModeComboBox.SelectionChanged += ThemeMode_SelectionChanged;
            ColorModeComboBox.SelectionChanged += ColorMode_SelectionChanged;
            PrimaryArtistToggle.Toggled += PrimaryArtistToggle_Toggled;
            NowPlayingToggle.Toggled += NowPlayingToggle_Toggled;

            LoadSourceApplications();
        }

        private void SettingsPage_Unloaded(object sender, RoutedEventArgs e)
        {
            ThemeModeComboBox.SelectionChanged -= ThemeMode_SelectionChanged;
            ColorModeComboBox.SelectionChanged -= ColorMode_SelectionChanged;
            PrimaryArtistToggle.Toggled -= PrimaryArtistToggle_Toggled;
            MinTrackLengthSlider.ValueChanged -= MinTrackLengthSlider_ValueChanged;
            NowPlayingToggle.Toggled -= NowPlayingToggle_Toggled;
            PercentageThresholdSlider.ValueChanged -= PercentageThresholdSlider_ValueChanged;
            MaxTimeThresholdSlider.ValueChanged -= MaxTimeThresholdSlider_ValueChanged;
        }

        private void PercentageThresholdSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (PercentageThresholdSlider == null) return;
            int pct = (int)e.NewValue;
            UpdatePercentageThresholdText(pct);
            var mediaService = new WindowsMediaService();
            mediaService.SetScrobblePercentageThreshold(pct);
        }

        private void UpdatePercentageThresholdText(int pct)
        {
            if (PercentageThresholdText != null)
            {
                PercentageThresholdText.Text = $"{pct}%";
            }
        }

        private void MaxTimeThresholdSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (MaxTimeThresholdSlider == null) return;
            int seconds = (int)e.NewValue;
            UpdateMaxTimeThresholdText(seconds);
            var mediaService = new WindowsMediaService();
            mediaService.SetMaximumTimeThresholdSeconds(seconds);
        }

        private void UpdateMaxTimeThresholdText(int seconds)
        {
            if (MaxTimeThresholdText != null)
            {
                MaxTimeThresholdText.Text = TimeSpan.FromSeconds(seconds).ToString(@"mm\:ss");
            }
        }

        private void NowPlayingToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (NowPlayingToggle == null) return;
            UpdateNowPlayingStatusText();
            var mediaService = new WindowsMediaService();
            mediaService.SetSendNowPlayingEnabled(NowPlayingToggle.IsOn);
        }

        private void UpdateNowPlayingStatusText()
        {
            if (NowPlayingStatusText != null && NowPlayingToggle != null)
            {
                NowPlayingStatusText.Text = NowPlayingToggle.IsOn ? "On" : "Off";
            }
        }

        private void MinTrackLengthSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (MinTrackLengthSlider == null) return;
            int seconds = (int)e.NewValue;
            UpdateMinTrackLengthText(seconds);
            var mediaService = new WindowsMediaService();
            mediaService.SetMinimumTrackLengthSeconds(seconds);
        }

        private void UpdateMinTrackLengthText(int seconds)
        {
            if (MinTrackLengthText != null)
            {
                MinTrackLengthText.Text = TimeSpan.FromSeconds(seconds).ToString(@"mm\:ss");
            }
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
                    var itemGrid = new Grid();
                    itemGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    itemGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                    var leftStack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, VerticalAlignment = VerticalAlignment.Center };

                    var icon = new FluentIcons.WinUI.SymbolIcon
                    {
                        Symbol = FluentIcons.Common.Symbol.Speaker2,
                        FontSize = 16,
                        Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    leftStack.Children.Add(icon);

                    var nameText = new TextBlock
                    {
                        Text = app.DisplayName,
                        Style = (Style)Application.Current.Resources["BodyTextBlockStyle"],
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    leftStack.Children.Add(nameText);

                    Grid.SetColumn(leftStack, 0);
                    itemGrid.Children.Add(leftStack);

                    var check = new CheckBox
                    {
                        IsChecked = app.IsAllowed,
                        VerticalAlignment = VerticalAlignment.Center,
                        Tag = app.AppId
                    };
                    check.Checked += SourceApp_CheckChanged;
                    check.Unchecked += SourceApp_CheckChanged;

                    Grid.SetColumn(check, 1);
                    itemGrid.Children.Add(check);

                    SourceAppsStackPanel.Children.Add(itemGrid);
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

        private void ColorMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ColorModeComboBox == null || MainWindow.Current == null) return;

            bool isManual = ColorModeComboBox.SelectedIndex == 1;
            PaletteSection.Visibility = isManual ? Visibility.Visible : Visibility.Collapsed;
            MainWindow.Current.SetColorMode(isManual);

            if (!isManual)
            {
                var uiSettings = new Windows.UI.ViewManagement.UISettings();
                Color systemAccent = uiSettings.GetColorValue(Windows.UI.ViewManagement.UIColorType.Accent);
                MainWindow.Current.SetAccentColor(systemAccent);
            }
        }

        private void ColorPalette_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string hex)
            {
                try
                {
                    Color color = ParseColorFromHex(hex);
                    MainWindow.Current?.SetAccentColor(color);
                }
                catch
                {
                }
            }
        }

        private static Color ParseColorFromHex(string hex)
        {
            hex = hex.TrimStart('#');
            byte a = 255;
            byte r = 0, g = 0, b = 0;

            if (hex.Length == 8)
            {
                a = Convert.ToByte(hex.Substring(0, 2), 16);
                r = Convert.ToByte(hex.Substring(2, 2), 16);
                g = Convert.ToByte(hex.Substring(4, 2), 16);
                b = Convert.ToByte(hex.Substring(6, 2), 16);
            }
            else if (hex.Length == 6)
            {
                r = Convert.ToByte(hex.Substring(0, 2), 16);
                g = Convert.ToByte(hex.Substring(2, 2), 16);
                b = Convert.ToByte(hex.Substring(4, 2), 16);
            }

            return Color.FromArgb(a, r, g, b);
        }
    }
}
