using System;
using Windows.UI.ViewManagement;

namespace Fluent Scrobbler.Services
{
    public enum TrayTheme
    {
        Dark,
        Light
    }

    public class TrayThemeService
    {
        private readonly UISettings _uiSettings = new();

        public TrayTheme GetCurrentTheme()
        {
            var background = _uiSettings.GetColorValue(UIColorType.Background);

            return (background.R + background.G + background.B) < 384
                ? TrayTheme.Dark
                : TrayTheme.Light;
        }

        public void RegisterThemeChangeCallback(Action onThemeChanged)
        {
            _uiSettings.ColorValuesChanged += (sender, args) =>
            {
                onThemeChanged?.Invoke();
            };
        }
    }
}
