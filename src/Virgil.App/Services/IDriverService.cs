using System.Threading.Tasks;

namespace Virgil.App.Services;

public interface IDriverService
{
    Task<int> BackupDriversAsync(string outDir);
    Task<int> ScanAndUpdateDriversAsync();
}
