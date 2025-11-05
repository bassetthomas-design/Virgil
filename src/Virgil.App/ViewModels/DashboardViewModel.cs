using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;

namespace Virgil.App.ViewModels
{
    // ⚠️ important: partial, pour séparer la logique d’actions dans un autre fichier
    public partial class DashboardViewModel : BaseViewModel
    {
        private bool _isSurveillanceEnabled;
        public bool IsSurveillanceEnabled
        {
            get => _isSurveillanceEnabled;
            set => Set(ref _isSurveillanceEnabled, value);
        }

        // Chat (bulle texte dans l’UI)
        public ObservableCollection<string> Chat { get; } = new();

        // Commands exposées à la MainWindow / XAML
        public ICommand ToggleSurveillanceCmd { get; }
        public ICommand RunMaintenanceCmd { get; }
        public ICommand CleanTempFilesCmd { get; }
        public ICommand CleanBrowsersCmd { get; }
        public ICommand UpdateAllCmd { get; }
        public ICommand RunDefenderScanCmd { get; }
        public ICommand OpenConfigurationCmd { get; }

        public DashboardViewModel()
        {
            ToggleSurveillanceCmd  = new RelayCommand(_ => ToggleSurveillance());
            RunMaintenanceCmd      = new RelayCommand(_ => RunMaintenance());
            CleanTempFilesCmd      = new RelayCommand(_ => CleanTempFiles());
            CleanBrowsersCmd       = new RelayCommand(_ => CleanBrowsers());
            UpdateAllCmd           = new RelayCommand(_ => UpdateAll());
            RunDefenderScanCmd     = new RelayCommand(_ => RunDefenderScan());
            OpenConfigurationCmd   = new RelayCommand(_ => OpenConfiguration());
        }

        // Helper centralisé pour pousser un message dans le chat
        public void AppendChat(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return;
            if (Application.Current?.Dispatcher is { } d && !d.CheckAccess())
                d.Invoke(() => Chat.Add(message));
            else
                Chat.Add(message);
        }
    }
}
