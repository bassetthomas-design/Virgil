using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Virgil.App.ViewModels
{
    // On complète la classe existante SANS la remplacer
    public partial class DashboardViewModel
    {
        // Hooks optionnels vers ton moteur / services (si tu veux brancher plus tard)
        public Func<bool, Task>? ToggleSurveillanceAsyncHook { get; set; }
        public Func<Task>? RunMaintenanceAsyncHook { get; set; }
        public Func<Task>? CleanTempFilesAsyncHook { get; set; }
        public Func<Task>? CleanBrowsersAsyncHook { get; set; }
        public Func<Task>? UpdateAllAsyncHook { get; set; }
        public Func<Task>? RunDefenderScanAsyncHook { get; set; }
        public Func<Task>? OpenConfigurationAsyncHook { get; set; }

        // Les méthodes attendues par MainWindow.xaml.cs
        public void ToggleSurveillance(bool isOn)
        {
            Debug.WriteLine($"[VM] ToggleSurveillance({isOn})");
            _ = (ToggleSurveillanceAsyncHook?.Invoke(isOn) ?? Task.CompletedTask);
        }

        public void RunMaintenance()
        {
            Debug.WriteLine("[VM] RunMaintenance()");
            _ = (RunMaintenanceAsyncHook?.Invoke() ?? Task.CompletedTask);
        }

        public void CleanTempFiles()
        {
            Debug.WriteLine("[VM] CleanTempFiles()");
            _ = (CleanTempFilesAsyncHook?.Invoke() ?? Task.CompletedTask);
        }

        public void CleanBrowsers()
        {
            Debug.WriteLine("[VM] CleanBrowsers()");
            _ = (CleanBrowsersAsyncHook?.Invoke() ?? Task.CompletedTask);
        }

        public void UpdateAll()
        {
            Debug.WriteLine("[VM] UpdateAll()");
            _ = (UpdateAllAsyncHook?.Invoke() ?? Task.CompletedTask);
        }

        public void RunDefenderScan()
        {
            Debug.WriteLine("[VM] RunDefenderScan()");
            _ = (RunDefenderScanAsyncHook?.Invoke() ?? Task.CompletedTask);
        }

        public void OpenConfiguration()
        {
            Debug.WriteLine("[VM] OpenConfiguration()");
            _ = (OpenConfigurationAsyncHook?.Invoke() ?? Task.CompletedTask);
        }
    }
}