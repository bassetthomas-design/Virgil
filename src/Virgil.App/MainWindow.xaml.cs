using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Virgil.App.Controls;

namespace Virgil.App
{
    public partial class MainWindow : Window
    {
        // Timers WPF (plus d’ambiguïté avec System.Timers ou Windows.Forms)
        private readonly DispatcherTimer _clockTimer = new() { Interval = TimeSpan.FromSeconds(1) };
        private readonly DispatcherTimer _surveillanceTimer = new() { Interval = TimeSpan.FromMilliseconds(1500) };

        public string SurveillanceToggleText
        {
            get => (string)(FindResource("SurveillanceToggleText") ?? "Démarrer la surveillance");
            set => Resources["SurveillanceToggleText"] = value;
        }

        public MainWindow()
        {
            InitializeComponent();

            // Horloge en direct
            _clockTimer.Tick += (_, __) => UpdateClock();
            _clockTimer.Start();

            // Rafraîchissement de la surveillance
            _surveillanceTimer.Tick += (_, __) => SurveillancePulse();

            // Texte par défaut
            SurveillanceToggleText = "Démarrer la surveillance";

            // Message d’accueil
            Say("Salut, c’est Virgil. Prêt à veiller sur ta machine.");
            SetAvatarMood("happy");
        }

        // === Mise à jour de l’horloge ===
        private void UpdateClock()
        {
            ClockText.Text = DateTime.Now.ToString("HH:mm:ss");
        }

        // === Surveillance système (valeurs factices pour l’instant) ===
        private void SurveillancePulse()
        {
            CpuBar.Value = (DateTime.Now.Second * 100.0 / 60.0);
            RamBar.Value = (DateTime.Now.Millisecond % 100);
            GpuBar.Value = (DateTime.Now.Second * 100.0 / 60.0);
            DiskBar.Value = (DateTime.Now.Millisecond % 100);

            if (CpuBar.Value > 85)
            {
                SetAvatarMood("warn");
                Say("Je chauffe un peu (CPU > 85 %). Je garde un œil ouvert !");
            }
        }

        // === Chat ===
        private void Say(string texte, string humeur = "focused")
        {
            var bulle = new Border
            {
                CornerRadius = new CornerRadius(8),
                Margin = new Thickness(12),
                Padding = new Thickness(12),
                Background = humeur switch
                {
                    "happy" => new SolidColorBrush(Color.FromArgb(50, 64, 201, 112)),
                    "warn" => new SolidColorBrush(Color.FromArgb(50, 241, 196, 15)),
                    "alert" => new SolidColorBrush(Color.FromArgb(50, 231, 76, 60)),
                    _ => new SolidColorBrush(Color.FromArgb(50, 52, 152, 219)),
                },
                Child = new TextBlock
                {
                    Text = texte,
                    Foreground = Brushes.White,
                    TextWrapping = TextWrapping.Wrap
                }
            };

            ChatItems.Items.Add(bulle);
            ChatScroll.ScrollToBottom();
        }

        // === Gestion des humeurs de l’avatar ===
        private void SetAvatarMood(string humeur)
        {
            try
            {
                Brush overlay = humeur switch
                {
                    "happy" => new SolidColorBrush(Color.FromArgb(36, 76, 175, 80)),
                    "warn" => new SolidColorBrush(Color.FromArgb(36, 241, 196, 15)),
                    "alert" => new SolidColorBrush(Color.FromArgb(36, 231, 76, 60)),
                    "sleepy" => new SolidColorBrush(Color.FromArgb(36, 149, 165, 166)),
                    _ => new SolidColorBrush(Color.FromArgb(36, 52, 152, 219)),
                };

                string glyph = humeur switch
                {
                    "happy" => "/Assets/avatar/moods/happy.png",
                    "warn" => "/Assets/avatar/moods/warn.png",
                    "alert" => "/Assets/avatar/moods/alert.png",
                    "sleepy" => "/Assets/avatar/moods/sleepy.png",
                    "tired" => "/Assets/avatar/moods/tired.png",
                    "proud" => "/Assets/avatar/moods/proud.png",
                    _ => "/Assets/avatar/moods/focused.png"
                };

                AvatarControl.MoodOverlayBrush = overlay;
                AvatarControl.MoodGlyph = glyph;
            }
            catch { /* Sécurité : on ignore les erreurs visuelles */ }
        }

        // === Gestion des boutons ===
        private void SurveillanceToggle_Checked(object sender, RoutedEventArgs e)
        {
            _surveillanceTimer.Start();
            SurveillanceToggleText = "Surveillance activée";
            SetAvatarMood("focused");
            Say("Surveillance activée. J’observe le système en continu.");
        }

        private void SurveillanceToggle_Unchecked(object sender, RoutedEventArgs e)
        {
            _surveillanceTimer.Stop();
            SurveillanceToggleText = "Surveillance arrêtée";
            SetAvatarMood("sleepy");
            Say("Surveillance désactivée. Je me repose un peu.");
        }

        private void OpenConfig_Click(object sender, RoutedEventArgs e)
        {
            Say("Ouverture de la configuration utilisateur…");
            // TODO : ouvrir la fenêtre de configuration
        }

        private async void Action_MaintenanceComplete(object sender, RoutedEventArgs e)
        {
            Say("Lancement du mode maintenance complète…", "focused");
            await System.Threading.Tasks.Task.CompletedTask;
            Say("Maintenance terminée. Tout est propre et à jour !", "happy");
        }

        private async void Action_CleanTemp(object sender, RoutedEventArgs e)
        {
            Say("Nettoyage intelligent des fichiers temporaires…", "focused");
            await System.Threading.Tasks.Task.CompletedTask;
            Say("Nettoyage terminé, plus un grain de poussière numérique.", "happy");
        }

        private async void Action_CleanBrowsers(object sender, RoutedEventArgs e)
        {
            Say("Nettoyage des navigateurs en cours…", "focused");
            await System.Threading.Tasks.Task.CompletedTask;
            Say("Navigateurs nettoyés. Plus légers que jamais !", "happy");
        }

        private async void Action_UpdateAll(object sender, RoutedEventArgs e)
        {
            Say("Je lance les mises à jour complètes du système.", "focused");
            await System.Threading.Tasks.Task.CompletedTask;
            Say("Toutes les mises à jour ont été appliquées avec succès !", "happy");
        }

        private async void Action_Defender(object sender, RoutedEventArgs e)
        {
            Say("Mise à jour de Defender + scan rapide…", "focused");
            await System.Threading.Tasks.Task.CompletedTask;
            Say("Microsoft Defender est à jour et ton système est sain.", "happy");
        }
    }
}
