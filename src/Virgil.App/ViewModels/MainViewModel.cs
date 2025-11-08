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
using Virgil.App.Views;

namespace Virgil.App.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly IMonitoringService _mon = new MonitoringService();
    private readonly IMaintenanceService _ops = new MaintenanceService(new ProcessRunner());
    private readonly ICleaningService _clean = new CleaningService();
    private readonly IStoreService _store = new StoreService(new ProcessRunner());
    private readonly IDriverService _drivers = new DriverService(new ProcessRunner());
    private readonly ISettingsService _settings = new JsonSettingsService();
    private readonly ISystemActionsService _sys = new SystemActionsService();
    private readonly IFileLogger _log = new FileLogger();
    private readonly IJsonLogger _jlog = new JsonFileLogger();

    private string _headerStatus = "Prêt";
    private string _footerStatus = string.Empty;
    private double _cpu = 0, _ram = 0;
    private double _cpuTemp = 0;
    private Mood _mood = Mood.Sleepy;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<string> Messages { get; } = new();

    public string HeaderStatus { get => _headerStatus; set { _headerStatus = value; OnPropertyChanged(); } }
    public string FooterStatus { get => _footerStatus; set { _footerStatus = value; OnPropertyChanged(); } }
    public string Now => DateTime.Now.ToString("HH:mm:ss");

    public double CpuUsage { get => _cpu; set { _cpu = value; OnPropertyChanged(); UpdateMood(); } }
    public double RamUsage { get => _ram; set { _ram = value; OnPropertyChanged(); UpdateMood(); } }
    public double CpuTemp { get => _cpuTemp; set { _cpuTemp = value; OnPropertyChanged(); UpdateMood(); } }

    public Mood Mood { get => _mood; set { _mood = value; OnPropertyChanged(); OnPropertyChanged(nameof(MoodColor)); OnPropertyChanged(nameof(AvatarSource)); } }

    public SolidColorBrush MoodColor => MoodPalette.For(Mood);
    public System.Windows.Media.ImageSource AvatarSource => new BitmapImage(new Uri($"pack://application:,,,/assets/avatar/{MoodToFile(Mood)}"));

    public ICommand CmdMaintenanceAll   => new SimpleCommand(async () => await DoMaintenanceAll());
    public ICommand CmdCleanSmart       => new SimpleCommand(async () => await DoCleanSmart());
    public ICommand CmdCleanAdvanced    => new SimpleCommand(async () => await DoCleanAdvanced());
    public ICommand CmdOpenOptions      => new SimpleCommand(() => OpenOptions());
    public ICommand CmdWsReset          => new SimpleCommand(async () => await DoWsReset());
    public ICommand CmdRebuildCaches    => new SimpleCommand(async () => await DoRebuildCaches());
    public ICommand CmdEmptyRecycleBin  => new SimpleCommand(async () => await DoEmptyRecycleBin());

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
        Messages.Add("Virgil prêt. Options + actions système.");
        _timer.Tick += (_, _) => { OnTick(); OnPropertyChanged(nameof(Now)); };
        _timer.Start();
    }

    private void OnTick(){ var m=_mon.Read(); CpuUsage=m.Cpu; RamUsage=m.Ram; CpuTemp=m.CpuTemp; }
    private void UpdateMood(){ if (CpuTemp>80||CpuUsage>90||RamUsage>90) Mood=Mood.Alert; else if (CpuUsage>70||RamUsage>80) Mood=Mood.Warn; else if (CpuUsage>35) Mood=Mood.Focused; else Mood=Mood.Happy; }

    private async Task DoMaintenanceAll(){ await DoCleanSmart(); await DoWinget(); await DoStore(); await DoWU(); await DoDef(); await DoDrivers(); }
    private async Task DoCleanSmart(){ var n = await _clean.CleanIntelligentAsync(); Messages.Add($"Fichiers supprimés: {n}."); _jlog.Write(new { op="clean", files=n }); }
    private async Task DoCleanAdvanced(){ var opt = await _settings.LoadAsync(); var s = await _clean.CleanAdvancedAsync(opt); Messages.Add($"Avancé: fichiers {s.Files}, dossiers {s.Dirs}, ~{s.Bytes/1024/1024} Mo."); _jlog.Write(new { op="clean-advanced", files=s.Files, dirs=s.Dirs, bytes=s.Bytes }); }

    private void OpenOptions(){ var w = new OptionsWindow(); w.ShowDialog(); }
    private async Task DoWsReset(){ var code = await _sys.WsResetAsync(); Messages.Add(code==0?"Store reset OK.":$"wsreset code {code}."); }
    private async Task DoRebuildCaches(){ var code = await _sys.RebuildExplorerCachesAsync(); Messages.Add(code==0?"Caches Explorer nettoyés.":$"Rebuild caches code {code}."); }
    private async Task DoEmptyRecycleBin(){ var code = await _sys.EmptyRecycleBinAsync(); Messages.Add(code==0?"Corbeille vidée.":$"RecycleBin code {code}."); }

    private async Task DoStore(){ var code = await _store.UpdateStoreAppsAsync(); Messages.Add(code==0?"Store OK.":$"Store code {code}."); }
    private async Task DoDrivers(){ var b=System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"Virgil","drivers-backup"); await _drivers.BackupDriversAsync(b); var code=await _drivers.ScanAndUpdateDriversAsync(); Messages.Add(code==0?"Pilotes: OK.":$"Pilotes code {code}."); }
    private async Task DoWinget(){ var code = await _ops.RunWingetUpgradeAsync(); Messages.Add(code==0?"Mises à jour apps/jeux OK.":$"Winget code {code}."); }
    private async Task DoWU(){ var code = await _ops.RunWindowsUpdateAsync(); Messages.Add(code==0?"Windows Update OK.":$"Windows Update code {code}."); }
    private async Task DoDef(){ var code = await _ops.RunDefenderUpdateAndQuickScanAsync(); Messages.Add(code==0?"Defender OK.":$"Defender code {code}."); }

    private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
