using System;
using System.Windows.Input;
using Virgil.App.Core;
using Virgil.App.Services;
using Virgil.Services.Narration;

namespace Virgil.App.ViewModels
{
    public partial class DashboardViewModel : BaseViewModel
    {
        private readonly SystemActionsService _systemActions;
        private readonly VirgilNarrationService _narration;

        public DashboardViewModel(SystemActionsService systemActions, VirgilNarrationService narration)
        {
            _systemActions = systemActions ?? throw new ArgumentNullException(nameof(systemActions));
            _narration     = narration     ?? throw new ArgumentNullException(nameof(narration));

            RunMaintenanceCmd    = new RelayCommand(_ => RunMaintenance());
            SmartCleanupCmd      = new RelayCommand(_ => RunSmartCleanup());
            CleanBrowsersCmd     = new RelayCommand(_ => RunBrowsersCleanup());
            UpdateAllCmd         = new RelayCommand(_ => RunUpdateAll());
            RunDefenderScanCmd   = new RelayCommand(_ => RunDefenderScan());
            OpenConfigurationCmd = new RelayCommand(_ => OpenConfiguration());
        }

        public ICommand RunMaintenanceCmd    { get; }
        public ICommand SmartCleanupCmd      { get; }
        public ICommand CleanBrowsersCmd     { get; }
        public ICommand UpdateAllCmd         { get; }
        public ICommand RunDefenderScanCmd   { get; }
        public ICommand OpenConfigurationCmd { get; }
    }
}
