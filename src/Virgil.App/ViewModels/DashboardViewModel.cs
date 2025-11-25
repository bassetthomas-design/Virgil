using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using Virgil.App.Services;
using Virgil.Services.Narration;

namespace Virgil.App.ViewModels
{
    /// <summary>
    /// ViewModel principal du dashboard (gros boutons + état global).
    /// La logique des actions est répartie dans le fichier partiel DashboardViewModel.Actions.cs.
    /// </summary>
    public partial class DashboardViewModel : BaseViewModel
    {
        private readonly SystemActionsService _systemActions;
        private readonly VirgilNarrationService _virgil;

        private bool _isSurveillanceEnabled;
        public bool IsSurveillanceEnabled
        {
            get => _isSurveillanceEnabled;
            set => Set(ref _isSurveillanceEnabled, value);
        }

        /// <summary>
        /// Flux de messages textuels affichés dans le chat du dashboard.
        /// </summary>
        public ObservableCollection<string> Chat { get; } = new();

        // Commands bindées dans le XAML
        public ICommand ToggleSurveillanceCmd { get; }
        public ICommand RunMaintenanceCmd { get; }
        public ICommand CleanTempFilesCmd { get; }
        public ICommand CleanBrowsersCmd { get; }
        public ICommand UpdateAllCmd { get; }
        public ICommand RunDefenderScanCmd { get; }
        public ICommand OpenConfigurationCmd { get; }

        public DashboardViewModel(SystemActionsService systemActions, VirgilNarrationService virgil)
        {
            _systemActions = systemActions ?? throw new ArgumentNullException(nameof(systemActions));
            _virgil = virgil ?? throw new ArgumentNullException(nameof(virgil));

            ToggleSurveillanceCmd  = new RelayCommand(_ => ToggleSurveillance());
            RunMaintenanceCmd      = new RelayCommand(_ => RunMaintenance());
            CleanTempFilesCmd      = new RelayCommand(_ => CleanTempFiles());
            CleanBrowsersCmd       = new RelayCommand(_ => CleanBrowsers());
            UpdateAllCmd           = new RelayCommand(_ => UpdateAll());
            RunDefenderScanCmd     = new RelayCommand(_ => RunDefenderScan());
            OpenConfigurationCmd   = new RelayCommand(_ => OpenConfiguration());
        }

        /// <summary>
        /// Helper centralisé pour pousser un message dans le chat,
        /// en respectant le thread d'UI WPF.
        /// </summary>
        public void AppendChat(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return;

            if (Application.Current?.Dispatcher is { } d && !d.CheckAccess())
            {
                d.Invoke(() => Chat.Add(message));
            }
            else
            {
                Chat.Add(message);
            }
        }
    }
}
