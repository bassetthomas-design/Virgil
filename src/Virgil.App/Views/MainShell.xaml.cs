using System.Windows;

namespace Virgil.App.Views
{
    public partial class MainShell : Window
    {
        public MainShell(){ InitializeComponent(); Loaded += (_,__)=> AddToolbarExtras(); }

        private void OnOpenSettings(object sender, RoutedEventArgs e)
        {
            try{ new SettingsWindow{ Owner = this }.ShowDialog(); }
            catch{ /* if SettingsWindow missing, ignore for now */ }
        }

        private void OnHudToggled(object sender, RoutedEventArgs e)
        {
            // TODO: toggle mini HUD visibility via AppSettings once wired
        }
    }
}
