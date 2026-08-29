using System.Collections.Generic;

namespace FluentScrobbler.Services
{
    public static class FeatureBadgeService
    {
        private static readonly HashSet<string> SettingsFeatures = new()
        {
            "CleanTrackTitles"
        };

        private static string GetKey(string id) => $"SeenFeature_{id}_{AppInfoService.Version}";

        public static bool IsFeatureNew(string id)
        {
            return SettingsService.GetSetting(GetKey(id)) != "true";
        }

        public static void MarkFeatureAsSeen(string id)
        {
            SettingsService.SetSetting(GetKey(id), "true");
        }

        public static bool HasUnseenSettingsFeatures()
        {
            foreach (var id in SettingsFeatures)
            {
                if (IsFeatureNew(id)) return true;
            }
            return false;
        }

        public static void MarkAllSettingsFeaturesAsSeen()
        {
            foreach (var id in SettingsFeatures)
            {
                MarkFeatureAsSeen(id);
            }
        }
    }
}
