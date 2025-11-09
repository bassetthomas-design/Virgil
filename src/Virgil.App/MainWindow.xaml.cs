using System.Windows;
using Virgil.App.ViewModels;
using Virgil.App.Services;

namespace Virgil.App;

public partial class MainWindow : Window
{
    public MainViewModel VM { get; }

    public MainWindow()
    {
        InitializeComponent();
        var monitoring = new MonitoringViewModel(new MonitoringService());
        VM = new MainViewModel(monitoring);
        DataContext = VM;
    }

    private void ToggleSurveillance_Click(object sender, RoutedEventArgs e)
    {
        if (VM.Monitoring.IsRunning){ VM.Monitoring.Stop(); ToggleSurveillance.Content = "Surveillance: OFF"; }
        else { VM.Monitoring.Start(); ToggleSurveillance.Content = "Surveillance: ON"; }
    }
}
