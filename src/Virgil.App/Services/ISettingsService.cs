using System.Threading.Tasks;

namespace Virgil.App.Services;

public interface ISettingsService
{
    Task<CleanOptions> LoadAsync();
    Task SaveAsync(CleanOptions options);
}
