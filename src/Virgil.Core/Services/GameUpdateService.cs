using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Virgil.Core.Services
{
    /// <summary>
    /// Mise à jour “best-effort” des plateformes de jeux.
    /// - Steam : tente steamcmd si présent, sinon lance Steam pour forcer la mise à jour auto du client.
    /// - Epic : lance le launcher (il met à jour automatiquement au démarrage).
    /// NOTE: sans cred/app list, on ne peut pas forcer chaque jeu programmatique­ment de façon fiable.
    /// </summary>
    public class GameUpdateService
    {
        public async Task<string> UpdateAllAsync()
        {
            var sb = new StringBuilder();
            try { sb.AppendLine(await UpdateSteamAsync()); } catch (Exception ex) { sb.AppendLine($"[Steam] {ex.Message}"); }
            try { sb.AppendLine(await UpdateEpicAsync()); }  catch (Exception ex) { sb.AppendLine($"[Epic] {ex.Message}"); }
            return sb.ToString().Trim();
        }

        public async Task<string> UpdateSteamAsync()
        {
            // 1) steamcmd si dispo
            var steamCmd = ResolveSteamCmd();
            if (!string.IsNullOrEmpty(steamCmd))
            {
                // On met à jour le client + caches (pas les jeux individuellement sans app ids)
                var output = await RunAsync(steamCmd, "+login anonymous +quit");
                return "[Steam] Client vérifié (steamcmd).";
            }

            // 2) sinon, lancer Steam normal (il se met à jour tout seul)
            var steamExe = ResolveSteamExe();
            if (!string.IsNullOrEmpty(steamExe))
            {
                _ = Process.Start(new ProcessStartInfo(steamExe) { UseShellExecute = true });
                return "[Steam] Lancement du client (mise à jour auto si dispo).";
            }

            return "[Steam] Introuvable.";
        }

        public async Task<string> UpdateEpicAsync()
        {
            var epicExe = ResolveEpicExe();
            if (!string.IsNullOrEmpty(epicExe))
            {
                _ = Process.Start(new ProcessStartInfo(epicExe) { UseShellExecute = true });
                await Task.Delay(2000);
                return "[Epic] Lancement du client (mise à jour auto si dispo).";
            }
            return "[Epic] Introuvable.";
        }

        private static string? ResolveSteamCmd()
        {
            // chemins classiques
            var candidates = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam", "steamcmd.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),     "Steam", "steamcmd.exe"),
            };
            foreach (var c in candidates) if (File.Exists(c)) return c;
            // PATH ?
            var path = Environment.GetEnvironmentVariable("PATH") ?? "";
            foreach (var p in path.Split(Path.PathSeparator))
            {
                var exe = Path.Combine(p.Trim(), "steamcmd.exe");
                if (File.Exists(exe)) return exe;
            }
            return null;
        }

        private static string? ResolveSteamExe()
        {
            var candidates = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam", "steam.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),     "Steam", "steam.exe"),
            };
            foreach (var c in candidates) if (File.Exists(c)) return c;
            return null;
        }

        private static string? ResolveEpicExe()
        {
            var candidates = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),     "Epic Games", "Launcher", "Portal", "Binaries", "Win64", "EpicGamesLauncher.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Epic Games", "Launcher", "Portal", "Binaries", "Win64", "EpicGamesLauncher.exe"),
            };
            foreach (var c in candidates) if (File.Exists(c)) return c;
            return null;
        }

        private static async Task<string> RunAsync(string file, string args)
        {
            var psi = new ProcessStartInfo(file, args)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            using var p = Process.Start(psi);
            if (p == null) return "";
            var stdout = await p.StandardOutput.ReadToEndAsync();
            var stderr = await p.StandardError.ReadToEndAsync();
            await p.WaitForExitAsync();
            return (stdout + Environment.NewLine + stderr).Trim();
        }
    }
}
