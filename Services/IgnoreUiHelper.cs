using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using FluentIcons.Common;
using FluentSymbolIcon = FluentIcons.WinUI.SymbolIcon;

namespace FluentScrobbler.Services
{
    public static class IgnoreUiHelper
    {
        public static readonly SolidColorBrush IgnoreRedBrush = new(Windows.UI.Color.FromArgb(255, 235, 47, 89));

        public static void SetIgnoreVisual(
            Button? button,
            FluentSymbolIcon? icon,
            Border? badgeBorder,
            TextBlock? badgeText,
            bool isIgnored,
            bool hasScrobbled)
        {
            if (button != null && icon != null)
            {
                icon.IconVariant = IconVariant.Regular;

                if (hasScrobbled)
                {
                    button.IsEnabled = false;
                    if (Application.Current.Resources.TryGetValue("TextFillColorDisabledBrush", out var disabledBrush) && disabledBrush is Brush db)
                    {
                        icon.Foreground = db;
                    }
                    ToolTipService.SetToolTip(button, "Already scrobbled");
                }
                else if (isIgnored)
                {
                    button.IsEnabled = true;
                    icon.Foreground = IgnoreRedBrush;
                    ToolTipService.SetToolTip(button, "Undo ignore");
                }
                else
                {
                    button.IsEnabled = true;
                    if (Application.Current.Resources.TryGetValue("TextFillColorSecondaryBrush", out var secBrush) && secBrush is Brush sb)
                    {
                        icon.Foreground = sb;
                    }
                    ToolTipService.SetToolTip(button, "Ignore this scrobble");
                }
            }

            if (badgeBorder != null && badgeText != null)
            {
                if (isIgnored)
                {
                    badgeText.Text = "IGNORED";
                    badgeBorder.Background = IgnoreRedBrush;
                }
                else if (hasScrobbled)
                {
                    badgeText.Text = "SENT";
                    if (Application.Current.Resources.TryGetValue("AccentFillColorDefaultBrush", out var accentBrush) && accentBrush is Brush b)
                    {
                        badgeBorder.Background = b;
                    }
                }
                else
                {
                    badgeText.Text = "LISTENING";
                    if (Application.Current.Resources.TryGetValue("AccentFillColorDefaultBrush", out var accentBrush) && accentBrush is Brush b)
                    {
                        badgeBorder.Background = b;
                    }
                }
            }
        }
    }
}
