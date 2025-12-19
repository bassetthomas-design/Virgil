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

        public Task<ProcessResult?> MaintenanceCompleteAsync() => RunPsAsync("maintenance_complete.ps1");
        public Task<ProcessResult?> SmartCleanupAsync() => RunPsAsync("cleanup_smart.ps1");
        public Task<ProcessResult?> CleanBrowsersAsync() => RunPsAsync("clean_browsers.ps1");
        public Task<ProcessResult?> UpdateAllAsync() => RunPsAsync("update_all.ps1");
        public Task<ProcessResult?> DefenderUpdateAndScanAsync() => RunPsAsync("defender_update_scan.ps1");
        public Task<ProcessResult?> OpenConfigAsync() => RunPsAsync("open_config.ps1");

        private static async Task<ProcessResult?> RunPsAsync(string script)
        {
            Directory.CreateDirectory(ScriptsDir);
            var path = Path.Combine(ScriptsDir, script);
            if (!File.Exists(path)) return null;
            var args = $"-ExecutionPolicy Bypass -File '{path}'";
            var res = await ProcessRunner.RunAsync("powershell.exe", args);
            return res;
        }
    }
}
