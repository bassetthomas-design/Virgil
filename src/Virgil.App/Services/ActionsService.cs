using System;
using System.IO;
using System.Threading.Tasks;

namespace Virgil.App.Services
{
    public class ActionsService
    {
        private static string ScriptsDir => Path.Combine(AppContext.BaseDirectory, "scripts");
        public bool SurveillanceOn { get; private set; }

        public Task ToggleSurveillanceAsync(Func<Task> onStart, Func<Task> onStop)
        {
            if (!SurveillanceOn) { SurveillanceOn = true; return onStart(); }
            SurveillanceOn = false; return onStop();
        }

        public Task MaintenanceCompleteAsync() => RunPsAsync("maintenance_complete.ps1");
        public Task SmartCleanupAsync() => RunPsAsync("smart_cleanup.ps1");
        public Task CleanBrowsersAsync() => RunPsAsync("clean_browsers.ps1");
        public Task UpdateAllAsync() => RunPsAsync("update_all.ps1");
        public Task DefenderUpdateAndScanAsync() => RunPsAsync("defender_update_scan.ps1");
        public Task OpenConfigAsync() => RunPsAsync("open_config.ps1");

        private static Task RunPsAsync(string script)
        {
            Directory.CreateDirectory(ScriptsDir);
            var path = Path.Combine(ScriptsDir, script);
            if (!File.Exists(path)) return Task.CompletedTask;
            return ProcessRunner.RunAsync("powershell.exe", $"-ExecutionPolicy Bypass -File ""{path}""");
        }
    }
}
