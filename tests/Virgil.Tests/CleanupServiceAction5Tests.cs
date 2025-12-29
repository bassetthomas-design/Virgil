using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Virgil.Services;
using Xunit;

namespace Virgil.Tests;

public class CleanupServiceAction5Tests
{
    [Fact]
    public async Task QuickClean_ShouldReturnChatFriendlySummary()
    {
        using var workspace = new TempWorkspace();
        var tempDir = workspace.CreateFolder("temp");
        var cacheDir = workspace.CreateFolder("cache");
        var logDir = workspace.CreateFolder("logs");

        workspace.CreateFile(tempDir, "temp1.tmp", 2048);
        workspace.CreateFile(cacheDir, "cache.bin", 1024);
        var oldLog = workspace.CreateFile(logDir, "old.log", 512);
        File.SetLastWriteTimeUtc(oldLog, DateTime.UtcNow.AddDays(-10));

        var plan = new CleanupService.CleanupPlan(
            new[] { tempDir },
            new[] { cacheDir },
            new[] { logDir },
            Array.Empty<string>(),
            TimeSpan.FromDays(7),
            EmptyRecycleBin: false);

        var service = new CleanupService(() => plan);

        var result = await service.RunSimpleAsync();

        result.Success.Should().BeTrue();
        result.Message.Should().Contain("Quantité libérée");
        result.Message.Should().Contain("Nombre de fichiers");
        File.Exists(oldLog).Should().BeFalse();
    }

    [Fact]
    public async Task QuickClean_ShouldRespectBrowserExclusions()
    {
        using var workspace = new TempWorkspace();
        var tempDir = workspace.CreateFolder("temp");

        var excludedDir = workspace.CreateFolder(Path.Combine(tempDir, "Chrome", "Cache"));
        var excludedFile = workspace.CreateFile(excludedDir, "browser.tmp", 256);
        var removableFile = workspace.CreateFile(tempDir, "remove.me", 256);

        var plan = new CleanupService.CleanupPlan(
            new[] { tempDir },
            Array.Empty<string>(),
            Array.Empty<string>(),
            new[] { "Chrome" },
            TimeSpan.FromDays(1),
            EmptyRecycleBin: false);

        var service = new CleanupService(() => plan);

        var result = await service.RunSimpleAsync();

        result.Success.Should().BeTrue();
        File.Exists(excludedFile).Should().BeTrue();
        File.Exists(removableFile).Should().BeFalse();
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
                // Cleanup best-effort in tests.
            }

            _disposed = true;
        }
    }
}
