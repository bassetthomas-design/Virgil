#nullable enable
using System;

namespace Virgil.App
{
    // ⚠️ Qualifie explicitement l’Application WPF pour éviter le conflit avec WinForms.Application
    public partial class App : System.Windows.Application
    {
        private Tray.TrayIconService? _tray;

        protected override void OnStartup(System.Windows.StartupEventArgs e)
        {
            base.OnStartup(e);

            var win = new MainWindow();
            win.Show();

            _tray = new Tray.TrayIconService(win);
            _tray.Initialize();
        }

        protected override void OnExit(System.Windows.ExitEventArgs e)
        {
            try { _tray?.Dispose(); } catch { /* ignore */ }
            base.OnExit(e);
        }
    }
}
