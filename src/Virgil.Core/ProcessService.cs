using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace Virgil.Core
{
    public sealed class ProcessInfo
    {
        public int Pid { get; set; }
        public string Name { get; set; } = "";
        public double CpuPercent { get; set; }
        public double RamMb { get; set; }
    }

    public sealed class ProcessService
    {
        /// <summary>Échantillonne l’usage CPU sur 1 seconde et renvoie le top N.</summary>
        public List<ProcessInfo> GetTopCpu(int top = 10)
        {
            var procs = Process.GetProcesses();
            var dictStart = procs.ToDictionary(p => p.Id, p => SafeTotalProcessorTime(p));
            Thread.Sleep(1000);
            var cpuCount = Environment.ProcessorCount;

            var list = new List<ProcessInfo>();
            foreach (var p in procs)
            {
                try
                {
                    var t0 = dictStart.TryGetValue(p.Id, out var t) ? t : TimeSpan.Zero;
                    var t1 = SafeTotalProcessorTime(p);
                    var delta = (t1 - t0).TotalMilliseconds / (1000.0 * cpuCount);
                    var wsMb = SafeWs(p) / (1024.0 * 1024);
                    list.Add(new ProcessInfo { Pid = p.Id, Name = p.ProcessName, CpuPercent = Math.Max(0, Math.Min(100, delta * 100)), RamMb = wsMb });
                }
                catch { /* ignore */ }
            }
            return list.OrderByDescending(x => x.CpuPercent).ThenByDescending(x => x.RamMb).Take(top).ToList();
        }

        public List<ProcessInfo> GetTopRam(int top = 10)
        {
            return Process.GetProcesses()
                          .Select(p => new ProcessInfo { Pid = p.Id, Name = p.ProcessName, CpuPercent = 0, RamMb = SafeWs(p) / (1024.0 * 1024) })
                          .OrderByDescending(x => x.RamMb)
                          .Take(top)
                          .ToList();
        }

        public bool Kill(int pid)
        {
            try { Process.GetProcessById(pid).Kill(true); return true; }
            catch { return false; }
        }

        private static TimeSpan SafeTotalProcessorTime(Process p) { try { return p.TotalProcessorTime; } catch { return TimeSpan.Zero; } }
        private static long SafeWs(Process p) { try { return p.WorkingSet64; } catch { return 0; } }
    }
}
