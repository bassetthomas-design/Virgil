using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Virgil.Core
{
    public sealed class BrowserCleaningOptions
    {
        public bool Force { get; set; } = false; // ignorer les navigateurs en cours d'exécution (déconseillé)
    }

    public sealed class BrowserCleaningReport
    {
        public long BytesFound { get; set; }
        public long BytesDeleted { get; set; }
        public int FilesDeleted { get; set; }
        public List<string> PathsScanned { get; } = new();
        public List<string> Errors { get; } = new();

        public override string ToString()
        {
            var mbFound = BytesFound / (1024.0 * 1024);
            var mbDel = BytesDeleted / (1024.0 * 1024);
            return $"Browsers: found ~{mbFound:F1} MB, deleted ~{mbDel:F1} MB, files {FilesDeleted}.";
        }
    }

    /// <summary>
    /// Nettoyage multi-navigateurs (Chromium-like + Firefox-like).
    /// - Détection de profils (AppData\Local/Roaming)
    /// - Cibles: Cache, Code Cache, GPUCache, ShaderCache, Service Worker, etc.
    /// - Firefox: cache2, startupCache
    /// </summary>
    public sealed class BrowserCleaningService
    {
        public bool IsAnyBrowserRunning()
        {
            var names = new[]
            {
                "chrome","msedge","brave","opera","opera_gx","vivaldi","epic","yandex","iron",
                "firefox","waterfox","librewolf"
            };
            try
            {
                var procs = Process.GetProcesses();
                return procs.Any(p =>
                {
                    try { return names.Contains(p.ProcessName, StringComparer.OrdinalIgnoreCase); }
                    catch { return false; }
                });
            }
            catch { return false; }
        }

        public BrowserCleaningReport AnalyzeAndClean(BrowserCleaningOptions? opts = null)
        {
            opts ??= new BrowserCleaningOptions();
            var rep = new BrowserCleaningReport();

            // Sécurité: éviter de tuer des fichiers en cours d'usage
            if (!opts.Force && IsAnyBrowserRunning())
            {
                rep.Errors.Add("Un navigateur est en cours d'exécution. Fermez-le(s) ou passez Force=true.");
                return rep;
            }

            // Chromium-like profils (LocalAppData)
            var lad = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var rad = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

            var chromiumRoots = new[]
            {
                Path.Combine(lad, "Google", "Chrome", "User Data"),
                Path.Combine(lad, "Microsoft", "Edge", "User Data"),
                Path.Combine(lad, "BraveSoftware", "Brave-Browser", "User Data"),
                Path.Combine(lad, "Vivaldi", "User Data"),
                Path.Combine(lad, "Opera Software", "Opera Stable"),
                Path.Combine(lad, "Opera Software", "Opera GX Stable"),
                Path.Combine(lad, "Yandex", "YandexBrowser", "User Data"),
                Path.Combine(lad, "Chromium", "User Data"),
                Path.Combine(lad, "SRWare Iron", "User Data")
            };

            var chromiumTargets = new[]
            {
                "Cache", "Code Cache", "GPUCache", "ShaderCache",
                Path.Combine("Service Worker","CacheStorage"),
                Path.Combine("Service Worker","ScriptCache")
            };

            foreach (var root in chromiumRoots)
            {
                if (!Directory.Exists(root)) continue;

                // profils typiques: "Default", "Profile *"
                var profiles = Directory.EnumerateDirectories(root, "*", SearchOption.TopDirectoryOnly)
                                        .Where(p => Path.GetFileName(p).Equals("Default", StringComparison.OrdinalIgnoreCase)
                                                 || Path.GetFileName(p).StartsWith("Profile", StringComparison.OrdinalIgnoreCase))
                                        .ToList();

                if (profiles.Count == 0) profiles.Add(root); // certains comme Opera n'ont pas de sous-profils

                foreach (var prof in profiles)
                {
                    foreach (var target in chromiumTargets)
                    {
                        var p = Path.Combine(prof, target);
                        CleanFolder(p, rep);
                    }
                }
            }

            // Firefox-like (Roaming AppData)
            var firefoxRoots = new[]
            {
                Path.Combine(rad, "Mozilla", "Firefox"),
                Path.Combine(rad, "Waterfox"),
                Path.Combine(rad, "LibreWolf")
            };

            foreach (var root in firefoxRoots)
            {
                if (!Directory.Exists(root)) continue;

                // profils *.default* dans Profiles
                var profRoot = Path.Combine(root, "Profiles");
                if (!Directory.Exists(profRoot)) continue;

                foreach (var prof in Directory.EnumerateDirectories(profRoot))
                {
                    CleanFolder(Path.Combine(prof, "cache2"), rep);
                    CleanFolder(Path.Combine(prof, "startupCache"), rep);
                }
            }

            return rep;
        }

        private static void CleanFolder(string path, BrowserCleaningReport rep)
        {
            try
            {
                if (!Directory.Exists(path)) return;
                rep.PathsScanned.Add(path);

                long bytesFound = 0;
                try
                {
                    foreach (var f in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                    {
                        try { bytesFound += new FileInfo(f).Length; } catch { }
                    }
                } catch { }

                long bytesDeleted = 0; int filesDeleted = 0;

                // Suppression fichiers
                try
                {
                    foreach (var f in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                    {
                        try
                        {
                            var fi = new FileInfo(f);
                            var len = fi.Length;
                            File.SetAttributes(f, FileAttributes.Normal);
                            fi.Delete();
                            filesDeleted++;
                            bytesDeleted += len;
                        }
                        catch { /* ignore */ }
                    }
                } catch { }

                // Suppression sous-dossiers
                try
                {
                    foreach (var d in Directory.EnumerateDirectories(path, "*", SearchOption.AllDirectories)
                                               .OrderByDescending(s => s.Length))
                    {
                        try { Directory.Delete(d, true); } catch { }
                    }
                } catch { }

                rep.BytesFound += bytesFound;
                rep.BytesDeleted += bytesDeleted;
                rep.FilesDeleted += filesDeleted;
            }
            catch (Exception ex)
            {
                rep.Errors.Add($"{path}: {ex.Message}");
            }
        }
    }
}
