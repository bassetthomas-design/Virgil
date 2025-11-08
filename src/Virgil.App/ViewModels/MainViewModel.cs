using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Virgil.App.Infrastructure;
using Virgil.App.Services;

namespace Virgil.App.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly IMonitoringService _mon = new MonitoringService();
    private readonly IMaintenanceService _ops = new MaintenanceService(new ProcessRunner());
    private readonly ICleaningService _clean = new CleaningService();
    private readonly IStoreService _store = new StoreService(new ProcessRunner());
    private readonly IDriverService _drivers = new DriverService(new ProcessRunner());
    private readonly IFileLogger _log = new FileLogger();

    private string _headerStatus = "Prêt";
    private string _footerStatus = string.Empty;
    private double _cpu = 0, _ram = 0, _gpu = 0;
    private double _cpuTemp = 0;
    private Mood _mood = Mood.Sleepy;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<string> Messages { get; } = new();

    public string HeaderStatus { get => _headerStatus; set { _headerStatus = value; OnPropertyChanged(); } }
    public string FooterStatus { get => _footerStatus; set { _footerStatus = value; OnPropertyChanged(); } }
    public string Now => DateTime.Now.ToString("HH:mm:ss");

    public double CpuUsage { get => _cpu; set { _cpu = value; OnPropertyChanged(); UpdateMood(); } }
    public double RamUsage { get => _ram; set { _ram = value; OnPropertyChanged(); UpdateMood(); } }
    public double GpuUsage { get => _gpu; set { _gpu = value; OnPropertyChanged(); UpdateMood(); } }
    public double CpuTemp { get => _cpuTemp; set { _cpuTemp = value; OnPropertyChanged(); UpdateMood(); } }

    public Mood Mood { get => _mood; set { _mood = value; OnPropertyChanged(); OnPropertyChanged(nameof(MoodColor)); OnPropertyChanged(nameof(AvatarSource)); } }

    public SolidColorBrush MoodColor => MoodPalette.For(Mood);
    public System.Windows.Media.ImageSource AvatarSource => new BitmapImage(new Uri($"pack://application:,,,/assets/avatar/{MoodToFile(Mood)}"));

    public ICommand CmdMaintenanceAll   => new SimpleCommand(async () => await DoMaintenanceAll());
    public ICommand CmdWingetUpgrade    => new SimpleCommand(async () => await DoWinget());
    public ICommand CmdWindowsUpdate    => new SimpleCommand(async () => await DoWU());
    public ICommand CmdDefenderUpdate   => new SimpleCommand(async () => await DoDef());
    public ICommand CmdCleanSmart       => new SimpleCommand(async () => await DoCleanSmart());
    public ICommand CmdStoreUpdate      => new SimpleCommand(async () => await DoStore());
    public ICommand CmdDriversUpdate    => new SimpleCommand(async () => await DoDrivers());

    private static string MoodToFile(Mood m) => m switch
    {
        Mood.Happy   => "happy.png",
        Mood.Focused => "focused.png",
        Mood.Warn    => "warn.png",
        Mood.Alert   => "alert.png",
        Mood.Proud   => "proud.png",
        Mood.Tired   => "tired.png",
        Mood.Angry   => "angry.png",
        Mood.Love    => "love.png",
        Mood.Chat    => "chat.png",
        Mood.Sleepy  => "sleepy.png",
        _            => "neutral.png"
    };

    public MainViewModel()
    {
        Messages.Add("Virgil prêt. Maintenance ++.");
        _timer.Tick += (_, _) => { OnTick(); OnPropertyChanged(nameof(Now)); };
        _timer.Start();
    }

    private void OnTick()
    {
        var m = _mon.Read();
        CpuUsage = m.Cpu;
        RamUsage = m.Ram;
        CpuTemp = m.CpuTemp;
    }

    private void UpdateMood()
    {
        if (CpuTemp > 80 || CpuUsage > 90 || RamUsage > 90) Mood = Mood.Alert;
        else if (CpuUsage > 70 || RamUsage > 80) Mood = Mood.Warn;
        else if (CpuUsage > 35) Mood = Mood.Focused;
        else Mood = Mood.Happy;
    }

    private async Task DoMaintenanceAll()
    {
        Messages.Add("Mode maintenance activé."); _log.Info("Maintenance all");
        await DoCleanSmart();
        await DoWinget();
        await DoStore();
        await DoWU();
        await DoDef();
        await DoDrivers();
        Messages.Add("Maintenance terminée."); _log.Info("Maintenance done");
    }

    private async Task DoCleanSmart()
    {
        Messages.Add("Nettoyage intelligent en cours..."); _log.Info("Clean smart");
        var n = await _clean.CleanIntelligentAsync();
        Messages.Add($"Fichiers supprimés: {n}."); _log.Info($"Cleaned files: {n}");
    }

    private async Task DoStore()
    {
        Messages.Add("Store: mise à jour des apps UWP."); _log.Info("Store update");
        var code = await _store.UpdateStoreAppsAsync();
        Messages.Add(code==0 ? "Store OK." : $"Store code {code}.");
    }

    private async Task DoDrivers()
    {
        Messages.Add("Pilotes: sauvegarde + scan mise à jour."); _log.Info("Drivers backup+scan");
        var backupDir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Virgil", "drivers-backup");
        await _drivers.BackupDriversAsync(backupDir);
        var code = await _drivers.ScanAndUpdateDriversAsync();
        Messages.Add(code==0 ? "Pilotes: OK." : $"Pilotes code {code}.");
    }

    private async Task DoWinget()
    {
        Messages.Add("Applications et jeux: mise à jour."); _log.Info("Winget upgrade");
        var code = await _ops.RunWingetUpgradeAsync();
        Messages.Add(code==0 ? "Mises à jour apps/jeux OK." : $"Winget code {code}.");
    }

    private async Task DoWU()
    {
        Messages.Add("Windows Update: scan, download, install."); _log.Info("WU");
        var code = await _ops.RunWindowsUpdateAsync();
        Messages.Add(code==0 ? "Windows Update OK." : $"Windows Update code {code}.");
    }

    private async Task DoDef()
    {
        Messages.Add("Defender: signatures + scan rapide."); _log.Info("Defender");
        var code = await _ops.RunDefenderUpdateAndQuickScanAsync();
        Messages.Add(code==0 ? "Defender OK." : $"Defender code {code}.");
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
