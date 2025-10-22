using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Virgil.Core.Services
{
    public class ExtendedCleaningResult
    {
        public long BytesFound { get; set; }
        public long BytesDeleted { get; set; }
        public int FilesDeleted { get; set; }
        public string Log { get; set; } = string.Empty;
    }

    /// <summary>
    /// Nettoyage étendu : caches Adobe/Unity/launchers, logs volumineux, etc.
    /// Best-effort : ignore erreurs d’accès et dossiers introuvables.
    /// </summary>
    public class ExtendedCleaningService
    {
        public ExtendedCleaningResult AnalyzeAndClean()
        {
            var log = new List<string>();
            long found = 0, deleted = 0;
            int filesDeleted = 0;

            IEnumerable<string> Targets()
            {
                var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

                // Adobe, Unity, Logs divers
                yield return Path.Combine(local, "Adobe", "Common", "Media Cache Files");
                yield return Path.Combine(roaming, "Adobe", "Common", "Media Cache Files");
                yield return Path.Combine(roaming, "Unity", "Cache");
                yield return Path.Combine(local, "Temp"); // déjà couvert ailleurs, mais inclus ici pour étendu
                yield return Path.Combine(local, "CrashDumps");

                // Launchers
                yield return Path.Combine(local, "Battle.net", "BrowserCache");
                yield return Path.Combine(roaming, "Battle.net", "Cache");
                yield return Path.Combine(local, "EpicGamesLauncher", "Saved", "Logs");
                yield return Path.Combine(local, "Steam", "htmlcache");
                yield return Path.Combine(local, "Steam", "logs");

                // Logs Windows volumineux (attention : best-effort)
                yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Logs");
            }

            var toDelete = new List<string>();

            foreach (var root in Targets().Distinct())
            {
                if (!Directory.Exists(root)) continue;
                try
                {
                    foreach (var f in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
                    {
                        try
                        {
                            var fi = new FileInfo(f);
                            found += fi.Length;
                            toDelete.Add(fi.FullName);
                        }
                        catch { /* ignore */ }
                    }
                }
                catch { /* ignore */ }
            }

            foreach (var f in toDelete.OrderByDescending(x => x.Length)) // supprime plus courts en dernier
            {
                try
                {
                    var fi = new FileInfo(f);
                    var len = fi.Exists ? fi.Length : 0;
                    File.SetAttributes(f, FileAttributes.Normal);
                    fi.Delete();
                    deleted += len;
                    filesDeleted++;
                }
                catch { /* lock ou droit */ }
            }

            log.Add($"Extended cleaning: found ~{found / (1024.0 * 1024):F1} MB; deleted ~{deleted / (1024.0 * 1024):F1} MB; files {filesDeleted}");
            return new ExtendedCleaningResult
            {
                BytesFound = found,
                BytesDeleted = deleted,
                FilesDeleted = filesDeleted,
                Log = string.Join(Environment.NewLine, log)
            };
        }
    }
}
