using System.Threading.Tasks;

namespace Virgil.App.Services;

public interface ISystemActionsService
{
    Task<int> WsResetAsync();
    Task<int> RebuildExplorerCachesAsync();
    Task<int> EmptyRecycleBinAsync();
}
