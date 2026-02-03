using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Virgil.Services;

public sealed record BrowserCleanupOptions
{
    public bool Cache { get; init; } = true;
    public bool Cookies { get; init; }
    public bool History { get; init; }
    public bool Downloads { get; init; }
    public bool Sessions { get; init; }
    public bool SiteData { get; init; }
    public bool Autofill { get; init; }
}

public sealed record BrowserCleanupResult(
    long FreedBytes,
    int FilesDeleted,
    IReadOnlyCollection<string> BrowsersProcessed,
    int LockedItems,
    IReadOnlyCollection<string> BrowsersDetected,
    IReadOnlyCollection<string> BrowsersRunning);

public sealed record BrowserCleanupTarget(
    string BrowserName,
    string ProcessName,
    IReadOnlyCollection<BrowserCleanupProfile> Profiles);

public sealed record BrowserCleanupProfile(
    string ProfileName,
    string ProfilePath,
    IReadOnlyCollection<string> CachePaths,
    IReadOnlyCollection<string> CookieFiles,
    IReadOnlyCollection<string> HistoryFiles,
    IReadOnlyCollection<string> DownloadFiles,
    IReadOnlyCollection<string> SessionFiles,
    IReadOnlyCollection<string> SiteDataPaths,
    IReadOnlyCollection<string> AutofillFiles);

public sealed class BrowserCleanupService
{
    private readonly Func<bool> _isWindows;
    private readonly Func<string, bool> _isProcessRunning;
    private readonly Func<IReadOnlyCollection<BrowserCleanupTarget>> _targetsProvider;

    public BrowserCleanupService(
        Func<bool>? isWindows = null,
        Func<string, bool>? isProcessRunning = null,
        Func<IReadOnlyCollection<BrowserCleanupTarget>>? targetsProvider = null)
    {
        _isWindows = isWindows ?? OperatingSystem.IsWindows;
        _isProcessRunning = isProcessRunning ?? CleanupService.IsProcessRunning;
        _targetsProvider = targetsProvider ?? DiscoverTargets;
    }

    public async Task<BrowserCleanupResult> CleanAsync(
        BrowserCleanupOptions options,
        IProgress<double>? progress,
        CancellationToken ct)
    {
        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        if (!_isWindows())
        {
            return new BrowserCleanupResult(0, 0, Array.Empty<string>(), 0, Array.Empty<string>(), Array.Empty<string>());
        }

        var targets = _targetsProvider();
        if (targets.Count == 0)
        {
            return new BrowserCleanupResult(0, 0, Array.Empty<string>(), 0, Array.Empty<string>(), Array.Empty<string>());
        }

        var totalSteps = EstimateSteps(targets, options);
        var completed = 0;
        var processed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var running = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var detected = targets.Select(t => t.BrowserName).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        long freed = 0;
        int files = 0;
        int locked = 0;

        foreach (var target in targets)
        {
            ct.ThrowIfCancellationRequested();
            var hadActivity = false;

            if (_isProcessRunning(target.ProcessName))
            {
                running.Add(target.BrowserName);
            }

            foreach (var profile in target.Profiles)
            {
                ct.ThrowIfCancellationRequested();

                if (options.Cache)
                {
                    foreach (var cachePath in profile.CachePaths)
                    {
                        var stats = await CleanupService.CleanDirectoryAsync(cachePath, ct).ConfigureAwait(false);
                        freed += stats.FreedBytes;
                        files += stats.FilesDeleted;
                        locked += stats.LockedItems;
                        hadActivity |= stats.HasActivity;
                        ReportProgress(progress, totalSteps, ref completed);
                    }
                }

                if (options.Cookies)
                {
                    var stats = DeleteFiles(profile.CookieFiles);
                    freed += stats.FreedBytes;
                    files += stats.FilesDeleted;
                    locked += stats.LockedItems;
                    hadActivity |= stats.HasActivity;
                    ReportProgress(progress, totalSteps, ref completed);
                }

                if (options.History)
                {
                    var stats = DeleteFiles(profile.HistoryFiles);
                    freed += stats.FreedBytes;
                    files += stats.FilesDeleted;
                    locked += stats.LockedItems;
                    hadActivity |= stats.HasActivity;
                    ReportProgress(progress, totalSteps, ref completed);
                }

                if (options.Downloads)
                {
                    var stats = DeleteFiles(profile.DownloadFiles);
                    freed += stats.FreedBytes;
                    files += stats.FilesDeleted;
                    locked += stats.LockedItems;
                    hadActivity |= stats.HasActivity;
                    ReportProgress(progress, totalSteps, ref completed);
                }

                if (options.Sessions)
                {
                    var stats = DeleteFiles(profile.SessionFiles);
                    freed += stats.FreedBytes;
                    files += stats.FilesDeleted;
                    locked += stats.LockedItems;
                    hadActivity |= stats.HasActivity;
                    ReportProgress(progress, totalSteps, ref completed);
                }

                if (options.SiteData)
                {
                    foreach (var path in profile.SiteDataPaths)
                    {
                        var siteStats = await CleanupService.CleanDirectoryAsync(path, ct).ConfigureAwait(false);
                        freed += siteStats.FreedBytes;
                        files += siteStats.FilesDeleted;
                        locked += siteStats.LockedItems;
                        hadActivity |= siteStats.HasActivity;
                        ReportProgress(progress, totalSteps, ref completed);
                    }
                }

                if (options.Autofill)
                {
                    var stats = DeleteFiles(profile.AutofillFiles);
                    freed += stats.FreedBytes;
                    files += stats.FilesDeleted;
                    locked += stats.LockedItems;
                    hadActivity |= stats.HasActivity;
                    ReportProgress(progress, totalSteps, ref completed);
                }
            }

            if (hadActivity || running.Contains(target.BrowserName))
            {
                processed.Add(target.BrowserName);
            }
        }

        return new BrowserCleanupResult(freed, files, processed.ToList(), locked, detected, running.ToList());
    }

    private static void ReportProgress(IProgress<double>? progress, int totalSteps, ref int completed)
    {
        if (progress is null || totalSteps <= 0)
        {
            return;
        }

        completed++;
        var value = Math.Clamp(completed * 100d / totalSteps, 0, 100);
        progress.Report(value);
    }

    private static int EstimateSteps(IReadOnlyCollection<BrowserCleanupTarget> targets, BrowserCleanupOptions options)
    {
        var steps = 0;
        foreach (var target in targets)
        {
            foreach (var profile in target.Profiles)
            {
                if (options.Cache)
                {
                    steps += profile.CachePaths.Count;
                }

                if (options.Cookies)
                {
                    steps++;
                }

                if (options.History)
                {
                    steps++;
                }

                if (options.Downloads)
                {
                    steps++;
                }

                if (options.Sessions)
                {
                    steps++;
                }

                if (options.SiteData)
                {
                    steps += profile.SiteDataPaths.Count;
                }

                if (options.Autofill)
                {
                    steps++;
                }
            }
        }

        return steps;
    }

    private static CleanupService.CleanupStats DeleteFiles(IEnumerable<string> paths)
    {
        var stats = new CleanupService.CleanupStats();
        foreach (var path in paths.Where(p => !string.IsNullOrWhiteSpace(p)))
        {
            stats = stats.Add(CleanupService.DeleteFileSafely(path));
        }

        return stats;
    }

    private static IReadOnlyCollection<BrowserCleanupTarget> DiscoverTargets()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var targets = new List<BrowserCleanupTarget>();

        void AddChromium(string name, string process, string userDataRoot)
        {
            if (string.IsNullOrWhiteSpace(userDataRoot) || !Directory.Exists(userDataRoot))
            {
                return;
            }

            var profiles = new List<BrowserCleanupProfile>();
            var defaultProfile = Path.Combine(userDataRoot, "Default");
            if (Directory.Exists(defaultProfile))
            {
                profiles.Add(BuildChromiumProfile("Default", defaultProfile));
            }

            foreach (var dir in Directory.EnumerateDirectories(userDataRoot, "Profile *", SearchOption.TopDirectoryOnly))
            {
                var namePart = new DirectoryInfo(dir).Name;
                profiles.Add(BuildChromiumProfile(namePart, dir));
            }

            if (profiles.Count > 0)
            {
                targets.Add(new BrowserCleanupTarget(name, process, profiles));
            }
        }

        void AddOpera(string name, string process, string profileRoot, string cacheRoot)
        {
            if (string.IsNullOrWhiteSpace(profileRoot) || !Directory.Exists(profileRoot))
            {
                return;
            }

            var profile = new BrowserCleanupProfile(
                name,
                profileRoot,
                cachePaths: new[]
                {
                    Path.Combine(cacheRoot, "Cache"),
                    Path.Combine(cacheRoot, "Code Cache"),
                    Path.Combine(cacheRoot, "GPUCache"),
                    Path.Combine(cacheRoot, "ShaderCache"),
                    Path.Combine(cacheRoot, "System Cache"),
                },
                cookieFiles: new[]
                {
                    Path.Combine(profileRoot, "Cookies"),
                    Path.Combine(profileRoot, "Network", "Cookies"),
                },
                historyFiles: new[]
                {
                    Path.Combine(profileRoot, "History"),
                    Path.Combine(profileRoot, "History-journal"),
                },
                downloadFiles: new[]
                {
                    Path.Combine(profileRoot, "History"),
                },
                sessionFiles: new[]
                {
                    Path.Combine(profileRoot, "Sessions"),
                    Path.Combine(profileRoot, "sessionstore.jsonlz4"),
                },
                siteDataPaths: new[]
                {
                    Path.Combine(profileRoot, "IndexedDB"),
                    Path.Combine(profileRoot, "Local Storage"),
                    Path.Combine(profileRoot, "Session Storage"),
                    Path.Combine(profileRoot, "Service Worker", "CacheStorage"),
                },
                autofillFiles: new[]
                {
                    Path.Combine(profileRoot, "Web Data"),
                });

            targets.Add(new BrowserCleanupTarget(name, process, new[] { profile }));
        }

        AddChromium(
            "Chrome",
            "chrome",
            Path.Combine(local, "Google", "Chrome", "User Data"));

        var operaProfile = Path.Combine(roaming, "Opera Software", "Opera Stable");
        var operaCache = Path.Combine(local, "Opera Software", "Opera Stable");
        AddOpera("Opera", "opera", operaProfile, operaCache);

        var operaGxProfile = Path.Combine(roaming, "Opera Software", "Opera GX Stable");
        var operaGxCache = Path.Combine(local, "Opera Software", "Opera GX Stable");
        AddOpera("Opera GX", "opera", operaGxProfile, operaGxCache);

        AddFirefox(targets, Path.Combine(roaming, "Mozilla", "Firefox", "Profiles"), Path.Combine(local, "Mozilla", "Firefox", "Profiles"));

        return targets;
    }

    private static BrowserCleanupProfile BuildChromiumProfile(string name, string root)
    {
        return new BrowserCleanupProfile(
            name,
            root,
            cachePaths: new[]
            {
                Path.Combine(root, "Cache"),
                Path.Combine(root, "Code Cache"),
                Path.Combine(root, "GPUCache"),
                Path.Combine(root, "ShaderCache"),
            },
            cookieFiles: new[]
            {
                Path.Combine(root, "Cookies"),
                Path.Combine(root, "Network", "Cookies"),
            },
            historyFiles: new[]
            {
                Path.Combine(root, "History"),
                Path.Combine(root, "History-journal"),
            },
            downloadFiles: new[]
            {
                Path.Combine(root, "History"),
            },
            sessionFiles: new[]
            {
                Path.Combine(root, "Sessions"),
            },
            siteDataPaths: new[]
            {
                Path.Combine(root, "IndexedDB"),
                Path.Combine(root, "Local Storage"),
                Path.Combine(root, "Session Storage"),
                Path.Combine(root, "Service Worker", "CacheStorage"),
            },
            autofillFiles: new[]
            {
                Path.Combine(root, "Web Data"),
            });
    }

    private static void AddFirefox(List<BrowserCleanupTarget> targets, string profileRoot, string cacheRoot)
    {
        if (!Directory.Exists(profileRoot))
        {
            return;
        }

        var profiles = new List<BrowserCleanupProfile>();
        foreach (var dir in Directory.EnumerateDirectories(profileRoot))
        {
            var profileName = new DirectoryInfo(dir).Name;
            var localCache = Path.Combine(cacheRoot, profileName, "cache2");
            profiles.Add(new BrowserCleanupProfile(
                profileName,
                dir,
                cachePaths: new[]
                {
                    localCache,
                    Path.Combine(dir, "cache2"),
                    Path.Combine(dir, "shader-cache"),
                },
                cookieFiles: new[]
                {
                    Path.Combine(dir, "cookies.sqlite"),
                    Path.Combine(dir, "cookies.sqlite-wal"),
                    Path.Combine(dir, "cookies.sqlite-shm"),
                },
                historyFiles: new[]
                {
                    Path.Combine(dir, "places.sqlite"),
                    Path.Combine(dir, "places.sqlite-wal"),
                    Path.Combine(dir, "places.sqlite-shm"),
                },
                downloadFiles: new[]
                {
                    Path.Combine(dir, "places.sqlite"),
                },
                sessionFiles: new[]
                {
                    Path.Combine(dir, "sessionstore.jsonlz4"),
                    Path.Combine(dir, "sessionstore-backups"),
                },
                siteDataPaths: new[]
                {
                    Path.Combine(dir, "storage"),
                    Path.Combine(dir, "storage", "default"),
                    Path.Combine(dir, "storage", "permanent"),
                    Path.Combine(dir, "storage", "temporary"),
                },
                autofillFiles: new[]
                {
                    Path.Combine(dir, "formhistory.sqlite"),
                }));
        }

        if (profiles.Count > 0)
        {
            targets.Add(new BrowserCleanupTarget("Firefox", "firefox", profiles));
        }
    }
}
