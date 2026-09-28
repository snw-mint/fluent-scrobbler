using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace FluentScrobbler.Services
{
    public static class SettingsService
    {
        private static readonly string SettingsFilePath = Path.Combine(
            AppInfoService.AppDataPath,
            "settings.json"
        );

        public const string LegacyPlayersEnabledKey = "EnableLegacyPlayerMonitoring";

        public static bool IsLegacyPlayersEnabled()
        {
            return GetSetting(LegacyPlayersEnabledKey) == "true";
        }

        public static void SetLegacyPlayersEnabled(bool enabled)
        {
            SetSetting(LegacyPlayersEnabledKey, enabled ? "true" : "false");
            LegacyPlayerWatcher.Instance.SetMonitoringState(enabled);
        }

        public static string? GetSetting(string key)
        {
            try
            {
                var localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
                if (localSettings.Values.TryGetValue(key, out var val) && val != null)
                {
                    return val.ToString();
                }
            }
            catch
            {
            }

            var fileSettings = LoadSettingsFromFile();
            return fileSettings.TryGetValue(key, out var fileVal) ? fileVal : null;
        }

        public static void SetSetting(string key, string value)
        {
            try
            {
                var localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
                localSettings.Values[key] = value;
            }
            catch
            {
            }

            var fileSettings = LoadSettingsFromFile();
            fileSettings[key] = value;
            SaveSettingsToFile(fileSettings);
        }

        private static readonly object LockObj = new();

        private static Dictionary<string, string> LoadSettingsFromFile()
        {
            lock (LockObj)
            {
                try
                {
                    if (File.Exists(SettingsFilePath))
                    {
                        string json = File.ReadAllText(SettingsFilePath);
                        return JsonSerializer.Deserialize(json, AppJsonContext.Default.DictionaryStringString)
                               ?? new Dictionary<string, string>();
                    }
                }
                catch
                {
                }
                return new Dictionary<string, string>();
            }
        }

        private static void SaveSettingsToFile(Dictionary<string, string> settings)
        {
            lock (LockObj)
            {
                try
                {
                    string? dir = Path.GetDirectoryName(SettingsFilePath);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }
                    string json = JsonSerializer.Serialize(settings, AppJsonContext.Default.DictionaryStringString);
                    File.WriteAllText(SettingsFilePath, json);
                }
                catch
                {
                }
            }
        }

        public static string GetSettingsFilePath() => SettingsFilePath;

        public static async Task ExportSettingsAsync(string destinationFilePath)
        {
            var settings = LoadSettingsFromFile();
            try
            {
                var localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
                foreach (var pair in localSettings.Values)
                {
                    if (pair.Value != null)
                    {
                        settings[pair.Key] = pair.Value.ToString() ?? string.Empty;
                    }
                }
            }
            catch
            {
            }

            string json = JsonSerializer.Serialize(settings, AppJsonContext.Default.DictionaryStringString);
            await File.WriteAllTextAsync(destinationFilePath, json);
        }

        public static async Task<bool> ImportSettingsAsync(string sourceFilePath)
        {
            if (!File.Exists(sourceFilePath)) return false;

            string json = await File.ReadAllTextAsync(sourceFilePath);
            var imported = JsonSerializer.Deserialize(json, AppJsonContext.Default.DictionaryStringString);
            if (imported == null || imported.Count == 0) return false;

            SaveSettingsToFile(imported);

            try
            {
                var localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
                foreach (var pair in imported)
                {
                    localSettings.Values[pair.Key] = pair.Value;
                }
            }
            catch
            {
            }

            return true;
        }
    }
}
