using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Virgil.Core; // MoodService côté Core

namespace Virgil.App.ViewModels
{
    // ===== Base INotifyPropertyChanged (remplace BaseViewModel manquant) =====
    public abstract class BaseViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? name = null)
        {
            if (Equals(storage, value)) return false;
            storage = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
            return true;
        }
    }

    // ===== Stubs internes : SystemMonitor & ChatService =====
    internal sealed class SystemMonitor
    {
        private readonly Random _r = new();
        public int GetCpuUsage() => _r.Next(1, 35);
        public int GetGpuUsage() => _r.Next(0, 25);
        public int GetRamUsage() => _r.Next(10, 70);
        public int GetDiskUsage() => _r.Next(1, 40);
        public int GetCpuTemperature() => _r.Next(35, 75);
    }

    internal sealed class ChatService
    {
        public void SendMessage(string msg) { /* hook futur vers le chat */ }
    }

    public sealed class ChatMessage
    {
        public string Text { get; set; } = "";
        public DateTime Timestamp { get; set; }
    }

    // ===== Relay standard (garde CanExecuteChanged) =====
    public sealed class Relay : ICommand
    {
        private readonly Action<object?> _exec;
        private readonly Func<object?, bool>? _can;
        public Relay(Action<object?> exec, Func<object?, bool>? can = null)
        {
            _exec = exec;
            _can = can;
        }
        public bool CanExecute(object? parameter) => _can?.Invoke(parameter) ?? true;
        public void Execute(object? parameter) => _exec(parameter);
        public event EventHandler? CanExecuteChanged;
    }

    // ===== ViewModel principale =====
    public class DashboardViewModel : BaseViewModel
    {
        private readonly SystemMonitor _monitor;
        private readonly ChatService _chatService;
        private readonly MoodService _moodService;
        private readonly CancellationTokenSource _cts = new();

        private string _currentMood = "Neutral";
        private string _statusText = "Initialisation de Virgil...";
        private string _cpuUsage = "0 %";
        private string _gpuUsage = "0 %";
        private string _ramUsage = "0 %";
        private string _diskUsage = "0 %";
        private string _temperature = "0 °C";
        private bool _isMonitoringActive;

        public ObservableCollection<ChatMessage> ChatMessages { get; } = new();

        // Commandes optionnelles (si tu les bindes dans XAML)
        public ICommand ToggleMonitoringCommand { get; }
        public ICommand RunMaintenanceCommand { get; }
        public ICommand CleanTempFilesCommand { get; }
        public ICommand CleanBrowsersCommand { get; }
        public ICommand UpdateAllCommand { get; }
        public ICommand RunDefenderScanCommand { get; }
        public ICommand OpenConfigCommand { get; }

        public string CurrentMood
        {
            get => _currentMood;
            set => SetProperty(ref _currentMood, value);
        }

        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        public string CpuUsage
        {
            get => _cpuUsage;
            set => SetProperty(ref _cpuUsage, value);
        }

        public string GpuUsage
        {
            get => _gpuUsage;
            set => SetProperty(ref _gpuUsage, value);
        }

        public string RamUsage
        {
            get => _ramUsage;
            set => SetProperty(ref _ramUsage, value);
        }

        public string DiskUsage
        {
            get => _diskUsage;
            set => SetProperty(ref _diskUsage, value);
        }

        public string Temperature
        {
            get => _temperature;
            set => SetProperty(ref _temperature, value);
        }

        public bool IsMonitoringActive
        {
            get => _isMonitoringActive;
            set => SetProperty(ref _isMonitoringActive, value);
        }

        public event EventHandler<string>? OnChatGenerated;

        public DashboardViewModel()
        {
            _monitor = new SystemMonitor();
            _chatService = new ChatService();
            _moodService = new MoodService();

            ToggleMonitoringCommand   = new Relay(_ => ToggleMonitoring());
            RunMaintenanceCommand     = new Relay(_ => RunMaintenance());
            CleanTempFilesCommand     = new Relay(_ => CleanTempFiles());
            CleanBrowsersCommand      = new Relay(_ => CleanBrowsers());
            UpdateAllCommand          = new Relay(_ => UpdateAll());
            RunDefenderScanCommand    = new Relay(_ => RunDefenderScan());
            OpenConfigCommand         = new Relay(_ => OpenConfiguration());

            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            AddChatMessage("Virgil est prêt à vous assister.");
            await Task.Delay(300);
            AddChatMessage("Surveillance désactivée pour le moment.");
        }

        private void AddChatMessage(string message)
        {
            if (Application.Current?.Dispatcher != null)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    ChatMessages.Add(new ChatMessage
                    {
                        Text = message,
                        Timestamp = DateTime.Now
                    });
                    OnChatGenerated?.Invoke(this, message);
                });
            }
        }

        // ==== Compat avec MainWindow : ToggleSurveillance(bool) ====
        public async void ToggleSurveillance(bool enabled)
        {
            if (enabled)
            {
                if (!IsMonitoringActive)
                {
                    IsMonitoringActive = true;
                    StatusText = "Surveillance activée.";
                    AddChatMessage("Surveillance activée.");
                    _moodService.SetMood("Focused");
                    _ = MonitorLoopAsync(_cts.Token);
                }
            }
            else
            {
                if (IsMonitoringActive)
                {
                    IsMonitoringActive = false;
                    StatusText = "Surveillance désactivée.";
                    AddChatMessage("Surveillance désactivée.");
                    _moodService.SetMood("Neutral");
                }
            }
        }

        // Version “commande” sans paramètre (si utilisée en XAML)
        private void ToggleMonitoring()
        {
            ToggleSurveillance(!IsMonitoringActive);
        }

        private async Task MonitorLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested && IsMonitoringActive)
            {
                CpuUsage = $"{_monitor.GetCpuUsage()} %";
                GpuUsage = $"{_monitor.GetGpuUsage()} %";
                RamUsage = $"{_monitor.GetRamUsage()} %";
                DiskUsage = $"{_monitor.GetDiskUsage()} %";
                Temperature = $"{_monitor.GetCpuTemperature()} °C";
                await Task.Delay(1500);
            }
        }

        // ==== Actions appelées par MainWindow (noms requis) ====
        public void RunMaintenance()
        {
            AddChatMessage("Maintenance complète lancée.");
            _chatService.SendMessage("Nettoyage en cours...");
            _moodService.SetMood("Focused");
            // TODO: Nettoyage intelligent -> Navigateurs -> Mises à jour -> Rapport
        }

        public void CleanTempFiles()
        {
            AddChatMessage("Suppression des fichiers temporaires…");
            // TODO: TEMP, Prefetch, logs, etc.
        }

        public void CleanBrowsers()
        {
            AddChatMessage("Nettoyage des navigateurs…");
            // TODO: Chrome/Edge/Firefox caches
        }

        public void UpdateAll()
        {
            AddChatMessage("Mise à jour de tous les composants…");
            // TODO: winget + WU + drivers + Defender
        }

        public void RunDefenderScan()
        {
            AddChatMessage("Analyse Defender en cours…");
            // TODO: signatures + scan rapide
        }

        public void OpenConfiguration()
        {
            AddChatMessage("Ouverture des paramètres…");
            // TODO: ouvrir SettingsWindow / JSON config
        }
    }
}
