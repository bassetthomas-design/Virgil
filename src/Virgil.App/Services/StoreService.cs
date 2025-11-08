using System.Threading.Tasks;

namespace Virgil.App.Services;

public class StoreService : IStoreService
{
    private readonly IProcessRunner _runner;
    public StoreService(IProcessRunner runner) => _runner = runner;

    public Task<int> UpdateStoreAppsAsync()
    {
        // Tentative simple: winget sur source msstore + wsreset
        // 1) wsreset -i pour réinitialiser le cache
        // 2) winget upgrade --source msstore --all
        return _runner.RunAsync("cmd.exe", "/c wsreset -i && winget upgrade --source msstore --all --accept-package-agreements --accept-source-agreements", elevate:true);
    }
}
