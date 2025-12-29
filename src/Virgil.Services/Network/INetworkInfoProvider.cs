using System.Linq;
using System.Net.NetworkInformation;

namespace Virgil.Services.Network;

public interface INetworkInfoProvider
{
    string? GetDefaultGateway();
}

public sealed class RuntimeNetworkInfoProvider : INetworkInfoProvider
{
    public string? GetDefaultGateway()
    {
        var gateway = NetworkInterface
            .GetAllNetworkInterfaces()
            .Where(nic => nic.OperationalStatus == OperationalStatus.Up)
            .SelectMany(nic => nic.GetIPProperties().GatewayAddresses)
            .Select(g => g?.Address?.ToString())
            .FirstOrDefault(address => !string.IsNullOrWhiteSpace(address));

        return gateway;
    }
}
