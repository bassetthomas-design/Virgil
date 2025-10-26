using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Virgil.Core.Logging;

public static class LogService
{
    private static string LogDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Virgil", "logs");
    private static string LogFile => Path.Combine(LogDir, $"Virgil_{DateTime.Now:yyyyMMdd}.log");

    public static async Task AppendAsync(string title, string content)
    {
        try
        {
            Directory.CreateDirectory(LogDir);
            var sb = new StringBuilder();
            sb.AppendLine($"=== {DateTime.Now:yyyy-MM-dd HH:mm:ss} :: {title} ===");
            sb.AppendLine(content);
            sb.AppendLine();
            await File.AppendAllTextAsync(LogFile, sb.ToString());
        }
        catch { /* no-throw logging */ }
    }
}
