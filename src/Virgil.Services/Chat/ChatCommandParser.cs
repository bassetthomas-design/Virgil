using System.Text.Json;

namespace Virgil.Services.Chat;

public interface IChatCommandParser
{
    ChatEngineResult ParseResponse(string rawResponse);
}

public sealed class ChatCommandParser : IChatCommandParser
{
    public ChatEngineResult ParseResponse(string rawResponse)
    {
        if (string.IsNullOrWhiteSpace(rawResponse))
        {
            return ChatEngineResult.Empty;
        }

        try
        {
            var payload = JsonSerializer.Deserialize<ModelResponse>(rawResponse, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (payload?.Text is null)
            {
                return new ChatEngineResult(rawResponse.Trim(), ChatCommand.None);
            }

            var cleanedText = payload.Text.Trim();
            var command = ParseCommand(payload.Command, rawResponse);
            return new ChatEngineResult(cleanedText, command);
        }
        catch (JsonException)
        {
            return new ChatEngineResult(rawResponse.Trim(), ChatCommand.None);
        }
    }

    private static ChatCommand ParseCommand(CommandPayload? command, string raw)
    {
        if (command is null || string.IsNullOrWhiteSpace(command.Type))
        {
            return ChatCommand.None;
        }

        var normalizedType = command.Type.Trim().ToLowerInvariant();
        if (normalizedType != "action")
        {
            return ChatCommand.None;
        }

        var actionName = command.Action?.Trim();
        return new ChatCommand(ChatCommandType.Action, actionName, raw);
    }

    private sealed class ModelResponse
    {
        public string? Text { get; set; }
        public CommandPayload? Command { get; set; }
    }

    private sealed class CommandPayload
    {
        public string? Type { get; set; }
        public string? Action { get; set; }
    }
}
