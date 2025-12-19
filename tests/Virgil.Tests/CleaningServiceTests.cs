using Virgil.Core;
using Xunit;

namespace Virgil.Tests;

public class CleaningServiceTests
{
    [Fact]
    public void CleaningService_ShouldInstantiateWithoutError()
    {
        // Arrange & Act
        var service = new CleaningService();

        // Assert
        Assert.NotNull(service);
    }

    [Fact]
    public void GetTempFilesSize_ShouldReturnNonNegativeValue()
    {
        // Arrange
        var service = new CleaningService();

        // Act
        var size = service.GetTempFilesSize();

        // Assert
        Assert.True(size >= 0, "Temp files size should be non-negative");
    }
}
