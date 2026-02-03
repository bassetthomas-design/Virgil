using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Virgil.Services;
using Xunit;

namespace Virgil.Tests;

public class CleanupServiceAction9Tests
{
    [Fact]
    public async Task BrowserDeepClean_ShouldContinueWhenBrowserRunning()
    {
        using var workspace = new TempWorkspace();
        var profile = workspace.CreateFolder("Chrome/User Data/Default");
        var cache = workspace.CreateFolder(Path.Combine(profile, "Cache"));
        workspace.CreateFile(cache, "c.bin", 128);

        var browserService = CreateBrowserCleanupService(
            isProcessRunning: name => name.Equals("chrome", StringComparison.OrdinalIgnoreCase),
            new BrowserCleanupTarget("Chrome", "chrome", new[]
            {
                new BrowserCleanupProfile(
                    "Default",
                    profile,
                    CachePaths: new[] { cache },
                    CookieFiles: Array.Empty<string>(),
                    HistoryFiles: Array.Empty<string>(),
                    DownloadFiles: Array.Empty<string>(),
                    SessionFiles: Array.Empty<string>(),
                    SiteDataPaths: Array.Empty<string>(),
                    AutofillFiles: Array.Empty<string>())
            }));

        var service = new CleanupService(
            () => new CleanupService.CleanupPlan(Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), TimeSpan.FromDays(1), false),
            () => new CleanupService.BrowserCleanPlan(Array.Empty<CleanupService.BrowserTarget>()),
            browserCleanupService: browserService,
            isWindows: () => true);

        var result = await service.RunBrowserDeepAsync();

        result.Success.Should().BeTrue();
        result.Message.Should().Contain("Certaines données");
    }

    [Fact]
    public async Task BrowserDeepClean_ShouldCleanProfilesAndReportSummary()
    {
        using var workspace = new TempWorkspace();
        var profile = workspace.CreateFolder("Chrome/User Data/Default");
        var cache = workspace.CreateFolder(Path.Combine(profile, "Cache"));
        workspace.CreateFile(cache, "c.bin", 1024);
        var cookies = workspace.CreateFile(profile, "Cookies", 2048);
        var history = workspace.CreateFile(profile, "History", 4096);
        var indexed = workspace.CreateFolder(Path.Combine(profile, "IndexedDB"));
        workspace.CreateFile(indexed, "db", 1024);

        var browserService = CreateBrowserCleanupService(
            isProcessRunning: _ => false,
            new BrowserCleanupTarget("Chrome", "chrome", new[]
            {
                new BrowserCleanupProfile(
                    "Default",
                    profile,
                    CachePaths: new[] { cache },
                    CookieFiles: new[] { cookies },
                    HistoryFiles: new[] { history },
                    DownloadFiles: new[] { history },
                    SessionFiles: Array.Empty<string>(),
                    SiteDataPaths: new[] { indexed },
                    AutofillFiles: Array.Empty<string>())
            }));

        var service = new CleanupService(
            () => new CleanupService.CleanupPlan(Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), TimeSpan.FromDays(1), false),
            () => new CleanupService.BrowserCleanPlan(Array.Empty<CleanupService.BrowserTarget>()),
            browserCleanupService: browserService,
            isWindows: () => true);

        var result = await service.RunBrowserDeepAsync();

        result.Success.Should().BeTrue();
        result.Message.Should().Contain("Nettoyage navigateurs terminé");
        result.Message.Should().Contain("libérés");

        File.Exists(cookies).Should().BeFalse();
        File.Exists(history).Should().BeFalse();
        Directory.EnumerateFiles(cache, "*", SearchOption.AllDirectories).Should().BeEmpty();
        Directory.Exists(indexed).Should().BeTrue();
        Directory.EnumerateFileSystemEntries(indexed).Should().BeEmpty();
    }

    [Fact]
    public async Task BrowserDeepClean_ShouldIgnoreMissingProfiles()
    {
        using var workspace = new TempWorkspace();
        var profile = Path.Combine(workspace.Root, "NonExistentProfile");

        var browserService = CreateBrowserCleanupService(
            isProcessRunning: _ => false,
            new BrowserCleanupTarget("Chrome", "chrome", new[]
            {
                new BrowserCleanupProfile(
                    "Ghost",
                    profile,
                    CachePaths: Array.Empty<string>(),
                    CookieFiles: Array.Empty<string>(),
                    HistoryFiles: Array.Empty<string>(),
                    DownloadFiles: Array.Empty<string>(),
                    SessionFiles: Array.Empty<string>(),
                    SiteDataPaths: Array.Empty<string>(),
                    AutofillFiles: Array.Empty<string>())
            }));

        var service = new CleanupService(
            () => new CleanupService.CleanupPlan(Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), TimeSpan.FromDays(1), false),
            () => new CleanupService.BrowserCleanPlan(Array.Empty<CleanupService.BrowserTarget>()),
            browserCleanupService: browserService,
            isWindows: () => true);

        var result = await service.RunBrowserDeepAsync();

        result.Success.Should().BeTrue();
        result.Message.Should().Contain("aucun");
        Directory.Exists(profile).Should().BeFalse();
    }

    private static BrowserCleanupService CreateBrowserCleanupService(
        Func<string, bool> isProcessRunning,
        params BrowserCleanupTarget[] targets)
    {
        return new BrowserCleanupService(
            isWindows: () => true,
            isProcessRunning: isProcessRunning,
            targetsProvider: () => targets);
    }

    private sealed class TempWorkspace : IDisposable
    {
        public string Root { get; }
        private bool _disposed;

        public TempWorkspace()
        {
            Root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
        }

        public string CreateFolder(string relativePath)
        {
            var fullPath = Path.IsPathRooted(relativePath) ? relativePath : Path.Combine(Root, relativePath);
            Directory.CreateDirectory(fullPath);
            return fullPath;
        }

        public string CreateFile(string folder, string fileName, int sizeBytes)
        {
            Directory.CreateDirectory(folder);
            var fullPath = Path.Combine(folder, fileName);
            var bytes = new byte[sizeBytes];
            new Random().NextBytes(bytes);
            File.WriteAllBytes(fullPath, bytes);
            return fullPath;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                if (Directory.Exists(Root))
                {
                    Directory.Delete(Root, recursive: true);
                }
            }
            catch
            {
                // best effort
            }

            _disposed = true;
        }
    }
}
