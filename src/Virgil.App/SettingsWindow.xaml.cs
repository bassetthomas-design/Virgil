// src/Virgil.App/MainWindow.xaml.cs
// Code-behind propre : AUCUNE infra WPF (pas d'IComponentConnector, pas de _contentLoaded,
// pas de définition de InitializeComponent). On ne fait qu'APPELER InitializeComponent().

using System.Windows;           // RoutedEventArgs
using System.Windows.Threading; // si tu utilises un DispatcherTimer
using Virgil.Core;              // si tu utilises Mood, services, etc.

namespace Virgil.App
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            // IMPORTANT : on APPELLE InitializeComponent() (définie dans le .g.cs auto-généré)
            InitializeComponent();

            // ----- Ton initialisation UI/app ici (sans infra WPF) -----
            // Exemples :
            // this.Title = "Virgil";
            // ClockStart();
            // if (AvatarControl != null) AvatarControl.SetMood(Mood.Focused);
        }

        // =========================
        // Handlers d’événements UI
        // =========================

        private void SurveillanceToggle_Checked(object sender, RoutedEventArgs e)
        {
            // TODO: démarrer la surveillance
            // StartMonitoring();
        }

        private void SurveillanceToggle_Unchecked(object sender, RoutedEventArgs e)
        {
            // TODO: arrêter la surveillance
            // StopMonitoring();
        }

        private void Action_MaintenanceComplete(object sender, RoutedEventArgs e)
        {
            // TODO: appeler ton service de maintenance complète (async/await si nécessaire)
            // await _virgilService.FullMaintenanceAsync();
        }

        private void Action_CleanTemp(object sender, RoutedEventArgs e)
        {
            // TODO: nettoyage intelligent
        }

        private void Action_CleanBrowsers(object sender, RoutedEventArgs e)
        {
            // TODO: nettoyage navigateurs
        }

        private void Action_UpdateAll(object sender, RoutedEventArgs e)
        {
            // TODO: mises à jour complètes
        }

        private void Action_Defender(object sender, RoutedEventArgs e)
        {
            // TODO: maj + scan Defender
        }

        private void OpenConfig_Click(object sender, RoutedEventArgs e)
        {
            // TODO: ouvrir la fenêtre des paramètres
            // new SettingsWindow().ShowDialog();
        }

        // =========================
        // Méthodes utilitaires UI
        // =========================

        // Exemple si tu as une horloge :
        // private DispatcherTimer? _clockTimer;
        // private void ClockStart()
        // {
        //     _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        //     _clockTimer.Tick += (_, __) => { ClockText.Text = DateTime.Now.ToString("HH:mm:ss"); };
        //     _clockTimer.Start();
        // }
    }
}
