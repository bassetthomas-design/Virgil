using System.Threading.Tasks;

namespace Virgil.App.Services
{
    public interface ISpecialActionsService
    {
        Task RamboRepairAsync();
        Task PurgeChatHistoryAsync();
        Task ReloadSettingsAsync();
        Task RescanMonitoringAsync();
    }
}
