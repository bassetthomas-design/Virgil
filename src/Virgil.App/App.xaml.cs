#nullable enable
using System;
using System.IO;
using System.Windows;                 // WPF only

namespace Virgil.App
{
    public partial class App : Application
    {
        public App()
        {
            this.DispatcherUnhandledException += (_, e) =>
            {
                try
                {
                    var dir = AppDomain.CurrentDomain.BaseDirectory;
                    var file = Path.Combine(dir, "Virgil_crash.log");
                    File.AppendAllText(file, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {e.Exception}\r\n");
                }
                catch { /* ignore */ }

                System.Windows.MessageBox.Show(
                    "La valeur fournie sur 'TypeConverterMarkupExtension' a levé une exception.\n" +
                    "Un journal a été écrit à côté de l'exécutable (Virgil_crash.log).",
                    "Virgil — erreur au démarrage",
                    MessageBoxButton.OK, MessageBoxImage.Error);

                e.Handled = true;
                Current.Shutdown();
            };
        }
    }
}
