using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Virgil.Core
{
    public sealed class ExtendedCleaningReport
    {
        public long BytesFound { get; set; }
        public long BytesDeleted { get; set; }
        public int FilesDeleted { get; set; }
        public List<string> PathsScanned { get; } = new();
        public List<string> Errors { get; } = new();
    }

    /// <summary>
    /// Nettoyage étendu: caches communs (Adobe, Unity, NVIDIA shaders, logs volumineux).
    /// Sûr par défaut (fichiers temporaires / caches seulement).
    /// </summary>
    public sealed class ExtendedCleaningService
    {
        public ExtendedCleaningReport AnalyzeAndClean()
        {
            var rep = new ExtendedCleaningReport();

            var lad = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var rad = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var com = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

            var targets = new[]
            {
                Path.Combine(lad, "Adobe", "Acrobat", "DC", "Cache"),
                Path.Combine(lad, "Adobe", "Lightroom", "Caches"),
                Path.Combine(lad, "Unity","Cache"),
                Path.Combine(lad, "NVIDIA", "ComputeCache"),
                Path.Combine(lad, "NVIDIA", "DXCache"),
                Path.Combine(lad, "NVIDIA Corporation", "NV_Cache"),
                Path.Combine(lad, "Temp"),
                Path.Combine(rad, "Temp"),
                Path.Combine(com, "Microsoft", "Windows", "WER", "ReportQueue")
            };

            foreach (var t in targets.Distinct().Where(Directory.Exists))
                CleanFolder(t, rep);

            return rep;
        }

        private static void CleanFolder(string path, ExtendedCleaningReport rep)
        {
            try
            {
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
                }
                catch { }

                try
                {
                    foreach (var d in Directory.EnumerateDirectories(path, "*", SearchOption.AllDirectories)
                                               .OrderByDescending(s => s.Length))
                    {
                        try { Directory.Delete(d, true); } catch { }
                    }
                }
                catch { }

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
