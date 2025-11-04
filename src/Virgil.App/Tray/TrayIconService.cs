#nullable enable
using System;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms; // NotifyIcon
using System.Drawing;                    // pour Icon / SystemIcons
using System.Windows;                    // WPF (Window, Application, etc.)
using WF = System.Windows.Forms;         // alias WinForms pour NotifyIcon/ContextMenuStrip

namespace Virgil.App.Tray
{
    public sealed class TrayIconService : IDisposable
    {
        private readonly WeakReference<MainWindow> _window;
        private NotifyIcon? _notify;

        public TrayIconService(MainWindow window)
        {
            _window = new WeakReference<MainWindow>(window);
        }

        public void Initialize()
        {
            _notify = new NotifyIcon
            {
                Visible = true,
                Text = "Virgil",
                Icon = SystemIcons.Application
            };

            var menu = new ContextMenuStrip();
            menu.Items.Add("Ouvrir Virgil", null, (_, __) => ShowMainWindow());
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Maintenance rapide", null, async (_, __) => await CallHandlerAsync("QuickMaintenanceButton_Click"));
            menu.Items.Add("MAJ apps/jeux (winget)", null, async (_, __) => await CallHandlerAsync("UpdateButton_Click"));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Quitter", null, (_, __) => System.Windows.Application.Current?.Shutdown());

            _notify.ContextMenuStrip = menu;
            _notify.DoubleClick += (_, __) => ShowMainWindow();
        }

        private void ShowMainWindow()
        {
            if (!_window.TryGetTarget(out var win) || win is null) return;

            win.Dispatcher.Invoke(() =>
            {
                if (win.WindowState == System.Windows.WindowState.Minimized)
                    win.WindowState = System.Windows.WindowState.Normal;

                if (!win.IsVisible)
                    win.Show();

                win.Activate();
                win.Topmost = true;
                win.Topmost = false;
                win.Focus();
            });
        }

        /// <summary>
        /// Appelle un handler (ex: "QuickMaintenanceButton_Click") de MainWindow par réflexion.
        /// </summary>
        private async Task CallHandlerAsync(string handlerName)
        {
            if (!_window.TryGetTarget(out var win) || win is null) return;

            await win.Dispatcher.InvokeAsync(() =>
            {
                try
                {
                    var mi = typeof(MainWindow).GetMethod(handlerName, BindingFlags.Instance | BindingFlags.NonPublic);
                    if (mi != null)
                    {
                        mi.Invoke(win, new object?[] { null, new System.Windows.RoutedEventArgs() });
                    }
                    else
                    {
                        ShowMainWindow();
                    }
                }
                catch
                {
                    ShowMainWindow();
                }
            });
        }

        public void Dispose()
        {
            if (_notify != null)
            {
                try
                {
                    _notify.Visible = false;
                    _notify.Dispose();
                }
                catch { /* ignore */ }
                _notify = null;
            }
        }
    }
}
