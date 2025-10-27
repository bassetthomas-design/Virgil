using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Threading;

namespace Virgil.App
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        // ---- Champs UI/état ---
        private DispatcherTimer _clockTimer;
        private DispatcherTimer _survTimer;
        private bool _isMonitoring;

        private string _clockText = "";
        public string ClockText
        {
            get => _clockText;
            private set { _clockText = value; OnPropertyChanged(); }
        }

        public bool IsMonitoring
        {
            get => _isMonitoring;
            private set { _isMonitoring = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;

            // Horloge en direct
            _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _clockTimer.Tick += (_, __) => ClockText = DateTime.Now.ToString("HH:mm:ss");
            _clockTimer.Start();
            ClockText = DateTime.Now.ToString("HH:mm:ss");

            // Timer de “surveillance” (pulse + lecture hardware si disponible)
            _survTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _survTimer.Tick += (_, __) => SurveillancePulse();
        }

        // ---- Appels depuis le XAML (événements) ----
        private void SurveillanceToggle_Checked(object sender, RoutedEventArgs e) => StartMonitoring();
        private void SurveillanceToggle_Unchecked(object sender, RoutedEventArgs e) => StopMonitoring();
        private void OpenConfig_Click(object sender, RoutedEventArgs e) => OpenConfig();

        private async void Action_MaintenanceComplete(object sender, RoutedEventArgs e) => await Handle_MaintenanceComplete();
        private async void Action_CleanTemp(object sender, RoutedEventArgs e) => await Handle_CleanTemp();
        private async void Action_CleanBrowsers(object sender, RoutedEventArgs e) => await Handle_CleanBrowsers();
        private async void Action_UpdateAll(object sender, RoutedEventArgs e) => await Handle_UpdateAll();
        private async void Action_Defender(object sender, RoutedEventArgs e) => await Handle_Defender();

        // ---- Surveillance / Avatar / Chat ----
        private void StartMonitoring()
        {
            if (IsMonitoring) return;
            IsMonitoring = true;
            _survTimer.Start();
            Say("Surveillance activée. Je garde un œil sur la machine 👀", mood: "focused");
            SetAvatarMood("focused");
        }

        private void StopMonitoring()
        {
            if (!IsMonitoring) return;
            IsMonitoring = false;
            _survTimer.Stop();
            Say("Surveillance arrêtée. Tu me dis si tu as besoin de moi ✋", mood: "neutral");
            SetAvatarMood("neutral");
        }

        private int _pulseCount = 0;
        private void SurveillancePulse()
        {
            // Ici on peut interroger le Core (si dispo) pour CPU/GPU/RAM/Disk
            // En attendant, on prouve le comportement UI:
            _pulseCount++;
            if (_pulseCount % 15 == 0) // ~toutes les 30 s avec interval = 2 s
            {
                Say("Tout roule. Températures et usages dans le vert ✅", mood: "happy");
            }
            // TODO: brancher AdvancedMonitoringService / HardwareSnapshot pour remplir l’UI et déclencher des alertes seuils.
        }

        // L’avatar côté XAML devrait s’appeler AvatarControl (x:Name="AvatarControl")
        // Si l’instance n’existe pas encore, on ignore silencieusement.
        private void SetAvatarMood(string mood)
        {
            try { AvatarControl?.SetMood(mood); } catch { /* no-op */ }
        }

        private void Say(string text, string mood = "neutral")
        {
            try
            {
                ChatPanel?.AppendMessage(new Controls.ChatMessage
                {
                    Text = text,
                    Mood = mood,
                    Timestamp = DateTime.Now
                });
            }
            catch { /* no-op */ }
        }

        private void OpenConfig()
        {
            Say("Ouverture de la configuration…", mood: "neutral");
            // TODO: affiche l’éditeur de config quand il sera prêt
        }
    }
}
