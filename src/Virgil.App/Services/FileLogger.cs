using System;
using System.IO;

namespace Virgil.App.Services;

public class FileLogger : IFileLogger
{
    private readonly string _logDir;
    private readonly object _sync = new();

    public FileLogger()
    {
        _logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Virgil", "logs");
        Directory.CreateDirectory(_logDir);
    }

    private string LogPath => Path.Combine(_logDir, DateTime.Now.ToString("yyyy-MM-dd") + ".log");

    public void Info(string message) => Write("INFO", message);
    public void Error(string message) => Write("ERROR", message);

    private void Write(string level, string message)
    {
        var line = $"{DateTime.Now:HH:mm:ss} [{level}] {message}";
        lock (_sync) File.AppendAllText(LogPath, line + Environment.NewLine);
    }
}
