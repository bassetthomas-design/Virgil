// src/Virgil.App/MainWindow.xaml.cs
using System;
using System.Windows;
using System.Windows.Threading;

namespace Virgil.App
{
    public partial class MainWindow : Window
    {
        // === Champs privés (UN SEUL exemplaire) ===
        private readonly DispatcherTimer _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };

        // === Constructeur (UN SEUL) ===
        public MainWindow()
        {
            InitializeComponent();

            // Horloge en direct (top bar)
            _clockTimer.Tick += (_, __) =>
            {
                try { ClockText?.SetCurrentValue(System.Windows.Controls.TextBlock.TextProperty, DateTime.Now.ToString("HH:mm:ss")); }
                catch { /* ignore UI timing errors */ }
            };
            _clockTimer.Start();

            // Texte d’état par défaut
            TrySetStatus("Prêt.");
            // Avatar à l’humeur "idle"
            SetAvatarMood("idle");
        }

        // === Utilitaires UI sûrs ===
        private void TrySetStatus(string message)
        {
            try { StatusText?.SetCurrentValue(System.Windows.Controls.TextBlock.TextProperty, message); }
            catch { /* ignore */ }
        }

        private void SetAvatarMood(string mood)
        {
            try { AvatarControl?.SetMood(mood); } catch { /* ignore */ }
        }

        // === Handlers top-bar Surveillance (référencés par MainWindow.xaml) ===
        private void SurveillanceToggle_Checked(object sender, RoutedEventArgs e)
        {
            TrySetStatus("Surveillance : ON");
            SetAvatarMood("focused");
            // TODO: démarrer le monitoring temps réel ici (timers, services, etc.)
        }

        private void SurveillanceToggle_Unchecked(object sender, RoutedEventArgs e)
        {
            TrySetStatus("Surveillance : OFF");
            SetAvatarMood("idle");
            // TODO: arrêter le monitoring temps réel ici
        }

        // === Handler bouton configuration ===
        private void OpenConfig_Click(object sender, RoutedEventArgs e)
        {
            TrySetStatus("Ouverture de la configuration...");
            // TODO: ouvrir l’UI/éditeur pour la config (machine + user)
        }

        // === Actions (boutons dans le panneau d’outils) ===
        private void Action_MaintenanceComplete(object sender, RoutedEventArgs e)
        {
            TrySetStatus("Maintenance complète en cours...");
            SetAvatarMood("working");
            // TODO: enchaîner nettoyage, updates, défense, etc.
        }

        private void Action_CleanTemp(object sender, RoutedEventArgs e)
        {
            TrySetStatus("Nettoyage des fichiers temporaires...");
            SetAvatarMood("working");
            // TODO: appeler ton service de nettoyage (temp)
        }

        private void Action_CleanBrowsers(object sender, RoutedEventArgs e)
        {
            TrySetStatus("Nettoyage navigateurs...");
            SetAvatarMood("working");
            // TODO: appeler ton service de nettoyage (browsers)
        }

        private void Action_UpdateAll(object sender, RoutedEventArgs e)
        {
            TrySetStatus("Mises à jour en cours...");
            SetAvatarMood("working");
            // TODO: enchaîner winget/choco/driver/game/etc. suivant ta logique
        }

        private void Action_Defender(object sender, RoutedEventArgs e)
        {
            TrySetStatus("Analyse Microsoft Defender...");
            SetAvatarMood("working");
            // TODO: lancer l’analyse Defender / sécurité
        }
    }
}
