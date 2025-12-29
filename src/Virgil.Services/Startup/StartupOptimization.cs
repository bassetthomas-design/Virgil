using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.ServiceProcess;
using System.Text.Json;
using Microsoft.Win32;

namespace Virgil.Services.Startup;

internal enum StartupEntrySource
{
    StartupFolderUser,
    StartupFolderCommon,
    RegistryRunCurrentUser,
    RegistryRunLocalMachine,
    RegistryRunOnceCurrentUser,
    RegistryRunOnceLocalMachine,
    ScheduledTask,
    Service,
}

internal enum StartupDecision
{
    Ok,
    Optional,
    Disable,
}

internal sealed record StartupEntry(
    string Name,
    StartupEntrySource Source,
    string Location,
    string? Command,
    bool Enabled,
    RegistryHive? Hive = null,
    string? RegistrySubKey = null,
    string? RegistryValueName = null);

internal sealed record ClassifiedStartupEntry(StartupEntry Entry, StartupDecision Decision, string Reason, bool Applied = false, string? ApplyNote = null);

internal sealed record StartupRules(
    bool AllowApply,
    IReadOnlyList<StartupRule> Critical,
    IReadOnlyList<StartupRule> AutoDisable,
    IReadOnlyList<StartupRule> OptionalHints)
{
    public static StartupRules Load(string basePath)
    {
        var rulesPath = Path.Combine(basePath, "startup_rules.safe.json");
        if (File.Exists(rulesPath))
        {
            try
            {
                using var stream = File.OpenRead(rulesPath);
                var rules = JsonSerializer.Deserialize<StartupRulesDto>(stream, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (rules != null)
                {
                    return rules.ToModel();
                }
            }
            catch
            {
                // Ignore malformed file and use defaults.
            }
        }

        return StartupRuleDefaults.Safe;
    }
}

internal sealed record StartupRule(string Keyword, string Reason)
{
    public bool IsMatch(string haystack)
        => haystack.Contains(Keyword, StringComparison.OrdinalIgnoreCase);
}

internal sealed class StartupRuleDefaults
{
    public static StartupRules Safe { get; } = new(
        AllowApply: true,
        Critical: new List<StartupRule>
        {
            new("defender", "Composant sécurité: ne pas toucher"),
            new("security", "Protection en place"),
            new("antivirus", "Protection en place"),
            new("nvidia", "Pilotes GPU"),
            new("amd", "Pilotes GPU"),
            new("intel", "Pilotes plateforme"),
            new("realtek", "Pilotes audio"),
            new("audio", "Pilotes audio"),
            new("synaptics", "Pilote input"),
            new("elan", "Pilote input"),
            new("touchpad", "Pilote input"),
            new("keyboard", "Pilote clavier"),
            new("mouse", "Pilote souris"),
            new("network", "Pilote réseau"),
            new("wifi", "Pilote réseau"),
            new("ethernet", "Pilote réseau"),
            new("bluetooth", "Pilote réseau"),
            new("windows", "Composant Windows"),
            new("system32", "Composant Windows"),
        },
        AutoDisable: new List<StartupRule>
        {
            new("updater", "Démarrage inutile: utilitaire de mise à jour"),
            new("update", "Démarrage inutile: utilitaire de mise à jour"),
            new("helper", "Helper en arrière-plan facultatif"),
            new("assistant", "Assistant non essentiel"),
            new("launcher", "Launcher non critique au boot"),
            new("auto-update", "Mise à jour silencieuse non critique"),
        },
        OptionalHints: new List<StartupRule>
        {
            new("cloud", "Sync cloud peut attendre"),
            new("drive", "Sync cloud peut attendre"),
            new("teams", "Messagerie pro optionnelle"),
            new("discord", "Chat gaming optionnel"),
            new("zoom", "Visio optionnelle"),
            new("spotify", "Lecture musique non critique"),
            new("game", "Composant gaming optionnel"),
            new("overlay", "Overlay optionnel"),
        });
}

internal sealed class StartupRulesDto
{
    public bool AllowApply { get; set; } = true;
    public List<StartupRuleDto> Critical { get; set; } = new();
    public List<StartupRuleDto> AutoDisable { get; set; } = new();
    public List<StartupRuleDto> OptionalHints { get; set; } = new();

    public StartupRules ToModel()
    {
        return new StartupRules(
            AllowApply,
            Critical.Select(r => r.ToModel()).ToList(),
            AutoDisable.Select(r => r.ToModel()).ToList(),
            OptionalHints.Select(r => r.ToModel()).ToList());
    }
}

internal sealed class StartupRuleDto
{
    public string Keyword { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;

    public StartupRule ToModel() => new(Keyword, string.IsNullOrWhiteSpace(Reason) ? "Règle sans raison" : Reason);
}

internal sealed class StartupInventory
{
    private static readonly string[] RegistryRunPaths =
    {
        @"Software\Microsoft\Windows\CurrentVersion\Run",
    };

    private static readonly string[] RegistryRunOncePaths =
    {
        @"Software\Microsoft\Windows\CurrentVersion\RunOnce",
    };

    public List<StartupEntry> Collect()
    {
        var list = new List<StartupEntry>();

        TryCollectStartupFolders(list);
        TryCollectRegistry(list);
        TryCollectServices(list);
        TryCollectScheduledTasks(list);

        return list;
    }

    private static void TryCollectStartupFolders(List<StartupEntry> list)
    {
        AddFolderEntries(Environment.SpecialFolder.Startup, StartupEntrySource.StartupFolderUser, list);
        AddFolderEntries(Environment.SpecialFolder.CommonStartup, StartupEntrySource.StartupFolderCommon, list);
    }

    private static void AddFolderEntries(Environment.SpecialFolder folder, StartupEntrySource source, List<StartupEntry> list)
    {
        try
        {
            var path = Environment.GetFolderPath(folder);
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
                return;

            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.TopDirectoryOnly))
            {
                var name = Path.GetFileName(file);
                list.Add(new StartupEntry(name, source, file, file, Enabled: true));
            }
        }
        catch
        {
            // ignore
        }
    }

    private static void TryCollectRegistry(List<StartupEntry> list)
    {
        if (!OperatingSystem.IsWindows())
            return;

        ReadRegistryHive(RegistryHive.CurrentUser, RegistryRunPaths, StartupEntrySource.RegistryRunCurrentUser, list);
        ReadRegistryHive(RegistryHive.LocalMachine, RegistryRunPaths, StartupEntrySource.RegistryRunLocalMachine, list);
        ReadRegistryHive(RegistryHive.CurrentUser, RegistryRunOncePaths, StartupEntrySource.RegistryRunOnceCurrentUser, list);
        ReadRegistryHive(RegistryHive.LocalMachine, RegistryRunOncePaths, StartupEntrySource.RegistryRunOnceLocalMachine, list);
    }

    private static void ReadRegistryHive(RegistryHive hive, IEnumerable<string> paths, StartupEntrySource source, List<StartupEntry> list)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
            foreach (var path in paths)
            {
                using var key = baseKey.OpenSubKey(path, writable: false);
                if (key == null)
                    continue;

                foreach (var name in key.GetValueNames())
                {
                    string? command = null;
                    try { command = key.GetValue(name)?.ToString(); } catch { }
                    list.Add(new StartupEntry(name, source, $"{hive}\\{path}", command, Enabled: true, Hive: hive, RegistrySubKey: path, RegistryValueName: name));
                }
            }
        }
        catch
        {
            // likely no permissions
        }
    }

    private static void TryCollectScheduledTasks(List<StartupEntry> list)
    {
        if (!OperatingSystem.IsWindows())
            return;

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "schtasks",
                Arguments = "/Query /FO CSV /V",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
            };

            using var process = Process.Start(psi);
            if (process == null)
                return;

            using var reader = process.StandardOutput;
            string? line;
            var headerSkipped = false;
            while ((line = reader.ReadLine()) != null)
            {
                if (!headerSkipped)
                {
                    headerSkipped = true;
                    continue;
                }

                if (string.IsNullOrWhiteSpace(line))
                    continue;

                // CSV columns: "TaskName","Next Run Time",...,"Schedule Type",...,"Task To Run",...
                var columns = ParseCsvLine(line);
                if (columns.Count < 7)
                    continue;

                var taskName = columns[0].Trim('"');
                var schedule = columns[6].Trim('"');
                if (!schedule.Contains("At log on", StringComparison.OrdinalIgnoreCase) &&
                    !schedule.Contains("At startup", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                list.Add(new StartupEntry(taskName, StartupEntrySource.ScheduledTask, taskName, columns.ElementAtOrDefault(8)?.Trim('"'), Enabled: true));
            }
        }
        catch
        {
            // ignore (schtasks not available or no permission)
        }
    }

    private static List<string> ParseCsvLine(string line)
    {
        var result = new List<string>();
        var current = string.Empty;
        var inQuotes = false;

        foreach (var c in line)
        {
            if (c == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (c == ',' && !inQuotes)
            {
                result.Add(current);
                current = string.Empty;
                continue;
            }

            current += c;
        }

        result.Add(current);
        return result;
    }

    private static void TryCollectServices(List<StartupEntry> list)
    {
        if (!OperatingSystem.IsWindows())
            return;

        try
        {
            foreach (var service in ServiceController.GetServices())
            {
                using var svc = service;
                if (svc.StartType != ServiceStartMode.Automatic)
                    continue;

                var location = string.Empty;
                try { location = svc.ServiceName; } catch { }

                list.Add(new StartupEntry(svc.DisplayName ?? svc.ServiceName, StartupEntrySource.Service, location, svc.ServiceName, svc.Status != ServiceControllerStatus.Stopped));
            }
        }
        catch
        {
            // ignore services if not available
        }
    }
}

internal sealed class StartupClassifier
{
    private readonly StartupRules _rules;

    public StartupClassifier(StartupRules rules)
    {
        _rules = rules;
    }

    public IReadOnlyList<ClassifiedStartupEntry> Classify(IEnumerable<StartupEntry> entries)
    {
        var results = new List<ClassifiedStartupEntry>();
        foreach (var entry in entries)
        {
            var normalized = Normalize(entry);

            var critical = _rules.Critical.FirstOrDefault(r => r.IsMatch(normalized));
            if (critical != null)
            {
                results.Add(new ClassifiedStartupEntry(entry, StartupDecision.Ok, critical.Reason));
                continue;
            }

            var auto = _rules.AutoDisable.FirstOrDefault(r => r.IsMatch(normalized));
            if (auto != null)
            {
                results.Add(new ClassifiedStartupEntry(entry, StartupDecision.Disable, auto.Reason));
                continue;
            }

            var optional = _rules.OptionalHints.FirstOrDefault(r => r.IsMatch(normalized));
            if (optional != null)
            {
                results.Add(new ClassifiedStartupEntry(entry, StartupDecision.Optional, optional.Reason));
                continue;
            }

            results.Add(new ClassifiedStartupEntry(entry, StartupDecision.Optional, "Pas dans la liste blanche"));
        }

        return results;
    }

    private static string Normalize(StartupEntry entry)
    {
        var parts = new[] { entry.Name, entry.Command ?? string.Empty, entry.Location };
        return string.Join(" ", parts).ToLowerInvariant();
    }
}

internal sealed class StartupApplier
{
    private readonly bool _allowApply;

    public StartupApplier(bool allowApply)
    {
        _allowApply = allowApply;
    }

    public IReadOnlyList<ClassifiedStartupEntry> Apply(IReadOnlyList<ClassifiedStartupEntry> plan)
    {
        if (!_allowApply || !OperatingSystem.IsWindows())
        {
            return plan.Select(p => p with { Applied = false, ApplyNote = "Analyse uniquement (pas d'application automatique)" }).ToList();
        }

        var elevated = IsAdministrator();
        var results = new List<ClassifiedStartupEntry>();

        foreach (var item in plan)
        {
            if (item.Decision != StartupDecision.Disable)
            {
                results.Add(item);
                continue;
            }

            if (!elevated)
            {
                results.Add(item with { Applied = false, ApplyNote = "Droits admin requis" });
                continue;
            }

            var success = TryDisable(item.Entry, out var note);
            results.Add(item with { Applied = success, ApplyNote = note });
        }

        return results;
    }

    private static bool IsAdministrator()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    private static bool TryDisable(StartupEntry entry, out string note)
    {
        try
        {
            switch (entry.Source)
            {
                case StartupEntrySource.StartupFolderUser:
                case StartupEntrySource.StartupFolderCommon:
                    return TryDisableFile(entry.Location, out note);
                case StartupEntrySource.RegistryRunCurrentUser:
                case StartupEntrySource.RegistryRunLocalMachine:
                case StartupEntrySource.RegistryRunOnceCurrentUser:
                case StartupEntrySource.RegistryRunOnceLocalMachine:
                    return TryDisableRegistry(entry, out note);
                default:
                    note = "Désactivation non implémentée pour cette source";
                    return false;
            }
        }
        catch (Exception ex)
        {
            note = $"Échec: {ex.Message}";
            return false;
        }
    }

    private static bool TryDisableFile(string path, out string note)
    {
        if (!File.Exists(path))
        {
            note = "Fichier introuvable";
            return false;
        }

        var target = path + ".disabled_by_virgil";
        var suffix = 1;
        while (File.Exists(target))
        {
            target = path + $".disabled_by_virgil_{suffix}";
            suffix++;
        }

        File.Move(path, target);
        note = "Fichier renommé (réversible)";
        return true;
    }

    private static bool TryDisableRegistry(StartupEntry entry, out string note)
    {
        if (entry.Hive is null || string.IsNullOrWhiteSpace(entry.RegistrySubKey) || string.IsNullOrWhiteSpace(entry.RegistryValueName))
        {
            note = "Données registre incomplètes";
            return false;
        }

        using var baseKey = RegistryKey.OpenBaseKey(entry.Hive.Value, RegistryView.Default);
        using var key = baseKey.OpenSubKey(entry.RegistrySubKey, writable: true);
        if (key == null)
        {
            note = "Clé registre inaccessible";
            return false;
        }

        var value = key.GetValue(entry.RegistryValueName);
        if (value is null)
        {
            note = "Entrée déjà absente";
            return false;
        }

        var backupName = entry.RegistryValueName + "_DisabledByVirgil";
        key.SetValue(backupName, value);
        key.DeleteValue(entry.RegistryValueName, throwOnMissingValue: false);
        note = "Valeur registre renommée (réversible)";
        return true;
    }
}

internal sealed class StartupOptimizationPlan
{
    public StartupOptimizationPlan(IReadOnlyList<ClassifiedStartupEntry> entries)
    {
        Entries = entries;
    }

    public IReadOnlyList<ClassifiedStartupEntry> Entries { get; }

    public int Total => Entries.Count;
    public int Disabled => Entries.Count(e => e.Decision == StartupDecision.Disable && e.Applied);
    public int DisablePlanned => Entries.Count(e => e.Decision == StartupDecision.Disable);
    public int Optionals => Entries.Count(e => e.Decision == StartupDecision.Optional);
    public int Critical => Entries.Count(e => e.Decision == StartupDecision.Ok);
}

internal sealed class StartupOptimizer
{
    private readonly string _basePath;

    public StartupOptimizer(string? basePath = null)
    {
        _basePath = basePath ?? AppContext.BaseDirectory;
    }

    public StartupOptimizationPlan BuildAndApply()
    {
        var rules = StartupRules.Load(_basePath);
        var inventory = new StartupInventory();
        var entries = inventory.Collect();
        var classifier = new StartupClassifier(rules);
        var classified = classifier.Classify(entries);

        var applier = new StartupApplier(rules.AllowApply);
        var applied = applier.Apply(classified);

        return new StartupOptimizationPlan(applied);
    }
}
