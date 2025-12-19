using System.Runtime.InteropServices;
using Virgil.Core;
using Xunit;

namespace Virgil.Tests;

public class MonitoringServiceTests
{
    private static bool IsWindowsPlatform() => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    [Fact]
    public void MonitoringService_ShouldInstantiateWithLatestMetrics()
    {
        // Skip on non-Windows platforms as PerformanceCounters are Windows-specific
        if (!IsWindowsPlatform())
        {
            return;
        }

        // Arrange & Act
        var service = new MonitoringService();

        // Assert
        Assert.NotNull(service);
        Assert.NotNull(service.LatestMetrics);
    }

    [Fact]
    public void LatestMetrics_ShouldHaveInitializedProperties()
    {
        // Skip on non-Windows platforms as PerformanceCounters are Windows-specific
        if (!IsWindowsPlatform())
        {
            return;
        }

        // Arrange
        var service = new MonitoringService();

        // Act
        var metrics = service.LatestMetrics;

        // Assert
        Assert.NotNull(metrics);
        Assert.True(metrics.CpuUsage >= 0);
        Assert.True(metrics.MemoryUsage >= 0);
    }
}
