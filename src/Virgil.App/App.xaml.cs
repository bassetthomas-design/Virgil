#nullable enable
using System;
using System.IO;
using System.Windows;

namespace Virgil.App
{
    // ⚠️ Hérite bien de System.Windows.Application (et pas de WinForms)
    public partial class App : Application
    {
        public App()
        {
            this.DispatcherUnhandledException += (s, e) =>
            {
                try
                {
                    var folder = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                        "Virgil_Logs");
                    Directory.CreateDirectory(folder);
                    File.WriteAllText(Path.Combine(folder, "startup_crash.txt"), e.Exception.ToString());
                }
                catch { /* best effort */ }

                MessageBox.Show(
                    e.Exception.Message,
                    "Virgil — erreur au démarrage",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            };
        }
    }
}
