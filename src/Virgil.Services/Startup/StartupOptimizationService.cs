using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        if (!OperatingSystem.IsWindows())
        {
            return Task.FromResult(new StartupOptimizeResult
            {
                Succeeded = false,
                Summary = "Optimisation disponible uniquement sous Windows.",
                FailureReason = "Plateforme non supportée."
            });
        }

        if (analysis is null)
        {
            throw new ArgumentNullException(nameof(analysis));
        }

        var actions = new List<string>();
        var itemsDisabled = 0;
        var tasksDisabled = 0;
        var servicesSetToManual = 0;

        try
        {
            var recommendations = analysis.Items
                .Where(item => item.RecommendedForDisable && item.Enabled)
                .GroupBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

            itemsDisabled += MoveRunEntries(RegistryHive.CurrentUser, recommendations, actions, ct);
            itemsDisabled += MoveRunEntries(RegistryHive.LocalMachine, recommendations, actions, ct);
            tasksDisabled += DisableScheduledTasks(analysis, actions, ct);

            var totalDisabled = itemsDisabled + tasksDisabled + servicesSetToManual;
            var summary = totalDisabled == 0
                ? "Aucun élément à optimiser."
                : $"Optimisation appliquée : {totalDisabled} élément(s) désactivé(s).";

            return Task.FromResult(new StartupOptimizeResult
            {
                ItemsDisabled = itemsDisabled,
                ServicesSetToManual = servicesSetToManual,
                TasksDisabled = tasksDisabled,
                Succeeded = true,
                Summary = summary,
                ActionsPerformed = actions
            });
        }
        catch (Exception ex)
        {
            return Task.FromResult(new StartupOptimizeResult
            {
                ItemsDisabled = itemsDisabled,
                ServicesSetToManual = servicesSetToManual,
                TasksDisabled = tasksDisabled,
                Succeeded = false,
                Summary = "Optimisation du démarrage impossible.",
                FailureReason = ex.Message,
                ActionsPerformed = actions
            });
        }

    }

    private static int MoveRunEntries(
        RegistryHive hive,
        IReadOnlyDictionary<string, StartupAnalysisItem> recommendations,
        List<string> actions,
        CancellationToken ct)
    {
        var disabled = 0;
        using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
        using var runKey = baseKey.OpenSubKey(RunKeyPath, writable: true);
        if (runKey == null)
        {
            return 0;
        }

        using var disabledKey = baseKey.CreateSubKey(RunDisabledKeyPath);
        if (disabledKey == null)
        {
            return 0;
        }

        foreach (var valueName in runKey.GetValueNames())
        {
            ct.ThrowIfCancellationRequested();
            if (!recommendations.TryGetValue(valueName, out var item))
            {
                continue;
            }

            if (ShouldSkip(item, out var reason))
            {
                actions.Add($"{valueName}: ignoré ({reason})");
                continue;
            }

            var existing = disabledKey.GetValue(valueName);
            if (existing != null)
            {
                actions.Add($"{valueName}: déjà sauvegardé dans Run_DISABLED");
                continue;
            }

            var value = runKey.GetValue(valueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
            if (value is null)
            {
                actions.Add($"{valueName}: valeur introuvable");
                continue;
            }

            var kind = runKey.GetValueKind(valueName);
            disabledKey.SetValue(valueName, value, kind);
            runKey.DeleteValue(valueName, throwOnMissingValue: false);
            disabled++;
            actions.Add($"{valueName}: déplacé vers Run_DISABLED");
        }

        return disabled;
    }

    private static int DisableScheduledTasks(StartupAnalysis analysis, List<string> actions, CancellationToken ct)
    {
        var disabled = 0;
        var candidates = analysis.Items
            .Where(item => item.Type.Contains("Tâche planifiée", StringComparison.OrdinalIgnoreCase))
            .Where(item => item.Impact == StartupImpactLevel.Fort)
            .ToList();

        foreach (var task in candidates)
        {
            ct.ThrowIfCancellationRequested();
            if (IsMicrosoftTask(task))
            {
                actions.Add($"{task.Name}: tâche Microsoft ignorée");
                continue;
            }

            if (!TryDisableScheduledTask(task.Name, out var note))
            {
                actions.Add($"{task.Name}: {note}");
                continue;
            }

            disabled++;
            actions.Add($"{task.Name}: tâche désactivée");
        }

        return disabled;
    }

    private static bool TryDisableScheduledTask(string taskName, out string note)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "schtasks",
                Arguments = $"/Change /TN \"{taskName}\" /Disable",
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null)
            {
                note = "Impossible de lancer schtasks";
                return false;
            }

            process.WaitForExit();
            if (process.ExitCode != 0)
            {
                var error = process.StandardError.ReadToEnd();
                note = string.IsNullOrWhiteSpace(error) ? "Erreur schtasks" : error.Trim();
                return false;
            }

            note = "Désactivée";
            return true;
        }
        catch (Exception ex)
        {
            note = ex.Message;
            return false;
        }
    }

    private static bool IsMicrosoftTask(StartupAnalysisItem item)
    {
        if (!string.IsNullOrWhiteSpace(item.Publisher) &&
            item.Publisher.Contains("Microsoft", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return item.Name.StartsWith("\\Microsoft", StringComparison.OrdinalIgnoreCase)
               || item.Name.Contains("\\Microsoft\\", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ShouldSkip(StartupAnalysisItem item, out string reason)
    {
        var context = string.Join(" ", item.Name, item.Publisher, item.Path, item.Command)
            .ToLowerInvariant();

        if (ContainsAny(context, "defender", "windows security", "securityhealth", "antivirus"))
        {
            reason = "sécurité/defender";
            return true;
        }

        if (ContainsAny(context, "nvidia", "amd", "intel", "realtek", "audio"))
        {
            reason = "pilotes GPU/audio";
            return true;
        }

        if (context.Contains("onedrive", StringComparison.OrdinalIgnoreCase))
        {
            reason = "OneDrive";
            return true;
        }

        if (context.Contains("update", StringComparison.OrdinalIgnoreCase)
            && IsCriticalPublisher(item.Publisher))
        {
            reason = "update critique";
            return true;
        }

        reason = string.Empty;
        return false;
    }

    private static bool IsCriticalPublisher(string? publisher)
        => !string.IsNullOrWhiteSpace(publisher)
           && (publisher.Contains("Microsoft", StringComparison.OrdinalIgnoreCase)
               || publisher.Contains("Intel", StringComparison.OrdinalIgnoreCase)
               || publisher.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase)
               || publisher.Contains("AMD", StringComparison.OrdinalIgnoreCase));

    private static bool ContainsAny(string context, params string[] keywords)
        => keywords.Any(keyword => context.Contains(keyword, StringComparison.OrdinalIgnoreCase));

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

            var summary = restored == 0
                ? "Aucun élément à restaurer."
                : $"Démarrage restauré : {restored} élément(s) réactivé(s).";

            return Task.FromResult(new StartupRestoreResult
            {
                ItemsRestored = restored,
                Succeeded = true,
                Summary = summary,
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
        if (disabledKey == null)
        {
            return 0;
        }

        using var runKey = baseKey.CreateSubKey(RunKeyPath);
        if (runKey == null)
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
}
