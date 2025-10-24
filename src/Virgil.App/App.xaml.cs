#nullable enable
using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace Virgil.App
{
    // IMPORTANT : hérite explicitement de System.Windows.Application (WPF)
    public partial class App : System.Windows.Application
    {
        public App()
        {
            // Journaliser toute exception WPF non-capturée pour éviter un crash silencieux
            this.DispatcherUnhandledException += App_DispatcherUnhandledException;
        }

        private void App_DispatcherUnhandledException(object? sender, DispatcherUnhandledExceptionEventArgs e)
        {
            try
            {
                var baseDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "Downloads", "virgil-app-win-x64");

                Directory.CreateDirectory(baseDir);

                var logPath = Path.Combine(baseDir, "Virgil_crash.log");
                File.AppendAllText(logPath,
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {e.Exception}\r\n");
            }
            catch
            {
                // best-effort; ne bloque jamais la fermeture
            }

            // Toujours utiliser le MessageBox WPF pour éviter l'ambiguïté WinForms
            System.Windows.MessageBox.Show(
                "La valeur fournie sur 'System.Windows.Baml2006.TypeConverterMarkupExtension' a levé une exception.",
                "Virgil — erreur au démarrage",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            e.Handled = true;
            Shutdown(-1);
        }
    }
}
