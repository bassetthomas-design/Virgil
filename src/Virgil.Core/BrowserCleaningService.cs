using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Virgil.Core
{
    /// <summary>
    /// Provides methods to clean browser cache directories for common web browsers.
    /// This service attempts to locate and remove cache directories for Chrome, Edge,
    /// Brave, Opera, Vivaldi and Firefox. Errors cleaning individual browsers are
    /// ignored so that cleaning of other browsers can proceed.
    /// </summary>
    public class BrowserCleaningService
    {
        /// <summary>
        /// Scans known browser cache locations and deletes the corresponding
        /// directories where they exist. Returns a summary of the actions taken.
        /// </summary>
        public string CleanBrowserCaches()
        {
            var output = new StringBuilder();
            try
            {
                var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var appDataRoaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var candidates = new List<(string Name, string Path)>
                {
                    ("Chrome", Path.Combine(localAppData, "Google", "Chrome", "User Data", "Default", "Cache")),
                    ("Edge", Path.Combine(localAppData, "Microsoft", "Edge", "User Data", "Default", "Cache")),
                    ("Brave", Path.Combine(localAppData, "BraveSoftware", "Brave-Browser", "User Data", "Default", "Cache")),
                    ("Opera", Path.Combine(localAppData, "Opera Software", "Opera Stable", "Cache")),
                    ("Vivaldi", Path.Combine(localAppData, "Vivaldi", "User Data", "Default", "Cache")),
                    ("Firefox", Path.Combine(appDataRoaming, "Mozilla", "Firefox", "Profiles"))
                };
                foreach (var (name, path) in candidates)
                {
                    if (Directory.Exists(path))
                    {
                        try
                        {
                            if (name == "Firefox")
                            {
                                // Firefox stores cache in each profile's cache2 subdirectory
                                foreach (var profileDir in Directory.EnumerateDirectories(path))
                                {
                                    var cacheDir = Path.Combine(profileDir, "cache2");
                                    if (Directory.Exists(cacheDir))
                                    {
                                        Directory.Delete(cacheDir, true);
                                    }
                                }
                                output.AppendLine($"{name} caches cleaned.");
                            }
                            else
                            {
                                Directory.Delete(path, true);
                                output.AppendLine($"{name} cache cleaned.");
                            }
                        }
                        catch
                        {
                            output.AppendLine($"Failed to clean {name} cache.");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                output.AppendLine($"Error cleaning browser caches: {ex.Message}");
            }
            return output.ToString();
        }
    }
}