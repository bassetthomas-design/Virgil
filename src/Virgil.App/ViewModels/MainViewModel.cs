using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Virgil.App.Services;

namespace Virgil.App.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly IMonitoringService _mon = new MonitoringService();

    private string _headerStatus = "Prêt";
    private string _footerStatus = string.Empty;
    private double _cpu = 0, _ram = 0, _gpu = 0;
    private double _cpuTemp = 0;
    private Mood _mood = Mood.Sleepy;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<string> Messages { get; } = new();

    public string HeaderStatus { get => _headerStatus; set { _headerStatus = value; OnPropertyChanged(); } }
    public string FooterStatus { get => _footerStatus; set { _footerStatus = value; OnPropertyChanged(); } }
    public string Now => DateTime.Now.ToString("HH:mm:ss");

    public double CpuUsage { get => _cpu; set { _cpu = value; OnPropertyChanged(); UpdateMood(); } }
    public double RamUsage { get => _ram; set { _ram = value; OnPropertyChanged(); UpdateMood(); } }
    public double GpuUsage { get => _gpu; set { _gpu = value; OnPropertyChanged(); UpdateMood(); } }
    public double CpuTemp { get => _cpuTemp; set { _cpuTemp = value; OnPropertyChanged(); UpdateMood(); } }

    public Mood Mood { get => _mood; set { _mood = value; OnPropertyChanged(); OnPropertyChanged(nameof(MoodColor)); OnPropertyChanged(nameof(AvatarSource)); } }

    public SolidColorBrush MoodColor => MoodPalette.For(Mood);

    public ImageSource AvatarSource => new BitmapImage(new Uri($"pack://application:,,,/assets/avatar/{MoodToFile(Mood)}"));

    private static string MoodToFile(Mood m) => m switch
    {
        Mood.Happy   => "happy.png",
        Mood.Focused => "focused.png",
        Mood.Warn    => "warn.png",
        Mood.Alert   => "alert.png",
        Mood.Proud   => "proud.png",
        Mood.Tired   => "tired.png",
        Mood.Angry   => "angry.png",
        Mood.Love    => "love.png",
        Mood.Chat    => "chat.png",
        Mood.Sleepy  => "sleepy.png",
        _            => "neutral.png"
    };

    public MainViewModel()
    {
        Messages.Add("Virgil prêt. Monitoring activé.");
        FooterStatus = "UI redesign";
        _timer.Tick += (_, _) => { OnTick(); OnPropertyChanged(nameof(Now)); };
        _timer.Start();
    }

    private void OnTick()
    {
        var m = _mon.Read();
        CpuUsage = m.Cpu;
        RamUsage = m.Ram;
        CpuTemp = m.CpuTemp;
    }

    private void UpdateMood()
    {
        if (CpuTemp > 80 || CpuUsage > 90 || RamUsage > 90) Mood = Mood.Alert;
        else if (CpuUsage > 70 || RamUsage > 80) Mood = Mood.Warn;
        else if (CpuUsage > 35) Mood = Mood.Focused;
        else Mood = Mood.Happy;
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
