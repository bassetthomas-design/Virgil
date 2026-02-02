using System;
using System.Threading;
using System.Threading.Tasks;
using Virgil.Core.Config;
using Virgil.Core.Logging;
using Virgil.Services.Assistant;

namespace Virgil.App.Services
{
    public sealed class LocalLlamaController
    {
        private readonly SettingsService _settingsService;
        private readonly AssistantProviderFactory _assistantProviderFactory;
        private readonly SemaphoreSlim _gate = new(1, 1);

        public LocalLlamaController(SettingsService settingsService, AssistantProviderFactory assistantProviderFactory)
        {
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _assistantProviderFactory = assistantProviderFactory ?? throw new ArgumentNullException(nameof(assistantProviderFactory));
        }

        public async Task EnableAsync(CancellationToken ct = default)
        {
            if (LocalLlamaStateService.Instance.Status is LocalStatus.Ready or LocalStatus.Starting)
            {
                return;
            }

            await _gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                if (LocalLlamaStateService.Instance.Status is LocalStatus.Ready or LocalStatus.Starting)
                {
                    return;
                }

                _settingsService.Settings.LocalAiAutoEnable = true;
                _settingsService.Save();

                var runtimeManager = _assistantProviderFactory.GetLocalRuntimeManager();
                var modelLocator = new ModelLocator();
                if (modelLocator.TryResolve(out var modelPath, out _))
                {
                    runtimeManager.SetModelPath(modelPath);
                }

                try
                {
                    await runtimeManager.StartAsync(ct).ConfigureAwait(false);
                }
                catch (AssistantProviderUnavailableException ex)
                {
                    RecordFailure(ex.Message, "StartupFailed");
                }
                catch (Exception ex)
                {
                    RecordFailure($"Démarrage IA locale: {ex.Message}", "StartupFailed");
                }
            }
            finally
            {
                _gate.Release();
            }
        }

        public async Task DisableAsync(CancellationToken ct = default)
        {
            if (LocalLlamaStateService.Instance.Status == LocalStatus.Disabled)
            {
                return;
            }

            await _gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                if (LocalLlamaStateService.Instance.Status == LocalStatus.Disabled)
                {
                    return;
                }

                _settingsService.Settings.LocalAiAutoEnable = false;
                _settingsService.Save();

                var runtimeManager = _assistantProviderFactory.GetLocalRuntimeManager();
                try
                {
                    await runtimeManager.StopAsync(ct).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    RecordFailure($"Arrêt IA locale: {ex.Message}", "ShutdownFailed");
                }
            }
            finally
            {
                LocalLlamaStateService.Instance.MarkDisabled();
                _gate.Release();
            }
        }

        private static void RecordFailure(string message, string category)
        {
            Log.Warn(message);
            LlamaRuntimeDiagnosticsStore.Update(existing => existing with
            {
                LastErrorMessage = message,
                LocalStatus = LocalStatus.Failed,
                FailureCategory = category
            });
            LocalLlamaStateService.Instance.MarkFailed(message);
        }
    }
}
