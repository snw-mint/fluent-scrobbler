using System;
using System.Diagnostics;
using System.IO;

namespace FluentScrobbler.Services
{
    public static class LogService
    {
        private static readonly string LogFolderPath = Path.Combine(
            AppInfoService.AppDataPath,
            "Logs"
        );

        private static readonly string LogFilePath = Path.Combine(LogFolderPath, "app.log");
        private static readonly object LogLock = new();

        static LogService()
        {
            try
            {
                Directory.CreateDirectory(LogFolderPath);
            }
            catch
            {
            }
        }

        public static string GetLogFilePath() => LogFilePath;
        public static string GetLogFolderPath() => LogFolderPath;

        public static void LogInfo(string message) { }
        public static void LogWarning(string message) { }
        public static void LogError(string message, Exception? ex = null)
        {
            string msg = ex != null ? $"{message}\nException: {ex}" : message;
            Log("ERROR", msg);
        }

        public static void Log(string level, string message)
        {
            if (level != "ERROR") return;

            try
            {
                lock (LogLock)
                {
                    Directory.CreateDirectory(LogFolderPath);
                    string entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {message}{Environment.NewLine}";
                    File.AppendAllText(LogFilePath, entry);
                }
            }
            catch
            {
            }
        }

        public static void OpenLogLocation()
        {
            try
            {
                string dir = AppInfoService.IsPackaged
                    ? Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "Packages",
                        "SnowMint.FluentScrobbler_cms0gyw6zz74e",
                        "LocalCache",
                        "Local",
                        AppInfoService.AppDataFolderName,
                        "Logs"
                    )
                    : LogFolderPath;

                Directory.CreateDirectory(dir);
                string file = Path.Combine(dir, "app.log");
                if (File.Exists(file))
                {
                    Process.Start("explorer.exe", $"/select,\"{file}\"");
                }
                else
                {
                    Process.Start("explorer.exe", dir);
                }
            }
            catch
            {
            }
        }
    }
}
