using Virgil.App.Models;
using Virgil.App.Services;
using Xunit;

namespace Virgil.Tests;

public class ProviderPreferenceResolverTests
{
    [Fact]
    public void Resolve_LocalFirst_PrefersLocalWhenAvailable()
    {
        var result = ProviderPreferenceResolver.Resolve(ProviderPreference.LocalFirst, localEnabled: true, openAiEnabled: true);

        Assert.Equal(AiProvider.EmbeddedLlama, result);
    }

    [Fact]
    public void Resolve_LocalFirst_FallsBackToOpenAiWhenLocalUnavailable()
    {
        var result = ProviderPreferenceResolver.Resolve(ProviderPreference.LocalFirst, localEnabled: false, openAiEnabled: true);

        Assert.Equal(AiProvider.OpenAI, result);
    }

    [Fact]
    public void Resolve_OpenAiFirst_PrefersOpenAiWhenAvailable()
    {
        var result = ProviderPreferenceResolver.Resolve(ProviderPreference.OpenAIFirst, localEnabled: true, openAiEnabled: true);

        Assert.Equal(AiProvider.OpenAI, result);
    }

    [Fact]
    public void Resolve_OpenAiFirst_FallsBackToLocalWhenOpenAiUnavailable()
    {
        var result = ProviderPreferenceResolver.Resolve(ProviderPreference.OpenAIFirst, localEnabled: true, openAiEnabled: false);

        Assert.Equal(AiProvider.EmbeddedLlama, result);
    }

    [Fact]
    public void Resolve_LocalOnly_DisablesWhenLocalUnavailable()
    {
        var result = ProviderPreferenceResolver.Resolve(ProviderPreference.LocalOnly, localEnabled: false, openAiEnabled: true);

        Assert.Equal(AiProvider.Disabled, result);
    }

    [Fact]
    public void Resolve_OpenAiOnly_DisablesWhenOpenAiUnavailable()
    {
        var result = ProviderPreferenceResolver.Resolve(ProviderPreference.OpenAIOnly, localEnabled: true, openAiEnabled: false);

        Assert.Equal(AiProvider.Disabled, result);
    }
}
