using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virgil.Core.Services
{
    public sealed class ExtendedCleaningService
    {
        public async Task<string> CleanAsync()
        {
            var sb = new StringBuilder();
            long freed = 0;

            await Task.Run(() =>
            {
                freed += PurgeDir(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "D3DSCache"), sb, "D3DSCache");
                freed += PurgeDir(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "DirectX Shader Cache"), sb, "DirectX Shader Cache");
                try
                {
                    var inet = Environment.GetFolderPath(Environment.SpecialFolder.InternetCache);
                    if (!string.IsNullOrWhiteSpace(inet))
                        freed += PurgeDir(inet, sb, "INetCache");
                } catch { }
            });

            sb.AppendLine($"[Extended] libéré ≈ {FormatBytes(freed)}");
            return sb.ToString();
        }

        private static long PurgeDir(string path, StringBuilder sb, string tag)
        {
            long freed = 0; if (!Directory.Exists(path)) return 0;
            try
            {
                foreach (var f in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                { try { var fi = new FileInfo(f); freed += fi.Length; File.SetAttributes(f, FileAttributes.Normal); File.Delete(f); } catch { } }
            } catch { }

            try
            {
                foreach (var d in Directory.EnumerateDirectories(path, "*", SearchOption.AllDirectories).OrderByDescending(s => s.Length))
                { try { if (Directory.Exists(d) && !Directory.EnumerateFileSystemEntries(d).Any()) Directory.Delete(d); } catch { } }
            } catch { }

            sb.AppendLine($"  · {tag} -> {FormatBytes(freed)} supprimés");
            return freed;
        }

        private static string FormatBytes(long bytes)
        {
            string[] s = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes; int o = 0; while (len >= 1024 && o < s.Length - 1) { o++; len /= 1024; }
            return $"{len:0.##} {s[o]}";
        }
    }
}
