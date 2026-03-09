using FluentAssertions;
using Virgil.Services.Assistant;
using Xunit;

namespace Virgil.Tests;

public class ActionIntentParserTests
{
    [Fact]
    public void Parse_ShouldResolveKnownIntent()
    {
        var parser = new ActionIntentParser();

        var intent = parser.Parse("lance un scan rapide defender");

        intent.Should().NotBeNull();
        intent!.IntentName.Should().Be("DefenderQuickScan");
        intent.ActionId.Should().Be("defender_quick_scan");
    }

    [Fact]
    public void Parse_ShouldReturnNull_WhenUnknownMessage()
    {
        var parser = new ActionIntentParser();

        var intent = parser.Parse("parle-moi de la pluie et du beau temps");

        intent.Should().BeNull();
    }
}
