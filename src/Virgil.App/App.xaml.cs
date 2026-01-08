using System;
using System.IO;
using System.Text;
using System.Windows;
using Virgil.Core;
using Virgil.App.Views;

namespace Virgil.App
{
    public partial class App : Application
    {
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            AppDomain.CurrentDomain.UnhandledException += (s, ex) => LogAndShow(ex.ExceptionObject as Exception);
            DispatcherUnhandledException += (s, ex) => { LogAndShow(ex.Exception); ex.Handled = true; };
            if (!ProcessElevation.IsProcessElevated())
            {
                StartupLog.Write("Application launched without administrative privileges. Exiting.");
                MessageBox.Show("Virgil requiert des droits administrateur pour démarrer.", "Virgil", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown(-1);
                return;
            }
            try{ var shell = new MainShell(); MainWindow = shell; shell.Show(); }
            catch(Exception ex){ LogAndShow(ex); Shutdown(-1);}
        }

        private static void LogAndShow(Exception? ex){
            try{
                var path = Path.Combine(StartupLog.LogsDirectory, $"crash_{DateTime.Now:yyyyMMdd_HHmmss}.log");
                File.WriteAllText(path, BuildReport(ex));
                System.Windows.MessageBox.Show("Virgil a rencontré une erreur.\n\n"+path, "Virgil", MessageBoxButton.OK, MessageBoxImage.Error);
            }catch{}
        }
        private static string BuildReport(Exception? ex){
            var sb = new StringBuilder(); sb.AppendLine($"Time: {DateTime.Now}");
            if(ex!=null){ sb.AppendLine(ex.GetType().FullName); sb.AppendLine(ex.Message); sb.AppendLine(ex.StackTrace);
                if(ex.InnerException!=null){ sb.AppendLine("-- Inner --"); sb.AppendLine(ex.InnerException.GetType().FullName); sb.AppendLine(ex.InnerException.Message); sb.AppendLine(ex.InnerException.StackTrace);} }
            return sb.ToString();
        }

    }
}
