using System;
using System.Windows;
using Virgil.Core;

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
        private bool _isMonitoring;

        public MainWindow()
        {
            InitializeComponent();
            _monitoringService = new MonitoringService();
            _cleaningService = new CleaningService();
            _updateService = new UpdateService();
            _monitoringService.MetricsUpdated += OnMetricsUpdated;
        }

        /// <summary>
        /// Handles the click event for the Scan & Clean button.
        /// Scans the temporary folder, reports the size and deletes it.
        /// </summary>
        private void CleanButton_Click(object sender, RoutedEventArgs e)
        {
            OutputBox.AppendText($"[{DateTime.Now:T}] Scanning temporary files…\n");
            var size = _cleaningService.GetTempFilesSize();
            var sizeMb = size / (1024.0 * 1024.0);
            OutputBox.AppendText($"[{DateTime.Now:T}] Found {sizeMb:F1} MB of temporary files.\n");
            _cleaningService.CleanTempFiles();
            OutputBox.AppendText($"[{DateTime.Now:T}] Temporary files cleaned.\n\n");
            OutputBox.ScrollToEnd();
        }

        /// <summary>
        /// Handles the click event for the Check Updates button.
        /// Invokes winget to upgrade all installed packages and displays the result.
        /// </summary>
        private async void UpdateButton_Click(object sender, RoutedEventArgs e)
        {
            OutputBox.AppendText($"[{DateTime.Now:T}] Checking for updates via winget…\n");
            var result = await _updateService.UpgradeAllAsync();
            OutputBox.AppendText(result + "\n\n");
            OutputBox.ScrollToEnd();
        }

        /// <summary>
        /// Handles the click event for the Start/Stop Monitoring button.
        /// Starts or stops periodic sampling of CPU and memory usage.
        /// </summary>
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

        private void OnMetricsUpdated(object? sender, EventArgs e)
        {
            var metrics = _monitoringService.LatestMetrics;
            // Ensure UI updates occur on the dispatcher thread
            Dispatcher.Invoke(() =>
            {
                OutputBox.AppendText($"CPU: {metrics.CpuUsage:F1}%  Memory: {metrics.MemoryUsage:F1}%\n");
                OutputBox.ScrollToEnd();
            });
        }
    }
}
