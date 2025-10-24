#nullable enable
using System;
using System.IO;
using System.Windows;

namespace Virgil.App
{
    public partial class App : Application
    {
        public App()
        {
            DispatcherUnhandledException += (_, e) =>
            {
                try
                {
                    var dir = AppDomain.CurrentDomain.BaseDirectory;
                    var file = Path.Combine(dir, "Virgil_crash.log");
                    File.AppendAllText(file, $"[{DateTime.Now}] {e.Exception}\r\n");
                }
                catch { }

                System.Windows.MessageBox.Show(
                    "Une erreur interne est survenue lors du chargement de l’interface.\n" +
                    "Un journal a été enregistré dans Virgil_crash.log.",
                    "Virgil — erreur au démarrage",
                    MessageBoxButton.OK, MessageBoxImage.Error);

                e.Handled = true;
                Current.Shutdown();
            };
        }
    }
}
