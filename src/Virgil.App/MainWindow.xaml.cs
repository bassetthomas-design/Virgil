using System;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Linq; // requis pour OrderBy/ThenBy/Any/Take
using Virgil.Core;
using Virgil.App.Controls;

namespace Virgil.App
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly MonitoringService _monitoringService;
        private readonly CleaningService _cleaningService;
        private readonly UpdateService _updateService;
        private readonly ApplicationUpdateService _appUpdateService;
        private readonly DriverUpdateService _driverUpdateService;
        private readonly StartupManager _startupManager;
        private readonly ProcessService _processService;
        private readonly BrowserCleaningService _browserCleaningService;
        private readonly WindowsUpdateService _windowsUpdateService;
        private readonly MaintenancePresetsService _presetsService;
        private readonly ServiceManager _serviceManager;
        private readonly MoodService _moodService;
        private readonly DialogService _dialogService;
        private readonly VirgilAvatarViewModel _avatarViewModel;
        private bool _isMonitoring;

        public MainWindow()
        {
            InitializeComponent();
            _monitoringService = new MonitoringService();
            _cleaningService = new CleaningService();
            _updateService = new UpdateService();
            _appUpdateService = new ApplicationUpdateService();
            _driverUpdateService = new DriverUpdateService();
            _startupManager = new StartupManager();
            _processService = new ProcessService();
            _browserCleaningService = new BrowserCleaningService();
            _windowsUpdateService = new WindowsUpdateService();
            _moodService = new MoodService();
            _dialogService = new DialogService();
            _avatarViewModel = new VirgilAvatarViewModel(_moodService, _dialogService);
            _serviceManager = new ServiceManager();
            _presetsService = new MaintenancePresetsService(_cleaningService, _browserCleaningService, _appUpdateService, _driverUpdateService, _windowsUpdateService);

            // Bind the avatar control to its view model
            AvatarControl.DataContext = _avatarViewModel;

            // Subscribe to monitoring events
            _monitoringService.MetricsUpdated += OnMetricsUpdated;

            // Display a greeting on startup
            var greeting = _dialogService.GetRandomMessage("startup");
            _avatarViewModel.Message = greeting;
            OutputBox.AppendText($"[{DateTime.Now:T}] {greeting}\n");
            LoggingService.LogInfo("Application started.");
        }

        private void CleanButton_Click(object sender, RoutedEventArgs e)
        {
            OutputBox.AppendText($"[{DateTime.Now:T}] Scanning temporary files...\n");
            LoggingService.LogInfo("Scanning temporary files.");
            var size = _cleaningService.GetTempFilesSize();
            var sizeMb = size / (1024.0 * 1024.0);
            OutputBox.AppendText($"[{DateTime.Now:T}] Found {sizeMb:F1} MB of temporary files.\n");
            _cleaningService.CleanTempFiles();
            OutputBox.AppendText($"[{DateTime.Now:T}] Temporary files cleaned.\n\n");
            LoggingService.LogInfo($"Temporary files cleaned ({sizeMb:F1} MB).");
            _avatarViewModel.SetMood(Mood.Proud, "clean_success");
            OutputBox.ScrollToEnd();
        }

        private async void UpdateButton_Click(object sender, RoutedEventArgs e)
        {
            OutputBox.AppendText($"[{DateTime.Now:T}] Checking for updates via winget...\n");
            LoggingService.LogInfo("Checking for updates via winget.");
            var result = await _updateService.UpgradeAllAsync();
            OutputBox.AppendText(result + "\n\n");
            LoggingService.LogInfo("Winget update completed.");
            _avatarViewModel.SetMood(Mood.Proud, "update_success");
            OutputBox.ScrollToEnd();
        }

        private void MonitorButton_Click(object sender, RoutedEventArgs e)
        {
            _isMonitoring = !_isMonitoring;
            if (_isMonitoring)
            {
                _monitoringService.Start();
                MonitorButton.Content = "Stop Monitoring";
                OutputBox.AppendText($"[{DateTime.Now:T}] Monitoring started.\n");
            }
            else
            {
                _monitoringService.Stop();
                MonitorButton.Content = "Start Monitoring";
                OutputBox.AppendText($"[{DateTime.Now:T}] Monitoring stopped.\n\n");
            }
            OutputBox.ScrollToEnd();
        }

        // NOTE: enlever le '?' ici pour éviter l'erreur nullable en CI
        private void OnMetricsUpdated(object sender, EventArgs e)
        {
            var metrics = _monitoringService.LatestMetrics;
            Dispatcher.Invoke(() =>
            {
                OutputBox.AppendText($"CPU: {metrics.CpuUsage:F1}%  Memory: {metrics.MemoryUsage:F1}%  Disk: {metrics.DiskUsage:F1}%\n");
                if (metrics.CpuUsage > 90 || metrics.MemoryUsage > 90 || metrics.DiskUsage > 90)
                    _moodService.CurrentMood = Mood.Alert;
                else if (metrics.CpuUsage > 75 || metrics.MemoryUsage > 75 || metrics.DiskUsage > 75)
                    _moodService.CurrentMood = Mood.Vigilant;
                else
                    _moodService.CurrentMood = Mood.Neutral;

                OutputBox.ScrollToEnd();
            });
        }

        private async void AppUpdateButton_Click(object sender, RoutedEventArgs e)
        {
            OutputBox.AppendText($"[{DateTime.Now:T}] Updating applications...\n");
            LoggingService.LogInfo("Updating applications...");
            var result = await _appUpdateService.UpdateAllApplicationsAsync();
            OutputBox.AppendText(result + "\n\n");
            LoggingService.LogInfo("Application update completed.");
            _avatarViewModel.SetMood(Mood.Proud, "update_success");
            OutputBox.ScrollToEnd();
        }

        private async void DriverUpdateButton_Click(object sender, RoutedEventArgs e)
        {
            OutputBox.AppendText($"[{DateTime.Now:T}] Updating drivers...\n");
            LoggingService.LogInfo("Updating drivers...");
            var result = await _driverUpdateService.UpdateDriversAsync();
            OutputBox.AppendText(result + "\n\n");
            LoggingService.LogInfo("Driver update completed.");
            _avatarViewModel.SetMood(Mood.Proud, "driver_update_success");
            OutputBox.ScrollToEnd();
        }

        private void BrowserCleanButton_Click(object sender, RoutedEventArgs e)
        {
            OutputBox.AppendText($"[{DateTime.Now:T}] Cleaning browser caches...\n");
            LoggingService.LogInfo("Cleaning browser caches...");
            var result = _browserCleaningService.CleanBrowserCaches();
            OutputBox.AppendText(result + "\n\n");
            LoggingService.LogInfo("Browser cleaning completed.");
            _avatarViewModel.SetMood(Mood.Proud, "browser_clean_success");
            OutputBox.ScrollToEnd();
        }

        private async void WindowsUpdateButton_Click(object sender, RoutedEventArgs e)
        {
            OutputBox.AppendText($"[{DateTime.Now:T}] Updating Windows...\n");
            LoggingService.LogInfo("Updating Windows...");
            var result = await _windowsUpdateService.UpdateWindowsAsync();
            OutputBox.AppendText(result + "\n\n");
            LoggingService.LogInfo("Windows update completed.");
            _avatarViewModel.SetMood(Mood.Proud, "windows_update_success");
            OutputBox.ScrollToEnd();
        }

        private void ServicesButton_Click(object sender, RoutedEventArgs e)
        {
            OutputBox.AppendText($"[{DateTime.Now:T}] Listing services...\n");
            LoggingService.LogInfo("Listing services...");
            var services = _serviceManager.ListServices();
            foreach (var svc in services)
            {
                OutputBox.AppendText($"{svc.ServiceName}: {svc.Status}\n");
            }
            _avatarViewModel.SetMood(Mood.Neutral, "service_list");
            OutputBox.AppendText("\n");
            OutputBox.ScrollToEnd();
        }

        private async void MaintenanceButton_Click(object sender, RoutedEventArgs e)
        {
            OutputBox.AppendText($"[{DateTime.Now:T}] Running full maintenance...\n");
            LoggingService.LogInfo("Running full maintenance...");
            var result = await _presetsService.RunFullMaintenanceAsync();
            OutputBox.AppendText(result + "\n\n");
            LoggingService.LogInfo("Full maintenance completed.");
            _avatarViewModel.SetMood(Mood.Proud, "maintenance_complete");
            OutputBox.ScrollToEnd();
        }

        // === Added Handlers ===

        private void StartupButton_Click(object sender, RoutedEventArgs e)
        {
            OutputBox.AppendText($"[{DateTime.Now:T}] Listing startup items...\n");
            LoggingService.LogInfo("Listing startup items...");

            // Extension method ListItems() (voir fichier CompatibilityExtensions)
            var items = _startupManager.ListItems();
            foreach (var it in items)
            {
                OutputBox.AppendText($"{it.Name}  [{it.Location}]  {(it.Enabled ? "Enabled" : "Disabled")}\n");
            }

            _avatarViewModel.SetMood(Mood.Neutral, "startup_list");
            OutputBox.ScrollToEnd();
        }

        private async void ProcessesButton_Click(object sender, RoutedEventArgs e)
        {
            OutputBox.AppendText($"[{DateTime.Now:T}] Listing processes...\n");
            LoggingService.LogInfo("Listing processes...");

            // Extension method ListAsync() (voir fichier CompatibilityExtensions)
            var processes = await _processService.ListAsync();

            foreach (var p in processes
                            .OrderByDescending(p => p.CpuUsage)
                            .ThenByDescending(p => p.MemoryMb)
                            .Take(30))
            {
                OutputBox.AppendText(
                    $"{p.Name,-28} PID:{p.Pid,6}  CPU:{p.CpuUsage,5:F1}%  MEM:{p.MemoryMb,6:F0} MB\n");
            }

            var heavy = processes.Any(p => p.CpuUsage > 75 || p.MemoryMb > 1500);
            _avatarViewModel.SetMood(heavy ? Mood.Alert : Mood.Neutral,
                                     heavy ? "high_usage_detected" : "process_list");

            OutputBox.ScrollToEnd();
        }
    }
}