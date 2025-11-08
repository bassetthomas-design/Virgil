using System.Collections.Generic;

namespace Virgil.App.Services;

public record Talker(string ProcessName, int Pid, int Connections);

public interface INetworkInsightService
{
    IEnumerable<Talker> GetTopTalkers(int top = 5);
}
