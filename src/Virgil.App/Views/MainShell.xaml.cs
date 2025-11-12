using System.Windows;

namespace Virgil.App.Views
{
    public partial class MainShell : Window
    {
        public MainShell(){ InitializeComponent(); Loaded += (_,__)=> AddToolbarExtras(); }

        private void OnOpenSettings(object sender, RoutedEventArgs e)
        {
            // TODO: inject SettingsService and open dialog when wired
            // for now, no-op to keep build green
        }

        private void OnHudToggled(object sender, RoutedEventArgs e)
        {
            // TODO: toggle mini HUD visibility via AppSettings once wired
        }
    }
}
