using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Virgil.Services;
using Xunit;

namespace Virgil.Tests;

public class CleanupServiceAction6Tests
{
    [Fact]
    public async Task BrowserLightClean_ShouldReportSummaryAndDeleteCaches()
    {
        using var workspace = new TempWorkspace();
        var chromeCache = workspace.CreateFolder("Chrome/Cache");
        var chromeGpu = workspace.CreateFolder("Chrome/GPUCache");
        var firefoxCache = workspace.CreateFolder("Firefox/cache2");

        workspace.CreateFile(chromeCache, "c1.bin", 1024);
        workspace.CreateFile(chromeGpu, "shader.bin", 2048);
        workspace.CreateFile(firefoxCache, "f1.tmp", 512);

        var browserPlan = new CleanupService.BrowserCleanPlan(new[]
        {
            new CleanupService.BrowserTarget("Chrome", new[] { chromeCache, chromeGpu }),
            new CleanupService.BrowserTarget("Firefox", new[] { firefoxCache })
        });

        var service = new CleanupService(
            () => new CleanupService.CleanupPlan(Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), TimeSpan.FromDays(1), false),
            () => browserPlan);

        var result = await service.RunBrowserLightAsync();

        result.Success.Should().BeTrue();
        result.Message.Should().Contain("Navigateurs traités");
        result.Message.Should().Contain("Quantité libérée");
        result.Message.Should().Contain("Fichiers supprimés");
        Directory.Exists(chromeCache).Should().BeTrue();
        Directory.EnumerateFiles(chromeCache, "*", SearchOption.AllDirectories).Should().BeEmpty();
        Directory.EnumerateFiles(firefoxCache, "*", SearchOption.AllDirectories).Should().BeEmpty();
    }

    [Fact]
    public async Task BrowserLightClean_ShouldNotTouchNonCacheFiles()
    {
        using var workspace = new TempWorkspace();
        var profileRoot = workspace.CreateFolder("ChromeProfile");
        var cache = workspace.CreateFolder(Path.Combine(profileRoot, "Cache"));
        var historyFile = workspace.CreateFile(profileRoot, "History", 512);
        workspace.CreateFile(cache, "c.bin", 128);

        var plan = new CleanupService.BrowserCleanPlan(new[]
        {
            new CleanupService.BrowserTarget("Chrome", new[] { cache })
        });

        var service = new CleanupService(
            () => new CleanupService.CleanupPlan(Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), TimeSpan.FromDays(1), false),
            () => plan);

        var result = await service.RunBrowserLightAsync();

        result.Success.Should().BeTrue();
        File.Exists(historyFile).Should().BeTrue();
    }

    [Fact]
    public async Task BrowserLightClean_ShouldMarkLockedBrowsersAsIgnored()
    {
        using var workspace = new TempWorkspace();
        var cache = workspace.CreateFolder("Edge/Cache");
        var lockedFile = workspace.CreateFile(cache, "lock.me", 64);

        using var lockHandle = new FileStream(lockedFile, FileMode.Open, FileAccess.Read, FileShare.None);

        var plan = new CleanupService.BrowserCleanPlan(new[]
        {
            new CleanupService.BrowserTarget("Edge", new[] { cache })
        });

        var service = new CleanupService(
            () => new CleanupService.CleanupPlan(Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), TimeSpan.FromDays(1), false),
            () => plan);

        var result = await service.RunBrowserLightAsync();

        result.Success.Should().BeTrue();
        result.Message.Should().Contain("ignorés");
        File.Exists(lockedFile).Should().BeTrue();
    }

    private sealed class TempWorkspace : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        private bool _disposed;

        public TempWorkspace()
        {
            Directory.CreateDirectory(_root);
        }

        public string CreateFolder(string relativePath)
        {
            var fullPath = Path.IsPathRooted(relativePath) ? relativePath : Path.Combine(_root, relativePath);
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
                if (Directory.Exists(_root))
                {
                    Directory.Delete(_root, recursive: true);
                }
            }
            catch
            {
                // Best effort cleanup.
            }

            _disposed = true;
        }
    }
}
