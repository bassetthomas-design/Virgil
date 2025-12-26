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
        private Window? _miniHudWindow;



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
            var specialActionsService = new SpecialActionsService(chat, settingsService, monitoringService);

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
                MonitoringToggleButton.Content = "Désactiver la surveillance";
            }
            else
            {
                // Ensure monitoring is stopped and update button content
                _monitoringService.Stop();
                MonitoringToggleButton.Content = "Activer la surveillance";
            }

            UpdateHudToggleUi();

            if (_settingsService.Settings.ShowMiniHud)
            {
                OnHudToggled(this, new RoutedEventArgs());
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
            if (_miniHudWindow is { IsVisible: true })
            {
                _miniHudWindow.Close();
                _miniHudWindow = null;
                _settingsService.Settings.ShowMiniHud = false;
            }
            else
            {
                var vm = DataContext as MainViewModel;
                _miniHudWindow = new Window
                {
                    Owner = this,
                    Title = "Virgil — Mini HUD",
                    Width = 240,
                    Height = 180,
                    Content = new MiniHud { DataContext = vm?.Monitoring },
                    WindowStyle = WindowStyle.ToolWindow,
                    ResizeMode = ResizeMode.NoResize,
                    Topmost = true,
                    ShowInTaskbar = false
                };

                _miniHudWindow.Closed += (_, _) => _miniHudWindow = null;
                _miniHudWindow.Show();
                _settingsService.Settings.ShowMiniHud = true;
            }

            _settingsService.Save();
            UpdateHudToggleUi();
        }

        private void UpdateHudToggleUi()
        {
            if (_settingsService.Settings.ShowMiniHud)
            {
                HudToggleButton.Content = "Masquer HUD";
                HudToggleButton.ToolTip = "Fermer le mini HUD";
            }
            else
            {
                HudToggleButton.Content = "Mini HUD";
                HudToggleButton.ToolTip = "Afficher le mini HUD";
            }
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

                MonitoringToggleButton.Content = "Activer la surveillance";

            }

            else

            {

                // Enable monitoring

                _monitoringService.Start();

                _settingsService.Settings.MonitoringEnabled = true;

                MonitoringToggleButton.Content = "Désactiver la surveillance";

            }

            // Persist the updated setting

            _settingsService.Save();

        }

    }

}
