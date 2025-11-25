using System.Threading.Tasks;

namespace Virgil.App.Services
{
    public interface INetworkActionsService
    {
        Task RunDiagnosticsAsync();
        Task SoftResetAsync();
        Task HardResetAsync();
        Task RunLatencyTestAsync();
    }
}
