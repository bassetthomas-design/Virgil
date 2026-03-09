using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Virgil.Services;

public sealed record DiskAutopsyEntry(string Category, string PathLabel, long SizeBytes);

public sealed class DiskAutopsyReport
{
    public List<DiskAutopsyEntry> Entries { get; init; } = new();
    public Dictionary<string, long> CategoryTotals { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public string Summary { get; set; } = string.Empty;
}

public sealed class DiskAutopsyService
{
    public DiskAutopsyReport Analyze()
    {
        var report = new DiskAutopsyReport();
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var user = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        var mappings = new (string Category, string Label, string Path)[]
        {
            ("System", "Windows", windows),
            ("Applications", "Program Files", @"C:\Program Files"),
            ("Applications", "Program Files (x86)", @"C:\Program Files (x86)"),
            ("Games", "SteamLibrary", Path.Combine(@"C:\", "SteamLibrary")),
            ("Games", "Steam", Path.Combine(user, "Steam")),
            ("Documents", "Documents", Path.Combine(user, "Documents")),
            ("Videos", "Videos", Path.Combine(user, "Videos")),
            ("Downloads", "Downloads", Path.Combine(user, "Downloads")),
            ("Caches", "Temp", Path.GetTempPath()),
            ("Caches", "AppData Temp", Path.Combine(local, "Temp")),
        };

        foreach (var (category, label, path) in mappings.Where(x => Directory.Exists(x.Path)))
        {
            var bytes = TryGetDirectorySize(path);
            if (bytes <= 0)
            {
                continue;
            }

            report.Entries.Add(new DiskAutopsyEntry(category, label, bytes));
            report.CategoryTotals.TryAdd(category, 0);
            report.CategoryTotals[category] += bytes;
        }

        var dominant = report.CategoryTotals.OrderByDescending(x => x.Value).Take(2).Select(x => x.Key).ToList();
        report.Summary = dominant.Count > 0
            ? $"L’espace disque est principalement occupé par {string.Join(" et ", dominant).ToLowerInvariant()}."
            : "Autopsie disque: aucune catégorie dominante détectée.";

        return report;
    }

    private static long TryGetDirectorySize(string path)
    {
        try
        {
            return Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories)
                .Select(file =>
                {
                    try { return new FileInfo(file).Length; }
                    catch { return 0L; }
                })
                .Sum();
        }
        catch
        {
            return 0;
        }
    }
}
