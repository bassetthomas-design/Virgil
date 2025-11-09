using System;
using System.Windows;
using System.Windows.Threading;

namespace Virgil.App;

public partial class MainWindow : Window
{
    private readonly DispatcherTimer _clock = new(){ Interval = TimeSpan.FromSeconds(1)};
    private bool _surveillanceOn;

    public MainWindow()
    {
        InitializeComponent();
        _clock.Tick += (_,_) => TopClock.Text = DateTime.Now.ToString("HH:mm:ss");
        _clock.Start();
    }

    private void BtnToggle_Click(object sender, RoutedEventArgs e)
    {
        _surveillanceOn = !_surveillanceOn;
        if (BtnToggle != null) BtnToggle.Content = _surveillanceOn ? "Surveillance: ON" : "Surveillance: OFF";
        // Le hook vers MonitoringService sera ajouté dans une PR séparée.
    }
}
