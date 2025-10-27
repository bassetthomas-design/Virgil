using System;
using System.Windows;
using System.Windows.Threading;

namespace Virgil.App
{
    public partial class MainWindow : Window
    {
        // === Fields (unique) ===
        private readonly DispatcherTimer _clockTimer;

        // === Constructor (unique) ===
        public MainWindow()
        {
            InitializeComponent();

            // Clock
            _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _clockTimer.Tick += (s, e) =>
            {
                try
                {
                    if (ClockText != null)
                        ClockText.Text = DateTime.Now.ToString("HH:mm:ss");
                }
                catch { /* no-op */ }
            };
            _clockTimer.Start();

            // UI init
            try
            {
                if (StatusText != null)
                    StatusText.Text = "En attente...";
            }
            catch { /* no-op */ }
        }

        // === Avatar helper (unique) ===
        private void SetAvatarMood(string mood)
        {
            try { AvatarControl?.SetMood(mood); } catch { /* no-op */ }
        }

        // === Top bar handlers ===
        private void SurveillanceToggle_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                StatusText.Text = "Surveillance : ON";
                SetAvatarMood("focused");
            }
            catch { /* no-op */ }
        }

        private void SurveillanceToggle_Unchecked(object sender, RoutedEventArgs e)
        {
            try
            {
                StatusText.Text = "Surveillance : OFF";
                SetAvatarMood("idle");
            }
            catch { /* no-op */ }
        }

        private void OpenConfig_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                StatusText.Text = "Ouverture de la configuration (à implémenter)…";
                // TODO: ouvrir le fichier de config / fenêtre de config
            }
            catch { /* no-op */ }
        }

        // === Actions rapides (stubs sûrs qui compilent) ===
        private void Action_MaintenanceComplete(object sender, RoutedEventArgs e)
        {
            try
            {
                StatusText.Text = "Maintenance complète en cours (stub)…";
                SetAvatarMood("working");
                // TODO: appeler la logique réelle
            }
            catch { /* no-op */ }
        }

        private void Action_CleanTemp(object sender, RoutedEventArgs e)
        {
            try
            {
                StatusText.Text = "Nettoyage des fichiers temporaires (stub)…";
                SetAvatarMood("working");
                // TODO: implémentation
            }
            catch { /* no-op */ }
        }

        private void Action_CleanBrowsers(object sender, RoutedEventArgs e)
        {
            try
            {
                StatusText.Text = "Nettoyage navigateurs (stub)…";
                SetAvatarMood("working");
                // TODO: implémentation
            }
            catch { /* no-op */ }
        }

        private void Action_UpdateAll(object sender, RoutedEventArgs e)
        {
            try
            {
                StatusText.Text = "Mises à jour (winget + jeux + applis) (stub)…";
                SetAvatarMood("working");
                // TODO: implémentation
            }
            catch { /* no-op */ }
        }

        private void Action_Defender(object sender, RoutedEventArgs e)
        {
            try
            {
                StatusText.Text = "Microsoft Defender (scan & update) (stub)…";
                SetAvatarMood("working");
                // TODO: implémentation
            }
            catch { /* no-op */ }
        }
    }
}
