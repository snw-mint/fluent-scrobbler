using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Fluent Scrobbler.Services
{
    public static class SettingsService
    {
        private static readonly string SettingsFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Fluent Scrobbler",
            "settings.json"
        );

        private static readonly object LockObj = new();

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
    }
}
