using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Virgil.Services.Network;

public interface IPingClient
{
    Task<PingAttemptResult> SendAsync(string host, int timeoutMs, CancellationToken ct = default);
}

public sealed record PingAttemptResult(PingAttemptStatus Status, long RoundtripTimeMs = 0);

public enum PingAttemptStatus
{
    Success,
    Timeout,
    DnsError,
    Failed
}

public sealed class RuntimePingClient : IPingClient
{
    public async Task<PingAttemptResult> SendAsync(string host, int timeoutMs, CancellationToken ct = default)
    {
        using var ping = new Ping();

        try
        {
            var reply = await ping.SendPingAsync(host, timeoutMs).WaitAsync(ct);
            if (reply.Status == IPStatus.Success)
            {
                return new PingAttemptResult(PingAttemptStatus.Success, reply.RoundtripTime);
            }

            if (reply.Status == IPStatus.TimedOut)
            {
                return new PingAttemptResult(PingAttemptStatus.Timeout);
            }

            return new PingAttemptResult(PingAttemptStatus.Failed);
        }
        catch (PingException ex) when (ex.InnerException is SocketException { SocketErrorCode: SocketError.HostNotFound })
        {
            return new PingAttemptResult(PingAttemptStatus.DnsError);
        }
        catch
        {
            return new PingAttemptResult(PingAttemptStatus.Failed);
        }
    }
}
