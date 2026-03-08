using System;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Virgil.Services.Abstractions;

namespace Virgil.Services;

public sealed class SpecialService : ISpecialService
{
    private readonly IConfigurationReloader _reloader;
    private readonly IConfirmationPrompt _confirmation;
    private readonly IChatService _chat;
    private readonly RamboModeService _rambo;
    private readonly IActionProgressController _progress;

    public SpecialService(
        IConfigurationReloader reloader,
        IConfirmationPrompt confirmation,
        IChatService chat,
        RamboModeService? rambo = null,
        IActionProgressController? progress = null)
    {
        _reloader = reloader ?? throw new ArgumentNullException(nameof(reloader));
        _confirmation = confirmation ?? throw new ArgumentNullException(nameof(confirmation));
        _chat = chat ?? throw new ArgumentNullException(nameof(chat));
        _rambo = rambo ?? new RamboModeService(confirmationPrompt: _confirmation);
        _progress = progress ?? new NoopProgressController();
    }

    public async Task<ActionExecutionResult> RamboModeAsync(CancellationToken ct = default)
    {
        var confirmed = await _confirmation.ConfirmRamboAsync(ct).ConfigureAwait(false);
        if (!confirmed)
        {
            return ActionExecutionResult.Skipped("Mode RAMBO", "Mode RAMBO annulé par l'utilisateur.");
        }

        await _chat.InfoAsync("Mode RAMBO activé. Je lance un nettoyage profond et une optimisation du système.", ct).ConfigureAwait(false);
        _progress.StartIndeterminate();
        try
        {
            var result = await _rambo.RunAsync(ct).ConfigureAwait(false);
            if (!result.Succeeded)
            {
                var reason = string.IsNullOrWhiteSpace(result.FailureReason) ? "raison inconnue" : result.FailureReason;
                var shortFailure = $"Mode RAMBO: échec partiel ({reason}).";
                await _chat.WarnAsync(shortFailure, ct).ConfigureAwait(false);
                return ActionExecutionResult.Partial("Mode RAMBO", shortFailure, debugInfo: BuildDebugInfo(result));
            }

            await _chat.InfoAsync(result.Summary, ct).ConfigureAwait(false);
            return ActionExecutionResult.Ok("Mode RAMBO terminé", result.Summary, debugInfo: BuildDebugInfo(result));
        }
        catch (Exception ex)
        {
            var shortFailure = $"Mode RAMBO: échec partiel ({ex.Message}).";
            await _chat.WarnAsync(shortFailure, ct).ConfigureAwait(false);
            return ActionExecutionResult.Failure("Mode RAMBO", shortFailure);
        }
        finally
        {
            _progress.Complete();
        }
    }

    public async Task<ActionExecutionResult> ReloadConfigurationAsync(CancellationToken ct = default)
    {
        var confirmed = await _confirmation.ConfirmAsync("les changements seront appliqués", ct).ConfigureAwait(false);
        if (!confirmed)
        {
            const string cancelled = "Résultat: Échec — rien rechargé, votre veto est respecté.";
            await _chat.InfoAsync(cancelled, ct).ConfigureAwait(false);
            return ActionExecutionResult.Failure(cancelled);
        }

        try
        {
            var outcome = await _reloader.ReloadAsync(ct).ConfigureAwait(false);
            var status = outcome.GetOverallStatus();
            var message = status switch
            {
                ConfigurationReloadStatus.Ok => "Résultat: OK — configuration rafraîchie sans drama.",
                ConfigurationReloadStatus.Partial => "Résultat: Partiel — ça a tiqué mais on s'en contente.",
                _ => "Résultat: Échec — personne n'aime tout refaire pour rien.",
            };

            await _chat.InfoAsync(message, ct).ConfigureAwait(false);
            return status switch
            {
                ConfigurationReloadStatus.Ok => ActionExecutionResult.Ok("Configuration rechargée", message),
                ConfigurationReloadStatus.Partial => ActionExecutionResult.Partial("Configuration rechargée", message),
                _ => ActionExecutionResult.Failure("Configuration rechargée", message),
            };
        }
        catch (Exception ex)
        {
            var message = $"Résultat: Échec — {ex.Message}";
            await _chat.WarnAsync(message, ct).ConfigureAwait(false);
            return ActionExecutionResult.Failure(message);
        }
    }

    private static string BuildDebugInfo(RamboResult result)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"TempFilesFreedBytes={result.TempFilesFreedBytes.ToString(CultureInfo.InvariantCulture)}");
        sb.AppendLine($"BrowserCacheFreedBytes={result.BrowserCacheFreedBytes.ToString(CultureInfo.InvariantCulture)}");
        sb.AppendLine($"FilesDeleted={result.FilesDeleted}");
        sb.AppendLine($"FoldersDeleted={result.FoldersDeleted}");
        sb.AppendLine($"StandbyMemoryFreedBytes={result.StandbyMemoryFreedBytes.ToString(CultureInfo.InvariantCulture)}");
        sb.AppendLine($"HeavyProcessesClosed={result.HeavyProcessesClosed}");
        sb.AppendLine($"IgnoredItems={result.IgnoredItems}");
        sb.AppendLine($"FailedSteps={result.FailedSteps}");
        sb.AppendLine($"SystemCacheFreedBytes={result.SystemCacheFreedBytes.ToString(CultureInfo.InvariantCulture)}");
        sb.AppendLine($"DuplicateFilesPotentialBytes={result.DuplicateFilesPotentialBytes.ToString(CultureInfo.InvariantCulture)}");
        sb.AppendLine($"InactiveFoldersPotentialBytes={result.InactiveFoldersPotentialBytes.ToString(CultureInfo.InvariantCulture)}");
        sb.AppendLine($"EmptyFoldersDeleted={result.EmptyFoldersDeleted}");
        sb.AppendLine($"AutoContinueUsed={result.AutoContinueUsed}");
        if (result.ErrorLogs.Count > 0)
        {
            sb.AppendLine("ErrorLogs:");
            foreach (var x in result.ErrorLogs)
            {
                sb.AppendLine($"- {x}");
            }
        }

        if (result.DiskInsights.Count > 0)
        {
            sb.AppendLine("DiskInsights:");
            foreach (var x in result.DiskInsights)
            {
                sb.AppendLine($"- {x}");
            }
        }

        if (result.StartupInsights.Count > 0)
        {
            sb.AppendLine("StartupInsights:");
            foreach (var x in result.StartupInsights)
            {
                sb.AppendLine($"- {x}");
            }
        }

        if (result.RamInsights.Count > 0)
        {
            sb.AppendLine("RamInsights:");
            foreach (var x in result.RamInsights)
            {
                sb.AppendLine($"- {x}");
            }
        }

        return sb.ToString().TrimEnd();
    }


    private sealed class NoopProgressController : IActionProgressController
    {
        public void StartIndeterminate()
        {
        }

        public void Complete()
        {
        }
    }
}
