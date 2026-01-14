using System;
using System.IO;

namespace Virgil.Services.Assistant;

internal static class LocalAiFileLog
{
    private static readonly object LockObject = new();
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Virgil",
        "logs",
        "ai-local.log");

    public static string FilePath => LogPath;

    public static void Write(string message)
    {
        try
        {
            var timestamp = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff zzz");
            var line = $"[{timestamp}] {message}";
            var directory = Path.GetDirectoryName(LogPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            lock (LockObject)
            {
                File.AppendAllText(LogPath, $"{line}{Environment.NewLine}");
            }
        }
        catch (Exception)
        {
        }
    }
}
