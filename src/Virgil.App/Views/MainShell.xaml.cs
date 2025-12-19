using System.Windows;
using System;
using System.Windows.Threading;

using Virgil.App.Chat;
using Virgil.App.Services;
using Virgil.App.ViewModels;
using Virgil.Domain;
using Virgil.Services.Narration;

namespace Virgil.App.Views
{
    /// <summary>
    /// Minimal code-behind for the main shell window.
    /// This version is intentionally simplified on the dev branch so that
    /// the application can compile cleanly while the chat services layer
    /// is being refactored.
    /// </summary>
    public partial class MainShell : Window
    {
        public MainShell()
        {
            InitializeComponent();

            // Initialize services
            var chat = new ChatService();
            var monitoringService = new MonitoringService();
            var settingsService = new SettingsService();
            var networkInsightService = new NetworkInsightService();

            // Services d'actions
            var systemActionsService = new SystemActionsService();
            var networkActionsService = new NetworkActionsService();
            var performanceActionsService = new PerformanceActionsService();
            var specialActionsService = new SpecialActionsService();
            var phraseService = new VirgilPhraseService();
            var narrationService = new VirgilNarrationService(chat, phraseService);

            // Create monitoring ViewModel
            var monitoringVm = new MonitoringViewModel(
                monitoringService,
                settingsService,
                networkInsightService
            );

            // Create actions ViewModel
            var actionsVm = new ActionsViewModel(
                systemActionsService,
                narrationService,
                networkActionsService,
                performanceActionsService,
                specialActionsService
            );

            // Create and set the main ViewModel as DataContext
            DataContext = new MainViewModel(chat, monitoringVm, actionsVm);
                        // Initialize real-time clock
            var clockTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            clockTimer.Tick += (s, e) =>
            {
                ClockTextBlock.Text = DateTime.Now.ToString("HH:mm:ss");
            };
            clockTimer.Start();
        }


        /// <summary>
        /// Placeholder handler for the settings button declared in MainShell.xaml.
        /// Hooked up to keep the dev branch build green; the real behaviour can be
        /// reintroduced later from the previous implementation.
        /// </summary>
        private void OnOpenSettings(object sender, RoutedEventArgs e)
        {
            // TODO: wire to settings window when the UX flow is finalized again.
        }

        /// <summary>
        /// Placeholder handler for the HUD toggle button declared in MainShell.xaml.
        /// </summary>
        private void OnHudToggled(object sender, RoutedEventArgs e)
        {
            // TODO: implement HUD toggle logic (show/hide mini HUD) if required.
        }
    }
}
