using System;
using System.Collections.ObjectModel;
using System.Timers;
using System.Windows;
using Virgil.Core.Config;
using Virgil.Core.Logging;
using Virgil.Domain;
using Virgil.Core;\r\nusing Virgil.Core.Config;
using Virgil.Services;
namespace Virgil.App {
  public partial class MainWindow : Window {
    private readonly IMaintenanceService _maint = new MaintenanceService();
    private readonly IUpdateService _update = new UpdateService();
    private readonly ObservableCollection<string> _chat = new();
        private VirgilConfig _cfg = VirgilConfig.Load();
        private System.Timers.Timer? _punchTimer;
    public MainWindow(){
      InitializeComponent();
            InitializeConfigRuntime(); ChatList.ItemsSource=_chat;
      var t=new Timer(1000); t.Elapsed+=(_,__)=>Dispatcher.Invoke(()=>ClockText.Text=DateTime.Now.ToString(""HH:mm:ss"")); t.Start();
      BtnCleanSmart.Click += async (_,__) => {
        Post(""Nettoyage intelligent en cours..."");
        var res = await _maint.CleanAsync(CleanLevel.Complete,new Progress<string>(Post));
        Post($""PC nettoyé. {res.BytesFreed/(1024*1024)} Mo libérés."");
      };
      BtnUpdateAll.Click += async (_,__) => {
        Post(""Je prépare les mises à jour système..."");
        var upd = await _update.UpdateAllAsync(new Progress<string>(Post));
        Post(""Tout est à jour, je me sens rajeuni."");
      };
    }
    private void Post(string m)=>_chat.Add($""[{DateTime.Now:HH:mm:ss}] {m}"");
  }
        private void InitializeConfigRuntime()
        {
            try
            {
                _cfg = VirgilConfig.Load();
                ApplyTheme(_cfg.Theme);
                Log.Info($"App démarrée. Thème: {_cfg.Theme}, Ton: {_cfg.Tone}, PunchFreq: {_cfg.PunchlineFrequency}s");

                _punchTimer = new System.Timers.Timer(Math.Max(5, _cfg.PunchlineFrequency) * 1000);
                _punchTimer.Elapsed += (_,__) => Dispatcher.Invoke(() => {
                    var line = PunchlineEngine.GetRandom(_cfg.Tone);
                    Post(line);
                    Log.Info($"Punchline: {line}");
                });
                _punchTimer.AutoReset = true;
                _punchTimer.Start();
            }
            catch (Exception ex)
            {
                Log.Error("InitializeConfigRuntime error: " + ex.Message);
            }
        }

        private void ApplyTheme(string theme)
        {
            try
            {
                var dark = theme?.Equals("dark", StringComparison.OrdinalIgnoreCase) ?? true;
                var bg = (System.Windows.Media.SolidColorBrush)FindResource("BackgroundBrush");
                var fg = (System.Windows.Media.SolidColorBrush)FindResource("ForegroundBrush");
                bg.Color = dark ? System.Windows.Media.Color.FromRgb(0x11,0x11,0x11) : System.Windows.Media.Color.FromRgb(0xF7,0xF7,0xF7);
                fg.Color = dark ? System.Windows.Media.Color.FromRgb(0xFF,0xFF,0xFF) : System.Windows.Media.Color.FromRgb(0x11,0x11,0x11);
            }
            catch (Exception ex)
            {
                Log.Error("ApplyTheme error: " + ex.Message);
            }
        }
        private void InitializeConfigRuntime()
        {
            try
            {
                _cfg = VirgilConfig.Load();
                ApplyTheme(_cfg.Theme);
                Log.Info($"App démarrée. Thème: {_cfg.Theme}, Ton: {_cfg.Tone}, PunchFreq: {_cfg.PunchlineFrequency}s");

                _punchTimer = new System.Timers.Timer(Math.Max(5, _cfg.PunchlineFrequency) * 1000);
                _punchTimer.Elapsed += (_,__) => Dispatcher.Invoke(() => {
                    var line = PunchlineEngine.GetRandom(_cfg.Tone);
                    Post(line);
                    Log.Info($"Punchline: {line}");
                });
                _punchTimer.AutoReset = true;
                _punchTimer.Start();
            }
            catch (Exception ex)
            {
                Log.Error("InitializeConfigRuntime error: " + ex.Message);
            }
        }

        private void ApplyTheme(string theme)
        {
            try
            {
                var dark = theme?.Equals("dark", StringComparison.OrdinalIgnoreCase) ?? true;
                var bg = (System.Windows.Media.SolidColorBrush)FindResource("BackgroundBrush");
                var fg = (System.Windows.Media.SolidColorBrush)FindResource("ForegroundBrush");
                bg.Color = dark ? System.Windows.Media.Color.FromRgb(0x11,0x11,0x11) : System.Windows.Media.Color.FromRgb(0xF7,0xF7,0xF7);
                fg.Color = dark ? System.Windows.Media.Color.FromRgb(0xFF,0xFF,0xFF) : System.Windows.Media.Color.FromRgb(0x11,0x11,0x11);
            }
            catch (Exception ex)
            {
                Log.Error("ApplyTheme error: " + ex.Message);
            }
        }
}

