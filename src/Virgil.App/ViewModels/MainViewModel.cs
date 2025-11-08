using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Threading;

namespace Virgil.App.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private string _headerStatus = "Prêt";
    private string _footerStatus = string.Empty;
    private string _input = string.Empty;
    private double _cpu = 0, _ram = 0, _gpu = 0;
    private double _cpuTemp = 0;

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

    public ICommand SendCommand { get; }

    public MainViewModel()
    {
        Messages.Add("Virgil prêt. Fenêtre initialisée.");
        FooterStatus = "UI chargée";

        _timer.Tick += (_, _) => OnPropertyChanged(nameof(Now));
        _timer.Start();

        SendCommand = new RelayCommand(_ => {
            if (!string.IsNullOrWhiteSpace(Input)) {
                Messages.Add("→ " + Input);
                Input = string.Empty;
            }
        });
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public sealed class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Func<object?, bool>? _canExecute;

    public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
    {
        _execute = execute; _canExecute = canExecute;
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;
    public void Execute(object? parameter) => _execute(parameter);
    public event EventHandler? CanExecuteChanged { add { } remove { } }
}