using System;
using System.IO;
using System.Text;

namespace Virgil.App.Utils
{
    internal static class StartupLog
    {
        private static readonly object _gate = new();

        public static string LogsDirectory
        {
            get
            {
                var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Virgil", "logs");
                Directory.CreateDirectory(dir);
                return dir;
            }
        }

        public static void Write(string message, Exception? ex = null)
        {
            try
            {
                lock (_gate)
                {
                    var path = Path.Combine(LogsDirectory, $"startup_{DateTime.Now:yyyyMMdd_HHmmss_fff}.log");
                    var sb = new StringBuilder();
                    sb.AppendLine($"[{DateTime.Now:O}] {message}");
                    if (ex != null)
                    {
                        sb.AppendLine(ex.ToString());
                    }

                    File.WriteAllText(path, sb.ToString());
                }
            }
            catch
            {
                // never throw from logger
            }
        }
    }
}
