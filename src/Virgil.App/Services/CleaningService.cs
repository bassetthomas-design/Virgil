using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Virgil.App.Services;

public class CleaningService : ICleaningService
{
    public async Task<int> CleanIntelligentAsync(bool dryRun = false)
    {
        int deleted = 0;
        deleted += await PurgeDir(Path.GetTempPath(), dryRun).ContinueWith(t=>t.Result.Files).ConfigureAwait(false);
        var winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        deleted += await PurgeDir(Path.Combine(winDir, "Temp"), dryRun).ContinueWith(t=>t.Result.Files).ConfigureAwait(false);
        deleted += await PurgeDir(Path.Combine(winDir, "Prefetch"), dryRun, pattern: "*.pf").ContinueWith(t=>t.Result.Files).ConfigureAwait(false);
        deleted += await PurgeDir(Path.Combine(winDir, "SoftwareDistribution", "Download"), dryRun).ContinueWith(t=>t.Result.Files).ConfigureAwait(false);
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        deleted += await PurgeDir(Path.Combine(local, "Google", "Chrome", "User Data", "Default", "Cache"), dryRun).ContinueWith(t=>t.Result.Files).ConfigureAwait(false);
        deleted += await PurgeDir(Path.Combine(local, "Microsoft", "Edge", "User Data", "Default", "Cache"), dryRun).ContinueWith(t=>t.Result.Files).ConfigureAwait(false);
        var ffProfiles = Path.Combine(local, "Mozilla", "Firefox", "Profiles");
        deleted += await PurgeDir(ffProfiles, dryRun, pattern: "*cache*").ContinueWith(t=>t.Result.Files).ConfigureAwait(false);
        return deleted;
    }

    public async Task<CleanStats> CleanAdvancedAsync(CleanOptions options, bool dryRun = false)
    {
        int files = 0, dirs = 0; long bytes = 0;
        void Acc(CleanStats s) { files += s.Files; dirs += s.Dirs; bytes += s.Bytes; }

        // Corbeille (non géré ici, dépend SHEmptyRecycleBin)

        // Miniatures & cache Explorer
        if (options.Thumbnails || options.ExplorerCache)
        {
            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var explorer = Path.Combine(local, "Microsoft", "Windows", "Explorer");
            if (options.Thumbnails) Acc(await PurgeDir(explorer, dryRun, pattern: "thumbcache*"));
            if (options.ExplorerCache) Acc(await PurgeDir(explorer, dryRun, pattern: "iconcache*"));
        }

        // MRU / Récents
        if (options.MruRecent)
        {
            var app = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var recent = Path.Combine(app, "Microsoft", "Windows", "Recent");
            Acc(await PurgeDir(recent, dryRun));
        }

        // Cookies navigateurs (optionnelle, par défaut faux) — non destructive si off
        if (options.BrowserCookies)
        {
            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            Acc(await PurgeDir(Path.Combine(local, "Google", "Chrome", "User Data", "Default", "Cookies"), dryRun));
            Acc(await PurgeDir(Path.Combine(local, "Microsoft", "Edge", "User Data", "Default", "Cookies"), dryRun));
        }

        return new CleanStats(files, dirs, bytes);
    }

    private static Task<CleanStats> PurgeDir(string? path, bool dryRun, string pattern = "*")
    {
        return Task.Run(() =>
        {
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path)) return new CleanStats(0,0,0);
            int f = 0, d = 0; long b = 0;
            try
            {
                foreach (var file in Directory.EnumerateFiles(path, pattern, SearchOption.TopDirectoryOnly))
                {
                    try { var fi = new FileInfo(file); b += fi.Exists ? fi.Length : 0; if (!dryRun) fi.Delete(); f++; } catch { }
                }
                foreach (var dir in Directory.EnumerateDirectories(path, pattern, SearchOption.TopDirectoryOnly).OrderByDescending(x => x.Length))
                {
                    try { if (!dryRun) Directory.Delete(dir, true); d++; } catch { }
                }
            } catch { }
            return new CleanStats(f,d,b);
        });
    }
}
