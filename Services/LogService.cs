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

        public static void LogInfo(string message)
        {
            Log("INFO", message);
        }

        public static void LogWarning(string message)
        {
            Log("WARN", message);
        }

        public static void LogError(string message, Exception? ex = null)
        {
            string msg = ex != null ? $"{message}\nException: {ex}" : message;
            Log("ERROR", msg);
        }

        public static void Log(string level, string message)
        {
            try
            {
                string entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {message}";
                Console.WriteLine(entry);
                Debug.WriteLine(entry);

                lock (LogLock)
                {
                    if (!Directory.Exists(LogFolderPath))
                    {
                        Directory.CreateDirectory(LogFolderPath);
                    }

                    var fi = new FileInfo(LogFilePath);
                    if (fi.Exists && fi.Length > 2 * 1024 * 1024)
                    {
                        File.Move(LogFilePath, LogFilePath + ".old", overwrite: true);
                    }

                    File.AppendAllText(LogFilePath, entry + Environment.NewLine);
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
                    Process.Start("explorer.exe", $"\"{dir}\"");
                }
            }
            catch
            {
            }
        }
    }
}
