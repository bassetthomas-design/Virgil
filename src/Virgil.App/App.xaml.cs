using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace Virgil.App
{
    public partial class App : Application
    {
        protected override async void OnStartup(StartupEventArgs e)
        {
            // Gestion des crashs globaux
            AppDomain.CurrentDomain.UnhandledException += (s, ex) => WriteCrash("AppDomain", ex.ExceptionObject);
            this.DispatcherUnhandledException += (s, ex) =>
            {
                WriteCrash("Dispatcher", ex.Exception);
                ex.Handled = true;
                Environment.Exit(1);
            };

            // Mode CI (GitHub Actions)
            bool ciMode =
                Array.Exists(e.Args, a => string.Equals(a, "--ci", StringComparison.OrdinalIgnoreCase)) ||
                string.Equals(Environment.GetEnvironmentVariable("VIRGIL_CI"), "1", StringComparison.OrdinalIgnoreCase);

            if (ciMode)
            {
                int code = await CiSelfTest.RunAsync();
                Environment.Exit(code);
                return;
            }

            // Démarrage normal (UI)
            base.OnStartup(e);
            var w = new MainWindow();
            w.Show();
        }

        private static void WriteCrash(string source, object exObj)
        {
            try
            {
                var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                var logDir = Path.Combine(baseDir, "Virgil", "logs");
                Directory.CreateDirectory(logDir);
                File.AppendAllText(Path.Combine(logDir, "last-crash.txt"),
                    $"[{DateTime.UtcNow:O}] {source} :: {exObj}\r\n");
            }
            catch
            {
                // Ignorer les erreurs de log
            }
        }
    }
}
