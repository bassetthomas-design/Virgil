using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Virgil.Services;
using Virgil.Services.Abstractions;
using Xunit;

namespace Virgil.Tests;

public class RamboModeTests
{
    [Fact]
    public async Task RamboMode_ShouldSkip_WhenNotConfirmed()
    {
        var service = new SpecialService(
            new FakeReloader(),
            new FakePrompt(confirmRambo: false),
            new FakeChat());

        var result = await service.RamboModeAsync(CancellationToken.None);

        result.Status.Should().Be(Virgil.Core.Models.ActionResultStatus.Skipped);
    }

    private sealed class FakeReloader : IConfigurationReloader
    {
        public Task<ConfigurationReloadResult> ReloadAsync(CancellationToken ct = default)
            => Task.FromResult(new ConfigurationReloadResult(true, true, true, true, true, true));
    }

    private sealed class FakePrompt : IConfirmationPrompt
    {
        private readonly bool _confirmRambo;

        public FakePrompt(bool confirmRambo)
        {
            _confirmRambo = confirmRambo;
        }

        public Task<bool> ConfirmAsync(string message, CancellationToken ct = default) => Task.FromResult(true);

        public Task<bool> ConfirmRamboAsync(CancellationToken ct = default) => Task.FromResult(_confirmRambo);

        public Task<RamboErrorDialogResult> AskRamboErrorDecisionAsync(string friendlyMessage, CancellationToken ct = default)
            => Task.FromResult(new RamboErrorDialogResult
            {
                Decision = RamboErrorDecision.Stop,
                AutoContinueSimilarErrors = false
            });

    }

    private sealed class FakeChat : IChatService
    {
        public List<string> Messages { get; } = new();

        public Task InfoAsync(string message, CancellationToken ct = default)
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }

        public Task WarnAsync(string message, CancellationToken ct = default)
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }

        public Task ErrorAsync(string message, CancellationToken ct = default)
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }

        public Task ThanosWipeAsync(bool preservePinned = true, CancellationToken ct = default) => Task.CompletedTask;
    }
}
