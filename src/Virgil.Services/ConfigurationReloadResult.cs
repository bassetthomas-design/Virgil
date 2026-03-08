using System;
using System.Threading;
using System.Threading.Tasks;

namespace Virgil.Services;

public interface IConfigurationReloader
{
    Task<ConfigurationReloadResult> ReloadAsync(CancellationToken ct = default);
}

public interface IConfirmationPrompt
{
    Task<bool> ConfirmAsync(string message, CancellationToken ct = default);
    Task<bool> ConfirmRamboAsync(CancellationToken ct = default);
    Task<RamboErrorDialogResult> AskRamboErrorDecisionAsync(string friendlyMessage, CancellationToken ct = default);
}

public enum ConfigurationReloadStatus
{
    Ok,
    Partial,
    Failure
}

public sealed record ConfigurationReloadResult(
    bool UserSettings,
    bool SystemSettings,
    bool UiPreferences,
    bool ViewModels,
    bool Services,
    bool Caches)
{
    public ConfigurationReloadStatus GetOverallStatus()
    {
        var successes = 0;
        var attempts = 0;

        void Count(bool attempt)
        {
            attempts++;
            if (attempt)
            {
                successes++;
            }
        }

        Count(UserSettings);
        Count(SystemSettings);
        Count(UiPreferences);
        Count(ViewModels);
        Count(Services);
        Count(Caches);

        if (successes == attempts)
        {
            return ConfigurationReloadStatus.Ok;
        }

        if (successes > 0)
        {
            return ConfigurationReloadStatus.Partial;
        }

        return ConfigurationReloadStatus.Failure;
    }
}
