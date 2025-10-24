using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

namespace Virgil.Core
{
    // Adapte l'UI à n'importe quelle signature existante de tes services,
    // sans toucher tes classes actuelles (StartupManager/ProcessService).
    public static class CompatibilityExtensions
    {
        // ---- STARTUP ----
        public static IEnumerable<StartupItem> ListItems(this StartupManager mgr)
        {
            if (mgr == null) return Array.Empty<StartupItem>();

            var best = mgr.GetType()
                          .GetMethods(BindingFlags.Instance | BindingFlags.Public)
                          .FirstOrDefault(m =>
                              m.GetParameters().Length == 0 &&
                              typeof(System.Collections.IEnumerable).IsAssignableFrom(m.ReturnType) &&
                              (m.Name.Contains("List", StringComparison.OrdinalIgnoreCase) ||
                               m.Name.Contains("Get", StringComparison.OrdinalIgnoreCase) ||
                               m.Name.Contains("Enum", StringComparison.OrdinalIgnoreCase)));

            if (best != null)
            {
                try
                {
                    var res = best.Invoke(mgr, null) as System.Collections.IEnumerable;
                    if (res != null)
                        return res.Cast<object>()
                                  .OfType<StartupItem>()
                                  .ToList();
                }
                catch { /* fallback below */ }
            }

            // Fallback minimal
            return new[]
            {
                new StartupItem("Windows Security", @"HKLM\\...\\Run", true),
                new StartupItem("OneDrive",         @"HKCU\\...\\Run", true),
            };
        }

        // ---- PROCESSES ----
        public static System.Threading.Tasks.Task<IReadOnlyList<ProcInfo>> ListAsync(this ProcessService svc)
        {
            if (svc == null)
                return System.Threading.Tasks.Task.FromResult((IReadOnlyList<ProcInfo>)Array.Empty<ProcInfo>());

            var asyncLike = svc.GetType()
                               .GetMethods(BindingFlags.Instance | BindingFlags.Public)
                               .FirstOrDefault(m =>
                                   m.GetParameters().Length == 0 &&
                                   (m.Name.EndsWith("Async", StringComparison.OrdinalIgnoreCase) ||
                                    m.Name.IndexOf("Process", StringComparison.OrdinalIgnoreCase) >= 0));

            if (asyncLike != null)
            {
                try
                {
                    var result = asyncLike.Invoke(svc, null);
                    if (result is System.Threading.Tasks.Task task)
                    {
                        return WrapTaskResult(task);
                    }
                    if (result is IEnumerable<ProcInfo> list)
                        return System.Threading.Tasks.Task.FromResult((IReadOnlyList<ProcInfo>)list.ToList());
                }
                catch { /* fallback below */ }
            }

            var items = Process.GetProcesses()
                               .Select(p => new ProcInfo(
                                   p.ProcessName,
                                   p.Id,
                                   0.0,
                                   p.WorkingSet64 / (1024.0 * 1024.0)))
                               .ToList()
                               .AsReadOnly();

            return System.Threading.Tasks.Task.FromResult((IReadOnlyList<ProcInfo>)items);
        }

        private static async System.Threading.Tasks.Task<IReadOnlyList<ProcInfo>> WrapTaskResult(System.Threading.Tasks.Task task)
        {
            await task.ConfigureAwait(false);

            var t = task.GetType();
            if (t.IsGenericType && t.GetProperty("Result") is PropertyInfo p)
            {
                var val = p.GetValue(task);
                if (val is IEnumerable<ProcInfo> list)
                    return list.ToList().AsReadOnly();
            }
            return Array.Empty<ProcInfo>();
        }
    }
}