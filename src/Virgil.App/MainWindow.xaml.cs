using System;
using System.Windows;
using System.Windows.Threading;

namespace Virgil.App
{
    public partial class MainWindow : Window
    {
        // Timers uniques (NE PAS redéclarer ailleurs)
        private readonly DispatcherTimer _clockTimer;
        private readonly DispatcherTimer _survTimer;

        // États
        private bool _monitoring = false;

        public MainWindow()
        {
            InitializeComponent();

            // Horloge en haut (tick chaque seconde)
            _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _clockTimer.Tick += (_, __) => {
                try
                {
                    // Si tu as un TextBlock nommé ClockText dans le XAML:
                    // ClockText.Text = DateTime.Now.ToString("HH:mm:ss");
                    // Si c’est du binding, laisse vide.
                }
                catch { /* safe */ }
            };
            _clockTimer.Start();

            // Surveillance (tick toutes les 5s par défaut)
            _survTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            _survTimer.Tick += (_, __) =>
            {
                if (!_monitoring) return;
                try
                {
                    // TODO: Appeler ici ton service de monitoring et pousser les valeurs dans les bindings
                    // ex: AdvancedMonitoringService.Snapshot() -> met à jour VM/Bindings
                    // Et déclencher les messages "pulse" si ON (via Say(...))
                }
                catch { /* safe */ }
            };
            // Ne pas démarrer ici : on ne démarre que quand le toggle est ON
        }

        // Garde ce helper UNE seule fois dans tout le projet
        private void SetAvatarMood(string mood)
        {
            try
            {
                // N'appelle pas de type fort si tu n'as pas la ref du contrôle:
                // on tente via réflexion (évite un using/dep manquante)
                var field = GetType().GetField("AvatarControl",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
                var ctrl = field?.GetValue(this);
                var m = ctrl?.GetType().GetMethod("SetMood");
                m?.Invoke(ctrl, new object[] { mood });
            }
            catch { /* safe */ }
        }

        // Petit utilitaire chat côté fenêtre (dump texte)
        // Garde juste la signature; implémente selon ton UI (ItemsControl, ListBox, etc.)
        private void Say(string text, string mood = "neutral")
        {
            try
            {
                // TODO: ajoute "text" à ta zone de chat et adapte la couleur/Style selon "mood"
                // Ex: ChatList.Items.Add(new ChatMessage { Text = text, Mood = mood, When = DateTime.Now });
                // Pense à binder l’UI à une ObservableCollection si tu as un VM.
            }
            catch { /* safe */ }
        }

        // Facultatif : expose un démarrage/arrêt programmatique du monitoring
        private void StartMonitoring()
        {
            if (_monitoring) return;
            _monitoring = true;
            _survTimer.Start();
            SetAvatarMood("focused");
            Say("Surveillance démarrée.", "info");
        }

        private void StopMonitoring()
        {
            if (!_monitoring) return;
            _monitoring = false;
            _survTimer.Stop();
            SetAvatarMood("idle");
            Say("Surveillance arrêtée.", "info");
        }
    }
}
