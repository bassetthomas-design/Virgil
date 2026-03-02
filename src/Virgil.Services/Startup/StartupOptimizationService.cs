using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace Virgil.Services.Startup;

public sealed class StartupOptimizationService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunDisabledKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run_DISABLED";

    public Task<StartupOptimizeResult> OptimizeAsync(StartupAnalysis analysis, CancellationToken ct)
    {
        if (analysis is null)
        {
            throw new ArgumentNullException(nameof(analysis));
        }

        var selected = analysis.Items.Where(i => i.IsRecommended && !i.IsEssential).Select(i => i with { IsSelected = true });
        return OptimizeSelectedAsync(selected, ct);
    }

    public Task<StartupOptimizeResult> OptimizeSelectedAsync(IEnumerable<StartupItem> selectedItems, CancellationToken ct)
    {
        if (!OperatingSystem.IsWindows())
        {
            return Task.FromResult(new StartupOptimizeResult
            {
                Succeeded = false,
                Summary = "Optimisation disponible uniquement sous Windows.",
                FailureReason = "Plateforme non supportée."
            });
        }

        var actions = new List<string>();
        var disabled = 0;

        try
        {
            foreach (var item in selectedItems.Where(i => i.IsSelected))
            {
                ct.ThrowIfCancellationRequested();
                if (item.IsEssential)
                {
                    actions.Add($"{item.Name}: ignoré (élément protégé)");
                    continue;
                }

                if (!TryParseHive(item.Location, out var hive))
                {
                    actions.Add($"{item.Name}: emplacement non supporté ({item.Location})");
                    continue;
                }

                if (MoveRunEntryToDisabled(hive, item.Name, actions))
                {
                    disabled++;
                }
            }

            return Task.FromResult(new StartupOptimizeResult
            {
                ItemsDisabled = disabled,
                ServicesSetToManual = 0,
                TasksDisabled = 0,
                Succeeded = true,
                Summary = $"Démarrage: {disabled} élément(s) désactivé(s).",
                ActionsPerformed = actions
            });
        }
        catch (Exception ex)
        {
            return Task.FromResult(new StartupOptimizeResult
            {
                ItemsDisabled = disabled,
                Succeeded = false,
                Summary = "Optimisation du démarrage impossible.",
                FailureReason = ex.Message,
                ActionsPerformed = actions
            });
        }
    }

    public static bool IsEssential(string name, string? publisher, string? path, string? command)
    {
        var context = string.Join(" ", name, publisher, path, command).ToLowerInvariant();
        if (ContainsAny(context, "defender", "windows security", "securityhealth", "antivirus")) return true;
        if (ContainsAny(context, "nvidia", "amd", "intel", "realtek", "audio")) return true;
        if (context.Contains("onedrive", StringComparison.OrdinalIgnoreCase)) return true;
        if (context.Contains("update", StringComparison.OrdinalIgnoreCase)
            && IsCriticalPublisher(publisher)) return true;
        return false;
    }

    public static bool HasDisabledRunEntries()
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        return HasDisabledRunEntries(RegistryHive.CurrentUser) || HasDisabledRunEntries(RegistryHive.LocalMachine);
    }

    public Task<StartupRestoreResult> RestoreRunEntriesAsync(CancellationToken ct)
    {
        if (!OperatingSystem.IsWindows())
        {
            return Task.FromResult(new StartupRestoreResult
            {
                Succeeded = false,
                Summary = "Restauration disponible uniquement sous Windows.",
                FailureReason = "Plateforme non supportée."
            });
        }

        var actions = new List<string>();
        var restored = 0;

        try
        {
            restored += RestoreRunEntries(RegistryHive.CurrentUser, actions, ct);
            restored += RestoreRunEntries(RegistryHive.LocalMachine, actions, ct);

            return Task.FromResult(new StartupRestoreResult
            {
                ItemsRestored = restored,
                Succeeded = true,
                Summary = $"Démarrage: restauration terminée ({restored} élément(s)).",
                ActionsPerformed = actions
            });
        }
        catch (Exception ex)
        {
            return Task.FromResult(new StartupRestoreResult
            {
                ItemsRestored = restored,
                Succeeded = false,
                Summary = "Restauration du démarrage impossible.",
                FailureReason = ex.Message,
                ActionsPerformed = actions
            });
        }
    }

    private static bool MoveRunEntryToDisabled(RegistryHive hive, string valueName, List<string> actions)
    {
        using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
        using var runKey = baseKey.OpenSubKey(RunKeyPath, writable: true);
        using var disabledKey = baseKey.CreateSubKey(RunDisabledKeyPath);
        if (runKey == null || disabledKey == null)
        {
            actions.Add($"{valueName}: clé Run inaccessible");
            return false;
        }

        var value = runKey.GetValue(valueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
        if (value is null)
        {
            actions.Add($"{valueName}: déjà absent");
            return false;
        }

        if (disabledKey.GetValue(valueName) != null)
        {
            actions.Add($"{valueName}: déjà dans Run_DISABLED");
            return false;
        }

        var kind = runKey.GetValueKind(valueName);
        disabledKey.SetValue(valueName, value, kind);
        runKey.DeleteValue(valueName, throwOnMissingValue: false);
        actions.Add($"{valueName}: déplacé vers Run_DISABLED");
        return true;
    }

    private static bool TryParseHive(string location, out RegistryHive hive)
    {
        if (location.Contains("HKLM", StringComparison.OrdinalIgnoreCase))
        {
            hive = RegistryHive.LocalMachine;
            return true;
        }

        if (location.Contains("HKCU", StringComparison.OrdinalIgnoreCase))
        {
            hive = RegistryHive.CurrentUser;
            return true;
        }

        hive = RegistryHive.CurrentUser;
        return false;
    }

    private static bool HasDisabledRunEntries(RegistryHive hive)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
            using var disabledKey = baseKey.OpenSubKey(RunDisabledKeyPath, writable: false);
            return disabledKey?.GetValueNames().Length > 0;
        }
        catch
        {
            return false;
        }
    }

    private static int RestoreRunEntries(RegistryHive hive, List<string> actions, CancellationToken ct)
    {
        var restored = 0;
        using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
        using var disabledKey = baseKey.OpenSubKey(RunDisabledKeyPath, writable: true);
        using var runKey = baseKey.CreateSubKey(RunKeyPath);
        if (disabledKey == null || runKey == null)
        {
            return 0;
        }

        foreach (var valueName in disabledKey.GetValueNames())
        {
            ct.ThrowIfCancellationRequested();
            if (runKey.GetValue(valueName) != null)
            {
                actions.Add($"{valueName}: déjà présent dans Run");
                continue;
            }

            var value = disabledKey.GetValue(valueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
            if (value is null)
            {
                continue;
            }

            var kind = disabledKey.GetValueKind(valueName);
            runKey.SetValue(valueName, value, kind);
            disabledKey.DeleteValue(valueName, throwOnMissingValue: false);
            restored++;
            actions.Add($"{valueName}: restauré");
        }

        return restored;
    }

    private static bool IsCriticalPublisher(string? publisher)
        => !string.IsNullOrWhiteSpace(publisher)
           && (publisher.Contains("Microsoft", StringComparison.OrdinalIgnoreCase)
               || publisher.Contains("Intel", StringComparison.OrdinalIgnoreCase)
               || publisher.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase)
               || publisher.Contains("AMD", StringComparison.OrdinalIgnoreCase));

    private static bool ContainsAny(string context, params string[] keywords)
        => keywords.Any(keyword => context.Contains(keyword, StringComparison.OrdinalIgnoreCase));
}
