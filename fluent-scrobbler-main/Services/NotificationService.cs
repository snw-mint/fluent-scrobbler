using System;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;

namespace FluentScrobbler.Services
{
    public static class NotificationService
    {
        public static void ShowAuthSuccessNotification(string username)
        {
            try
            {
                var notification = new AppNotificationBuilder()
                    .AddText("Authentication Successful")
                    .AddText($"You are connected to Last.fm as {username}.")
                    .AddArgument("action", "open_home")
                    .BuildNotification();

                AppNotificationManager.Default.Show(notification);
            }
            catch (Exception ex)
            {
                LogService.LogError("[Notification] Failed to show auth success notification", ex);
            }
        }

        public static void ShowNewInstanceNotification(string instanceName)
        {
            try
            {
                var notification = new AppNotificationBuilder()
                    .AddText("New instance detected")
                    .AddText($"Scrobble from {instanceName}?")
                    .AddText("Settings > Source & Track Filtering > Source Filtering")
                    .BuildNotification();

                AppNotificationManager.Default.Show(notification);
            }
            catch (Exception ex)
            {
                LogService.LogError("[Notification] Failed to show new instance notification", ex);
            }
        }

        public static void ShowUpdateAvailableNotification(string version, string releaseUrl)
        {
            try
            {
                var notification = new AppNotificationBuilder()
                    .AddText("Update Available")
                    .AddText($"Fluent Scrobbler {version} is available for download.")
                    .AddArgument("action", "open_update_url")
                    .AddArgument("url", releaseUrl)
                    .BuildNotification();

                AppNotificationManager.Default.Show(notification);
            }
            catch (Exception ex)
            {
                LogService.LogError("[Notification] Failed to show update available notification", ex);
            }
        }
    }
}
