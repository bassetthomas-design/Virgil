using System;
using System.Windows;
using Virgil.Core;
using Virgil.App.Controls;

namespace Virgil.App
{
    public partial class MainWindow : Window
    {
        private readonly MonitoringService _monitoringService;
        private readonly CleaningService _cleaningService;
        private readonly ApplicationUpdateService _appUpdateService;
        private readonly DriverUpdateService _driverUpdateService;
        private readonly BrowserCleaningService _browserCleaningService;
        private readonly WindowsUpdateService _windowsUpdateService;
        private readonly StartupManager _startupManager;
        private readonly ProcessService _processService;
        private readonly ServiceManager _serviceManager;
        private readonly MaintenancePresetsService _maintenancePresetsService;
        private readonly DialogService _dialogService;
        private readonly MoodService _moodService;
        private readonly LoggingService _loggingService;
        private readonly VirgilAvatarViewModel _avatarViewModel;
        private bool _isMonitoring;

        public MainWindow()
        {
            InitializeComponent();

            _monitoringService = new MonitoringService();
            _cleaningService = new CleaningService();
            _appUpdateService = new ApplicationUpdateService();
            _driverUpdateService = new DriverUpdateService();
            _browserCleaningService = new BrowserCleaningService();
            _windowsUpdateService = new WindowsUpdateService();
            _startupManager = new StartupManager();
            _processService = new ProcessService();
            _serviceManager = new ServiceManager();
            _maintenancePresetsService = new MaintenancePresetsService();
            _dialogService = new DialogService();
            _moodService = new MoodService();
            _loggingService = new LoggingService();

            _avatarViewModel = new VirgilAvatarViewModel();
            AvatarControl.DataContext = _avatarViewModel;

            _monitoringService.MetricsUpdated += OnMetricsUpdated;

            var greeting = _dialogService.GetRandomMessage("startup");
            _avatarViewModel.Message = greeting;
            _avatarViewModel.Color = _moodService.GetColor(Mood.Neutral);
            OutputBox.AppendText($"[{DateTime.Now:T}] {greeting}\n");
            _loggingService.LogInfo("Application started.");
        }

        private void UpdateMood(Mood mood, string? category = null)
        {
            _avatarViewModel.Color = _moodService.GetColor(mood);
            if (category != null)
            {
                _avatarViewModel.Message = _dialogService.GetRandomMessage(category);
            }
        }

        private void LogAndAppend(string message)
        {
            OutputBox.AppendText(message + "\n");
            OutputBox.ScrollToEnd();
            _loggingService.LogInfo(message);
        }

        private void CleanButton_Click(object sender, RoutedEventArgs e)
        {
            UpdateMood(Mood.Vigilant);
            LogAndAppend($"[{DateTime.Now:T}] Scanning temporary files...");
            var size = _cleaningService.GetTempFilesSize();
            var mb = size / (1024.0 * 1024.0);
            LogAndAppend($"[{DateTime.Now:T}] Found {mb:F1} MB of temporary files.");
            _cleaningService.CleanTempFiles();
            LogAndAppend($"[{DateTime.Now:T}] Temporary files cleaned.\n");
            UpdateMood(Mood.Proud, "clean_success");
        }

        private async void AppUpdateButton_Click(object sender, RoutedEventArgs e)
        {
            UpdateMood(Mood.Vigilant);
            LogAndAppend($"[{DateTime.Now:T}] Updating applications and games...");
            var result = await _appUpdateService.UpdateAllApplicationsAsync();
            LogAndAppend(result + "\n");
            UpdateMood(Mood.Proud, "update_success");
        }

        private async void DriverUpdateButton_Click(object sender, RoutedEventArgs e)
        {
            UpdateMood(Mood.Vigilant);
            LogAndAppend($"[{DateTime.Now:T}] Updating drivers...");
            var result = await _driverUpdateService.UpdateDriversAsync();
            LogAndAppend(result + "\n");
            UpdateMood(Mood.Proud, "driver_update_success");
        }

        private void BrowserCleanButton_Click(object sender, RoutedEventArgs e)
        {
            UpdateMood(Mood.Vigilant);
            LogAndAppend($"[{DateTime.Now:T}] Cleaning browser caches...");
            _browserCleaningService.CleanBrowserCaches();
            LogAndAppend($"[{DateTime.Now:T}] Browser caches cleaned.\n");
            UpdateMood(Mood.Proud, "browser_clean_success");
        }

        private async void WindowsUpdateButton_Click(object sender, RoutedEventArgs e)
        {
            UpdateMood(Mood.Vigilant);
            LogAndAppend($"[{DateTime.Now:T}] Running Windows Update...");
            var result = await _windowsUpdateService.UpdateWindowsAsync();
            LogAndAppend(result + "\n");
            UpdateMood(Mood.Proud, "windows_update_success");
        }

        private void StartupButton_Click(object sender, RoutedEventArgs e)
        {
            UpdateMood(Mood.Neutral);
            LogAndAppend($"[{DateTime.Now:T}] Listing startup programs...");
            var list = _startupManager.GetStartupPrograms();
            foreach (var item in list)
            {
                LogAndAppend("  " + item);
            }
            LogAndAppend(string.Empty);
            UpdateMood(Mood.Neutral, "startup_list");
        }

        private void ProcessesButton_Click(object sender, RoutedEventArgs e)
        {
            UpdateMood(Mood.Neutral);
            LogAndAppend($"[{DateTime.Now:T}] Listing processes...");
            var procs = _processService.ListProcesses();
            foreach (var p in procs)
            {
                LogAndAppend("  " + p);
            }
            LogAndAppend(string.Empty);
            UpdateMood(Mood.Neutral, "process_list");
        }

        private void ServicesButton_Click(object sender, RoutedEventArgs e)
        {
            UpdateMood(Mood.Neutral);
            LogAndAppend($"[{DateTime.Now:T}] Listing services...");
            var list = _serviceManager.ListServices();
            foreach (var svc in list)
            {
                LogAndAppend($"  {svc.DisplayName} - {svc.Status}");
            }
            LogAndAppend(string.Empty);
            UpdateMood(Mood.Neutral, "service_list");
        }

        private async void MaintenanceButton_Click(object sender, RoutedEventArgs e)
        {
            UpdateMood(Mood.Vigilant);
            LogAndAppend($"[{DateTime.Now:T}] Running full maintenance...");
            var summary = await _maintenancePresetsService.RunFullMaintenanceAsync();
            LogAndAppend(summary + "\n");
            UpdateMood(Mood.Proud, "maintenance_complete");
        }

        private void MonitorButton_Click(object sender, RoutedEventArgs e)
        {
            _isMonitoring = !_isMonitoring;
            if (_isMonitoring)
            {
                _monitoringService.Start();
                MonitorButton.Content = "Stop Monitoring";
                UpdateMood(Mood.Vigilant);
                LogAndAppend($"[{DateTime.Now:T}] Monitoring started.");
            }
            else
            {
                _monitoringService.Stop();
                MonitorButton.Content = "Start Monitoring";
                UpdateMood(Mood.Neutral);
                LogAndAppend($"[{DateTime.Now:T}] Monitoring stopped.");
            }
        }

        private void OnMetricsUpdated(object? sender, EventArgs e)
        {
            var metrics = _monitoringService.LatestMetrics;
            Dispatcher.Invoke(() =>
            {
                LogAndAppend($"CPU: {metrics.CpuUsage:F1}%  Mem: {metrics.MemoryUsage:F1}%  Disk: {metrics.DiskUsage:F1}%");
                if (metrics.CpuUsage > 90 || metrics.MemoryUsage > 90)
                {
                    UpdateMood(Mood.Alert, "alert_temp");
                }
                else if (metrics.CpuUsage > 80 || metrics.MemoryUsage > 80)
                {
                    UpdateMood(Mood.Vigilant);
                }
                else
                {
                    UpdateMood(Mood.Neutral);
                }
            });
        }
    }
}
