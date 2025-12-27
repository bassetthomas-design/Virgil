using System;
using System.IO;

namespace Virgil.Core.Logging
{
    public static class Log
    {
        private static string LogDir {
            get {
                var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Virgil", "logs");
                Directory.CreateDirectory(dir);
                return dir;
            }
        }

        private static string TodayFile => Path.Combine(LogDir, $"{DateTime.Now:yyyy-MM-dd}.log");

        /// <summary>
        /// Gets the path of the log file currently used by the simple file logger.
        /// </summary>
        public static string CurrentLogFile => TodayFile;

        private static void Write(string level, string message)
        {
            var line = $"{DateTime.Now:HH:mm:ss} [{level}] {message}";
            try { File.AppendAllLines(TodayFile, new[]{line}); } catch { }
        }

        public static void Info(string message)  => Write("INFO",  message);
        public static void Warn(string message)  => Write("WARN",  message);
        public static void Error(string message) => Write("ERROR", message);
    }
}
