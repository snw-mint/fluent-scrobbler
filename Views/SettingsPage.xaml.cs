using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.UI;

namespace FluentScrobbler.Views
{
    public sealed partial class SettingsPage : Page
    {
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
            BackdropToggle.Toggled -= Backdrop_Toggled;

            if (MainWindow.Current != null)
            {
                ThemeModeComboBox.SelectedIndex = MainWindow.Current.CurrentTheme switch
                {
                    ElementTheme.Light => 0,
                    ElementTheme.Dark => 1,
                    ElementTheme.Default => 2,
                    _ => 0
                };

                BackdropToggle.IsOn = MainWindow.Current.IsAcrylic;
                ColorModeComboBox.SelectedIndex = MainWindow.Current.IsManualColor ? 1 : 0;
                PaletteSection.Visibility = MainWindow.Current.IsManualColor ? Visibility.Visible : Visibility.Collapsed;
            }

            ThemeModeComboBox.SelectionChanged += ThemeMode_SelectionChanged;
            ColorModeComboBox.SelectionChanged += ColorMode_SelectionChanged;
            BackdropToggle.Toggled += Backdrop_Toggled;
        }

        private void SettingsPage_Unloaded(object sender, RoutedEventArgs e)
        {
            ThemeModeComboBox.SelectionChanged -= ThemeMode_SelectionChanged;
            ColorModeComboBox.SelectionChanged -= ColorMode_SelectionChanged;
            BackdropToggle.Toggled -= Backdrop_Toggled;
        }

        private void ThemeMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ThemeModeComboBox == null || MainWindow.Current == null) return;

            ElementTheme theme = ThemeModeComboBox.SelectedIndex switch
            {
                0 => ElementTheme.Light,
                1 => ElementTheme.Dark,
                2 => ElementTheme.Default,
                _ => ElementTheme.Light
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
                Color defaultBlue = Color.FromArgb(255, 0, 120, 212);
                MainWindow.Current.SetAccentColor(defaultBlue);
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

        private void Backdrop_Toggled(object sender, RoutedEventArgs e)
        {
            if (BackdropToggle == null || MainWindow.Current == null) return;
            MainWindow.Current.SetSystemBackdrop(BackdropToggle.IsOn);
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
