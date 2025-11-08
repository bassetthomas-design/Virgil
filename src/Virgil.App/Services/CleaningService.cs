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
        deleted += await PurgeDir(Path.GetTempPath(), dryRun);
        deleted += await PurgeDir(Environment.GetFolderPath(Environment.SpecialFolder.Windows) + "\Temp", dryRun);
        deleted += await PurgeDir(Environment.GetFolderPath(Environment.SpecialFolder.Windows) + "\Prefetch", dryRun, pattern:"*.pf");
        var sd = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "SoftwareDistribution\Download");
        deleted += await PurgeDir(sd, dryRun);
        // Browsers caches (best effort)
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        deleted += await PurgeDir(Path.Combine(local, "Google\Chrome\User Data\Default\Cache"), dryRun);
        deleted += await PurgeDir(Path.Combine(local, "Microsoft\Edge\User Data\Default\Cache"), dryRun);
        deleted += await PurgeDir(Path.Combine(local, "Mozilla\Firefox\Profiles"), dryRun, pattern:"*cache*");
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
                foreach (var dir in Directory.EnumerateDirectories(path, "*", SearchOption.AllDirectories).OrderByDescending(d => d.Length))
                {
                    try { if (!dryRun) Directory.Delete(dir, false); } catch { }
                }
            } catch { }
            return count;
        });
    }
}
