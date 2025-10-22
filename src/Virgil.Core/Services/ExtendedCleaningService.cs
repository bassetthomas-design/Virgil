using System;
using System.Collections.Generic;
using System.IO;

namespace Virgil.Core.Services
{
    public sealed class ExtendedCleaningReport
    {
        public long BytesFound { get; set; }
        public long BytesDeleted { get; set; }
        public List<string> Paths { get; } = new();
    }

    public sealed class ExtendedCleaningService
    {
        private static string Local => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        private static string Roaming => Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        private static string ProgramData => Environment.ExpandEnvironmentVariables("%ProgramData%");

        private static IEnumerable<string> Targets()
        {
            // Jeux / launchers
            yield return Path.Combine(Local, "Steam\\htmlcache");
            yield return Path.Combine(Local, "EpicGamesLauncher\\Saved\\Logs");
            yield return Path.Combine(Roaming, "Epic\\EpicGamesLauncher\\Saved\\Logs");
            yield return Path.Combine(Local, "NVIDIA\\GLCache");
            yield return Path.Combine(Local, "D3DSCache");
            yield return Path.Combine(Local, "Temp"); // redondant mais utile

            // Adobe (cache)
            yield return Path.Combine(Roaming, "Adobe\\Common");
            yield return Path.Combine(Local, "Adobe\\Common");
            yield return Path.Combine(Local, "Adobe\\CoreSync\\plugins");

            // Windows Delivery Optimization cache
            yield return Path.Combine(ProgramData, "Microsoft\\Windows\\DeliveryOptimization\\Cache");
        }

        public ExtendedCleaningReport AnalyzeAndClean()
        {
            var rep = new ExtendedCleaningReport();

            foreach (var path in Targets())
            {
                if (!Directory.Exists(path)) continue;
                rep.Paths.Add(path);

                try
                {
                    foreach (var f in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                    {
                        try { rep.BytesFound += new FileInfo(f).Length; } catch { }
                    }

                    try { Directory.Delete(path, true); rep.BytesDeleted += rep.BytesFound; }
                    catch
                    {
                        try
                        {
                            foreach (var f in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                            { try { File.Delete(f); } catch { } }
                            foreach (var d in Directory.EnumerateDirectories(path, "*", SearchOption.AllDirectories))
                            { try { Directory.Delete(d, true); } catch { } }
                        }
                        catch { }
                    }
                }
                catch { }
            }

            return rep;
        }
    }
}
