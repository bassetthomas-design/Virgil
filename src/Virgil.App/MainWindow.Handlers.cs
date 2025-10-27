using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace Virgil.App
{
    public partial class MainWindow : Window
    {
        // Stub minimal pour lever l’erreur “_cleaning does not exist…”
        // Tu pourras remplacer par l’implémentation réelle (ex: Virgil.Core.Services.CleaningService)
        private readonly object _cleaning = new();

        // Timer/horloge simple (si un autre partial gère déjà l’horloge, ceci restera neutre)
        private readonly System.Windows.Threading.DispatcherTimer _clockTimer =
            new System.Windows.Threading.DispatcherTimer();

        public MainWindow()
        {
            InitializeComponent();

            // Horloge en direct
            _clockTimer.Interval = TimeSpan.FromSeconds(1);
            _clockTimer.Tick += (_, __) =>
            {
                if (ClockText != null)
                    ClockText.Text = DateTime.Now.ToString("HH:mm:ss");
            };
            _clockTimer.Start();

            // Libellé du toggle selon état initial
            if (SurveillanceToggle != null)
                SurveillanceToggle.Content = "Démarrer la surveillance";
        }

        // ====== Handlers attendus par MainWindow.xaml ======

        private void SurveillanceToggle_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                if (SurveillanceToggle != null)
                    SurveillanceToggle.Content = "Arrêter la surveillance";

                // Ici tu démarrerais le monitoring réel si déjà implémenté
                // StartMonitoringSafely();
                SetAvatarMood("focus");
            }
            catch { /* no-op */ }
        }

        private void SurveillanceToggle_Unchecked(object sender, RoutedEventArgs e)
        {
            try
            {
                if (SurveillanceToggle != null)
                    SurveillanceToggle.Content = "Démarrer la surveillance";

                // Ici tu arrêterais le monitoring réel si déjà implémenté
                // StopMonitoringSafely();
                SetAvatarMood("idle");
            }
            catch { /* no-op */ }
        }

        private void OpenConfig_Click(object sender, RoutedEventArgs e)
        {
            // Stub non bloquant build.
            // Tu peux ouvrir ton fichier de config ici (machine+user) si besoin.
            StatusTextSafe("Ouverture de la configuration (stub)");
        }

        private async void Action_MaintenanceComplete(object sender, RoutedEventArgs e)
        {
            StatusTextSafe("Maintenance complète en cours (stub)...");
            await Task.Delay(200);
            StatusTextSafe("Maintenance complète terminée (stub).");
        }

        private async void Action_CleanTemp(object sender, RoutedEventArgs e)
        {
            StatusTextSafe("Nettoyage des fichiers temporaires (stub)...");
            await Task.Delay(200);
            StatusTextSafe("Nettoyage fichiers temporaires terminé (stub).");
        }

        private async void Action_CleanBrowsers(object sender, RoutedEventArgs e)
        {
            StatusTextSafe("Nettoyage des navigateurs (stub)...");
            await Task.Delay(200);
            StatusTextSafe("Nettoyage navigateurs terminé (stub).");
        }

        private async void Action_UpdateAll(object sender, RoutedEventArgs e)
        {
            StatusTextSafe("Mises à jour globales (stub)...");
            await Task.Delay(200);
            StatusTextSafe("Mises à jour terminées (stub).");
        }

        private async void Action_Defender(object sender, RoutedEventArgs e)
        {
            StatusTextSafe("Microsoft Defender: scan & update (stub)...");
            await Task.Delay(200);
            StatusTextSafe("Défender: opérations terminées (stub).");
        }

        // ====== Utilitaires sûrs ======

        private void StatusTextSafe(string text)
        {
            try
            {
                if (StatusText != null)
                    StatusText.Text = text;
            }
            catch { /* no-op */ }
        }

        // Expose exactement la signature que tu m’avais demandée
        private void SetAvatarMood(string mood)
        {
            try { AvatarControl?.SetMood(mood); } catch { /* no-op */ }
        }
    }
}
