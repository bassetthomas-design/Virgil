using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Virgil.Services;
using Xunit;

namespace Virgil.Tests;

public class SystemTempCleanupAction18Tests
{
    [Fact]
    public async Task SystemTempCleanup_ShouldReportFreedSpaceAndCategories()
    {
        using var workspace = new TempWorkspace();
        var tempDir = workspace.CreateFolder("temp-files");
        var updatesDir = workspace.CreateFolder("updates");
        var logsDir = workspace.CreateFolder("logs");

        var tempFile = workspace.CreateFile(tempDir, "temp1.tmp", 2048);
        var oldCab = workspace.CreateFile(updatesDir, "old.cab", 4096);
        File.SetLastWriteTimeUtc(oldCab, DateTime.UtcNow.AddDays(-3));
        var oldLog = workspace.CreateFile(logsDir, "old.log", 512);
        File.SetLastWriteTimeUtc(oldLog, DateTime.UtcNow.AddDays(-10));
        var freshLog = workspace.CreateFile(logsDir, "fresh.log", 512);

        var categories = new[]
        {
            new CleanupService.SystemTempCategory("Temp Win", new[] { tempDir }, _ => true, WindowsOnly: false),
            new CleanupService.SystemTempCategory("Update Cache", new[] { updatesDir }, fi => fi.LastWriteTimeUtc < DateTime.UtcNow.AddDays(-1), WindowsOnly: false),
            new CleanupService.SystemTempCategory("Logs", new[] { logsDir }, fi => fi.LastWriteTimeUtc < DateTime.UtcNow.AddDays(-7), WindowsOnly: false),
        };

        var plan = new CleanupService.SystemTempCleanupPlan(categories);
        var quickPlan = new CleanupService.CleanupPlan(Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), TimeSpan.Zero, false);
        var service = new CleanupService(() => quickPlan, systemTempPlanFactory: () => plan);

        var result = await service.RunSystemTempCleanupAsync();

        result.Success.Should().BeTrue();
        result.Message.Should().Contain("Espace libéré");
        result.Message.Should().Contain("Fichiers supprimés");
        result.Message.Should().Contain("Catégories nettoyées");
        result.Message.Should().Contain("Temp Win");
        File.Exists(tempFile).Should().BeFalse();
        File.Exists(oldCab).Should().BeFalse();
        File.Exists(oldLog).Should().BeFalse();
        File.Exists(freshLog).Should().BeTrue();
    }

    [Fact]
    public async Task SystemTempCleanup_ShouldHandleLockedFilesGracefully()
    {
        using var workspace = new TempWorkspace();
        var tempDir = workspace.CreateFolder("temp-files");

        var removable = workspace.CreateFile(tempDir, "free.tmp", 1024);
        var locked = workspace.CreateFile(tempDir, "locked.tmp", 1024);

        using var handle = File.Open(locked, FileMode.Open, FileAccess.Read, FileShare.None);

        var categories = new[]
        {
            new CleanupService.SystemTempCategory("Temp Win", new[] { tempDir }, _ => true, WindowsOnly: false),
        };

        var plan = new CleanupService.SystemTempCleanupPlan(categories);
        var quickPlan = new CleanupService.CleanupPlan(Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), TimeSpan.Zero, false);
        var service = new CleanupService(() => quickPlan, systemTempPlanFactory: () => plan);

        var result = await service.RunSystemTempCleanupAsync();

        result.Success.Should().BeTrue();
        result.Message.Should().Contain("Espace libéré");
        result.TryGetDetails(out var details).Should().BeTrue();
        details.Should().Contain("verrouillé");
        File.Exists(removable).Should().BeFalse();
        File.Exists(locked).Should().BeTrue();
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
                // Best effort in tests.
            }

            _disposed = true;
        }
    }
}
