using System;
using Microsoft.Win32;

namespace FluentScrobbler.Services
{
    public static class StartupService
    {
        private const string RegistryRunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "FluentScrobbler";
        private const string StartMinimizedSettingKey = "StartMinimizedToTray";

        public static bool IsStartupEnabled()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegistryRunKeyPath, false);
                var value = key?.GetValue(ValueName) as string;
                return !string.IsNullOrEmpty(value);
            }
            catch
            {
                return false;
            }
        }

        public static void SetStartup(bool enable)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegistryRunKeyPath, true);
                if (key == null) return;

                if (enable)
                {
                    string? exePath = Environment.ProcessPath;
                    if (!string.IsNullOrEmpty(exePath))
                    {
                        bool minimized = IsStartMinimizedToTrayEnabled();
                        string args = minimized ? " --minimized" : "";
                        key.SetValue(ValueName, $"\"{exePath}\"{args}");
                    }
                }
                else
                {
                    key.DeleteValue(ValueName, false);
                }
            }
            catch
            {
            }
        }

        public static bool IsStartMinimizedToTrayEnabled()
        {
            string? val = SettingsService.GetSetting(StartMinimizedSettingKey);
            if (val != null)
            {
                return val == "true";
            }

            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegistryRunKeyPath, false);
                var value = key?.GetValue(ValueName) as string;
                if (!string.IsNullOrEmpty(value) && value.Contains("--minimized", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            catch
            {
            }

            return false;
        }

        public static void SetStartMinimizedToTrayEnabled(bool enable)
        {
            SettingsService.SetSetting(StartMinimizedSettingKey, enable ? "true" : "false");
            if (IsStartupEnabled())
            {
                SetStartup(true);
            }
        }
    }
}
