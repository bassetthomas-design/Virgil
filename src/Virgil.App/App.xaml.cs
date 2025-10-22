#nullable enable
using System.Windows;
using Serilog.Events;
using Virgil.Core.Services;

namespace Virgil.App
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            LoggingService.Init(LogEventLevel.Information);
            var _ = new ConfigService(); // force création dossiers + charge config
        }

        protected override void OnExit(ExitEventArgs e)
        {
            Serilog.Log.CloseAndFlush();
            base.OnExit(e);
        }
    }
}
