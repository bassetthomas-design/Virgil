#nullable enable
using System;
using System.Windows;

namespace Virgil.App
{
    public partial class App : Application
    {
        private Tray.TrayIconService? _tray;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Crée la fenêtre principale
            var win = new MainWindow();
            win.Show();

            // Tray (menu: Ouvrir, Maintenance rapide, MAJ apps/jeux, Quitter)
            _tray = new Tray.TrayIconService(win);
            _tray.Initialize();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try { _tray?.Dispose(); } catch { /* ignore */ }
            base.OnExit(e);
        }
    }
}
