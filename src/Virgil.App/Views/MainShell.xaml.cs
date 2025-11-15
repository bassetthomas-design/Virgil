using System.Windows;

namespace Virgil.App.Views
{
    /// <summary>
    /// Minimal code-behind for the main shell window.
    /// This version is intentionally simplified on the dev branch so that
    /// the application can compile cleanly while the chat services layer
    /// is being refactored.
    /// </summary>
    public partial class MainShell : Window
    {
        public MainShell()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Placeholder handler for the settings button declared in MainShell.xaml.
        /// Hooked up to keep the dev branch build green; the real behaviour can be
        /// reintroduced later from the previous implementation.
        /// </summary>
        private void OnOpenSettings(object sender, RoutedEventArgs e)
        {
            // TODO: wire to settings window when the UX flow is finalized again.
        }

        /// <summary>
        /// Placeholder handler for the HUD toggle button declared in MainShell.xaml.
        /// </summary>
        private void OnHudToggled(object sender, RoutedEventArgs e)
        {
            // TODO: implement HUD toggle logic (show/hide mini HUD) if required.
        }
    }
}
