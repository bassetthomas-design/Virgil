using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Virgil.Core
{
    public sealed class BrowserCleaningReport
    {
        public long BytesFound { get; set; }
        public long BytesDeleted { get; set; }
        public List<string> TouchedPaths { get; } = new();
        public List<string> SkippedReasons { get; } = new();
    }

    public sealed class BrowserCleaningOptions
    {
        /// <summary>Supprimer même si le navigateur est en cours d’exécution (non recommandé)</summary>
        public bool Force { get; set; } = false;
        /// <summary>Inclure Firefox/Gecko</summary>
        public bool IncludeFirefox { get; set; } = true;
        /// <summary>Inclure Chromium-like (Chrome/Edge/Brave/Opera/Vivaldi…)</summary>
        public bool IncludeChromium { get; set; } = true;
    }

    /// <summary>
    /// Détecte les profils navigateurs locaux et nettoie leurs caches en “best-effort”.
    /// Ne jette pas les profils/fichiers importants (History, Bookmarks, Login Data).
    /// </summary>
    public sealed class BrowserCleaningService
    {
        private static readonly string Local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        private static readonly string Roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        private static readonly string[] ChromiumRoots =
        {
            // profils principaux
            "Google\\Chrome\\User Data",
            "Microsoft\\Edge\\User Data",
            "BraveSoftware\\Brave-Browser\\User Data",
            "Opera Software\\Opera Stable",
            "Opera Software\\Opera GX Stable",
            "Vivaldi\\User Data",
            "Yandex\\YandexBrowser\\User Data",
            "Chromium\\User Data"
        };

        private static readonly string[] ChromiumCachePatterns =
        {
            "Cache", "Code Cache", "GPUCache", "GrShaderCache",
            "ShaderCache", "Media Cache", "Session Storage",
            "Service Worker\\CacheStorage", "IndexedDB", "Local Storage"
        };

        private static readonly string[] FirefoxRoots =
        {
            "Mozilla\\Firefox\\Profiles" // dossiers *.default-release / *.default
        };

        private static readonly string[] FirefoxCachePatterns =
        {
            "cache2", "jumpListCache", "startupCache", "shader-cache"
        };

        private static readonly string[] BrowserProcessNames =
        {
            "chrome","msedge","brave","opera","opera_gx","vivaldi","yandex","chromium","firefox","waterfox","librewolf"
        };

        public bool IsAnyBrowserRunning()
            => Process.GetProcesses().Any(p => SafeName(p) is string n && BrowserProcessNames.Contains(n));

        public BrowserCleaningReport AnalyzeAndClean(BrowserCleaningOptions? options = null)
        {
            options ??= new BrowserCleaningOptions();
            var rep = new BrowserCleaningReport();

            if (!options.Force && IsAnyBrowserRunning())
            {
                rep.SkippedReasons.Add("Au moins un navigateur est en cours d’exécution. Fermez-le(s) ou utilisez Force=true.");
                return rep;
            }

            if (options.IncludeChromium)
            {
                foreach (var root in ChromiumRoots)
                {
                    var basePath = Path.Combine(Local, root);
                    CollectAndCleanChromium(basePath, rep);
                }
            }

            if (options.IncludeFirefox)
            {
                foreach (var root in FirefoxRoots)
                {
                    var basePath = Path.Combine(Roaming, root);
                    CollectAndCleanFirefox(basePath, rep);
                }
            }

            return rep;
        }

        private static void CollectAndCleanChromium(string basePath, BrowserCleaningReport rep)
        {
            if (!Directory.Exists(basePath)) return;

            // Profils : "Default", "Profile 1", "Profile 2", etc. certains navigateurs ont les caches directement au root.
            var candidates = new List<string> { basePath };
            try
            {
                candidates.AddRange(Directory.EnumerateDirectories(basePath, "Profile *", SearchOption.TopDirectoryOnly));
                var defaultDir = Path.Combine(basePath, "Default");
                if (Directory.Exists(defaultDir)) candidates.Add(defaultDir);
                var profilesDir = Path.Combine(basePath, "User Data");
                if (Directory.Exists(profilesDir)) candidates.AddRange(Directory.EnumerateDirectories(profilesDir, "*", SearchOption.TopDirectoryOnly));
            }
            catch { /* ignore */ }

            foreach (var prof in candidates.Distinct())
            {
                foreach (var pat in ChromiumCachePatterns)
                {
                    var p = Path.Combine(prof, pat);
                    DeleteFolderBestEffort(p, rep);
                }
            }
        }

        private static void CollectAndCleanFirefox(string basePath, BrowserCleaningReport rep)
        {
            if (!Directory.Exists(basePath)) return;

            IEnumerable<string> profiles = Enumerable.Empty<string>();
            try { profiles = Directory.EnumerateDirectories(basePath, "*.default*", SearchOption.TopDirectoryOnly); } catch { }

            foreach (var prof in profiles)
            {
                foreach (var pat in FirefoxCachePatterns)
                {
                    var p = Path.Combine(prof, pat);
                    DeleteFolderBestEffort(p, rep);
                }
            }
        }

        private static void DeleteFolderBestEffort(string path, BrowserCleaningReport rep)
        {
            try
            {
                if (!Directory.Exists(path)) return;

                long bytes = 0;
                try
                {
                    foreach (var f in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                    {
                        try { bytes += new FileInfo(f).Length; } catch { }
                    }
                }
                catch { /* ignore size traversal errors */ }

                rep.BytesFound += bytes;
                rep.TouchedPaths.Add(path);

                // suppression best-effort
                try { Directory.Delete(path, recursive: true); rep.BytesDeleted += bytes; }
                catch
                {
                    // fallback : vider récursivement
                    try
                    {
                        foreach (var f in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                        { try { File.Delete(f); } catch { } }
                        foreach (var d in Directory.EnumerateDirectories(path, "*", SearchOption.AllDirectories))
                        { try { Directory.Delete(d, true); } catch { } }
                        try { Directory.Delete(path, false); } catch { }
                    }
                    catch { /* ignore */ }
                }
            }
            catch { /* ignore */ }
        }

        private static string? SafeName(Process p)
        {
            try { return Path.GetFileNameWithoutExtension(p.ProcessName)?.ToLowerInvariant(); }
            catch { return null; }
        }
    }
}
