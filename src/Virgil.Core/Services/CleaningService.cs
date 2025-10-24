#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Virgil.Core.Services
{
    /// <summary>
    /// Statistiques de nettoyage : octets détectés/supprimés + nombre de fichiers supprimés.
    /// </summary>
    public readonly struct CleaningStats
    {
        public long BytesFound { get; }
        public long BytesDeleted { get; }
        public int FilesDeleted { get; }

        public CleaningStats(long bytesFound, long bytesDeleted, int filesDeleted)
        {
            BytesFound = bytesFound;
            BytesDeleted = filesDeleted < 0 ? 0 : bytesDeleted;
            FilesDeleted = filesDeleted;
        }
    }

    /// <summary>
    /// Nettoyage des répertoires temporaires standards de Windows.
    /// </summary>
    public sealed class CleaningService
    {
        /// <summary>
        /// Calcule la taille totale des fichiers temporaires (sans les supprimer).
        /// </summary>
        public long GetTempFilesSize()
        {
            var (targets, _) = GetTargets();
            long bytes = 0;
            foreach (var file in EnumerateFilesSafe(targets))
            {
                try { bytes += new FileInfo(file).Length; }
                catch { /* ignore */ }
            }
            return bytes;
        }

        /// <summary>
        /// Supprime les fichiers temporaires (sans stats détaillées).
        /// </summary>
        public void CleanTempFiles()
        {
            _ = CleanTempWithStats();
        }

        /// <summary>
        /// Nettoyage complet avec statistiques : détecté vs supprimé, nombre de fichiers supprimés.
        /// Utilisé par les presets d’entretien.
        /// </summary>
        public CleaningStats CleanTempWithStats()
        {
            var (targets, windowsTemp) = GetTargets();

            long bytesFound = 0;
            long bytesDeleted = 0;
            int filesDeleted = 0;

            // 1) scanner
            var files = EnumerateFilesSafe(targets).ToList();
            foreach (var f in files)
            {
                try { bytesFound += new FileInfo(f).Length; }
                catch { /* ignore */ }
            }

            // 2) supprimer fichiers
            foreach (var f in files)
            {
                try
                {
                    var fi = new FileInfo(f);
                    var len = fi.Exists ? fi.Length : 0;
                    // ne pas bidouiller Windows\Temp de façon agressive si on n'est pas admin
                    if (IsInside(windowsTemp, f) && !IsElevated())
                    {
                        // suppression soft : on respecte les verrouillages
                        TryDeleteFileSoft(fi, ref filesDeleted, ref bytesDeleted, len);
                    }
                    else
                    {
                        // suppression normale
                        TryDeleteFileHard(fi, ref filesDeleted, ref bytesDeleted, len);
                    }
                }
                catch { /* ignore */ }
            }

            // 3) tenter de supprimer les dossiers vides (best-effort)
            foreach (var root in targets)
            {
                TryDeleteEmptyDirs(root);
            }

            return new CleaningStats(bytesFound, bytesDeleted, filesDeleted);
        }

        // ---------- Helpers ----------

        private static (IReadOnlyList<string> Targets, string WindowsTemp) GetTargets()
        {
            var tempUser = Environment.ExpandEnvironmentVariables("%TEMP%");
            var appLocalTemp = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Temp");
            var winTemp = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp");

            var list = new List<string>();
            if (Directory.Exists(tempUser)) list.Add(tempUser);
            if (Directory.Exists(appLocalTemp)) list.Add(appLocalTemp);
            if (Directory.Exists(winTemp)) list.Add(winTemp);

            // de-dup (ex : %TEMP% peut déjà être AppData\Local\Temp)
            list = list.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            return (list, winTemp);
        }

        private static IEnumerable<string> EnumerateFilesSafe(IEnumerable<string> roots)
        {
            foreach (var root in roots)
            {
                IEnumerable<string> files;
                try { files = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories); }
                catch
                {
                    // fallback : ne pas descendre récursivement si erreur d'accès
                    try { files = Directory.EnumerateFiles(root, "*", SearchOption.TopDirectoryOnly); }
                    catch { continue; }
                }

                foreach (var f in files)
                    yield return f;
            }
        }

        private static void TryDeleteFileSoft(FileInfo fi, ref int filesDeleted, ref long bytesDeleted, long size)
        {
            try
            {
                // pas d’attributes reset agressif ici
                fi.Delete();
                filesDeleted++;
                bytesDeleted += size;
            }
            catch { /* ignore locked */ }
        }

        private static void TryDeleteFileHard(FileInfo fi, ref int filesDeleted, ref long bytesDeleted, long size)
        {
            try
            {
                File.SetAttributes(fi.FullName, FileAttributes.Normal);
                fi.Delete();
                filesDeleted++;
                bytesDeleted += size;
            }
            catch { /* ignore locked */ }
        }

        private static void TryDeleteEmptyDirs(string root)
        {
            try
            {
                // on supprime les sous-dossiers du plus profond au plus proche
                var dirs = Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories)
                                    .OrderByDescending(p => p.Length)
                                    .ToList();

                foreach (var d in dirs)
                {
                    try
                    {
                        if (!Directory.EnumerateFileSystemEntries(d).Any())
                        {
                            Directory.Delete(d, false);
                        }
                    }
                    catch { /* ignore */ }
                }
            }
            catch { /* ignore */ }
        }

        private static bool IsInside(string parent, string path)
        {
            try
            {
                var p = Path.GetFullPath(parent).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                var c = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                return c.StartsWith(p, StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        private static bool IsElevated()
        {
            // pas de référence à WindowsIdentity ici pour rester léger :
            // on tente une opération sensible et on voit si ça jette (mais on ne l’appelle pas ici).
            // Pour l’instant, on retourne false (comportement conservateur).
            return false;
        }
    }
}
