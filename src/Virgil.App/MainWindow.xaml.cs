using System;
using System.Windows;
using System.Windows.Threading;

namespace Virgil.App
{
    public partial class MainWindow : Window
    {
        private readonly DispatcherTimer _clockTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };

        public MainWindow()
        {
            InitializeComponent(); // L’InitializeComponent généré par WPF (ne pas re-définir)

            // Horloge
            _clockTimer.Tick += (_, __) => ClockText.Text = DateTime.Now.ToString("HH:mm:ss");
            _clockTimer.Start();

            // Texte du toggle surveillance au démarrage
            Resources["SurveillanceToggleText"] = "Démarrer la surveillance";
        }
    }
}
