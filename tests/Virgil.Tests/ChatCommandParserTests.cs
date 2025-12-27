using System.Threading.Tasks;
using FluentAssertions;
using Virgil.Services.Chat;
using Xunit;

namespace Virgil.Tests;

public class ChatCommandParserTests
{
    [Fact]
    public void ParseResponse_ShouldParseValidAction()
    {
        var parser = new ChatCommandParser();
        var json = "{\"text\":\"Hello\",\"command\":{\"type\":\"action\",\"action\":\"clean_quick\"}}";

        var result = parser.ParseResponse(json);

        result.Text.Should().Be("Hello");
        result.Command.Type.Should().Be(ChatCommandType.Action);
        result.Command.Action.Should().Be("clean_quick");
    }

    [Fact]
    public void ParseResponse_ShouldFallbackOnInvalidJson()
    {
        var parser = new ChatCommandParser();
        var json = "not a json";

        var result = parser.ParseResponse(json);

        result.Command.Type.Should().Be(ChatCommandType.None);
        result.Text.Should().Be("not a json");
    }

    [Fact]
    public void ParseResponse_ShouldIgnoreUnknownCommandType()
    {
        var parser = new ChatCommandParser();
        var json = "{\"text\":\"Hello\",\"command\":{\"type\":\"other\"}}";

        var result = parser.ParseResponse(json);

        result.Command.Type.Should().Be(ChatCommandType.None);
        result.Text.Should().Be("Hello");
    }
}
