using System.Windows;

namespace Virgil.App.Views
{
    public partial class MainShell : Window
    {
        public MainShell(){ InitializeComponent(); Loaded += (_,__)=> AddToolbarExtras(); }
    }
}
