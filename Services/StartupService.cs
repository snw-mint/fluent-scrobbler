using System;
using System.Threading.Tasks;
using Microsoft.Win32;
using Windows.ApplicationModel;

namespace FluentScrobbler.Services
{
    public static class StartupService
    {
        private const string RegistryRunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private static readonly string ValueName = AppInfoService.AppDataFolderName;
        private const string StartMinimizedSettingKey = "StartMinimizedToTray";
        private const string StartupTaskId = "FluentScrobblerStartup";

        public static bool IsStartupEnabled()
        {
            if (AppInfoService.IsPackaged)
            {
                try
                {
                    var op = StartupTask.GetAsync(StartupTaskId);
                    var task = op.AsTask().GetAwaiter().GetResult();
                    return task.State == StartupTaskState.Enabled || task.State == StartupTaskState.EnabledByPolicy;
                }
                catch
                {
                    return false;
                }
            }

            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegistryRunKeyPath, false);
                var val = key?.GetValue(ValueName) as string;
                return !string.IsNullOrEmpty(val);
            }
            catch
            {
                return false;
            }
        }

        public static async Task<bool> IsStartupEnabledAsync()
        {
            if (AppInfoService.IsPackaged)
            {
                try
                {
                    var task = await StartupTask.GetAsync(StartupTaskId);
                    return task.State == StartupTaskState.Enabled || task.State == StartupTaskState.EnabledByPolicy;
                }
                catch
                {
                    return false;
                }
            }

            return IsStartupEnabled();
        }

        public static void SetStartup(bool enable)
        {
            if (AppInfoService.IsPackaged)
            {
                try
                {
                    _ = SetStartupPackagedAsync(enable);
                }
                catch
                {
                }
                return;
            }

            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegistryRunKeyPath, true);
                if (key == null) return;

                if (enable)
                {
                    string? exePath = Environment.ProcessPath;
                    if (!string.IsNullOrEmpty(exePath))
                    {
                        bool min = IsStartMinimizedToTrayEnabled();
                        string args = min ? " --minimized" : "";
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

        public static async Task SetStartupAsync(bool enable)
        {
            if (AppInfoService.IsPackaged)
            {
                await SetStartupPackagedAsync(enable);
                return;
            }

            SetStartup(enable);
        }

        private static async Task SetStartupPackagedAsync(bool enable)
        {
            try
            {
                var task = await StartupTask.GetAsync(StartupTaskId);
                if (enable)
                {
                    if (task.State != StartupTaskState.Enabled)
                    {
                        await task.RequestEnableAsync();
                    }
                }
                else
                {
                    if (task.State == StartupTaskState.Enabled)
                    {
                        task.Disable();
                    }
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

            if (!AppInfoService.IsPackaged)
            {
                try
                {
                    using var key = Registry.CurrentUser.OpenSubKey(RegistryRunKeyPath, false);
                    var valReg = key?.GetValue(ValueName) as string;
                    if (!string.IsNullOrEmpty(valReg) && valReg.Contains("--minimized", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
                catch
                {
                }
            }

            return false;
        }

        public static void SetStartMinimizedToTrayEnabled(bool enable)
        {
            SettingsService.SetSetting(StartMinimizedSettingKey, enable ? "true" : "false");
            if (!AppInfoService.IsPackaged && IsStartupEnabled())
            {
                SetStartup(true);
            }
        }
    }
}
