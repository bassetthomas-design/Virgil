using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virgil.Core.Services
{
    /// <summary>
    /// Provides an extended cleaning routine that can purge shader caches, browser caches,
    /// logs, leftover installers and optionally defragment the system drive. The cleaning
    /// behaviour is controlled via <see cref="ExtendedCleaningOptions"/>.
    /// </summary>
    public sealed class ExtendedCleaningService
    {
        /// <summary>
        /// Options controlling which categories of files the extended cleaning will remove.
        /// </summary>
        public sealed class ExtendedCleaningOptions
        {
            /// <summary>
            /// Remove shader caches and browser caches. Enabled by default.
            /// </summary>
            public bool CleanCaches { get; set; } = true;

            /// <summary>
            /// Remove various log files and obsolete application logs. Enabled by default.
            /// </summary>
            public bool CleanLogs { get; set; } = true;

            /// <summary>
            /// Remove leftover installation files, orphaned install directories and archives. Enabled by default.
            /// </summary>
            public bool CleanInstallLeftovers { get; set; } = true;

            /// <summary>
            /// Whether to defragment local disks after cleaning. Disabled by default because
            /// the operation can take a long time.
            /// </summary>
            public bool DefragmentDisks { get; set; } = false;
        }

        /// <summary>
        /// Perform an extended cleaning with custom options.
        /// </summary>
        /// <param name="options">Options selecting what to clean. If null, defaults are used.</param>
        public async Task<string> CleanAsync(ExtendedCleaningOptions? options)
        {
            options ??= new ExtendedCleaningOptions();
            var sb = new StringBuilder();
            long freed = 0;

            await Task.Run(() =>
            {
                // Shader and browser caches
                if (options.CleanCaches)
                {
                    freed += PurgeDir(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "D3DSCache"), sb, "D3DSCache");
                    freed += PurgeDir(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "DirectX Shader Cache"), sb, "DirectX Shader Cache");
                    try
                    {
                        var inet = Environment.GetFolderPath(Environment.SpecialFolder.InternetCache);
                        if (!string.IsNullOrWhiteSpace(inet))
                            freed += PurgeDir(inet, sb, "INetCache");
                    }
                    catch { }
                }

                // Application/system logs
                if (options.CleanLogs)
                {
                    // Remove a few common log directories; this avoids pruning system event logs.
                    var candidateLogs = new[]
                    {
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Logs"),
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Logs"),
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CrashDumps")
                    };
                    foreach (var logDir in candidateLogs)
                    {
                        freed += PurgeDir(logDir, sb, "Logs");
                    }
                }

                // Leftover installation files
                if (options.CleanInstallLeftovers)
                {
                    // Attempt to delete common leftover installers in the user's Downloads folder
                    var downloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
                    if (Directory.Exists(downloads))
                    {
                        foreach (var file in Directory.EnumerateFiles(downloads, "*.exe"))
                        {
                            try
                            {
                                var info = new FileInfo(file);
                                freed += info.Length;
                                File.Delete(file);
                                sb.AppendLine($"  · Installateur {info.Name} supprimé");
                            }
                            catch { }
                        }
                    }
                }
            });

            // Optionally defragment disks
            if (options.DefragmentDisks)
            {
                try
                {
                    sb.AppendLine("  · Défragmentation du disque système en cours...");
                    using var p = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "defrag.exe",
                        Arguments = "C: /H /U /V",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    });
                    p?.WaitForExit();
                    sb.AppendLine("  · Défragmentation terminée");
                }
                catch
                {
                    sb.AppendLine("  · Défragmentation impossible (droits insuffisants ou non supporté)");
                }
            }

            sb.AppendLine($"[Extended] libéré ≈ {FormatBytes(freed)}");
            return sb.ToString();
        }

        /// <summary>
        /// Backwards compatibility: call extended cleaning with default options.
        /// </summary>
        public Task<string> CleanAsync()
        {
            return CleanAsync(options: null);
        }

        private static long PurgeDir(string path, StringBuilder sb, string tag)
        {
            long freed = 0;
            if (!Directory.Exists(path)) return 0;
            try
            {
                foreach (var f in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                {
                    try
                    {
                        var fi = new FileInfo(f);
                        freed += fi.Length;
                        File.SetAttributes(f, FileAttributes.Normal);
                        fi.Delete();
                    }
                    catch { }
                }
            }
            catch { }

            try
            {
                foreach (var d in Directory.EnumerateDirectories(path, "*", SearchOption.AllDirectories).OrderByDescending(s => s.Length))
                {
                    try
                    {
                        if (Directory.Exists(d) && !Directory.EnumerateFileSystemEntries(d).Any())
                            Directory.Delete(d);
                    }
                    catch { }
                }
            }
            catch { }

            sb.AppendLine($"  · {tag} -> {FormatBytes(freed)} supprimés");
            return freed;
        }

        private static string FormatBytes(long bytes)
        {
            string[] s = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int o = 0;
            while (len >= 1024 && o < s.Length - 1)
            {
                o++;
                len /= 1024;
            }
            return $"{len:0.##} {s[o]}";
        }
    }
}
