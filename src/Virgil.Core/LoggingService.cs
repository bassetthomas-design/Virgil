using System;
using System.IO;

namespace Virgil.Core
{
    /// <summary>
    /// Provides simple file-based logging functionality for the Virgil application.
    /// Log files are written to the user's local application data folder under a
    /// 'Virgil' subdirectory with one log file per day.
    /// </summary>
    public static class LoggingService
    {
        private static readonly string LogDirectory =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Virgil");

        private static void Write(string level, string message)
        {
            try
            {
                Directory.CreateDirectory(LogDirectory);
                var logFile = Path.Combine(LogDirectory, $"virgil-log-{DateTime.Now:yyyyMMdd}.txt");
                var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {level}: {message}{Environment.NewLine}";
                File.AppendAllText(logFile, line);
            }
            catch
            {
                // Suppress any logging errors to avoid crashing the application.
            }
        }

        /// <summary>
        /// Writes an informational log entry.
        /// </summary>
        public static void LogInfo(string message) => Write("INFO", message);

        /// <summary>
        /// Writes an error log entry.
        /// </summary>
        public static void LogError(string message) => Write("ERROR", message);
    }
}