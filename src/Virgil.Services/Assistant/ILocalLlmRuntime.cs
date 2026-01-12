using System.Threading;
using System.Threading.Tasks;

namespace Virgil.Services.Assistant;

public interface ILocalLlmRuntime
{
    bool IsRuntimeAvailable();
    string RuntimePathUsed { get; }
    Task StartAsync(CancellationToken ct = default);
    Task<bool> HealthCheckAsync(CancellationToken ct = default);
}
