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
        // Pas de bouton ToggleSurveillance dans le XAML courant. On ne démarre pas automatiquement.
    }

    // Si on veut un toggle plus tard, on liera une commande dans le VM.
}
