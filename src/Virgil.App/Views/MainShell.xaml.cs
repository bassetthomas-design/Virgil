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
    }
}
