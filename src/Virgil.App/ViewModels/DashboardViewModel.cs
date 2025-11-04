using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Virgil.App.Models;
using Virgil.App.Services;
using Virgil.Core;

namespace Virgil.App.ViewModels
{
    public partial class DashboardViewModel : BaseViewModel
    {
        private readonly SystemMonitor _monitor;
        private readonly ChatService _chatService;
        private readonly MoodService _moodService;
        private readonly CancellationTokenSource _cts = new();
        private readonly Random _random = new();

        private string _currentMood = "Neutral";
        private string _statusText = "Initialisation de Virgil...";
        private string _cpuUsage = "0 %";
        private string _gpuUsage = "0 %";
        private string _ramUsage = "0 %";
        private string _diskUsage = "0 %";
        private string _temperature = "0 °C";
        private bool _isMonitoringActive;

        public ObservableCollection<ChatMessage> ChatMessages { get; } = new();

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

        public DashboardViewModel()
        {
            _monitor = new SystemMonitor();
            _chatService = new ChatService();
            _moodService = new MoodService();

            ToggleMonitoringCommand = new Relay(_ => ToggleMonitoring());
            RunMaintenanceCommand = new Relay(_ => RunMaintenance());
            CleanTempFilesCommand = new Relay(_ => CleanTempFiles());
            CleanBrowsersCommand = new Relay(_ => CleanBrowsers());
            UpdateAllCommand = new Relay(_ => UpdateAll());
            RunDefenderScanCommand = new Relay(_ => RunDefenderScan());
            OpenConfigCommand = new Relay(_ => OpenConfiguration());

            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            AddChatMessage("Virgil est prêt à vous assister.");
            await Task.Delay(1000);
            AddChatMessage("Surveillance désactivée pour le moment.");
        }

        private void AddChatMessage(string message)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                ChatMessages.Add(new ChatMessage
                {
                    Text = message,
                    Timestamp = DateTime.Now
                });
            });
        }

        private async void ToggleMonitoring()
        {
            if (IsMonitoringActive)
            {
                IsMonitoringActive = false;
                AddChatMessage("Surveillance désactivée.");
                return;
            }

            IsMonitoringActive = true;
            AddChatMessage("Surveillance activée.");
            await MonitorLoopAsync(_cts.Token);
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

        private void RunMaintenance()
        {
            AddChatMessage("Maintenance complète lancée.");
            _chatService.SendMessage("Nettoyage en cours...");
            _moodService.SetMood("Focused");
        }

        private void CleanTempFiles()
        {
            AddChatMessage("Suppression des fichiers temporaires...");
        }

        private void CleanBrowsers()
        {
            AddChatMessage("Nettoyage des navigateurs...");
        }

        private void UpdateAll()
        {
            AddChatMessage("Mise à jour de tous les composants...");
        }

        private void RunDefenderScan()
        {
            AddChatMessage("Analyse Defender en cours...");
        }

        private void OpenConfiguration()
        {
            AddChatMessage("Ouverture des paramètres...");
        }

        public event EventHandler<string>? OnChatGenerated;
    }
}