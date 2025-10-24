using System;
using System.Diagnostics;
using System.Text;

namespace Virgil.Core.Services
{
    /// <summary>
    /// Gestion minimale de tâches planifiées via schtasks.exe (compatible par défaut).
    /// Permet de créer/supprimer une tâche à l'ouverture de session ou quotidienne.
    /// </summary>
    public sealed class ScheduledTaskService
    {
        public string CreateLogonTask(string taskName, string exePath, string arguments = "")
        {
            // schtasks /Create /TN "Virgil\QuickClean" /TR "C:\...\Virgil.App.exe --quick-clean" /SC ONLOGON /RL HIGHEST
            var cmd = $" /Create /TN \"{taskName}\" /TR \"\\\"{exePath}\\\" {arguments}\" /SC ONLOGON /RL HIGHEST /F";
            return RunSchtasks(cmd);
        }

        public string CreateDailyTask(string taskName, string exePath, string timeHHmm = "09:00", string arguments = "")
        {
            // schtasks /Create /TN "Virgil\DailyClean" /TR "C:\...\Virgil.App.exe --full-maintenance" /SC DAILY /ST 09:00 /RL HIGHEST
            var cmd = $" /Create /TN \"{taskName}\" /TR \"\\\"{exePath}\\\" {arguments}\" /SC DAILY /ST {timeHHmm} /RL HIGHEST /F";
            return RunSchtasks(cmd);
        }

        public string DeleteTask(string taskName)
        {
            // schtasks /Delete /TN "Virgil\DailyClean" /F
            var cmd = $" /Delete /TN \"{taskName}\" /F";
            return RunSchtasks(cmd);
        }

        public string QueryTask(string taskName)
        {
            // schtasks /Query /TN "Virgil\DailyClean" /V /FO LIST
            var cmd = $" /Query /TN \"{taskName}\" /V /FO LIST";
            return RunSchtasks(cmd);
        }

        private static string RunSchtasks(string args)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "schtasks.exe",
                    Arguments = args,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                var sb = new StringBuilder();
                using var p = new Process { StartInfo = psi };
                p.OutputDataReceived += (_, e) => { if (e.Data != null) sb.AppendLine(e.Data); };
                p.ErrorDataReceived  += (_, e) => { if (e.Data != null) sb.AppendLine(e.Data); };
                p.Start();
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();
                p.WaitForExit();
                return sb.ToString();
            }
            catch (Exception ex)
            {
                return $"[schtasks error] {ex.Message}";
            }
        }
    }
}
