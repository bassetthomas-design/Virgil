using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Media;

namespace Virgil.App.ViewModels
{
    public sealed class DashboardViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        void Raise([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        public string Clock { get => _clock; private set { _clock = value; Raise(); } }
        private string _clock = DateTime.Now.ToString("HH:mm:ss");

        public bool IsSurveillanceOn { get => _surv; set { _surv = value; Raise(); Raise(nameof(SurveillanceLabel)); }}
        private bool _surv;

        public string SurveillanceLabel => IsSurveillanceOn ? "Surveillance ON" : "Surveillance OFF";

        // Mood & avatar
        public string Mood { get => _mood; set { _mood = value; Raise(); Raise(nameof(MoodLabel)); } }
        private string _mood = "happy";
        public string MoodLabel => $"Humeur : {Mood}";

        // Gauges (mock values for now)
        public double CpuUsage { get => _cpu; set { _cpu = value; Raise(); } }  private double _cpu = 18;
        public double GpuUsage { get => _gpu; set { _gpu = value; Raise(); } }  private double _gpu = 12;
        public double RamUsage { get => _ram; set { _ram = value; Raise(); } }  private double _ram = 42;
        public double DiskUsage { get => _disk; set { _disk = value; Raise(); } } private double _disk = 8;

        public string CpuTempText => "CPU: 45°C";  public Brush CpuTempBrush => Brushes.LightGreen;
        public string GpuTempText => "GPU: 40°C";  public Brush GpuTempBrush => Brushes.LightGreen;
        public string DiskTempText => "Disque: 35°C"; public Brush DiskTempBrush => Brushes.LightGreen;
        public string RamText => "Utilisée: 6.8 Go / 16 Go";

        // Chat
        public ObservableCollection<ChatBubble> Chat { get; } = new();

        // Commands
        public ICommand CmdMaintenance  => _cmdMaint ??= new Relay(() => Say("Maintenance complète (pré-squelette)"));
        public ICommand CmdSmartClean   => _cmdSmart ??= new Relay(() => Say("Nettoyage intelligent (pré-squelette)"));
        public ICommand CmdBrowserClean => _cmdBrows ??= new Relay(() => Say("Nettoyage navigateurs (pré-squelette)"));
        public ICommand CmdUpdateAll    => _cmdUpd ??= new Relay(() => Say("Tout mettre à jour (pré-squelette)"));
        public ICommand CmdDefender     => _cmdDef ??= new Relay(() => Say("Defender MAJ + Scan (pré-squelette)"));
        public ICommand CmdOpenConfig   => _cmdCfg ??= new Relay(() => Say("Ouvrir configuration (pré-squelette)"));

        private ICommand? _cmdMaint, _cmdSmart, _cmdBrows, _cmdUpd, _cmdDef, _cmdCfg;

        public void Init()
        {
            Say("Bonjour, je suis Virgil. On peaufine l’interface — les actions arrivent juste après 😉");
        }

        public void TickClock()
        {
            Clock = DateTime.Now.ToString("HH:mm:ss");

            // Petite vie artificielle: varier légèrement pour montrer les bindings
            var rnd = new Random();
            if (IsSurveillanceOn)
            {
                CpuUsage  = Clamp01(CpuUsage + (rnd.NextDouble() - 0.5) * 5);
                GpuUsage  = Clamp01(GpuUsage + (rnd.NextDouble() - 0.5) * 5);
                RamUsage  = Clamp01(RamUsage + (rnd.NextDouble() - 0.5) * 2);
                DiskUsage = Clamp01(DiskUsage + (rnd.NextDouble() - 0.5) * 4);
            }
        }

        private static double Clamp01(double v) => Math.Max(0, Math.Min(100, v));

        private void Say(string text)
        {
            Chat.Add(ChatBubble.Info(text));
            if (Chat.Count > 200) Chat.RemoveAt(0);
        }
    }

    public sealed class ChatBubble
    {
        public string Text { get; set; } = "";
        public Brush BubbleBrush { get; set; } = Brushes.DimGray;

        public static ChatBubble Info(string t) => new() { Text = t, BubbleBrush = new SolidColorBrush(Color.FromRgb(40, 48, 66)) };
    }

    public sealed class Relay : ICommand
    {
        private readonly Action _act;
        public Relay(Action act) => _act = act;
        public bool CanExecute(object? p) => true;
        public void Execute(object? p) => _act();
        public event EventHandler? CanExecuteChanged;
    }
}
