using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virgil.Core.Services;

public sealed class CleaningService
{
    private static readonly string[] TempPaths = new[]{
        Environment.GetEnvironmentVariable("TEMP") ?? string.Empty,
        Environment.GetEnvironmentVariable("TMP") ?? string.Empty,
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Temp"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp")
    }.Where(p => !string.IsNullOrWhiteSpace(p)).Distinct().ToArray();

    public async Task<string> CleanTempAsync()
    {
        var sb = new StringBuilder();
        long total = 0; int files = 0; int deleted = 0;
        foreach (var p in TempPaths) sb.AppendLine("[PATH] " + p);

        await Task.Run(() =>{
            foreach (var root in TempPaths)
            {
                if (!Directory.Exists(root)) continue;
                foreach (var f in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
                {
                    try { var fi = new FileInfo(f); total += fi.Length; files++; } catch {}
                }
                foreach (var f in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
                {
                    try { File.SetAttributes(f, FileAttributes.Normal); File.Delete(f); deleted++; } catch {}
                }
                foreach (var d in Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories).OrderByDescending(s => s.Length))
                {
                    try { if (Directory.Exists(d) && !Directory.EnumerateFileSystemEntries(d).Any()) Directory.Delete(d); } catch {}
                }
            }
        });
        sb.AppendLine($"Files: {files}, Deleted: {deleted}, Size(before): {FormatBytes(total)}");
        return sb.ToString();
    }

    private static string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes; int order = 0;
        while (len >= 1024 && order < sizes.Length - 1) { order++; len /= 1024; }
        return $"{len:0.##} {sizes[order]}";
    }
}
