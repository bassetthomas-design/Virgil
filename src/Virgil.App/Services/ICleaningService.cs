using System.Threading.Tasks;

namespace Virgil.App.Services;

public interface ICleaningService
{
    Task<int> CleanIntelligentAsync(bool dryRun = false);
    Task<CleanStats> CleanAdvancedAsync(CleanOptions options, bool dryRun = false);
}
