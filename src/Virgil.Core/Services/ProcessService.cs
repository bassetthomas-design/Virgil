using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Virgil.Core.Services
{
    /// <summary>
    /// Aides pour lister et gérer les processus en toute sécurité.
    /// </summary>
    public sealed class ProcessInfo
    {
        public int Pid { get; set; }
        public string Name { get; set; } = "";
        public long WorkingSetBytes { get; set; }
        public string? MainWindowTitle { get; set; }
    }

    public sealed class ProcessService
    {
        /// <summary>
        /// Retourne la liste (limitée) des processus triés par mémoire descendante.
        /// </summary>
        public IReadOnlyList<ProcessInfo> TopByRam(int take = 20)
        {
            var list = new List<ProcessInfo>();
            foreach (var p in Process.GetProcesses())
            {
                try
                {
                    list.Add(new ProcessInfo
                    {
                        Pid = p.Id,
                        Name = p.ProcessName,
                        WorkingSetBytes = SafeGetWorkingSet(p),
                        MainWindowTitle = SafeGetTitle(p)
                    });
                }
                catch { /* ignore */ }
            }

            return list
                .OrderByDescending(x => x.WorkingSetBytes)
                .Take(Math.Max(1, take))
                .ToList();
        }

        public bool KillByPid(int pid, bool force = true)
        {
            try
            {
                var p = Process.GetProcessById(pid);
                if (force) p.Kill(entireProcessTree: true);
                else p.CloseMainWindow();
                return true;
            }
            catch { return false; }
        }

        public int KillByName(string name, bool force = true)
        {
            var count = 0;
            try
            {
                foreach (var p in Process.GetProcessesByName(name))
                {
                    try
                    {
                        if (force) p.Kill(entireProcessTree: true);
                        else p.CloseMainWindow();
                        count++;
                    }
                    catch { /* ignore */ }
                }
            }
            catch { }
            return count;
        }

        private static long SafeGetWorkingSet(Process p)
        {
            try { return p.WorkingSet64; } catch { return 0L; }
        }

        private static string? SafeGetTitle(Process p)
        {
            try { return p.MainWindowTitle; } catch { return null; }
        }
    }
}
