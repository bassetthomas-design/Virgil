#nullable enable
using System;
using System.IO;
using System.Windows; // WPF

namespace Virgil.App
{
    // ⚠️ Hérite explicitement de System.Windows.Application (évite le conflit avec WinForms)
    public partial class App : System.Windows.Application
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

                // WPF MessageBox (System.Windows)
                MessageBox.Show(
                    e.Exception.Message,
                    "Virgil — erreur au démarrage",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            };
        }
    }
}
