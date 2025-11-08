using System.Threading.Tasks;

namespace Virgil.App.Services;

public interface IPreflightService
{
    Task<bool> HasPowerAsync();
    Task<bool> HasNetworkAsync();
    Task<bool> HasFreeDiskAsync(string driveRoot, long minFreeBytes);
}
