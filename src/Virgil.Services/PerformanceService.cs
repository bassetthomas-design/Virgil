using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Virgil.Services.Abstractions;

namespace Virgil.Services;

/// <summary>
/// Stub de IPerformanceService – gestion des profils de performance / gaming plus tard.
/// </summary>
public sealed class PerformanceService : IPerformanceService
{
    public Task<ActionExecutionResult> EnableGamingModeAsync(CancellationToken ct = default)
        => Task.FromResult(ActionExecutionResult.NotAvailable("Mode performance non disponible"));

    public Task<ActionExecutionResult> RestoreNormalModeAsync(CancellationToken ct = default)
        => Task.FromResult(ActionExecutionResult.NotAvailable("Retour au mode normal non disponible"));

    public Task<ActionExecutionResult> AnalyzeStartupAsync(CancellationToken ct = default)
        => Task.FromResult(ActionExecutionResult.NotAvailable("Analyse démarrage non implémentée"));

    public Task<ActionExecutionResult> CloseGamingSessionAsync(CancellationToken ct = default)
        => Task.FromResult(ActionExecutionResult.NotAvailable("Fermeture session gaming non implémentée"));

    public async Task<ActionExecutionResult> SoftRamFlushAsync(CancellationToken ct = default)
    {
        if (!OperatingSystem.IsWindows())
        {
            return ActionExecutionResult.NotAvailable("Libération RAM uniquement supportée sur Windows");
        }

        var before = GetMemorySnapshot();
        var reclaimedBytes = 0L;
        var processed = 0;

        try
        {
            foreach (var process in Process.GetProcesses())
            {
                ct.ThrowIfCancellationRequested();

                try
                {
                    if (ShouldSkip(process))
                        continue;

                    var beforeWorkingSet = process.WorkingSet64;
                    if (beforeWorkingSet == 0)
                        continue;

                    if (EmptyWorkingSet(process.Handle))
                    {
                        process.Refresh();
                        var afterWorkingSet = process.WorkingSet64;
                        reclaimedBytes += Math.Max(0, beforeWorkingSet - afterWorkingSet);
                        processed++;
                    }
                }
                catch
                {
                    // Best effort: ignorer les processus protégés ou déjà terminés.
                }
                finally
                {
                    process.Dispose();
                }
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();

            var after = GetMemorySnapshot();
            var deltaMb = Math.Max(0, after.AvailablePhysicalMb - before.AvailablePhysicalMb);
            var reclaimedMb = reclaimedBytes / (1024.0 * 1024);

            var summary = $"RAM disponible : {before.AvailablePhysicalMb:F1} → {after.AvailablePhysicalMb:F1} MB";
            var details = $"Processus traités : {processed}, libération estimée : {reclaimedMb:F1} MB";
            if (deltaMb > 0)
            {
                details += $"\nGain net observé : +{deltaMb:F1} MB (best effort)";
            }

            return ActionExecutionResult.Ok(summary, details);
        }
        catch (OperationCanceledException)
        {
            return ActionExecutionResult.Failure("Libération RAM annulée");
        }
        catch (Exception ex)
        {
            return ActionExecutionResult.Failure($"Libération RAM impossible : {ex.Message}");
        }
    }

    private static bool ShouldSkip(Process process)
    {
        try
        {
            if (process.HasExited)
                return true;

            var name = process.ProcessName;
            if (string.IsNullOrWhiteSpace(name))
                return true;

            return string.Equals(name, "System", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(name, "Idle", StringComparison.OrdinalIgnoreCase)
                   || process.SessionId == 0;
        }
        catch
        {
            return true;
        }
    }

    private static MemorySnapshot GetMemorySnapshot()
    {
        var status = new MEMORYSTATUSEX();
        if (!GlobalMemoryStatusEx(status))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        return new MemorySnapshot(
            TotalPhysicalMb: status.ullTotalPhys / (1024.0 * 1024),
            AvailablePhysicalMb: status.ullAvailPhys / (1024.0 * 1024));
    }

    [DllImport("psapi.dll", SetLastError = true)]
    private static extern bool EmptyWorkingSet(IntPtr hProcess);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private class MEMORYSTATUSEX
    {
        public uint dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    private record MemorySnapshot(double TotalPhysicalMb, double AvailablePhysicalMb);
}
