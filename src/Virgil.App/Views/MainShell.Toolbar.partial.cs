using System.Windows;
using System.Windows.Controls;

namespace Virgil.App.Views
{
    public partial class MainShell
    {
        private void AddToolbarExtras(){
            if(ToolBarTray == null) return;
            var tb = new ToolBar();
            var btnLogs = new Button{ Content = "Logs", Margin = new Thickness(4,0,0,0)};
            btnLogs.Click += (s,e)=> { new LogsWindow{ Owner = this }.Show(); };
            var btnThanos = new Button{ Content = "Snap (Purge Chat)", Margin = new Thickness(4,0,0,0)};
            btnThanos.Click += (s,e)=> { ViewModel?.Chat?.SnapAll(); };
            tb.Items.Add(btnLogs);
            tb.Items.Add(btnThanos);
            ToolBarTray.ToolBars.Add(tb);
        }
    }
}
