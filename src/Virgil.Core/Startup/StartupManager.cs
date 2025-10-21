using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Win32;

namespace Virgil.Core
{
    /// <summary>
    /// Lit les vraies entrées de démarrage Windows :
    /// - Registre HKCU/HKLM : Run & RunOnce (vues x64 et x86)
    /// - Dossiers Startup (utilisateur & commun)
    ///
    /// Compile partout (CI Linux OK) : la collecte ne s'exécute que sous Windows.
    /// </summary>
    public class StartupManager
    {
        public IEnumerable<StartupItem> ListItems()
        {
            var results = new List<StartupItem>();

            // Compatible avec tous les runners : on ne collecte que sur Windows
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
                return results;

            try
            {
                // ----- Registre -----
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
                // laisser silencieux pour ne pas casser l'UI (loggable ailleurs)
            }

            // Déduplication + tri
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
                    string location = $"{hiveLabel}\\{subkey} ({(view == RegistryView.Registry64 ? "x64" : "x86")})";
                    yield return new StartupItem(
                        Name: name,
                        Location: location,
                        Enabled: true
                    );
                }
            }
            catch
            {
                yield break; // pas accessible / pas de droits
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