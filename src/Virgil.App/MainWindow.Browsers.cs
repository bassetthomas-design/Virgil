using System;
using System.Threading.Tasks;
using System.Windows;
using Virgil.Core.Services;

namespace Virgil.App;

public partial class MainWindow : Window
{
    private readonly BrowserCleaningService _browsers = new();
    private readonly ExtendedCleaningService _extended = new();

    private async void Action_CleanBrowsers(object sender, RoutedEventArgs e)
    {
        Say("Nettoyage navigateurs en cours…", Mood.Neutral);
        var report = await _browsers.AnalyzeAndCleanAsync();
        Say(report, Mood.Neutral);
        StatusText.Text = "Nettoyage navigateurs terminé";
    }
}
