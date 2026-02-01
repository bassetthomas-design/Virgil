using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Virgil.Core.Config;
using Virgil.Core.Logging;
using Virgil.App.Models;
using Virgil.Services.Assistant;

namespace Virgil.App.Services
{
    public sealed class AssistantProviderFactory
    {
        private readonly SettingsService _settingsService;
        private readonly ISecretStore _secretStore;
        private readonly object _runtimeLock = new();
        private ILocalLlmRuntime? _embeddedRuntimeManager;
        private static int _localStartGuard;

        public AssistantProviderFactory(SettingsService settingsService, ISecretStore secretStore)
        {
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _secretStore = secretStore ?? throw new ArgumentNullException(nameof(secretStore));
        }

        public IAssistantProvider? CreateProvider()
        {
            var settings = _settingsService.Settings;
            var defaultRuntimePath = LlamaRuntimeManager.DefaultRuntimePath;
            var embeddedRuntimeManager = GetEmbeddedRuntimeManager(settings, defaultRuntimePath);
            var embeddedProvider = new EmbeddedLlamaProvider(
                embeddedRuntimeManager,
                settings.EmbeddedLlamaBaseUrl,
                TimeSpan.FromSeconds(settings.EmbeddedLlamaTimeoutSeconds),
                providerPreference: _settingsService.EffectiveProviderPreference.ToString(),
                localEnabled: _settingsService.IsLocalEnabled,
                openAiEnabled: _settingsService.IsOpenAiEnabled,
                maxTokens: settings.LocalMaxTokens);

            var preference = _settingsService.EffectiveProviderPreference;
            var localEnabled = _settingsService.IsLocalEnabled;
            var openAiEnabled = _settingsService.IsOpenAiEnabled;
            var router = new AssistantProviderRouter();
            var provider = router.SelectProvider(
                preference,
                localEnabled,
                openAiEnabled,
                () => TryEnsureLocalReady(embeddedRuntimeManager),
                () => embeddedProvider,
                () => CreateOpenAiProvider(settings));

            if (provider is null)
            {
                Log.Warn("IA indisponible: aucune option locale ou OpenAI active.");
            }

            return provider;
        }

        public async Task StartLocalLlamaAsync(CancellationToken ct = default)
        {
            if (Interlocked.Exchange(ref _localStartGuard, 1) == 1)
            {
                return;
            }

            if (!_settingsService.IsLocalEnabled)
            {
                return;
            }

            var settings = _settingsService.Settings;
            var runtimeManager = GetEmbeddedRuntimeManager(settings, LlamaRuntimeManager.DefaultRuntimePath);
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
                Log.Warn($"Démarrage IA locale impossible: {ex.Message}");
            }
            catch (Exception ex)
            {
                Log.Error($"Démarrage IA locale: échec inattendu. {ex}");
            }
        }

        private IAssistantProvider CreateOpenAiProvider(AppSettings settings)
        {
            var apiKey = _secretStore.LoadOpenAiApiKey();
            return new OpenAiAssistantProvider(
                apiKey,
                settings.OpenAiModel,
                TimeSpan.FromSeconds(settings.OpenAiTimeoutSeconds),
                isProviderEnabled: settings.OpenAiEnabled && !string.IsNullOrWhiteSpace(apiKey));
        }

        private static bool TryEnsureLocalReady(ILocalLlmRuntime runtimeManager)
        {
            var modelLocator = new ModelLocator();
            if (!modelLocator.TryResolve(out var modelPath, out _))
            {
                Log.Warn("IA locale indisponible: modèle GGUF non trouvé.");
                return false;
            }

            try
            {
                runtimeManager.SetModelPath(modelPath);
                runtimeManager.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
                var healthy = runtimeManager.HealthCheckAsync(CancellationToken.None).GetAwaiter().GetResult();
                if (!healthy)
                {
                    Log.Warn("IA locale indisponible: runtime non prêt.");
                }

                return healthy;
            }
            catch (AssistantProviderUnavailableException ex)
            {
                Log.Warn($"IA locale indisponible: {ex.Message}");
                return false;
            }
            catch (Exception ex)
            {
                Log.Error($"IA locale indisponible: échec inattendu. {ex}");
                return false;
            }
        }

        private ILocalLlmRuntime GetEmbeddedRuntimeManager(AppSettings settings, string defaultRuntimePath)
        {
            if (_embeddedRuntimeManager is not null)
            {
                return _embeddedRuntimeManager;
            }

            lock (_runtimeLock)
            {
                if (_embeddedRuntimeManager is not null)
                {
                    return _embeddedRuntimeManager;
                }

                _embeddedRuntimeManager = File.Exists(defaultRuntimePath)
                    ? new LlamaRuntimeManager(
                        settings.EmbeddedLlamaBaseUrl,
                        defaultRuntimePath,
                        apiKey: settings.EmbeddedLlamaApiKey)
                    : new NoRuntimeLocalLlmRuntime(defaultRuntimePath);
            }

            return _embeddedRuntimeManager;
        }
    }
}
