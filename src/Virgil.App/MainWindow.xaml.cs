using System;
using System.Windows;
using System.Windows.Threading;
using Virgil.App.ViewModels;

namespace Virgil.App;

public partial class MainWindow : Window
{
    private readonly DispatcherTimer _clock = new(){ Interval = TimeSpan.FromSeconds(1)};
    public MainViewModel VM { get; } = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = VM;
        _clock.Tick += (_,_) => TopClock.Text = DateTime.Now.ToString("HH:mm:ss");
        _clock.Start();

        // message d’accueil pour vérifier le chat
        VM.Say("Salut, prêt pour bosser ?");
        VM.Progress("Initialisation", 25);
    }
}
