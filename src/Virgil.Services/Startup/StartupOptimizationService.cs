using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace Virgil.Services.Startup;

public sealed class StartupOptimizationService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunDisabledKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run_DISABLED";
    private const string RunOnceKeyPath = @"Software\Microsoft\Windows\CurrentVersion\RunOnce";
    private const string RunOnceDisabledKeyPath = @"Software\Microsoft\Windows\CurrentVersion\RunOnce_DISABLED";

    private static readonly string AppData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
    private static readonly string StartupDisabledRoot = Path.Combine(AppData, "Virgil", "Startup_DISABLED");
    private static readonly string UserDisabledDir = Path.Combine(StartupDisabledRoot, "User");
    private static readonly string CommonDisabledDir = Path.Combine(StartupDisabledRoot, "Common");
    private static readonly string DisabledTasksStorePath = Path.Combine(StartupDisabledRoot, "disabled_tasks.json");

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
        var tasksDisabled = 0;

        try
        {
            Directory.CreateDirectory(UserDisabledDir);
            Directory.CreateDirectory(CommonDisabledDir);

            foreach (var item in selectedItems.Where(i => i.IsSelected))
            {
                ct.ThrowIfCancellationRequested();
                if (item.IsEssential)
                {
                    actions.Add($"{item.Name}: ignoré (protégé)");
                    continue;
                }

                switch (item.Type)
                {
                    case "Registry":
                        if (MoveRegistryEntryToDisabled(item, actions))
                        {
                            disabled++;
                        }
                        break;

                    case "StartupFolder":
                        if (MoveStartupFileToDisabled(item, actions))
                        {
                            disabled++;
                        }
                        break;

                    case "ScheduledTask":
                        if (DisableScheduledTask(item, actions))
                        {
                            tasksDisabled++;
                            disabled++;
                        }
                        break;

                    default:
                        actions.Add($"{item.Name}: type non supporté ({item.Type})");
                        break;
                }
            }

            return Task.FromResult(new StartupOptimizeResult
            {
                ItemsDisabled = disabled,
                ServicesSetToManual = 0,
                TasksDisabled = tasksDisabled,
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
                TasksDisabled = tasksDisabled,
                Succeeded = false,
                Summary = "Optimisation du démarrage impossible.",
                FailureReason = ex.Message,
                ActionsPerformed = actions
            });
        }
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
            restored += RestoreRegistryEntries(RegistryHive.CurrentUser, RunDisabledKeyPath, RunKeyPath, actions, ct);
            restored += RestoreRegistryEntries(RegistryHive.LocalMachine, RunDisabledKeyPath, RunKeyPath, actions, ct);
            restored += RestoreRegistryEntries(RegistryHive.CurrentUser, RunOnceDisabledKeyPath, RunOnceKeyPath, actions, ct);
            restored += RestoreRegistryEntries(RegistryHive.LocalMachine, RunOnceDisabledKeyPath, RunOnceKeyPath, actions, ct);

            restored += RestoreStartupFiles(UserDisabledDir, Environment.GetFolderPath(Environment.SpecialFolder.Startup), actions, ct);
            restored += RestoreStartupFiles(CommonDisabledDir, Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup), actions, ct);

            restored += RestoreScheduledTasks(actions, ct);

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

    public static bool HasStartupDisabledEntries()
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        if (HasRegistryDisabledEntries(RegistryHive.CurrentUser, RunDisabledKeyPath)
            || HasRegistryDisabledEntries(RegistryHive.LocalMachine, RunDisabledKeyPath)
            || HasRegistryDisabledEntries(RegistryHive.CurrentUser, RunOnceDisabledKeyPath)
            || HasRegistryDisabledEntries(RegistryHive.LocalMachine, RunOnceDisabledKeyPath))
        {
            return true;
        }

        if (Directory.Exists(UserDisabledDir) && Directory.EnumerateFiles(UserDisabledDir, "*", SearchOption.AllDirectories).Any())
        {
            return true;
        }

        if (Directory.Exists(CommonDisabledDir) && Directory.EnumerateFiles(CommonDisabledDir, "*", SearchOption.AllDirectories).Any())
        {
            return true;
        }

        return LoadDisabledTasksStore().TaskNames.Count > 0;
    }

    public static bool IsEssential(string name, string? publisher, string? path, string? command)
    {
        var context = string.Join(" ", name, publisher, path, command).ToLowerInvariant();

        if (ContainsAny(context, "securityhealth", "windows security", "defender", "microsoft security")) return true;
        if (ContainsAny(context, "nvidia", "amd", "intel", "realtek", "audio")) return true;
        if (!string.IsNullOrWhiteSpace(publisher) && publisher.Contains("Microsoft", StringComparison.OrdinalIgnoreCase)) return true;
        if ((path ?? command ?? string.Empty).Contains("\\Microsoft\\", StringComparison.OrdinalIgnoreCase)) return true;

        return false;
    }

    private static bool MoveRegistryEntryToDisabled(StartupItem item, List<string> actions)
    {
        if (!TryParseHive(item.Location, out var hive))
        {
            actions.Add($"{item.Name}: ruche registre non reconnue");
            return false;
        }

        var sourceKey = item.Location.Contains("RunOnce", StringComparison.OrdinalIgnoreCase) ? RunOnceKeyPath : RunKeyPath;
        var targetKey = item.Location.Contains("RunOnce", StringComparison.OrdinalIgnoreCase) ? RunOnceDisabledKeyPath : RunDisabledKeyPath;

        using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
        using var runKey = baseKey.OpenSubKey(sourceKey, writable: true);
        using var disabledKey = baseKey.CreateSubKey(targetKey);

        if (runKey == null || disabledKey == null)
        {
            actions.Add($"{item.Name}: clé registre inaccessible");
            return false;
        }

        var value = runKey.GetValue(item.Name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
        if (value == null)
        {
            actions.Add($"{item.Name}: introuvable");
            return false;
        }

        var kind = runKey.GetValueKind(item.Name);
        disabledKey.SetValue(item.Name, value, kind);
        runKey.DeleteValue(item.Name, throwOnMissingValue: false);
        actions.Add($"{item.Name}: déplacé vers {targetKey}");
        return true;
    }

    private static bool MoveStartupFileToDisabled(StartupItem item, List<string> actions)
    {
        if (!File.Exists(item.Command))
        {
            actions.Add($"{item.Name}: fichier introuvable");
            return false;
        }

        var sourceRoot = item.Location.Contains("Common", StringComparison.OrdinalIgnoreCase)
            ? Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup)
            : Environment.GetFolderPath(Environment.SpecialFolder.Startup);
        var targetRoot = item.Location.Contains("Common", StringComparison.OrdinalIgnoreCase) ? CommonDisabledDir : UserDisabledDir;

        var relative = Path.GetRelativePath(sourceRoot, item.Command);
        var destination = Path.Combine(targetRoot, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);

        if (File.Exists(destination))
        {
            actions.Add($"{item.Name}: déjà déplacé");
            return false;
        }

        File.Move(item.Command, destination);
        actions.Add($"{item.Name}: déplacé vers sauvegarde startup");
        return true;
    }

    private static bool DisableScheduledTask(StartupItem item, List<string> actions)
    {
        if (item.Id.StartsWith("\\Microsoft\\", StringComparison.OrdinalIgnoreCase))
        {
            actions.Add($"{item.Name}: tâche protégée Microsoft");
            return false;
        }

        if (!RunSchtasks($"/Change /TN \"{item.Id}\" /Disable", out var error))
        {
            actions.Add($"{item.Name}: {error}");
            return false;
        }

        var store = LoadDisabledTasksStore();
        store.TaskNames.Add(item.Id);
        SaveDisabledTasksStore(store);
        actions.Add($"{item.Name}: tâche désactivée");
        return true;
    }

    private static int RestoreScheduledTasks(List<string> actions, CancellationToken ct)
    {
        var store = LoadDisabledTasksStore();
        if (store.TaskNames.Count == 0)
        {
            return 0;
        }

        var restored = 0;
        var remaining = new List<string>();

        foreach (var task in store.TaskNames.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            ct.ThrowIfCancellationRequested();
            if (RunSchtasks($"/Change /TN \"{task}\" /Enable", out var error))
            {
                restored++;
                actions.Add($"{task}: tâche réactivée");
            }
            else
            {
                remaining.Add(task);
                actions.Add($"{task}: {error}");
            }
        }

        if (remaining.Count == 0)
        {
            if (File.Exists(DisabledTasksStorePath))
            {
                File.Delete(DisabledTasksStorePath);
            }
        }
        else
        {
            SaveDisabledTasksStore(new DisabledTasksStore { TaskNames = remaining });
        }

        return restored;
    }

    private static int RestoreRegistryEntries(RegistryHive hive, string sourceDisabledPath, string destinationPath, List<string> actions, CancellationToken ct)
    {
        var restored = 0;
        using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
        using var disabledKey = baseKey.OpenSubKey(sourceDisabledPath, writable: true);
        using var runKey = baseKey.CreateSubKey(destinationPath);
        if (disabledKey == null || runKey == null)
        {
            return 0;
        }

        foreach (var valueName in disabledKey.GetValueNames())
        {
            ct.ThrowIfCancellationRequested();
            if (runKey.GetValue(valueName) != null)
            {
                actions.Add($"{valueName}: déjà présent");
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

    private static int RestoreStartupFiles(string backupRoot, string targetRoot, List<string> actions, CancellationToken ct)
    {
        if (!Directory.Exists(backupRoot))
        {
            return 0;
        }

        var restored = 0;
        foreach (var file in Directory.EnumerateFiles(backupRoot, "*", SearchOption.AllDirectories))
        {
            ct.ThrowIfCancellationRequested();
            var relative = Path.GetRelativePath(backupRoot, file);
            var destination = Path.Combine(targetRoot, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            if (File.Exists(destination))
            {
                actions.Add($"{Path.GetFileName(file)}: déjà présent dans startup");
                continue;
            }

            File.Move(file, destination);
            restored++;
            actions.Add($"{Path.GetFileName(file)}: restauré");
        }

        return restored;
    }

    private static bool HasRegistryDisabledEntries(RegistryHive hive, string keyPath)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
            using var key = baseKey.OpenSubKey(keyPath, writable: false);
            return key?.GetValueNames().Length > 0;
        }
        catch
        {
            return false;
        }
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

    private static bool RunSchtasks(string arguments, out string message)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "schtasks",
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
            };

            using var process = Process.Start(psi);
            if (process == null)
            {
                message = "Impossible de lancer schtasks";
                return false;
            }

            process.WaitForExit();
            var error = process.StandardError.ReadToEnd().Trim();
            if (process.ExitCode != 0)
            {
                message = string.IsNullOrWhiteSpace(error) ? "Erreur schtasks" : error;
                return false;
            }

            message = "OK";
            return true;
        }
        catch (Exception ex)
        {
            message = ex.Message;
            return false;
        }
    }

    private static DisabledTasksStore LoadDisabledTasksStore()
    {
        try
        {
            if (!File.Exists(DisabledTasksStorePath))
            {
                return new DisabledTasksStore();
            }

            var json = File.ReadAllText(DisabledTasksStorePath);
            return JsonSerializer.Deserialize<DisabledTasksStore>(json) ?? new DisabledTasksStore();
        }
        catch
        {
            return new DisabledTasksStore();
        }
    }

    private static void SaveDisabledTasksStore(DisabledTasksStore store)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(DisabledTasksStorePath)!);
        var json = JsonSerializer.Serialize(store, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(DisabledTasksStorePath, json);
    }

    private static bool ContainsAny(string context, params string[] keywords)
        => keywords.Any(keyword => context.Contains(keyword, StringComparison.OrdinalIgnoreCase));

    private sealed class DisabledTasksStore
    {
        public List<string> TaskNames { get; set; } = new();
    }
}
