using System;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Threading;

namespace Virgil.App
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            // Hooks de diagnostic très tôt
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            this.DispatcherUnhandledException += App_DispatcherUnhandledException;
            base.OnStartup(e);
            // StartupUri=MainWindow.xaml (dans App.xaml) ouvre la fenêtre
        }

        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            try
            {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Virgil_crash.log");
                File.AppendAllText(path, BuildCrashText(e.Exception));
                // 👉 Qualifier explicitement la MessageBox WPF
                System.Windows.MessageBox.Show(
                    "Une erreur est survenue au démarrage. Un journal a été écrit dans Virgil_crash.log",
                    "Virgil",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
            catch { }
            finally
            {
                e.Handled = true;
                Shutdown(-1);
            }
        }

        private void CurrentDomain_UnhandledException(object? sender, UnhandledExceptionEventArgs e)
        {
            try
            {
                Exception ex = e.ExceptionObject as Exception ?? new Exception("Unknown unhandled exception");
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Virgil_crash.log");
                File.AppendAllText(path, BuildCrashText(ex));
            }
            catch { }
        }

        private static string BuildCrashText(Exception ex)
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== VIRGIL CRASH === " + DateTime.Now.ToString("u"));
            sb.AppendLine(ex.ToString());
            sb.AppendLine();
            return sb.ToString();
        }
    }
}
