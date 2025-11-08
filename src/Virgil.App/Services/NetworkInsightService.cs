using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;

namespace Virgil.App.Services;

public class NetworkInsightService : INetworkInsightService
{
    public IEnumerable<Talker> GetTopTalkers(int top = 5)
    {
        var list = new List<Talker>();
        try
        {
            var psi = new ProcessStartInfo("cmd.exe", "/c netstat -ano")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var p = Process.Start(psi);
            if (p == null) return list;
            var output = p.StandardOutput.ReadToEnd();
            p.WaitForExit();

            var counts = new Dictionary<int, int>();
            var lines = output.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var raw in lines)
            {
                var line = raw.Trim();
                if (!line.StartsWith("TCP", StringComparison.OrdinalIgnoreCase)) continue;
                var parts = Regex.Split(line, @"\s+");
                if (parts.Length < 5) continue;
                var state = parts[3];
                if (!state.Equals("ESTABLISHED", StringComparison.OrdinalIgnoreCase)) continue;
                if (!int.TryParse(parts[^1], out var pid)) continue;
                if (pid <= 0) continue;
                counts.TryGetValue(pid, out var c);
                counts[pid] = c + 1;
            }

            foreach (var kv in counts.OrderByDescending(kv => kv.Value).Take(top))
            {
                string name;
                try { name = Process.GetProcessById(kv.Key).ProcessName; } catch { name = "?"; }
                list.Add(new Talker(name, kv.Key, kv.Value));
            }
        }
        catch
        {
            // ignore exceptions, we only log top talkers
        }
        return list;
    }
}
