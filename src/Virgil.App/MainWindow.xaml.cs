using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Virgil.App
{
    public partial class MainWindow : Window
    {
        private readonly DispatcherTimer _clockTimer;

        public MainWindow()
        {
            InitializeComponent();
            _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _clockTimer.Tick += (_, __) => ClockText.Text = DateTime.Now.ToString("HH:mm:ss");
            _clockTimer.Start();
            UpdateSurveillanceLabel();
        }

        // méthodes AppendChat, UpdateSurveillanceLabel, handlers des boutons…
    }
}
