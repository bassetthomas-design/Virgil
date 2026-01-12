using System;
using System.IO;
using System.Linq;
using Virgil.Core.Config;
using Xunit;

namespace Virgil.Tests;

public class ModelLocatorTests
{
    [Fact]
    public void GetCandidatePaths_BuildsPortableThenProgramData()
    {
        var locator = new ModelLocator("model.gguf");

        var paths = locator.GetCandidatePaths().ToArray();

        var expectedBase = Path.Combine(AppContext.BaseDirectory, "AI", "Models", "model.gguf");
        var expectedFallback = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Virgil",
            "AI",
            "Models",
            "model.gguf");

        Assert.Equal(new[] { expectedBase, expectedFallback }, paths);
    }
}
