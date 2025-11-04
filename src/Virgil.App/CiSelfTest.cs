using System;
using System.IO;
using System.Threading.Tasks;

namespace Virgil.App
{
    internal static class CiSelfTest
    {
        public static async Task<int> RunAsync()
        {
            try
            {
                var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                var logDir  = Path.Combine(baseDir, "Virgil", "logs");
                Directory.CreateDirectory(logDir);
                var logPath = Path.Combine(logDir, $"ci-{DateTime.UtcNow:yyyyMMdd-HHmmss}.log");

                await File.AppendAllTextAsync(logPath, $"[CI] Start {DateTime.UtcNow:O}\r\n");

                // Mini preuves de vie (ajoute ici ce que tu veux tester sans UI)
                await File.AppendAllTextAsync(logPath, "[CI] Sanity checks...\r\n");

                // ex: vérifier présence de DLL clés
                var assemblyPath = typeof(App).Assembly.Location;
                await File.AppendAllTextAsync(logPath, $"[CI] Assembly: {assemblyPath}\r\n");

                // petit délai pour simuler un run
                await Task.Delay(1500);
                await File.AppendAllTextAsync(logPath, "[CI] OK\r\n");

                return 0;
            }
            catch (Exception ex)
            {
                try
                {
                    var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                    var logDir  = Path.Combine(baseDir, "Virgil", "logs");
                    Directory.CreateDirectory(logDir);
                    await File.AppendAllTextAsync(Path.Combine(logDir, "ci-last-error.log"), ex.ToString());
                }
                catch { /* ignore */ }
                return 1;
            }
        }
    }
}
