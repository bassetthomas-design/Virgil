using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;

namespace Virgil.Services.Startup;

public enum StartupImpactLevel
{
    Faible,
    Moyen,
    Fort,
}

public sealed record StartupAnalysisItem(
    string Name,
    string? Publisher,
    string? Path,
    string Type,
    string Scope,
    bool Enabled,
    StartupImpactLevel Impact,
    bool RecommendedForDisable,
    string? Command);

public sealed record StartupAnalysisReport(
    IReadOnlyList<StartupAnalysisItem> Items,
    TimeSpan? EstimatedBootDuration,
    TimeSpan? LastBootAgo,
    IReadOnlyList<string> Notes);

public interface IStartupAnalyzer
{
    StartupAnalysisReport Analyze(CancellationToken ct = default);
}

internal interface IFileMetadataReader
{
    string? TryGetPublisher(string? path);
}

internal interface IStartupImpactEstimator
{
    StartupImpactLevel Estimate(StartupEntry entry, string? publisher);
    bool ShouldSuggestDisable(StartupEntry entry, string? publisher);
}

internal interface IBootTimeEstimator
{
    BootTimeEstimate TryEstimate();
}

internal sealed record BootTimeEstimate(TimeSpan? Duration, TimeSpan? Uptime, IReadOnlyList<string> Notes);

internal sealed class StartupAnalyzer : IStartupAnalyzer
{
    private readonly IStartupInventory _inventory;
    private readonly IFileMetadataReader _metadataReader;
    private readonly IStartupImpactEstimator _impactEstimator;
    private readonly IBootTimeEstimator _bootEstimator;

    public StartupAnalyzer(
        IStartupInventory? inventory = null,
        IFileMetadataReader? metadataReader = null,
        IStartupImpactEstimator? impactEstimator = null,
        IBootTimeEstimator? bootEstimator = null)
    {
        _inventory = inventory ?? new StartupInventory();
        _metadataReader = metadataReader ?? new FileMetadataReader();
        _impactEstimator = impactEstimator ?? new StartupImpactEstimator();
        _bootEstimator = bootEstimator ?? new BootTimeEstimator();
    }

    public StartupAnalysisReport Analyze(CancellationToken ct = default)
    {
        var entries = _inventory.Collect();
        var items = new List<StartupAnalysisItem>(entries.Count);

        foreach (var entry in entries)
        {
            ct.ThrowIfCancellationRequested();
            var path = ExtractExecutablePath(entry.Command ?? entry.Location);
            var publisher = _metadataReader.TryGetPublisher(path);
            var impact = _impactEstimator.Estimate(entry, publisher);
            var recommend = _impactEstimator.ShouldSuggestDisable(entry, publisher);
            var type = DescribeType(entry.Source);
            var scope = DescribeScope(entry.Source);

            items.Add(new StartupAnalysisItem(
                entry.Name,
                publisher,
                path,
                type,
                scope,
                entry.Enabled,
                impact,
                recommend,
                entry.Command));
        }

        var boot = _bootEstimator.TryEstimate();
        return new StartupAnalysisReport(items, boot.Duration, boot.Uptime, boot.Notes);
    }

    private static string DescribeType(StartupEntrySource source) => source switch
    {
        StartupEntrySource.StartupFolderUser => "Dossier démarrage (utilisateur)",
        StartupEntrySource.StartupFolderCommon => "Dossier démarrage (commun)",
        StartupEntrySource.RegistryRunCurrentUser => "Registre Run (HKCU)",
        StartupEntrySource.RegistryRunLocalMachine => "Registre Run (HKLM)",
        StartupEntrySource.RegistryRunOnceCurrentUser => "Registre RunOnce (HKCU)",
        StartupEntrySource.RegistryRunOnceLocalMachine => "Registre RunOnce (HKLM)",
        StartupEntrySource.ScheduledTask => "Tâche planifiée (logon/startup)",
        StartupEntrySource.Service => "Service (auto)",
        _ => "Inconnu",
    };

    private static string DescribeScope(StartupEntrySource source) => source switch
    {
        StartupEntrySource.RegistryRunLocalMachine or StartupEntrySource.RegistryRunOnceLocalMachine or StartupEntrySource.Service or StartupEntrySource.StartupFolderCommon => "Tous les utilisateurs",
        _ => "Utilisateur courant",
    };

    private static string? ExtractExecutablePath(string? command)
    {
        if (string.IsNullOrWhiteSpace(command))
            return null;

        var trimmed = command.Trim();
        if (trimmed.StartsWith("\"", StringComparison.Ordinal) && trimmed.Count(c => c == '\"') >= 2)
        {
            var endQuote = trimmed.IndexOf('"', 1);
            if (endQuote > 1)
            {
                return trimmed[1..endQuote];
            }
        }

        var firstSpace = trimmed.IndexOf(' ');
        return firstSpace > 0 ? trimmed[..firstSpace] : trimmed;
    }
}

internal sealed class FileMetadataReader : IFileMetadataReader
{
    public string? TryGetPublisher(string? path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return null;

            var info = FileVersionInfo.GetVersionInfo(path);
            return string.IsNullOrWhiteSpace(info.CompanyName) ? null : info.CompanyName;
        }
        catch
        {
            return null;
        }
    }
}

internal sealed class StartupImpactEstimator : IStartupImpactEstimator
{
    private static readonly string[] CriticalKeywords =
    {
        "defender", "security", "antivirus", "driver", "nvidia", "amd", "intel", "realtek", "system32", "windows", "audio", "bluetooth", "wifi", "ethernet", "touchpad", "keyboard", "mouse", "input", "gpu"
    };

    private static readonly string[] OptionalKeywords =
    {
        "updater", "update", "helper", "assistant", "launcher", "tray", "sync", "cloud", "drive", "dropbox", "onedrive", "steam", "epic", "origin", "discord", "teams", "zoom", "spotify", "game", "overlay", "backup"
    };

    public StartupImpactLevel Estimate(StartupEntry entry, string? publisher)
    {
        var context = NormalizeContext(entry, publisher);

        if (IsCritical(context))
            return StartupImpactLevel.Faible;

        var impact = StartupImpactLevel.Moyen;

        if (entry.Source == StartupEntrySource.Service)
        {
            impact = StartupImpactLevel.Moyen;
            if (OptionalKeywords.Any(context.Contains))
            {
                impact = StartupImpactLevel.Fort;
            }
        }
        else if (entry.Source == StartupEntrySource.ScheduledTask && OptionalKeywords.Any(context.Contains))
        {
            impact = StartupImpactLevel.Moyen;
        }
        else if (OptionalKeywords.Any(context.Contains))
        {
            impact = StartupImpactLevel.Moyen;
        }

        if (context.Contains("backup", StringComparison.OrdinalIgnoreCase) || context.Contains("sync", StringComparison.OrdinalIgnoreCase))
        {
            impact = StartupImpactLevel.Fort;
        }

        if (context.Contains("windows", StringComparison.OrdinalIgnoreCase) || context.Contains("system32", StringComparison.OrdinalIgnoreCase))
        {
            impact = StartupImpactLevel.Faible;
        }

        return impact;
    }

    public bool ShouldSuggestDisable(StartupEntry entry, string? publisher)
    {
        var context = NormalizeContext(entry, publisher);
        if (IsCritical(context))
            return false;

        return OptionalKeywords.Any(context.Contains);
    }

    private static bool IsCritical(string context) => CriticalKeywords.Any(context.Contains);

    private static string NormalizeContext(StartupEntry entry, string? publisher)
    {
        var pieces = new List<string>
        {
            entry.Name,
            entry.Command ?? string.Empty,
            entry.Location,
            publisher ?? string.Empty,
        };

        return string.Join(' ', pieces).ToLower(CultureInfo.InvariantCulture);
    }
}

internal sealed class BootTimeEstimator : IBootTimeEstimator
{
    public BootTimeEstimate TryEstimate()
    {
        if (!OperatingSystem.IsWindows())
        {
            return new BootTimeEstimate(null, null, new[] { "Temps de démarrage: uniquement disponible sous Windows." });
        }

        try
        {
            var uptime = TimeSpan.FromMilliseconds(Environment.TickCount64);
            return new BootTimeEstimate(null, uptime, Array.Empty<string>());
        }
        catch (Exception ex)
        {
            return new BootTimeEstimate(null, null, new[] { $"Estimation indisponible ({ex.Message})" });
        }
    }
}

internal static class StartupAnalysisFormatter
{
    public static string BuildMessage(StartupAnalysisReport report)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("1) Liste des éléments startup");
        if (report.Items.Count == 0)
        {
            sb.AppendLine("- Aucun élément détecté.");
        }
        else
        {
            foreach (var item in report.Items.Take(15))
            {
                sb.AppendLine($"- {item.Name} [{item.Type}] ({item.Scope}) – {(item.Enabled ? "activé" : "désactivé")} – Impact: {Describe(item.Impact)} – {(item.Publisher ?? "éditeur inconnu")} – {(item.Path ?? item.Command ?? "chemin inconnu")}");
            }

            if (report.Items.Count > 15)
            {
                sb.AppendLine($"… {report.Items.Count - 15} élément(s) supplémentaires non listés");
            }
        }

        sb.AppendLine();
        sb.AppendLine("2) Classement par impact");
        sb.AppendLine(FormatImpactLine("Fort", report.Items.Count(i => i.Impact == StartupImpactLevel.Fort)));
        sb.AppendLine(FormatImpactLine("Moyen", report.Items.Count(i => i.Impact == StartupImpactLevel.Moyen)));
        sb.AppendLine(FormatImpactLine("Faible", report.Items.Count(i => i.Impact == StartupImpactLevel.Faible)));

        sb.AppendLine();
        sb.AppendLine("3) Temps de démarrage estimé");
        if (report.EstimatedBootDuration is TimeSpan duration)
        {
            sb.AppendLine($"- Dernier démarrage estimé : ~{duration.TotalSeconds:F0}s (journal systèmes).");
        }
        else if (report.LastBootAgo is TimeSpan uptime)
        {
            sb.AppendLine($"- Durée exacte indisponible ; dernier démarrage observé il y a {FormatTimeSpan(uptime)} (estimation prudente).");
        }
        else
        {
            sb.AppendLine("- Non disponible (journal ou uptime inaccessible).");
        }

        sb.AppendLine();
        sb.AppendLine("4) Recommandations (peut être désactivé)");
        var recommendations = report.Items.Where(i => i.RecommendedForDisable).Take(10).ToList();
        if (recommendations.Count == 0)
        {
            sb.AppendLine("- Rien à signaler : rien n'a été touché, seulement observé.");
        }
        else
        {
            foreach (var item in recommendations)
            {
                sb.AppendLine($"- {item.Name} : peut être désactivé (type {item.Type}, impact {Describe(item.Impact)})");
            }
            if (report.Items.Count(i => i.RecommendedForDisable) > recommendations.Count)
            {
                sb.AppendLine("- D'autres éléments similaires sont listés mais non détaillés ici.");
            }
        }

        foreach (var note in report.Notes)
        {
            sb.AppendLine($"Note: {note}");
        }

        sb.AppendLine();
        sb.AppendLine("Suite : je peux ensuite lancer \"Optimiser le démarrage\" (non lancé ici) pour appliquer les suggestions.");
        sb.Append("Ton PC adore collectionner les trucs au démarrage.");

        return sb.ToString();
    }

    private static string FormatImpactLine(string label, int count) => $"- {label}: {count}";

    private static string FormatTimeSpan(TimeSpan time)
    {
        if (time.TotalHours >= 24)
        {
            return $"~{time.TotalDays:F1} j";
        }

        if (time.TotalMinutes >= 60)
        {
            return $"~{time.TotalHours:F1} h";
        }

        return $"~{time.TotalMinutes:F0} min";
    }

    private static string Describe(StartupImpactLevel impact) => impact switch
    {
        StartupImpactLevel.Fort => "fort",
        StartupImpactLevel.Moyen => "moyen",
        _ => "faible",
    };
}
