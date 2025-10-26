using System;
using System.Collections.ObjectModel;
using System.Timers;
using System.Windows;
using System.Windows.Media;

namespace Virgil.App;

public partial class MainWindow : Window
{
    private readonly Timer _clockTimer = new(1000);
    private readonly Timer _survTimer  = new(1500);
    public bool IsMonitoring { get; set; }

    public ObservableCollection<ChatMessage> Messages { get; } = new();
    private readonly Random _rng = new();
    private DateTime _nextPunch = DateTime.MinValue;

    public MainWindow(){
        InitializeComponent();
        DataContext = this;
        ChatList.ItemsSource = Messages;

        _clockTimer.Elapsed += (_, __) => Dispatcher.Invoke(() => ClockText.Text = DateTime.Now.ToString("HH:mm:ss"));
        _clockTimer.Start();

        _survTimer.Elapsed += (_, __) => Dispatcher.Invoke(SurveillancePulse);

        Say("Salut, je suis Virgil. Prêt à surveiller la machine.", Mood.Neutral);
        PlanNextPunchline();
    }

    private void SurveillanceToggle_Checked(object sender, RoutedEventArgs e){
        IsMonitoring = true;
        (sender as System.Windows.Controls.ToggleButton)!.Content = "Arrêter la surveillance";
        _survTimer.Start();
        Say("Surveillance activée.", Mood.Happy);
    }

    private void SurveillanceToggle_Unchecked(object sender, RoutedEventArgs e){
        IsMonitoring = false;
        (sender as System.Windows.Controls.ToggleButton)!.Content = "Démarrer la surveillance";
        _survTimer.Stop();
        Say("Surveillance arrêtée.", Mood.Neutral);
    }

    private void SurveillancePulse(){
        // TODO: Récup usage/temperatures du Core et binder les barres + alertes seuils
        StatusText.Text = $"Pulse @ {DateTime.Now:HH:mm:ss}";

        if(DateTime.Now >= _nextPunch){
            Say(GetRandomPunchline(), Mood.Playful);
            PlanNextPunchline();
        }
    }

    private void PlanNextPunchline(){
        int minutes = _rng.Next(1,7); // 1 à 6 min
        _nextPunch = DateTime.Now.AddMinutes(minutes);
    }

    private string GetRandomPunchline(){
        string[] lines = new[]{
            "Je veille, je vois tout. Même ton onglet 243.",
            "Si la température monte encore, j’appelle les pompiers (beaux gosses).",
            "Winget est mon cardio. Prêt pour un upgrade total ?",
            "Je sens une poussière dans le cache GPU… *atchoum*",
            "C’est moi ou ton CPU fait des squats ?",
            "On nettoie tout ? Corbeille comprise, promis j’suis doux."
        };
        return lines[_rng.Next(lines.Length)];
    }

    public void Say(string text, Mood mood){
        Messages.Add(new ChatMessage(text, mood));
        ChatList.UpdateLayout();
    }

    private void SendButton_Click(object sender, RoutedEventArgs e){
        var msg = UserInput.Text;
        if(string.IsNullOrWhiteSpace(msg)) return;
        UserInput.Clear();
        Messages.Add(new ChatMessage(msg, Mood.User));
    }

    private void Action_MaintenanceComplete(object sender, RoutedEventArgs e){
        Say("Maintenance complète lancée (journal en cours)…", Mood.Neutral);
        // TODO: await MaintenancePresetsService.FullAsync() -> poster journal
    }

    private void Action_CleanTemp(object sender, RoutedEventArgs e){
        Say("Nettoyage TEMP en cours…", Mood.Neutral);
        // TODO: CleanTempWithProgressInternal() + progression
    }

    private void Action_CleanBrowsers(object sender, RoutedEventArgs e){
        Say("Nettoyage navigateurs en cours…", Mood.Neutral);
        // TODO: BrowserCleaningService.AnalyzeAndClean() + rapport agrégé
    }

    private void Action_UpdateAll(object sender, RoutedEventArgs e){
        Say("Mises à jour globales (apps/jeux, pilotes, Windows Update, Defender)…", Mood.Neutral);
        // TODO: UpgradeAllAsync + DriverUpdateService + WindowsUpdateService + DefenderUpdateService
    }
}

public enum Mood{ Neutral, Happy, Alert, Playful, User }

public sealed class ChatMessage{
    public string Text { get; }
    public Mood Mood { get; }
    public Brush BubbleBrush => Mood switch{
        Mood.Happy   => new SolidColorBrush(Color.FromRgb(0x54,0xC5,0x6C)),
        Mood.Alert   => new SolidColorBrush(Color.FromRgb(0xD9,0x3D,0x3D)),
        Mood.Playful => new SolidColorBrush(Color.FromRgb(0x9B,0x59,0xB6)),
        Mood.User    => new SolidColorBrush(Color.FromRgb(0x34,0x98,0xDB)),
        _            => new SolidColorBrush(Color.FromRgb(0x44,0x55,0x66)),
    };
    public ChatMessage(string text, Mood mood){ Text = text; Mood = mood; }
}
