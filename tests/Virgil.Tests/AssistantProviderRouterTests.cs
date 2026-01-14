using System.Threading;
using System.Threading.Tasks;
using Virgil.App.Models;
using Virgil.App.Services;
using Virgil.Services.Assistant;
using Xunit;

namespace Virgil.Tests;

public class AssistantProviderRouterTests
{
    [Fact]
    public void LocalFirst_LocalReady_DoesNotCreateOpenAi()
    {
        var router = new AssistantProviderRouter();
        var localProvider = new StubProvider();
        var openAiCalled = false;

        var result = router.SelectProvider(
            ProviderPreference.LocalFirst,
            localEnabled: true,
            openAiEnabled: true,
            ensureLocalReady: () => true,
            createLocalProvider: () => localProvider,
            createOpenAiProvider: () =>
            {
                openAiCalled = true;
                return new StubProvider();
            });

        Assert.Same(localProvider, result);
        Assert.False(openAiCalled);
    }

    [Fact]
    public void LocalFirst_LocalFails_OpenAiDisabled_ReturnsLocalDiagnostics()
    {
        var router = new AssistantProviderRouter();
        var localProvider = new StubProvider();
        var openAiCalled = false;

        var result = router.SelectProvider(
            ProviderPreference.LocalFirst,
            localEnabled: true,
            openAiEnabled: false,
            ensureLocalReady: () => false,
            createLocalProvider: () => localProvider,
            createOpenAiProvider: () =>
            {
                openAiCalled = true;
                return new StubProvider();
            });

        Assert.Same(localProvider, result);
        Assert.False(openAiCalled);
    }

    [Fact]
    public void LocalFirst_LocalFails_OpenAiEnabled_FallsBackToOpenAi()
    {
        var router = new AssistantProviderRouter();
        var localProvider = new StubProvider();
        var openAiProvider = new StubProvider();

        var result = router.SelectProvider(
            ProviderPreference.LocalFirst,
            localEnabled: true,
            openAiEnabled: true,
            ensureLocalReady: () => false,
            createLocalProvider: () => localProvider,
            createOpenAiProvider: () => openAiProvider);

        Assert.Same(openAiProvider, result);
    }

    private sealed class StubProvider : IAssistantProvider
    {
        public Task<AssistantReply> AskAsync(string userMessage, AssistantContext ctx, CancellationToken ct = default)
            => Task.FromResult(new AssistantReply(string.Empty, System.Array.Empty<ProposedAction>()));
    }
}
