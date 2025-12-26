// AUTO-PATCH: pass ChatService into MainViewModel ctor
using System.Threading;
using System.Windows;
using Virgil.App.Chat;
using Virgil.App.Services;
using Virgil.App.ViewModels;
using Virgil.Domain;
using Virgil.Services.Narration;

namespace Virgil.App
{
    public partial class MainWindow : Window
    {
        private readonly ISystemMonitorService _systemMonitorService;
        private readonly MonitoringViewModel _monitoringVm;

        public MainWindow()
        {
            InitializeComponent();

            var chat = new ChatService();
            var settingsService = new SettingsService();
            var networkInsightService = new NetworkInsightService();

            _systemMonitorService = new SystemMonitorService();
            _monitoringVm = new MonitoringViewModel(_systemMonitorService, settingsService, networkInsightService);

            // Services d'actions
            var systemActionsService = new SystemActionsService();
            var networkActionsService = new NetworkActionsService();
            var performanceActionsService = new PerformanceActionsService();
            var specialActionsService = new SpecialActionsService(chat, settingsService, new MonitoringService());
            var phraseService = new VirgilPhraseService();
            var narrationService = new VirgilNarrationService(chat, phraseService);

            var actionsVm = new ActionsViewModel(
                systemActionsService,
                narrationService,
                networkActionsService,
                performanceActionsService,
                specialActionsService
            );

            DataContext = new MainViewModel(chat, _monitoringVm, actionsVm);

            Loaded += (_, _) => StartMonitoring();
            Closed += (_, _) => StopMonitoring();
        }
    }
}
