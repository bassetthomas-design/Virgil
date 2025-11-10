using System;
using System.IO;
using System.Threading.Tasks;

namespace Virgil.App.Services
{
    public class SystemActionsService
    {
        private static string ScriptsDir => Path.Combine(AppContext.BaseDirectory, "scripts");

        public Task RunMaintenanceAsync() => RunPsAsync("maintenance_complete.ps1");
        public Task RunSmartCleanupAsync() => RunPsAsync("smart_cleanup.ps1");
        public Task RunCleanBrowsersAsync() => RunPsAsync("clean_browsers.ps1");
        public Task RunUpdateAllAsync() => RunPsAsync("update_all.ps1");
        public Task RunDefenderAsync() => RunPsAsync("defender_update_scan.ps1");
        public Task OpenConfigAsync() => RunPsAsync("open_config.ps1");

        private static Task RunPsAsync(string script)
        {
            Directory.CreateDirectory(ScriptsDir);
            var path = Path.Combine(ScriptsDir, script);
            if (!File.Exists(path)) return Task.CompletedTask;
            var args = $"-ExecutionPolicy Bypass -File '{path}'";
            return ProcessRunner.RunAsync("powershell.exe", args);
        }
    }
}
