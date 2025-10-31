// src/Virgil.App/MainWindow.xaml.cs
// Code-behind PROPRE : aucune infra WPF (pas d'IComponentConnector, pas de _contentLoaded,
// pas de définition d'InitializeComponent). On FAIT SEULEMENT L'APPEL à InitializeComponent().

using System.Windows;           // RoutedEventArgs
using System.Windows.Threading; // si tu utilises un DispatcherTimer
using Virgil.Core;              // si tu utilises Mood, services, etc.

namespace Virgil.App
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent(); // définie dans le .g.cs auto-généré par WPF

            // -- ton init UI ICI (exemples) --
            // this.Title = "Virgil";
            // if (AvatarControl != null) AvatarControl.SetMood(Mood.Focused);
            // ClockStart();
        }

        // ===== Handlers de la fenêtre principale =====

        private void SurveillanceToggle_Checked(object sender, RoutedEventArgs e)
        {
            // TODO: start monitoring
        }

        private void SurveillanceToggle_Unchecked(object sender, RoutedEventArgs e)
        {
            // TODO: stop monitoring
        }

        private void Action_MaintenanceComplete(object sender, RoutedEventArgs e)
        {
            // TODO
        }

        private void Action_CleanTemp(object sender, RoutedEventArgs e)
        {
            // TODO
        }

        private void Action_CleanBrowsers(object sender, RoutedEventArgs e)
        {
            // TODO
        }

        private void Action_UpdateAll(object sender, RoutedEventArgs e)
        {
            // TODO
        }

        private void Action_Defender(object sender, RoutedEventArgs e)
        {
            // TODO
        }

        private void OpenConfig_Click(object sender, RoutedEventArgs e)
        {
            // Ouvre la fenêtre des paramètres
            var win = new SettingsWindow();
            win.Owner = this;
            win.ShowDialog();
        }

        // ===== Utilitaires (exemple horloge) =====
        // private DispatcherTimer? _clockTimer;
        // private void ClockStart()
        // {
        //     _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        //     _clockTimer.Tick += (_, __) => { ClockText.Text = DateTime.Now.ToString("HH:mm:ss"); };
        //     _clockTimer.Start();
        // }
    }
}
