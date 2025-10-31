using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Virgil.App
{
    public partial class MainWindow : Window
    {
        private readonly DispatcherTimer _clockTimer = new DispatcherTimer();

        public MainWindow()
        {
            InitializeComponent();

            // Branche les handlers sur le UserControl (évite les Click dans le XAML)
            UI.SurveillanceToggle.Checked   += SurveillanceToggle_Checked;
            UI.SurveillanceToggle.Unchecked += SurveillanceToggle_Unchecked;

            UI.BtnMaintenance.Click   += Action_MaintenanceComplete;
            UI.BtnCleanTemp.Click     += Action_CleanTemp;
            UI.BtnCleanBrowsers.Click += Action_CleanBrowsers;
            UI.BtnUpdateAll.Click     += Action_UpdateAll;
            UI.BtnDefender.Click      += Action_Defender;
            UI.BtnOpenConfig.Click    += OpenConfig_Click;

            // Horloge
            _clockTimer.Interval = TimeSpan.FromSeconds(1);
            _clockTimer.Tick += (s, e) => UI.ClockText.Text = DateTime.Now.ToString("HH:mm:ss");
            _clockTimer.Start();
        }
    }
}
