using System;
using System.Windows;
using System.Windows.Threading;

namespace Virgil.App
{
    public partial class MainWindow : Window
    {
        private readonly DispatcherTimer _clockTimer = new DispatcherTimer();

        public MainWindow()
        {
            InitializeComponent();   // ← unique endroit !
            Loaded += MainWindow_Loaded;
            Unloaded += MainWindow_Unloaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Horloge en haut à droite si le TextBlock existe dans le XAML (x:Name="ClockText")
            _clockTimer.Interval = TimeSpan.FromSeconds(1);
            _clockTimer.Tick += (s, _) =>
            {
                try { ClockText.Text = DateTime.Now.ToString("HH:mm:ss"); } catch { /* ignore si absent */ }
            };
            _clockTimer.Start();
        }

        private void MainWindow_Unloaded(object sender, RoutedEventArgs e)
        {
            try { _clockTimer.Stop(); } catch { }
        }
    }
}
