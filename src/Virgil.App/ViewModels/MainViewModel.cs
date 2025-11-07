using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Timers;
using System.Windows.Input;
using Virgil.App.Services;
using Virgil.Domain;

namespace Virgil.App.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private readonly Timer _timer = new(1000);
    private string _headerStatus = "Prêt";
    private string _footerStatus = string.Empty;
    private string _input = string.Empty;
    private double _cpu, _ram, _gpu;
    private double _cpuTemp;
    private Mood _mood = Mood.Neutral;
    private string _avatarPath = string.Empty;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<string> Messages { get; } = new();

    public string HeaderStatus { get => _headerStatus; set { _headerStatus = value; OnPropertyChanged(); } }
    public string FooterStatus { get => _footerStatus; set { _footerStatus = value; OnPropertyChanged(); } }
    public string Input { get => _input; set { _input = value; OnPropertyChanged(); } }

    public string Now => DateTime.Now.ToString("HH:mm:ss");

    public double CpuUsage { get => _cpu; set { _cpu = value; OnPropertyChanged(); } }
    public double RamUsage { get => _ram; set { _ram = value; OnPropertyChanged(); } }
    public double GpuUsage { get => _gpu; set { _gpu = value; OnPropertyChanged(); } }
    public double CpuTemp { get => _cpuTemp; set { _cpuTemp = value; OnPropertyChanged(); } }

    public Mood CurrentMood { get => _mood; set { _mood = value; AvatarPath = AvatarService.GetAvatarPath(_mood); OnPropertyChanged(); } }
    public string AvatarPath { get => _avatarPath; private set { _avatarPath = value; OnPropertyChanged(); } }

    public ICommand SendCommand { get; }
    public ICommand MoodCycleCommand { get; }

    public MainViewModel()
    {
        Messages.Add("Virgil prêt. Fenêtre initialisée.");
        FooterStatus = "UI chargée";
        AvatarPath = AvatarService.GetAvatarPath(CurrentMood);

        _timer.Elapsed += (_, _) => OnPropertyChanged(nameof(Now));
        _timer.AutoReset = true;
        _timer.Enabled = true;

        SendCommand = new RelayCommand(_ => { if (!string.IsNullOrWhiteSpace(Input)) { Messages.Add("→ " + Input); Input = string.Empty; } });
        MoodCycleCommand = new RelayCommand(_ => CycleMood());
    }

    private void CycleMood()
    {
        CurrentMood = CurrentMood switch
        {
            Mood.Neutral => Mood.Happy,
            Mood.Happy => Mood.Focused,
            Mood.Focused => Mood.Warn,
            Mood.Warn => Mood.Alert,
            Mood.Alert => Mood.Sleepy,
            Mood.Sleepy => Mood.Tired,
            Mood.Tired => Mood.Proud,
            Mood.Proud => Mood.Neutral,
            _ => Mood.Neutral
        };
        Messages.Add($"Humeur: {CurrentMood}");
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public sealed class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Func<object?, bool>? _canExecute;

    public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
    { _execute = execute; _canExecute = canExecute; }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;
    public void Execute(object? parameter) => _execute(parameter);
    public event EventHandler? CanExecuteChanged { add { } remove { } }
}
