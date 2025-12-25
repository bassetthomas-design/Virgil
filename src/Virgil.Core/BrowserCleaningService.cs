#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace Virgil.Core.Services
{
    public enum BrowserKind
    {
        Chrome, Edge, Brave, Opera, OperaGX, Vivaldi, // Chromium
        Firefox                                      // Gecko
    }

    public sealed class BrowserCleaningOptions
    {
        public bool Force { get; set; } = false;           // tenter suppression même si fichiers verrouillés
        public bool ExcludeActive { get; set; } = true;    // ignorer les navigateurs en cours d'exécution
        public HashSet<BrowserKind>? Include { get; set; } // null => tous
    }

    public sealed class BrowserProfile
    {
        public BrowserKind Kind { get; init; }
        public string Name { get; init; } = "";
        public string Path { get; init; } = "";
        public bool IsActive { get; init; }
    }

    public sealed class BrowserCleaningReport
    {
        public long BytesFound { get; set; }
        public long BytesDeleted { get; set; }
        public int ProfilesScanned { get; set; }
        public List<string> Lines { get; } = new();

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Profils scannés: {ProfilesScanned}");
            sb.AppendLine($"Trouvé ~{BytesFound / (1024.0 * 1024):F1} MB, supprimé ~{BytesDeleted / (1024.0 * 1024):F1} MB");
            foreach (var l in Lines) sb.AppendLine(l);
            return sb.ToString();
        }
    }

    public sealed class BrowserCleaningService
    {
        public BrowserCleaningReport AnalyzeAndClean(BrowserCleaningOptions opt)
        {
            var report = new BrowserCleaningReport();

            var profs = DiscoverProfiles();
            if (opt.Include is not null)
                profs = profs.Where(p => opt.Include.Contains(p.Kind)).ToList();

            foreach (var p in profs)
            {
                if (opt.ExcludeActive && p.IsActive)
                {
                    report.Lines.Add($"- {p.Kind} [{p.Name}] ignoré (actif).");
                    continue;
                }

                long found = 0, deleted = 0;
                try
                {
                    switch (p.Kind)
                    {
                        // Chromium-like
                        case BrowserKind.Chrome:
                        case BrowserKind.Edge:
                        case BrowserKind.Brave:
                        case BrowserKind.Opera:
                        case BrowserKind.OperaGX:
                        case BrowserKind.Vivaldi:
                            CleanChromiumProfile(p.Path, ref found, ref deleted, opt);
                            break;

                        // Firefox

                        case BrowserKind.Firefox:
                            CleanFirefoxProfile(p.Path, ref found, ref deleted, opt);
                            break;
                    }

                    report.ProfilesScanned++;
                    report.BytesFound += found;
                    report.BytesDeleted += deleted;
                    report.Lines.Add($"- {p.Kind} [{p.Name}] : ~{found / (1024.0 * 1024):F1} MB → ~{deleted / (1024.0 * 1024):F1} MB");
                }
                catch (Exception ex)
                {
                    report.Lines.Add($"- {p.Kind} [{p.Name}] : erreur {ex.Message}");
                }
            }

            return report;
        }

        // -------------------- Discovery

        private static List<BrowserProfile> DiscoverProfiles()
        {
            var list = new List<BrowserProfile>();
            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

            // Chromium family
            DiscoverChromium(list, BrowserKind.Chrome,  Path.Combine(local, "Google", "Chrome", "User Data"), "chrome");
            DiscoverChromium(list, BrowserKind.Edge,    Path.Combine(local, "Microsoft", "Edge", "User Data"), "msedge");
            DiscoverChromium(list, BrowserKind.Brave,   Path.Combine(local, "BraveSoftware", "Brave-Browser", "User Data"), "brave");
            DiscoverChromium(list, BrowserKind.Opera,   Path.Combine(roaming, "Opera Software", "Opera Stable"), "opera");
            DiscoverChromium(list, BrowserKind.OperaGX, Path.Combine(roaming, "Opera Software", "Opera GX Stable"), "opera");
            DiscoverChromium(list, BrowserKind.Vivaldi, Path.Combine(local, "Vivaldi", "User Data"), "vivaldi");

            // Firefox

            DiscoverFirefox(list, roaming);

            return list;
        }

        private static void DiscoverChromium(List<BrowserProfile> dst, BrowserKind kind, string userDataPath, string processName)
        {
            if (!Directory.Exists(userDataPath))
                return;

            var candidates = new List<(string name, string path)>();

            // "Default" + "Profile X"
            var defaultPath = Path.Combine(userDataPath, "Default");
            if (Directory.Exists(defaultPath)) candidates.Add(("Default", defaultPath));

            foreach (var dir in Directory.EnumerateDirectories(userDataPath, "Profile *", SearchOption.TopDirectoryOnly))
                candidates.Add((new DirectoryInfo(dir).Name, dir));

            // Opera/OperaGX (non-standard)
            if (kind is BrowserKind.Opera or BrowserKind.OperaGX)
                candidates.Add((new DirectoryInfo(userDataPath).Name, userDataPath));

            bool active = IsProcessRunning(processName);

            foreach (var (name, path) in candidates.DistinctBy(c => c.path))
            {
                dst.Add(new BrowserProfile
                {
                    Kind = kind,
                    Name = name,
                    Path = path,
                    IsActive = active
                });
            }
        }

        private static void DiscoverFirefox(List<BrowserProfile> dst, string roaming)
        {
            try
            {
                var baseDir = Path.Combine(roaming, "Mozilla", "Firefox");
                var ini = Path.Combine(baseDir, "profiles.ini");
                if (!File.Exists(ini)) return;

                var isActive = IsProcessRunning("firefox");
                string? currName = null;
                string? currPath = null;

                foreach (var raw in File.ReadAllLines(ini))
                {
                    var line = raw.Trim();
                    if (line.StartsWith("[", StringComparison.Ordinal))
                    {
                        // flush previous
                        if (!string.IsNullOrEmpty(currPath))
                        {
                            var profPath = Path.IsPathRooted(currPath)
                                ? currPath
                                : Path.Combine(baseDir, currPath.Replace('/', Path.DirectorySeparatorChar));

                            if (Directory.Exists(profPath))
                            {
                                dst.Add(new BrowserProfile
                                {
                                    Kind = BrowserKind.Firefox,

                                    Name = currName ?? new DirectoryInfo(profPath).Name,
                                    Path = profPath,

                                    IsActive = isActive
                                });
                            }
                        }

                        currName = null;
                        currPath = null;
                        continue;
                    }

                    if (line.StartsWith("Name=", StringComparison.OrdinalIgnoreCase))
                        currName = line.Substring("Name=".Length).Trim();
                    else if (line.StartsWith("Path=", StringComparison.OrdinalIgnoreCase))
                        currPath = line.Substring("Path=".Length).Trim();
                }

                // flush last section
                if (!string.IsNullOrEmpty(currPath))
                {
                    var profPath = Path.IsPathRooted(currPath)
                        ? currPath
                        : Path.Combine(baseDir, currPath.Replace('/', Path.DirectorySeparatorChar));

                    if (Directory.Exists(profPath))
                    {
                        dst.Add(new BrowserProfile
                        {
                            Kind = BrowserKind.Firefox,
                            Name = currName ?? new DirectoryInfo(profPath).Name,
                            Path = profPath,
                            IsActive = isActive
                        });

                    }
                }
            }


            catch { /* best-effort */ }
        }

        private static bool IsProcessRunning(string procName)
        {
            try
            {
                return Process.GetProcessesByName(procName).Any();
            }
            catch { return false; }
        }

        // -------------------- Cleaning

        private static void CleanChromiumProfile(string profilePath, ref long found, ref long deleted, BrowserCleaningOptions opt)
        {
            // Typical cache folders under a Chromium profile or User Data root
            var candidates = new[]
            {
                Path.Combine(profilePath, "Cache"),
                Path.Combine(profilePath, "Code Cache"),
                Path.Combine(profilePath, "GPUCache"),
                Path.Combine(profilePath, "Service Worker", "CacheStorage"),
                Path.Combine(profilePath, "DawnCache"),
            };

            foreach (var c in candidates)
                SweepFolder(c, ref found, ref deleted, opt);
        }

        private static void CleanFirefoxProfile(string profilePath, ref long found, ref long deleted, BrowserCleaningOptions opt)
        {
            var candidates = new[]
            {
                Path.Combine(profilePath, "cache2"),
                Path.Combine(profilePath, "cache2", "entries"),
                Path.Combine(profilePath, "jumpListCache"),
                Path.Combine(profilePath, "startupCache"),
                Path.Combine(profilePath, "OfflineCache")
            };

            foreach (var c in candidates)
                SweepFolder(c, ref found, ref deleted, opt);
        }

        private static void SweepFolder(string dir, ref long found, ref long deleted, BrowserCleaningOptions opt)
        {
            if (!Directory.Exists(dir)) return;

            IEnumerable<string> files;
            try { files = Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories); }
            catch { return; }

            foreach (var f in files)
            {
                try { var len = new FileInfo(f).Length; found += len; }
                catch { /* ignore */ }
            }

            foreach (var f in files)
            {
                try
                {
                    var fi = new FileInfo(f);
                    var len = fi.Exists ? fi.Length : 0;
                    File.SetAttributes(f, FileAttributes.Normal);
                    fi.Delete();
                    deleted += len;
                }
                catch
                {
                    if (opt.Force)

                    {
                        try { File.Delete(f); } catch { /* ignore */ }
                    }
                }
            }

            // Try to delete empty directories
            try
            {
                foreach (var d in Directory.EnumerateDirectories(dir, "*", SearchOption.AllDirectories)
                                           .OrderByDescending(s => s.Length))
                {
                    try { Directory.Delete(d, recursive: false); } catch { }
                }
            }
            catch { }
        }
    }
}
