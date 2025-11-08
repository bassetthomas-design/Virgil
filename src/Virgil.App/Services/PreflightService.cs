using System;
using System.IO;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Virgil.App.Services;

public class PreflightService : IPreflightService
{
    public Task<bool> HasPowerAsync()
    {
        try { return Task.FromResult(SystemInformation.PowerStatus.PowerLineStatus == PowerLineStatus.Online); } catch { return Task.FromResult(true); }
    }
    public Task<bool> HasNetworkAsync()
    {
        try { return Task.FromResult(NetworkInterface.GetIsNetworkAvailable()); } catch { return Task.FromResult(true); }
    }
    public Task<bool> HasFreeDiskAsync(string driveRoot, long minFreeBytes)
    {
        try { var d = new DriveInfo(driveRoot); return Task.FromResult(d.AvailableFreeSpace >= minFreeBytes); } catch { return Task.FromResult(true); }
    }
}
