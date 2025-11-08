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

        // System/temp locations
        deleted += await PurgeDir(Path.GetTempPath(), dryRun);
        var winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        deleted += await PurgeDir(Path.Combine(winDir, "Temp"), dryRun);
        deleted += await PurgeDir(Path.Combine(winDir, "Prefetch"), dryRun, pattern: "*.pf");
        deleted += await PurgeDir(Path.Combine(winDir, "SoftwareDistribution", "Download"), dryRun);

        // Browsers caches (best effort)
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        deleted += await PurgeDir(Path.Combine(local, "Google", "Chrome", "User Data", "Default", "Cache"), dryRun);
        deleted += await PurgeDir(Path.Combine(local, "Microsoft", "Edge", "User Data", "Default", "Cache"), dryRun);
        var ffProfiles = Path.Combine(local, "Mozilla", "Firefox", "Profiles");
        deleted += await PurgeDir(ffProfiles, dryRun, pattern: "*cache*");

        return deleted;
    }

    private static Task<int> PurgeDir(string? path, bool dryRun, string pattern = "*")
    {
        return Task.Run(() =>
        {
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path)) return 0;
            int count = 0;
            try
            {
                foreach (var file in Directory.EnumerateFiles(path, pattern, SearchOption.AllDirectories))
                {
                    try { if (!dryRun) File.Delete(file); count++; } catch { }
                }
                // Try remove empty subdirs (depth-first)
                foreach (var dir in Directory.EnumerateDirectories(path, "*", SearchOption.AllDirectories).OrderByDescending(d => d.Length))
                {
                    try { if (!dryRun) Directory.Delete(dir, false); } catch { }
                }
            } catch { }
            return count;
        });
    }
}
