using System;
using System.Threading.Tasks;
using System.Windows;
using Virgil.Core.Services;
using Virgil.Core.Logging;

namespace Virgil.App;

public partial class MainWindow : Window
{
    private readonly CleaningService _cleaning = new();
    private readonly ApplicationUpdateService _apps = new();
    private readonly WindowsUpdateService _wu = new();
    private readonly DriverUpdateService _drivers = new();
    private readonly DefenderUpdateService _def = new();
    private readonly MaintenancePresetsService _presets = new();
    private readonly BrowserCleaningService _browsers = new();

    private async void Action_MaintenanceComplete(object sender, RoutedEventArgs e)
    {
        ActionProgress.Visibility = Visibility.Visible;
        Say("Maintenance complète lancée (journal en cours)…", Mood.Neutral);
        try{
            var log = await _presets.FullAsync();
            await LogService.AppendAsync("Maintenance complète", log);
            Say(log, Mood.Neutral);
            StatusText.Text = "Maintenance complète terminée";
        }
        finally{ ActionProgress.Visibility = Visibility.Collapsed; }
    }

    private async void Action_CleanTemp(object sender, RoutedEventArgs e)
    {
        ActionProgress.Visibility = Visibility.Visible;
        Say("Nettoyage TEMP en cours…", Mood.Neutral);
        try{
            var log = await _cleaning.CleanTempAsync();
            await LogService.AppendAsync("Nettoyage TEMP", log);
            Say(log, Mood.Neutral);
            StatusText.Text = "Nettoyage TEMP terminé";
        }
        finally{ ActionProgress.Visibility = Visibility.Collapsed; }
    }

    private async void Action_CleanBrowsers(object sender, RoutedEventArgs e)
    {
        ActionProgress.Visibility = Visibility.Visible;
        Say("Nettoyage navigateurs en cours…", Mood.Neutral);
        try{
            var report = await _browsers.AnalyzeAndCleanAsync();
            await LogService.AppendAsync("Nettoyage navigateurs", report);
            Say(report, Mood.Neutral);
            StatusText.Text = "Nettoyage navigateurs terminé";
        }
        finally{ ActionProgress.Visibility = Visibility.Collapsed; }
    }

    private async void Action_UpdateAll(object sender, RoutedEventArgs e)
    {
        ActionProgress.Visibility = Visibility.Visible;
        Say("Mises à jour globales (apps/jeux, pilotes, Windows Update, Defender)…", Mood.Neutral);
        try{
            var a = await _apps.UpgradeAllAsync();
            var s = await _wu.StartScanAsync();
            var d = await _wu.StartDownloadAsync();
            var i = await _wu.StartInstallAsync();
            var r = await _drivers.UpgradeDriversAsync();
            var m = await _def.UpdateSignaturesAsync();
            var q = await _def.QuickScanAsync();
            var log = a + "
" + s + d + i + "
" + r + "
" + m + "
" + q;
            await LogService.AppendAsync("Mises à jour globales", log);
            Say(log, Mood.Neutral);
            StatusText.Text = "Mises à jour globales terminées";
        }
        finally{ ActionProgress.Visibility = Visibility.Collapsed; }
    }
}
