using System;
using System.Threading;
using System.Threading.Tasks;

namespace Virgil.Services.Network;

public interface INetworkCommandRunner
{
    Task<NetworkCommandResult> RunAsync(string fileName, string arguments, TimeSpan timeout, CancellationToken ct = default);
}

public sealed record NetworkCommandResult(bool Success, string? Output = null, string? Error = null);
