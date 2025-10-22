using System;
using System.IO;
using System.Linq;

namespace Virgil.Core.Services
{
    /// <summary>
    /// Nettoyage "étendu" : caches Windows courants, Adobe, DirectX shader cache, etc.
    /// Retourne un rapport simple des octets détectés/supprimés.
    /// </summary>
    public sealed class ExtendedCleaningService
    {
        public ExtendedCleanReport AnalyzeAndClean()
        {
            long bytesFound = 0;
            long bytesDeleted = 0;
            int filesDeleted = 0;

            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string roamingAppData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string windowsDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);

            // Dossiers cibles (tu pourras en ajouter facilement ici)
            var targets = new[]
            {
                // DirectX Shader Cache
                Path.Combine(localAppData, "D3DSCache"),

                // Edge/Chrome/Brave/Vivaldi (caches génériques Chromium par défaut)
                Path.Combine(localAppData, "Microsoft\\Edge\\User Data\\Default\\Cache"),
                Path.Combine(localAppData, "Google\\Chrome\\User Data\\Default\\Cache"),
                Path.Combine(localAppData, "BraveSoftware\\Brave-Browser\\User Data\\Default\\Cache"),
                Path.Combine(localAppData, "Vivaldi\\User Data\\Default\\Cache"),

                // Firefox
                Path.Combine(roamingAppData, "Mozilla\\Firefox\\Profiles"),

                // Adobe caches
                Path.Combine(roamingAppData, "Adobe\\Common"),
                Path.Combine(localAppData, "Adobe\\Common"),

                // Windows temp (en complément du nettoyage de base)
                Path.Combine(windowsDir, "Temp"),
            }
            .Distinct()
            .ToArray();

            // 1) Mesurer
            foreach (var t in targets)
            {
                if (!Directory.Exists(t)) continue;

                try
                {
                    foreach (var f in Directory.EnumerateFiles(t, "*", SearchOption.AllDirectories))
                    {
                        try { bytesFound += new FileInfo(f).Length; } catch { /* ignore */ }
                    }
                }
                catch { /* ignore */ }
            }

            // 2) Supprimer fichiers
            foreach (var t in targets)
            {
                if (!Directory.Exists(t)) continue;

                try
                {
                    foreach (var f in Directory.EnumerateFiles(t, "*", SearchOption.AllDirectories))
                    {
                        try
                        {
                            var fi = new FileInfo(f);
                            long len = 0;
                            if (fi.Exists)
                            {
                                len = fi.Length;
                                File.SetAttributes(f, FileAttributes.Normal);
                                fi.Delete();
                                filesDeleted++;
                                bytesDeleted += len;
                            }
                        }
                        catch { /* fichier verrouillé, ignorer */ }
                    }
                }
                catch { /* ignore */ }
            }

            // 3) Supprimer dossiers vides (meilleur ordre : plus profonds d'abord)
            foreach (var t in targets)
            {
                if (!Directory.Exists(t)) continue;

                try
                {
                    var dirs = Directory.EnumerateDirectories(t, "*", SearchOption.AllDirectories)
                                        .OrderByDescending(d => d.Length);
                    foreach (var d in dirs)
                    {
                        try { Directory.Delete(d, true); } catch { /* ignore */ }
                    }
                }
                catch { /* ignore */ }
            }

            return new ExtendedCleanReport
            {
                BytesFound = bytesFound,
                BytesDeleted = bytesDeleted,
                FilesDeleted = filesDeleted
            };
        }
    }

    public sealed class ExtendedCleanReport
    {
        public long BytesFound { get; set; }
        public long BytesDeleted { get; set; }
        public int FilesDeleted { get; set; }
    }
}
