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

        // Fields to store services for monitoring toggle

        private readonly MonitoringService _monitoringService;

        private readonly SettingsService _settingsService;



        public MainShell()

        {

            InitializeComponent();



            // Initialize services

            var chat = new ChatService();

            var monitoringService = new MonitoringService();

            var settingsService = new SettingsService();

            var networkInsightService = new NetworkInsightService();



            // Assign to fields for later use

            _monitoringService = monitoringService;

            _settingsService = settingsService;



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



            // Initialize monitoring toggle button state based on settings

            if (_settingsService.Settings.MonitoringEnabled)

            {

                // Ensure monitoring is started and update button content

                _monitoringService.Start();

                MonitoringTogglButton.Content = "Désactiver la surveillance";

            }

            else

            {

                // Ensure monitoring is stopped and update button content

                _monitoringService.Stop();

                MonitoringTogglButton.Content = "Activer la surveillance";

            }

        }





        /// <summary>

        /// Handler for the settings button declared in MainShell.xaml.
        /// Opens the configuration window when implemented.
        /// </summary>
        private void OnOpenSettings(object sender, RoutedEventArgs e)
        {
            var dlg = new SettingsWindow(_settingsService)
            {
                Owner = this
            };

            var result = dlg.ShowDialog();

            if (result == true && _settingsService.Settings.MonitoringEnabled)
            {
                _monitoringService.Start();
                MonitoringTogglButton.Content = "Désactiver la surveillance";
            }
            else
            {
                _monitoringService.Stop();
                MonitoringTogglButton.Content = "Activer la surveillance";
            }
        }




        /// <summary>

        /// Handler for the HUD toggle button declared in MainShell.xaml.

        /// </summary>

        private void OnHudToggled(object sender, RoutedEventArgs e)

        {

            // TODO: implement HUD toggle logic (show/hide mini HUD) if required.

        }




        /// <summary>

        /// Handler for the monitoring toggle button declared in MainShell.xaml.

        /// Starts or stops monitoring and updates settings and UI accordingly.

        /// </summary>

        private void OnMonitoringToggled(object sender, RoutedEventArgs e)

        {

            bool enabled = _settingsService.Settings.MonitoringEnabled;

            if (enabled)

            {

                // Disable monitoring

                _monitoringService.Stop();

                _settingsService.Settings.MonitoringEnabled = false;

                MonitoringTogglButton.Content = "Activer la surveillance";

            }

            else

            {

                // Enable monitoring

                _monitoringService.Start();

                _settingsService.Settings.MonitoringEnabled = true;

                MonitoringTogglButton.Content = "Désactiver la surveillance";

            }

            // Persist the updated setting

            _settingsService.Save();

        }

    }

}
