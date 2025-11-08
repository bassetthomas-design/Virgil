using System;
using System.Drawing;
using System.Windows;
using WF = System.Windows.Forms;

namespace Virgil.App.Tray;

public class TrayIconService : IDisposable
{
    private WF.NotifyIcon? _notifyIcon;

    public void Initialize()
    {
        _notifyIcon = new WF.NotifyIcon
        {
            Visible = true,
            Text = "Virgil",
            Icon = SystemIcons.Application
        };
        _notifyIcon.ContextMenuStrip = new WF.ContextMenuStrip();
        _notifyIcon.ContextMenuStrip.Items.Add("Quitter", null, (_, __) => Application.Current.Shutdown());
    }

    public void Dispose()
    {
        if (_notifyIcon != null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _notifyIcon = null;
        }
    }
}
