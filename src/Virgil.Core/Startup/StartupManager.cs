using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Win32;

namespace Virgil.Core
{
    /// <summary>
    /// Lit les vraies entrées de démarrage Windows :
    /// - Registre HKCU/HKLM: Run & RunOnce (en 32 et 64 bits)
    /// - Dossiers Startup (utilisateur & commun)
    ///
    /// Compile partout (CI Linux ok), mais ne collecte réellement que sous Windows.
    /// </summary>
    public class StartupManager
    {
        public IEnumerable<StartupItem> ListItems()
        {
            var results = new List<StartupItem>();

            if (!OperatingSystem.IsWindows())
                return results; // pas de collecte hors Windows

            try
            {
                // ----- Registre -----
                // HKCU/HKLM, vues 64 & 32 bits pour ne rien rater
                var runKeys = new[]
                {
                    ("HKCU", RegistryHive.CurrentUser,  @"Software\Microsoft\Windows\CurrentVersion\Run"),
                    ("HKCU", RegistryHive.CurrentUser,  @"Software\Microsoft\Windows\CurrentVersion\RunOnce"),
                    ("HKLM", RegistryHive.LocalMachine, @"Software\Microsoft\Windows\CurrentVersion\Run"),
                    ("HKLM", RegistryHive.LocalMachine, @"Software\Microsoft\Windows\CurrentVersion\RunOnce")
                };

                foreach (var (hiveLabel, hive, subkey) in runKeys)
                {
                    results.AddRange(ReadRunKey(hiveLabel, hive, subkey, RegistryView.Registry64));
                    results.AddRange(ReadRunKey(hiveLabel, hive, subkey, RegistryView.Registry32));
                }

                // ----- Dossiers Startup -----
                var userStartup   = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
                var commonStartup = Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup);
                results.AddRange(ReadStartupFolder(userStartup,   "Startup(User)"));
                results.AddRange(ReadStartupFolder(commonStartup, "Startup(Common)"));
            }
            catch
            {
                // On reste silencieux ici (l’UI continue de fonctionner). Les erreurs pourront être loguées ailleurs.
            }

            // Déduplication simple + tri lisible
            return results
                .GroupBy(i => (i.Name, i.Location, i.Enabled))
                .Select(g => g.First())
                .OrderBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(i => i.Location, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        // ---- Helpers ----

        private static IEnumerable<StartupItem> ReadRunKey(
            string hiveLabel,
            RegistryHive hive,
            string subkey,
            RegistryView view)
        {
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(hive, view);
                using var key     = baseKey.OpenSubKey(subkey);
                if (key == null) yield break;

                foreach (var name in key.GetValueNames())
                {
                    // Valeur = commande lancée au démarrage (string ou REG_EXPAND_SZ)
                    string location = $"{hiveLabel}\\{subkey} ({(view == RegistryView.Registry64 ? "x64" : "x86")})";
                    // Ici on ne parse pas la commande (chemin/args). On expose l’existence (Enabled=true).
                    yield return new StartupItem(
                        Name: name,
                        Location: location,
                        Enabled: true
                    );
                }
            }
            catch
            {
                yield break; // clé non accessible / droits insuffisants
            }
        }

        private static IEnumerable<StartupItem> ReadStartupFolder(string? path, string label)
        {
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
                yield break;

            IEnumerable<string> files;
            try { files = Directory.EnumerateFiles(path, "*", SearchOption.TopDirectoryOnly); }
            catch { yield break; }

            foreach (var file in files)
            {
                // .lnk, .exe, scripts, etc. — on affiche le nom et le chemin.
                string name = Path.GetFileNameWithoutExtension(file);
                yield return new StartupItem(
                    Name: string.IsNullOrWhiteSpace(name) ? Path.GetFileName(file) : name,
                    Location: $"{label}:{file}",
                    Enabled: true
                );
            }
        }
    }
}