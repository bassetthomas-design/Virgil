using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Virgil.Core
{
    /// <summary>
    /// Provides methods to list and control running processes.
    /// </summary>
    public class ProcessService
    {
        /// <summary>
        /// Returns a list of running processes sorted alphabetically by process name.
        /// </summary>
        public List<Process> ListProcesses()
        {
            return Process.GetProcesses().OrderBy(p => p.ProcessName).ToList();
        }

        /// <summary>
        /// Attempts to kill the process with the specified process name.
        /// Returns true if at least one process was terminated; false otherwise.
        /// </summary>
        public bool KillProcess(string processName)
        {
            try
            {
                var processes = Process.GetProcessesByName(processName);
                foreach (var process in processes)
                {
                    process.Kill();
                }
                return processes.Length > 0;
            }
            catch
            {
                return false;
            }
        }
    }
}