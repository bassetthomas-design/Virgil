using System;
using System.Windows.Input;
using Virgil.App.Core;
using Virgil.App.Services;
using Virgil.Services;
using Virgil.Services.Narration;

namespace Virgil.App.ViewModels
{
    public partial class DashboardViewModel : BaseViewModel
    {
        private readonly SystemActionsService _systemActions;
        private readonly VirgilNarrationService _narration;
        private readonly ChatService _chat;

        public DashboardViewModel(
            SystemActionsService systemActions,
            VirgilNarrationService narration,
            ChatService chat)
        {
            _systemActions = systemActions ?? throw new ArgumentNullException(nameof(systemActions));
            _narration = narration ?? throw new ArgumentNullException(nameof(narration));
            _chat = chat ?? throw new ArgumentNullException(nameof(chat));

            RunMaintenanceCmd = new RelayCommand(_ => RunMaintenance());
            SmartCleanupCmd = new RelayCommand(_ => RunSmartCleanup());
            CleanBrowsersCmd = new RelayCommand(_ => RunBrowsersCleanup());
            UpdateAllCmd = new RelayCommand(_ => RunUpdateAll());
            RunDefenderScanCmd = new RelayCommand(_ => RunDefenderScan());
            OpenConfigurationCmd = new RelayCommand(_ => OpenConfiguration());
        }

        public ICommand RunMaintenanceCmd { get; }
        public ICommand SmartCleanupCmd { get; }
        public ICommand CleanBrowsersCmd { get; }
        public ICommand UpdateAllCmd { get; }
        public ICommand RunDefenderScanCmd { get; }
        public ICommand OpenConfigurationCmd { get; }

        private void AppendChat(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            _chat.AppendSystemMessage(text);
        }
    }
}
