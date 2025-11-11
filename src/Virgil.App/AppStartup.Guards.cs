using System;
using System.IO;
using System.Text;
using System.Windows;

namespace Virgil.App
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            AppDomain.CurrentDomain.UnhandledException += (s, ex) => LogAndShow(ex.ExceptionObject as Exception);
            DispatcherUnhandledException += (s, ex) => { LogAndShow(ex.Exception); ex.Handled = true; };
            try
            {
                base.OnStartup(e);
            }
            catch (Exception ex)
            {
                LogAndShow(ex);
                Shutdown(-1);
            }
        }

        private static void LogAndShow(Exception? ex)
        {
            try
            {
                var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Virgil", "logs");
                Directory.CreateDirectory(dir);
                var path = Path.Combine(dir, $"crash_{DateTime.Now:yyyyMMdd_HHmmss}.log");
                File.WriteAllText(path, BuildReport(ex));
                MessageBox.Show("Virgil a rencontré une erreur au démarrage. Un rapport a été enregistré.\n\n" + path, "Virgil", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch { /* swallow logging errors */ }
        }

        private static string BuildReport(Exception? ex)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Time: {DateTime.Now}");
            if (ex != null)
            {
                sb.AppendLine(ex.GetType().FullName);
                sb.AppendLine(ex.Message);
                sb.AppendLine(ex.StackTrace);
                if (ex.InnerException != null)
                {
                    sb.AppendLine("-- Inner --");
                    sb.AppendLine(ex.InnerException.GetType().FullName);
                    sb.AppendLine(ex.InnerException.Message);
                    sb.AppendLine(ex.InnerException.StackTrace);
                }
            }
            return sb.ToString();
        }
    }
}
