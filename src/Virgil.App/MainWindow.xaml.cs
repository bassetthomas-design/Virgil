using System;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Virgil.App.Controls;

namespace Virgil.App
{
    public partial class MainWindow : Window
    {
        private readonly Timer _clockTimer = new(1000);
        private readonly Timer _surveillanceTimer = new(1500);

        public string SurveillanceToggleText
        {
            get => (string) (FindResource("SurveillanceToggleText") ?? "Démarrer la surveillance");
            set => Resources["SurveillanceToggleText"] = value;
        }

        public MainWindow()
        {
            InitializeComponent();

            // Timer horloge
            _clockTimer.Elapsed += (_, __) => Dispatcher.Invoke(UpdateClock);
            _clockTimer.AutoReset = true;
            _clockTimer.Start();

            // Timer surveillance
            _surveillanceTimer.Elapsed += (_, __) => Dispatcher.Invoke(SurveillancePulse);
            _surveillanceTimer.AutoReset = true;

            // Valeur init
            SurveillanceToggleText = "Démarrer la surveillance";

            // Message d’accueil
            Say("Salut, c’est Virgil. Prêt à veiller sur ta machine.");
            SetAvatarMood("happy");
        }

        private void UpdateClock()
        {
            ClockText.Text = DateTime.Now.ToString("HH:mm:ss");
        }

        private void SurveillancePulse()
        {
            // TODO: brancher tes sondes CPU/GPU/RAM/Disque et couleurs de seuils
            // Ci-dessous, placeholder pour la démo
            CpuBar.Value = (DateTime.Now.Second * 100.0 / 60.0);
            RamBar.Value = (DateTime.Now.Millisecond % 100);
            GpuBar.Value = (DateTime.Now.Second * 100.0 / 60.0);
            DiskBar.Value = (DateTime.Now.Millisecond % 100);

            if (CpuBar.Value > 85)
            {
                SetAvatarMood("warn");
                Say("Je chauffe un peu (CPU > 85%). Je garde un œil ouvert.");
            }
        }

        // ============ CHAT ============
        private void Say(string text, string mood = "focused")
        {
            var bubble = new Border
            {
                CornerRadius = new CornerRadius(8),
                Margin = new Thickness(12),
                Padding = new Thickness(12),
                Background = mood switch
                {
                    "happy" => new SolidColorBrush(Color.FromArgb(50, 64, 201, 112)),
                    "warn" => new SolidColorBrush(Color.FromArgb(50, 241, 196, 15)),
                    "alert" => new SolidColorBrush(Color.FromArgb(50, 231, 76, 60)),
                    _ => new SolidColorBrush(Color.FromArgb(50, 52, 152, 219)),
                },
                Child = new TextBlock
                {
                    Text = text,
                    Foreground = Brushes.White,
                    TextWrapping = TextWrapping.Wrap
                }
            };

            ChatItems.Items.Add(bubble);
            ChatScroll.ScrollToBottom();
        }

        // ============ AVATAR / HUMEUR ============
        private void SetAvatarMood(string mood)
        {
            try
            {
                // Map humeur → overlay + glyph
                Brush overlay = mood switch
                {
                    "happy" => new SolidColorBrush(Color.FromArgb(36, 76, 175, 80)),
                    "warn"  => new SolidColorBrush(Color.FromArgb(36, 241, 196, 15)),
                    "alert" => new SolidColorBrush(Color.FromArgb(36, 231, 76, 60)),
                    "sleepy"=> new SolidColorBrush(Color.FromArgb(36, 149, 165, 166)),
                    _       => new SolidColorBrush(Color.FromArgb(36, 52, 152, 219)),
                };

                string glyph = mood switch
                {
                    "happy" => "/Assets/avatar/moods/happy.png",
                    "warn"  => "/Assets/avatar/moods/warn.png",
                    "alert" => "/Assets/avatar/moods/alert.png",
                    "sleepy"=> "/Assets/avatar/moods/sleepy.png",
                    "tired" => "/Assets/avatar/moods/tired.png",
                    "proud" => "/Assets/avatar/moods/proud.png",
                    _       => "/Assets/avatar/moods/focused.png"
                };

                AvatarControl.MoodOverlayBrush = overlay;
                AvatarControl.MoodGlyph = glyph;
            }
            catch { /* safe */ }
        }

        // ============ HANDLERS (déclarés une seule fois) ============
        private void SurveillanceToggle_Checked(object sender, RoutedEventArgs e)
        {
            _surveillanceTimer.Start();
            SurveillanceToggleText = "Surveillance activée";
            SetAvatarMood("focused");
            Say("Surveillance ON. Je prends le pouls de la machine.");
        }

        private void SurveillanceToggle_Unchecked(object sender, RoutedEventArgs e)
        {
            _surveillanceTimer.Stop();
            SurveillanceToggleText = "Surveillance arrêtée";
            SetAvatarMood("sleepy");
            Say("Surveillance OFF. Je me mets en veille.");
        }

        private void OpenConfig_Click(object sender, RoutedEventArgs e)
        {
            Say("Ouverture de la configuration…");
            // TODO: ouvrir la fenêtre/config
        }

        private async void Action_MaintenanceComplete(object sender, RoutedEventArgs e)
        {
            Say("Mode maintenance activé. J’enchaîne nettoyage + MAJ.", "proud");
            // TODO: appeler les services core (cleaning, browsers, updates, defender)
            await System.Threading.Tasks.Task.CompletedTask;
            Say("Maintenance terminée. Le PC respire mieux. 😌", "happy");
        }

        private async void Action_CleanTemp(object sender, RoutedEventArgs e)
        {
            Say("Nettoyage intelligent en cours…", "focused");
            // TODO: cleaning intelligent (simple/complet/pro) + effet ‘Thanos’ si complet
            await System.Threading.Tasks.Task.CompletedTask;
            Say("Nettoyage terminé. Tout propre.", "happy");
        }

        private async void Action_CleanBrowsers(object sender, RoutedEventArgs e)
        {
            Say("Nettoyage navigateurs (profils/caches)…", "focused");
            await System.Threading.Tasks.Task.CompletedTask;
            Say("Navigateurs nettoyés.", "happy");
        }

        private async void Action_UpdateAll(object sender, RoutedEventArgs e)
        {
            Say("Je lance les mises à jour : apps/jeux/pilotes/Windows/Defender.", "focused");
            await System.Threading.Tasks.Task.CompletedTask;
            Say("Tout est à jour. Nickel.", "happy");
        }

        private async void Action_Defender(object sender, RoutedEventArgs e)
        {
            Say("Microsoft Defender : MAJ des signatures + scan rapide…", "focused");
            await System.Threading.Tasks.Task.CompletedTask;
            Say("Defender OK.", "happy");
        }
    }
}
