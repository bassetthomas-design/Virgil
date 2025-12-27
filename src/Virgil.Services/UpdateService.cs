using System;
using System.Threading;
using System.Threading.Tasks;
using Virgil.Services.Abstractions;

namespace Virgil.Services;

/// <summary>
/// Implémentation basique qui délègue aux services Windows/winget déjà présents
/// dans Virgil.Core. Les actions indisponibles retournent un résultat explicite.
/// </summary>
public sealed class UpdateService : IUpdateService
{
    private readonly Core.Services.ApplicationUpdateService _apps = new();
    private readonly Core.Services.WindowsUpdateService _windows = new();

    public async Task<ActionExecutionResult> UpdateAppsAsync(CancellationToken ct = default)
    {
        try
        {
            var log = await _apps.UpgradeAllAsync(includeUnknown: true, silent: true).ConfigureAwait(false);
            return ActionExecutionResult.Ok("Mise à jour des applications terminée", log);
        }
        catch (Exception ex)
        {
            return ActionExecutionResult.Failure($"Erreur mise à jour applications: {ex.Message}");
        }
    }

    public async Task<ActionExecutionResult> RunWindowsUpdateAsync(CancellationToken ct = default)
    {
        var sb = new System.Text.StringBuilder();
        try
        {
            sb.AppendLine(await _windows.StartScanAsync().ConfigureAwait(false));
            sb.AppendLine(await _windows.StartDownloadAsync().ConfigureAwait(false));
            sb.AppendLine(await _windows.StartInstallAsync().ConfigureAwait(false));
            return ActionExecutionResult.Ok("Windows Update exécuté", sb.ToString());
        }
        catch (Exception ex)
        {
            return ActionExecutionResult.Failure($"Erreur Windows Update: {ex.Message}");
        }
    }

    public Task<ActionExecutionResult> CheckGpuDriversAsync(CancellationToken ct = default)
        => Task.FromResult(ActionExecutionResult.NotAvailable("Vérification des pilotes GPU non disponible"));
}
