using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace Virgil.App
{
    public partial class App : Application
    {
        private static string LogDir =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Virgil", "logs");
        private static string LastRunLog => Path.Combine(LogDir, "last-run.txt");
        private static string LastCrashLog => Path.Combine(LogDir, "last-crash.txt");

        protected override async void OnStartup(StartupEventArgs e)
        {
            Directory.CreateDirectory(LogDir);
            File.WriteAllText(LastRunLog, $"[{DateTime.UtcNow:O}] App starting… args=({string.Join(" ", e.Args ?? Array.Empty<string>())}){Environment.NewLine}");

            AppDomain.CurrentDomain.UnhandledException += (s, ex) =>
                SafeAppend(LastCrashLog, $"[{DateTime.UtcNow:O}] AppDomain :: {ex.ExceptionObject}{Environment.NewLine}");

            this.DispatcherUnhandledException += (s, ex) =>
            {
                SafeAppend(LastCrashLog, $"[{DateTime.UtcNow:O}] Dispatcher :: {ex.Exception}{Environment.NewLine}");
                ex.Handled = true;
                // Affiche un message pour le debug local plutôt que de fermer silencieusement.
                System.Windows.MessageBox.Show(
                    "Virgil a rencontré une erreur au démarrage.\n\n" + ex.Exception.Message,
                    "Virgil", MessageBoxButton.OK, MessageBoxImage.Error);
                Current.Shutdown(1);
            };

            // Mode CI headless
            bool ciMode =
                e.Args.Any(a => string.Equals(a, "--ci", StringComparison.OrdinalIgnoreCase)) ||
                string.Equals(Environment.GetEnvironmentVariable("VIRGIL_CI"), "1", StringComparison.OrdinalIgnoreCase);
            if (ciMode)
            {
                var code = await CiSelfTest.RunAsync();
                SafeAppend(LastRunLog, $"[{DateTime.UtcNow:O}] CI exit code={code}{Environment.NewLine}");
                Environment.Exit(code);
                return;
            }

            // Démarrage normal UI
            base.OnStartup(e);
            try
            {
                var w = new MainWindow();
                w.Loaded += (_, __) => SafeAppend(LastRunLog, $"[{DateTime.UtcNow:O}] MainWindow Loaded{Environment.NewLine}");
                w.ContentRendered += (_, __) => SafeAppend(LastRunLog, $"[{DateTime.UtcNow:O}] MainWindow Rendered{Environment.NewLine}");
                w.Show();
                SafeAppend(LastRunLog, $"[{DateTime.UtcNow:O}] MainWindow Show() called{Environment.NewLine}");
            }
            catch (Exception ex)
            {
                SafeAppend(LastCrashLog, $"[{DateTime.UtcNow:O}] Startup UI :: {ex}{Environment.NewLine}");
                System.Windows.MessageBox.Show(
                    "Virgil n’a pas pu afficher la fenêtre principale.\n\n" + ex.Message,
                    "Virgil", MessageBoxButton.OK, MessageBoxImage.Error);
                Current.Shutdown(1);
            }
        }

        private static void SafeAppend(string path, string text)
        {
            try { File.AppendAllText(path, text); } catch { /* ignore */ }
        }
    }
}
