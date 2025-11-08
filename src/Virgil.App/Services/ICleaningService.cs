using System.Threading.Tasks;

namespace Virgil.App.Services;

public interface ICleaningService
{
    Task<int> CleanIntelligentAsync(bool dryRun = false);
}
