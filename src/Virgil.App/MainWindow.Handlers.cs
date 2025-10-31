// src/Virgil.App/MainWindow.Handlers.cs
// NOTE: fichier neutre : aucune infra WPF ici, pas d'IComponentConnector,
// pas d'InitializeComponent, pas de champs _contentLoaded.

using System.Windows; // pour RoutedEventArgs si un jour tu remets des handlers ici

namespace Virgil.App
{
    public partial class MainWindow
    {
        // Intentionnellement vide.
        // Tous les handlers vivent dans MainWindow.xaml.cs.
        // NE PAS implémenter IComponentConnector ici.
        // NE PAS déclarer InitializeComponent ici.
        // NE PAS déclarer _contentLoaded ici.
    }
}
