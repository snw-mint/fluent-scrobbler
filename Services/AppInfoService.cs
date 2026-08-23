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
                    var infoVersion = Assembly.GetExecutingAssembly()
                        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

                    if (!string.IsNullOrEmpty(infoVersion))
                    {
                        int plusIndex = infoVersion.IndexOf('+');
                        return plusIndex > 0 ? infoVersion.Substring(0, plusIndex) : infoVersion;
                    }

                    var assemblyVersion = Assembly.GetExecutingAssembly().GetName().Version;
                    return assemblyVersion != null 
                        ? $"{assemblyVersion.Major}.{assemblyVersion.Minor}.{assemblyVersion.Build}"
                        : "0.2.0";
                }
            }
        }

        public static string FormattedVersion => $"v{Version}";
    }
}