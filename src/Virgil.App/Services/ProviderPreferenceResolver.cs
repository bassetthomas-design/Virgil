using Virgil.App.Models;

namespace Virgil.App.Services;

public static class ProviderPreferenceResolver
{
    public static AiProvider Resolve(ProviderPreference preference, bool localEnabled, bool openAiEnabled)
        => preference switch
        {
            ProviderPreference.LocalFirst => ResolveLocalFirst(localEnabled, openAiEnabled),
            ProviderPreference.OpenAIFirst => ResolveOpenAiFirst(localEnabled, openAiEnabled),
            ProviderPreference.LocalOnly => localEnabled ? AiProvider.EmbeddedLlama : AiProvider.Disabled,
            ProviderPreference.OpenAIOnly => openAiEnabled ? AiProvider.OpenAI : AiProvider.Disabled,
            _ => ResolveLocalFirst(localEnabled, openAiEnabled)
        };

    private static AiProvider ResolveLocalFirst(bool localEnabled, bool openAiEnabled)
    {
        if (localEnabled)
        {
            return AiProvider.EmbeddedLlama;
        }

        return openAiEnabled ? AiProvider.OpenAI : AiProvider.Disabled;
    }

    private static AiProvider ResolveOpenAiFirst(bool localEnabled, bool openAiEnabled)
    {
        if (openAiEnabled)
        {
            return AiProvider.OpenAI;
        }

        return localEnabled ? AiProvider.EmbeddedLlama : AiProvider.Disabled;
    }
}
