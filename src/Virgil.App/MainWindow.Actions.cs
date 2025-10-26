using System;
using System.Threading.Tasks;
using System.Windows;
using Virgil.Core.Services;

namespace Virgil.App;

public partial class MainWindow : Window
{
    private readonly CleaningService _cleaning = new();
    private readonly ApplicationUpdateService _apps = new();
    private readonly WindowsUpdateService _wu = new();
    private readonly DriverUpdateService _drivers = new();
    private readonly DefenderUpdateService _def = new();
    private readonly MaintenancePresetsService _presets = new();

    private async void Action_MaintenanceComplete(object sender, RoutedEventArgs e)
    {
        Say("Maintenance complète lancée (journal en cours)…", Mood.Neutral);
        var log = await _presets.FullAsync();
        Say(log, Mood.Neutral);
        StatusText.Text = "Maintenance complète terminée";
    }

    private async void Action_CleanTemp(object sender, RoutedEventArgs e)
    {
        Say("Nettoyage TEMP en cours…", Mood.Neutral);
        var log = await _cleaning.CleanTempAsync();
        Say(log, Mood.Neutral);
        StatusText.Text = "Nettoyage TEMP terminé";
    }

    private async void Action_CleanBrowsers(object sender, RoutedEventArgs e)
    {
        Say("Nettoyage navigateurs en cours…", Mood.Neutral);
        // TODO: implémenter BrowserCleaningService (profils Chromium/Firefox)
        Say("[TODO] BrowserCleaningService en cours d’implémentation", Mood.Neutral);
    }

    private async void Action_UpdateAll(object sender, RoutedEventArgs e)
    {
        Say("Mises à jour globales (apps/jeux, pilotes, Windows Update, Defender)…", Mood.Neutral);
        var a = await _apps.UpgradeAllAsync();
        var s = await _wu.StartScanAsync();
        var d = await _wu.StartDownloadAsync();
        var i = await _wu.StartInstallAsync();
        var r = await _drivers.UpgradeDriversAsync();
        var m = await _def.UpdateSignaturesAsync();
        var q = await _def.QuickScanAsync();
        Say(a + "
" + s + d + i + "
" + r + "
" + m + "
" + q, Mood.Neutral);
        StatusText.Text = "Mises à jour globales terminées";
    }
}
