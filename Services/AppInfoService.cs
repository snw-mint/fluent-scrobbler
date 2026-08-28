using System;
using System.IO;
using System.Reflection;
using Windows.ApplicationModel;

namespace FluentScrobbler.Services
{
    public static class AppInfoService
    {
#if DEBUG
        public const string AppDataFolderName = "FluentScrobblerDev";
#else
        public const string AppDataFolderName = "FluentScrobbler";
#endif

        public static bool IsPackaged
        {
            get
            {
                try
                {
                    return Package.Current != null && !string.IsNullOrEmpty(Package.Current.Id.FamilyName);
                }
                catch
                {
                    return false;
                }
            }
        }

        public static string AppDataPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            AppDataFolderName
        );

        public static string Version
        {
            get
            {
                try
                {
                    var v = Package.Current.Id.Version;
                    return $"{v.Major}.{v.Minor}.{v.Build}";
                }
                catch
                {
                    var info = Assembly.GetExecutingAssembly()
                        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

                    if (!string.IsNullOrEmpty(info))
                    {
                        return NormalizeVersion(info);
                    }

                    var ver = Assembly.GetExecutingAssembly().GetName().Version;
                    return ver != null 
                        ? $"{ver.Major}.{ver.Minor}.{ver.Build}"
                        : "0.2.0";
                }
            }
        }

        private static string NormalizeVersion(string ver)
        {
            if (string.IsNullOrWhiteSpace(ver)) return "0.2.0";
            string clean = ver.Trim().TrimStart('v', 'V');
            int plus = clean.IndexOf('+');
            if (plus > 0) clean = clean.Substring(0, plus);
            int dash = clean.IndexOf('-');
            if (dash > 0) clean = clean.Substring(0, dash);

            var parts = clean.Split('.');
            if (parts.Length >= 3)
            {
                return $"{parts[0]}.{parts[1]}.{parts[2]}";
            }
            if (parts.Length == 2)
            {
                return $"{parts[0]}.{parts[1]}.0";
            }
            if (parts.Length == 1 && !string.IsNullOrEmpty(parts[0]))
            {
                return $"{parts[0]}.0.0";
            }
            return clean;
        }

        public static string FormattedVersion => $"v{Version}";
    }
}