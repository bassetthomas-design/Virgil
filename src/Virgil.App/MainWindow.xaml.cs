using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Virgil.App.Chat;
using Virgil.App.Interfaces;
using Virgil.App.Models;
using Virgil.App.Services;
using Virgil.App.ViewModels;
using Virgil.Services;
using Virgil.Services.Abstractions;
using ChatUiService = Virgil.App.Chat.ChatService;

namespace Virgil.App
{
    public partial class MainWindow : Window, IUiInteractionService
    {
        private readonly MonitoringService _monitoringService;
        private readonly SettingsService _settingsService;
        private readonly ChatUiService _chatService;
        private readonly IActionOrchestrator _orchestrator;
        private readonly IConfirmationService _confirmationService;
        private readonly DispatcherTimer _clockTimer;
        private Window? _miniHudWindow;

        public MainWindow()
        {
            InitializeComponent();

            _chatService = new ChatUiService();
            _settingsService = new SettingsService();
            _monitoringService = new MonitoringService();
            var networkInsightService = new NetworkInsightService();

            var monitoringVm = new MonitoringViewModel(
                _monitoringService,
                _settingsService,
                networkInsightService);

            var uiChat = new UiChatServiceAdapter(_chatService);
            _orchestrator = new ActionOrchestrator(
                new CleanupService(),
                new UpdateService(),
                new NetworkService(),
                new PerformanceService(),
                new DiagnosticService(),
                new SpecialService(),
                uiChat);

            _confirmationService = new ConfirmationService();

            var mainVm = new MainViewModel(
                _chatService,
                monitoringVm,
                _orchestrator,
                _monitoringService,
                _settingsService,
                this,
                _confirmationService);

            DataContext = mainVm;

            _clockTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _clockTimer.Tick += (_, _) => TopClock.Text = DateTime.Now.ToString("HH:mm:ss");
            _clockTimer.Start();

            Loaded += async (_, _) => await mainVm.InitializeAsync();
            Closed += (_, _) => _clockTimer.Stop();
        }

        public Task<ActionResult> OpenSettingsAsync(CancellationToken ct)
        {
            return Dispatcher.InvokeAsync(() =>
            {
                var dlg = new SettingsWindow(_settingsService)
                {
                    Owner = this
                };

                var result = dlg.ShowDialog();
                if (result == true && _settingsService.Settings.MonitoringEnabled)
                {
                    _monitoringService.Start();
                }
                else
                {
                    _monitoringService.Stop();
                }

                _settingsService.Save();
                return ActionResult.Completed("Paramètres mis à jour");
            }).Task;
        }

        public Task<ActionResult> ShowHudAsync(CancellationToken ct)
        {
            return Dispatcher.InvokeAsync(() =>
            {
                if (_miniHudWindow is { IsVisible: true })
                {
                    return ActionResult.Completed("HUD déjà affiché");
                }

                var vm = DataContext as MainViewModel;
                _miniHudWindow = new Window
                {
                    Owner = this,
                    Title = "Virgil — Mini HUD",
                    Width = 240,
                    Height = 180,
                    Content = new Views.MiniHud { DataContext = vm?.Monitoring },
                    WindowStyle = WindowStyle.ToolWindow,
                    ResizeMode = ResizeMode.NoResize,
                    Topmost = true,
                    ShowInTaskbar = false
                };

                _miniHudWindow.Closed += (_, _) => _miniHudWindow = null;
                _miniHudWindow.Show();
                _settingsService.Settings.ShowMiniHud = true;
                _settingsService.Save();
                return ActionResult.Completed("Mini HUD affiché");
            }).Task;
        }

        public Task<ActionResult> HideHudAsync(CancellationToken ct)
        {
            return Dispatcher.InvokeAsync(() =>
            {
                if (_miniHudWindow is { IsVisible: true })
                {
                    _miniHudWindow.Close();
                    _miniHudWindow = null;
                }

                _settingsService.Settings.ShowMiniHud = false;
                _settingsService.Save();
                return ActionResult.Completed("Mini HUD masqué");
            }).Task;
        }
    }
}
