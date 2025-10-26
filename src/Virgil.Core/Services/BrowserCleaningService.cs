using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virgil.Core.Services;

public sealed class BrowserCleaningService
{
    public async Task<string> AnalyzeAndCleanAsync()
    {
        var sb = new StringBuilder();
        sb.AppendLine("[Browsers] Analyse et nettoyage des caches");
        await Task.Run(() =>{
            CleanChromium(sb, "Chrome",    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Google\\Chrome\\User Data"));
            CleanChromium(sb, "Edge",      Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft\\Edge\\User Data"));
            CleanChromium(sb, "Brave",     Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BraveSoftware\\Brave-Browser\\User Data"));
            CleanChromium(sb, "Vivaldi",   Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Vivaldi\\User Data"));
            CleanOpera(sb,    "Opera",     Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Opera Software\\Opera Stable"));
            CleanOpera(sb,    "Opera GX",  Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Opera Software\\Opera GX Stable"));
            CleanFirefox(sb);
        });
        return sb.ToString();
    }

    private static void CleanChromium(StringBuilder sb, string name, string userData){
        if(!Directory.Exists(userData)){ sb.AppendLine($"- {name}: chemin introuvable"); return; }
        var profiles = Directory.EnumerateDirectories(userData, "*", SearchOption.TopDirectoryOnly)
                                 .Where(p => new[]{"Default","Profile "}.Any(prefix => Path.GetFileName(p).StartsWith(prefix, StringComparison.OrdinalIgnoreCase)));
        int profCount = 0; long freedAll = 0;
        foreach(var prof in profiles){
            profCount++;
            freedAll += WipeDirs(sb, name, prof, "Cache", "Code Cache", "GPUCache", Path.Combine("Service Worker", "CacheStorage"));
        }
        sb.AppendLine($"- {name}: profils={profCount}, libéré ≈ {FormatBytes(freedAll)}");

        long WipeDirs(StringBuilder log, string browser, string profile, params string[] rels){
            long total=0; foreach(var rel in rels){ var p=Path.Combine(profile, rel); total+=DeleteFilesUnder(p, log, browser+"|"+Path.GetFileName(profile)+":\\"+rel); } return total; }
    }

    private static void CleanOpera(StringBuilder sb, string name, string profilePath){
        long freed = 0; freed += DeleteFilesUnder(Path.Combine(profilePath, "Cache"), sb, name+":Cache");
        freed += DeleteFilesUnder(Path.Combine(profilePath, "GPUCache"), sb, name+":GPUCache");
        freed += DeleteFilesUnder(Path.Combine(profilePath, "Code Cache"), sb, name+":Code Cache");
        sb.AppendLine($"- {name}: libéré ≈ {FormatBytes(freed)}");
    }

    private static void CleanFirefox(StringBuilder sb){
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var profIni = Path.Combine(appData, "Mozilla\\Firefox\\profiles.ini");
        if(!File.Exists(profIni)){ sb.AppendLine("- Firefox: profiles.ini introuvable"); return; }
        var lines = File.ReadAllLines(profIni);
        var paths = lines.Where(l => l.StartsWith("Path=", StringComparison.OrdinalIgnoreCase))
                         .Select(l => l.Substring(5).Trim())
                         .Select(rel => Path.Combine(appData, "Mozilla\\Firefox", rel))
                         .Where(Directory.Exists)
                         .ToList();
        long freed=0; int count=0;
        foreach(var p in paths){ count++; freed += DeleteFilesUnder(Path.Combine(p, "cache2"), sb, "Firefox|"+Path.GetFileName(p)+":cache2"); }
        sb.AppendLine($"- Firefox: profils={count}, libéré ≈ {FormatBytes(freed)}");
    }

    private static long DeleteFilesUnder(string path, StringBuilder sb, string tag){
        long freed=0; if(!Directory.Exists(path)) return 0;
        try{ foreach(var f in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories)){ try{ var fi=new FileInfo(f); freed+=fi.Length; File.SetAttributes(f, FileAttributes.Normal); File.Delete(f);}catch{} } }catch{}
        try{ foreach(var d in Directory.EnumerateDirectories(path, "*", SearchOption.AllDirectories).OrderByDescending(s=>s.Length)){ try{ if(Directory.Exists(d) && !Directory.EnumerateFileSystemEntries(d).Any()) Directory.Delete(d);}catch{} } }catch{}
        sb.AppendLine($"  · {tag} -> {FormatBytes(freed)} supprimés");
        return freed;
    }

    private static string FormatBytes(long bytes){ string[] s={"B","KB","MB","GB","TB"}; double len=bytes; int o=0; while(len>=1024 && o<s.Length-1){o++; len/=1024;} return $"{len:0.##} {s[o]}"; }
}
