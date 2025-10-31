// src/Virgil.App/SettingsWindow.xaml.cs
// Code-behind PROPRE pour la fenêtre de paramètres.
// Aucune infra WPF doublonnée (pas d'IComponentConnector, pas de _contentLoaded, etc.)

using System.Windows;

namespace Virgil.App
{
    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();
        }

        // Place ici UNIQUEMENT des handlers SPECIFIQUES aux réglages, si besoin.
        // NE METS PAS les handlers de MainWindow (SurveillanceToggle_*, Action_*, etc.)
    }
}
