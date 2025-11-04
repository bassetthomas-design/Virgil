using System;
using System.IO;
using System.Linq;
using System.Windows;

namespace Virgil.App
{
    public partial class App : Application
    {
        private static string LogDir =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Virgil", "logs");

        private static string StartupLogPath => Path.Combine(LogDir, $"{DateTime.Now:yyyy-MM-dd}_startup.log");

        protected override void OnStartup(StartupEventArgs e)
        {
            // Handlers globaux pour capter toute exception non gérée
            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
                SafeLog("[AppDomain] " + (args.ExceptionObject as Exception)?.ToString());
            DispatcherUnhandledException += (s, args) =>
            {
                SafeLog("[Dispatcher] " + args.Exception.ToString());
                args.Handled = true;
                ShowFatal(args.Exception);
                Shutdown(-1);
            };

            try
            {
                Directory.CreateDirectory(LogDir);
                SafeLog("=== Virgil starting ===");

                // 🔒 NE PAS faire Any sur une source potentiellement nulle
                var hasSpecialArg = (e?.Args != null) && e.Args.Any(a => a.Equals("--headless", StringComparison.OrdinalIgnoreCase));
                SafeLog($"Args: {(e?.Args == null ? "(null)" : string.Join(" ", e.Args))}");
                SafeLog($"Headless: {hasSpecialArg}");

                if (hasSpecialArg)
                {
                    // Mode service/test si tu en as besoin
                    SafeLog("Headless mode: no MainWindow.");
                    Shutdown(0);
                    return;
                }

                // ⚙️ Initialisations légères avant UI (charger config si dispo)
                TryLoadEarlyConfig();

                // 🪟 Création robuste de la fenêtre principale
                // NOTE: adapte le namespace si ta vue est ailleurs (ex: Virgil.App.Views.MainWindow)
                var win = new Views.MainWindow();
                MainWindow = win;

                // Si DataContext nécessaire manuellement, fais-le ici
                // win.DataContext = new ViewModels.DashboardViewModel();

                win.Show();
                SafeLog("MainWindow shown. ✅");
            }
            catch (Exception ex)
            {
                SafeLog("[Startup Exception] " + ex.ToString());
                ShowFatal(ex);
                Shutdown(-1);
            }
        }

        private static void TryLoadEarlyConfig()
        {
            try
            {
                var cfgPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Virgil", "config.json");

                if (File.Exists(cfgPath))
                {
                    var json = File.ReadAllText(cfgPath); // tu pourras le parser plus tard
                    SafeLog($"Loaded config: {cfgPath} ({json.Length} chars)");
                }
                else
                {
                    SafeLog("No config.json yet (will use defaults).");
                }
            }
            catch (Exception ex)
            {
                SafeLog("[TryLoadEarlyConfig] " + ex.Message);
            }
        }

        private static void SafeLog(string line)
        {
            try
            {
                Directory.CreateDirectory(LogDir);
                File.AppendAllText(StartupLogPath, $"[{DateTime.Now:HH:mm:ss}] {line}{Environment.NewLine}");
            }
            catch
            {
                // pas d’exception secondaire au démarrage
            }
        }

        private static void ShowFatal(Exception ex)
        {
            // Message concis pour l’utilisateur, détail en log
            MessageBox.Show(
                "Virgil n’a pas pu afficher la fenêtre principale.\n\n" +
                $"Détail: {ex.Message}\n\n" +
                $"Un journal a été écrit ici :\n{StartupLogPath}",
                "Virgil",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }
}
