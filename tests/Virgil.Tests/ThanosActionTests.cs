using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Virgil.App.Chat;
using Xunit;

namespace Virgil.Tests;

public class ThanosActionTests
{
    [Fact]
    public async Task ClearHistory_ShouldResetStateAndPostFinalMessage()
    {
        var chat = new ChatService();
        await chat.Post("hello");
        await chat.Post("assistant", "world");

        await chat.ClearHistoryAsync(applyThanosEffect: false, effectDurationMs: 0, ct: CancellationToken.None, startAutoEraseTimer: false);

        chat.Messages.Should().ContainSingle(m => m.Content == "Tout a disparu.");
    }

    [Fact]
    public async Task ClearHistory_ShouldBeIdempotent()
    {
        var chat = new ChatService();

        await chat.ClearHistoryAsync(applyThanosEffect: false, effectDurationMs: 0, ct: CancellationToken.None, startAutoEraseTimer: false);
        await chat.ClearHistoryAsync(applyThanosEffect: false, effectDurationMs: 0, ct: CancellationToken.None, startAutoEraseTimer: false);

        chat.Messages.Should().ContainSingle(m => m.Content == "Tout a disparu.");
    }

    [Fact]
    public async Task AutoErase_TimerShouldTriggerAfterDelay()
    {
        var chat = new ChatService { AutoEraseDelay = TimeSpan.FromMilliseconds(50) };
        var clears = 0;
        chat.HistoryCleared += (_, __) => Interlocked.Increment(ref clears);

        await chat.Post("ping");
        await chat.ClearHistoryAsync(applyThanosEffect: false, effectDurationMs: 0, ct: CancellationToken.None, startAutoEraseTimer: true);

        await Task.Delay(140);

        clears.Should().BeGreaterOrEqualTo(2);
        chat.Messages.Should().ContainSingle(m => m.Content == "Tout a disparu.");
    }

    [Fact]
    public async Task NewActivity_ShouldRearmTimer()
    {
        var chat = new ChatService { AutoEraseDelay = TimeSpan.FromMilliseconds(40) };

        await chat.ClearHistoryAsync(applyThanosEffect: false, effectDurationMs: 0, ct: CancellationToken.None, startAutoEraseTimer: true);
        await chat.Post("nouveau");

        await Task.Delay(120);

        chat.Messages.Should().ContainSingle(m => m.Content == "Tout a disparu.");
    }
}
