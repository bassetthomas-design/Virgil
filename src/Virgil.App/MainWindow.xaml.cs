using System;
using System.Windows;
using System.Windows.Threading;

namespace Virgil.App
{
    public partial class MainWindow : Window
    {
        private readonly DispatcherTimer _uiTimer = new DispatcherTimer();

        public MainWindow()
        {
            InitializeComponent(); // NE PAS enlever
            Loaded += OnLoaded;

            _uiTimer.Interval = TimeSpan.FromSeconds(1);
            _uiTimer.Tick += (s, e) => ClockText.Text = DateTime.Now.ToString("HH:mm:ss");
            _uiTimer.Start();
        }

        private void OnLoaded(object? sender, RoutedEventArgs e)
        {
            // Init au chargement de la fenêtre (si nécessaire)
        }
    }
}
