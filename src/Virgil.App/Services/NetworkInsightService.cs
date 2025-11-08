using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Diagnostics;

namespace Virgil.App.Services;

public class NetworkInsightService : INetworkInsightService
{
    public IEnumerable<Talker> GetTopTalkers(int top = 5)
    {
        try
        {
            var conns = IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpConnections();
            var byPid = conns.GroupBy(c => c.State == TcpState.Established ? c.ProcessId : -1)
                              .Where(g => g.Key > 0)
                              .Select(g => new { Pid = g.Key, Count = g.Count() })
                              .OrderByDescending(x => x.Count)
                              .Take(top)
                              .ToList();
            foreach (var x in byPid)
            {
                string name;
                try { name = Process.GetProcessById(x.Pid).ProcessName; } catch { name = "?"; }
                yield return new Talker(name, x.Pid, x.Count);
            }
        }
        catch { yield break; }
    }
}
