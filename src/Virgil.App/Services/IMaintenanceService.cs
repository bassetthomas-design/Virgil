using System.Threading.Tasks;

namespace Virgil.App.Services;

public interface IMaintenanceService
{
    Task<int> RunWingetUpgradeAsync();
    Task<int> RunWindowsUpdateAsync();
    Task<int> RunDefenderUpdateAndQuickScanAsync();
}
