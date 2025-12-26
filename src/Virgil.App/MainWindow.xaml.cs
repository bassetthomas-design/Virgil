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
        private readonly ISystemMonitorService? _systemMonitorService;
        private readonly MonitoringService? _legacyMonitoringService;
        private readonly MonitoringViewModel _monitoringVm;

        public MainWindow()
        {
            InitializeComponent();

            var chat = new ChatService();
            var settingsService = new SettingsService();
            var networkInsightService = new NetworkInsightService();

            try
            {
                _systemMonitorService = new SystemMonitorService();
                _monitoringVm = new MonitoringViewModel(_systemMonitorService, settingsService, networkInsightService);
                _legacyMonitoringService = null;
            }
            catch
            {
                // En cas d'échec (ex: compteurs/perfs indisponibles),
                // on repasse sur l'ancien service pour éviter un crash au lancement.
                _systemMonitorService = null;
                _legacyMonitoringService = new MonitoringService();
                _monitoringVm = new MonitoringViewModel(_legacyMonitoringService, settingsService, networkInsightService);
            }

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
