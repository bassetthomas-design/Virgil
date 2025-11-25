using System.Threading.Tasks;

namespace Virgil.App.Services
{
    public interface IPerformanceActionsService
    {
        Task EnablePerfModeAsync();
        Task DisablePerfModeAsync();
        Task AnalyzeStartupAsync();
        Task KillGamingSessionProcessesAsync();
    }
}
