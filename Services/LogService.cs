using System;
using System.Diagnostics;
using System.IO;

namespace Fluent Scrobbler.Services
{
    public static class LogService
    {
        private static readonly string LogFolderPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Fluent Scrobbler",
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

        public static void LogInfo(string message) => Log("INFO", message);
        public static void LogWarning(string message) => Log("WARN", message);
        public static void LogError(string message, Exception? ex = null)
        {
            string fullMessage = ex != null ? $"{message}\nException: {ex}" : message;
            Log("ERROR", fullMessage);
        }

        public static void Log(string level, string message)
        {
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
                Directory.CreateDirectory(LogFolderPath);
                if (File.Exists(LogFilePath))
                {
                    Process.Start("explorer.exe", $"/select,\"{LogFilePath}\"");
                }
                else
                {
                    Process.Start("explorer.exe", LogFolderPath);
                }
            }
            catch
            {
            }
        }
    }
}
