using System;
using System.Windows;
using System.Windows.Threading;
using Virgil.Core; // pour Mood

namespace Virgil.App
{
    public partial class MainWindow : Window
    {
        private readonly DispatcherTimer _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;

            _clockTimer.Tick += (_, __) => ClockText.Text = DateTime.Now.ToString("HH:mm:ss");
            _clockTimer.Start();

            // Humeur initiale (affichage de l’avatar)
            AvatarControl?.SetMood(Mood.Neutral);
        }
    }
}
