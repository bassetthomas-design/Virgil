using System;
using System.IO;

namespace Virgil.App.Services
{
    public class LoggerService
    {
        private readonly string _dir;
        public LoggerService(string? folder = null)
        {
            _dir = folder ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Virgil", "logs");
            Directory.CreateDirectory(_dir);
        }

        public string Save(string action, ProcessResult res)
        {
            var name = $"{DateTime.Now:yyyyMMdd_HHmmss}_{Sanitize(action)}.log";
            var path = Path.Combine(_dir, name);
            File.WriteAllText(path, 
$"Action: {action}\nTime: {DateTime.Now:O}\nExitCode: {res.ExitCode}\n\n=== STDOUT ===\n{res.Stdout}\n\n=== STDERR ===\n{res.Stderr}\n");
            return path;
        }

        private static string Sanitize(string s)
            => string.Join("_", s.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries)).Replace(' ', '_');
    }
}
