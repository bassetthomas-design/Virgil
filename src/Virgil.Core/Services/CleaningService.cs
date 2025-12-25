using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virgil.Core.Services
{
    public sealed class CleaningService
    {
        private static readonly string[] TempPaths = new[]
        {
            Environment.GetEnvironmentVariable("TEMP") ?? string.Empty,
            Environment.GetEnvironmentVariable("TMP") ?? string.Empty,
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Temp"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp")
        }.Where(p => !string.IsNullOrWhiteSpace(p)).Distinct().ToArray();

        public async Task<string> CleanTempAsync()
        {
            var sb = new StringBuilder();
            long totalBytes = 0;
            int files = 0;
            int deletedFiles = 0;
            int deletedDirs = 0;

            // list the paths being cleaned
            foreach (var p in TempPaths)
                sb.AppendLine("[PATH] " + p);

            await Task.Run(() =>
            {
                foreach (var root in TempPaths)
                {
                    if (!Directory.Exists(root))
                        continue;

                    // accumulate sizes and file count
                    foreach (var f in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
                    {
                        try
                        {
                            var fi = new FileInfo(f);
                            totalBytes += fi.Length;
                            files++;
                        }
                        catch
                        {
                            // ignore errors reading file info
                        }
                    }

                    // delete files
                    foreach (var f in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
                    {
                        try
                        {
                            File.SetAttributes(f, FileAttributes.Normal);
                            File.Delete(f);
                            deletedFiles++;
                        }
                        catch
                        {
                            // ignore deletion failures
                        }
                    }

                    // remove empty directories and count them
                    foreach (var d in Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories).OrderByDescending(s => s.Length))
                    {
                        try
                        {
                            if (Directory.Exists(d) && !Directory.EnumerateFileSystemEntries(d).Any())
                            {
                                Directory.Delete(d);
                                deletedDirs++;
                            }
                        }
                        catch
                        {
                            // ignore deletion failures
                        }
                    }
                }
            });

            // approximate freed bytes equal to totalBytes since all enumerated files are slated for deletion
            string freed = FormatBytes(totalBytes);
            sb.AppendLine($"Files found: {files}, files deleted: {deletedFiles}, directories deleted: {deletedDirs}, space freed: {freed}");

            // Log the summary for historical tracking
            try
            {
                LoggingService.SafeInfo(
                    "Temp cleaning completed: {FilesDeleted} files, {DirsDeleted} directories, freed {FreedBytes}",
                    deletedFiles,
                    deletedDirs,
                    freed);
            }
            catch
            {
                // ignore logging failures
            }

            return sb.ToString();
        }

        private static string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }
    }
}
