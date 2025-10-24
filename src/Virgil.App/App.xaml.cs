#nullable enable
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace Virgil.App
{
    public partial class App : System.Windows.Application
    {
        private const string LogFileName = "Virgil_crash.log";

        public App()
        {
            // Hook global handlers BEFORE any UI
            this.DispatcherUnhandledException += App_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
        }

        private void App_DispatcherUnhandledException(object? sender, DispatcherUnhandledExceptionEventArgs e)
        {
            LogAndShow("DispatcherUnhandledException", e.Exception);
            e.Handled = true; // prevent silent shutdown
        }

        private void CurrentDomain_UnhandledException(object? sender, UnhandledExceptionEventArgs e)
        {
            var ex = e.ExceptionObject as Exception ?? new Exception("Unknown unhandled exception object");
            LogAndShow("CurrentDomain.UnhandledException", ex);
        }

        private void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            LogAndShow("TaskScheduler.UnobservedTaskException", e.Exception);
            e.SetObserved();
        }

        private static void LogAndShow(string source, Exception ex)
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {source}");
                sb.AppendLine(ex.ToString());
                sb.AppendLine(new string('-', 70));

                var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, LogFileName);
                File.AppendAllText(path, sb.ToString(), Encoding.UTF8);

                System.Windows.MessageBox.Show(
                    $"{source}\n\n{ex.Message}\n\nUn journal a été écrit ici :\n{path}",
                    "Virgil – erreur au démarrage",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
            catch
            {
                // last-resort: swallow
            }
        }
    }
}
