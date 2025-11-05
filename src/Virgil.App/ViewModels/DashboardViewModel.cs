using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Virgil.App.ViewModels
{
    // NOTE : On reste en 'internal' pour être au moins aussi accessible que BaseViewModel (souvent internal).
    internal partial class DashboardViewModel : BaseViewModel
    {
        // -------- ÉTAT --------
        private bool _isSurveillanceEnabled;
        public bool IsSurveillanceEnabled
        {
            get => _isSurveillanceEnabled;
            set => Set(ref _isSurveillanceEnabled, value);
        }

        private string _status = "Prêt.";
        public string Status
        {
            get => _status;
            set => Set(ref _status, value);
        }

        public ObservableCollection<string> ChatMessages { get; } = new();

        // -------- COMMANDES (liées au XAML / MainWindow.xaml.cs) --------
        public ICommand ToggleSurveillanceCommand { get; }
        public ICommand RunMaintenanceCommand     { get; }
        public ICommand CleanTempFilesCommand     { get; }
        public ICommand CleanBrowsersCommand      { get; }
        public ICommand UpdateAllCommand          { get; }
        public ICommand RunDefenderScanCommand    { get; }
        public ICommand OpenConfigurationCommand  { get; }

        public DashboardViewModel()
        {
            // Ces méthodes sont implémentées dans le fichier partiel .Actions.cs
            ToggleSurveillanceCommand = new RelayCommand(ToggleSurveillance);
            RunMaintenanceCommand     = new RelayCommand(RunMaintenanceAsync);
            CleanTempFilesCommand     = new RelayCommand(CleanTempFilesAsync);
            CleanBrowsersCommand      = new RelayCommand(CleanBrowsersAsync);
            UpdateAllCommand          = new RelayCommand(UpdateAllAsync);
            RunDefenderScanCommand    = new RelayCommand(RunDefenderScanAsync);
            OpenConfigurationCommand  = new RelayCommand(OpenConfiguration);
        }

        // -------- Utilitaire chat (présent UNE SEULE FOIS) --------
        protected void AppendChat(string text)
        {
            ChatMessages.Add($"[{DateTime.Now:HH:mm:ss}] {text}");
            Raise(nameof(ChatMessages));
        }
    }
}
