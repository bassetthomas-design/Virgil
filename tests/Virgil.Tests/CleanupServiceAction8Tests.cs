using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Virgil.Services;
using Xunit;

namespace Virgil.Tests;

public class CleanupServiceAction8Tests
{
    [Fact]
    public async Task AdvancedClean_ShouldRefuseWithoutAdmin()
    {
        using var workspace = new TempWorkspace();
        var systemTemp = workspace.CreateFolder("win/Temp");
        workspace.CreateFile(systemTemp, "locked.tmp", 128);

        var plan = new[]
        {
            new CleanupService.AdvancedStep("Temp système", new[] { systemTemp })
        };

        var service = new CleanupService(
            () => new CleanupService.CleanupPlan(Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), TimeSpan.FromDays(1), false),
            () => new CleanupService.BrowserCleanPlan(Array.Empty<CleanupService.BrowserTarget>()),
            advancedPlanFactory: () => plan,
            isWindows: () => true,
            isAdministrator: () => false);

        var result = await service.RunAdvancedAsync();

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("administrateur");
        File.Exists(Path.Combine(systemTemp, "locked.tmp")).Should().BeTrue();
    }

    [Fact]
    public async Task AdvancedClean_ShouldReportFreedSpaceAndCategories()
    {
        using var workspace = new TempWorkspace();
        var wuCache = workspace.CreateFolder("windows/SoftwareDistribution/Download");
        var logDir = workspace.CreateFolder("windows/Logs");

        workspace.CreateFile(wuCache, "update.bin", 5 * 1024 * 1024);
        var oldLog = workspace.CreateFile(logDir, "system.log", 1024);
        File.SetLastWriteTimeUtc(oldLog, DateTime.UtcNow.AddDays(-10));

        var plan = new[]
        {
            new CleanupService.AdvancedStep("Cache WU", new[] { wuCache }),
            new CleanupService.AdvancedStep("Logs système", new[] { logDir }, FileFilter: fi => fi.Extension.Equals(".log", StringComparison.OrdinalIgnoreCase))
        };

        var service = new CleanupService(
            () => new CleanupService.CleanupPlan(Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), TimeSpan.FromDays(1), false),
            () => new CleanupService.BrowserCleanPlan(Array.Empty<CleanupService.BrowserTarget>()),
            advancedPlanFactory: () => plan,
            isWindows: () => true,
            isAdministrator: () => true);

        var result = await service.RunAdvancedAsync();

        result.Success.Should().BeTrue();
        result.Message.Should().Contain("Espace libéré");
        result.Message.Should().Contain("Mo");
        result.TryGetDetails(out var details).Should().BeTrue();
        details.Should().Contain("Cache WU");
        details.Should().Contain("Logs système");
        Directory.EnumerateFiles(wuCache, "*", SearchOption.AllDirectories).Should().BeEmpty();
    }

    [Fact]
    public async Task AdvancedClean_ShouldNotTouchUserDocuments()
    {
        using var workspace = new TempWorkspace();
        var documents = workspace.CreateFolder("Users/Documents");
        var docFile = workspace.CreateFile(documents, "keep.txt", 256);
        var tempFolder = workspace.CreateFolder("TempArea");
        workspace.CreateFile(tempFolder, "remove.me", 512);

        var plan = new[]
        {
            new CleanupService.AdvancedStep("Temp", new[] { tempFolder })
        };

        var service = new CleanupService(
            () => new CleanupService.CleanupPlan(Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), TimeSpan.FromDays(1), false),
            () => new CleanupService.BrowserCleanPlan(Array.Empty<CleanupService.BrowserTarget>()),
            advancedPlanFactory: () => plan,
            isWindows: () => true,
            isAdministrator: () => true);

        var result = await service.RunAdvancedAsync();

        result.Success.Should().BeTrue();
        File.Exists(docFile).Should().BeTrue();
        Directory.Exists(tempFolder).Should().BeTrue();
        Directory.EnumerateFiles(tempFolder, "*", SearchOption.AllDirectories).Should().BeEmpty();
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
            var fullPath = Path.Combine(_root, relativePath);
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
                // Best effort cleanup for tests.
            }

            _disposed = true;
        }
    }
}
