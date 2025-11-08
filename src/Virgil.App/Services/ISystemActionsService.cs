using System.Threading.Tasks;

namespace Virgil.App.Services;

public interface ISystemActionsService
{
    // présents sur main
    Task<int> WsResetAsync();
    Task<int> RebuildExplorerCachesAsync();
    Task<int> EmptyRecycleBinAsync();

    // ajout Rambo
    Task<int> RestartExplorerAsync();
    Task<int> RebuildExplorerCachesAndRestartAsync();
}
