using System.Threading.Tasks;

namespace Virgil.App.Services;

public interface IStoreService
{
    Task<int> UpdateStoreAppsAsync();
}
