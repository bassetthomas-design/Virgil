using System;
using System.IO;
using System.Threading;
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

        public AssistantProviderFactory(SettingsService settingsService, ISecretStore secretStore)
        {
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _secretStore = secretStore ?? throw new ArgumentNullException(nameof(secretStore));
        }

        public IAssistantProvider? CreateProvider()
        {
            var settings = _settingsService.Settings;
            var defaultRuntimePath = LlamaRuntimeManager.DefaultRuntimePath;
            ILocalLlmRuntime embeddedRuntimeManager = File.Exists(defaultRuntimePath)
                ? new LlamaRuntimeManager(
                    settings.EmbeddedLlamaBaseUrl,
                    defaultRuntimePath,
                    apiKey: settings.EmbeddedLlamaApiKey)
                : new NoRuntimeLocalLlmRuntime(defaultRuntimePath);
            var embeddedProvider = new EmbeddedLlamaProvider(
                embeddedRuntimeManager,
                settings.EmbeddedLlamaBaseUrl,
                TimeSpan.FromSeconds(settings.EmbeddedLlamaTimeoutSeconds));

            var preference = _settingsService.EffectiveProviderPreference;
            var localEnabled = _settingsService.IsLocalEnabled;
            var openAiEnabled = _settingsService.IsOpenAiEnabled;
            var resolved = ProviderPreferenceResolver.Resolve(preference, localEnabled, openAiEnabled);

            if (preference == ProviderPreference.LocalFirst)
            {
                if (localEnabled && TryEnsureLocalReady(embeddedRuntimeManager))
                {
                    return embeddedProvider;
                }

                if (openAiEnabled)
                {
                    return CreateOpenAiProvider(settings);
                }

                Log.Warning("IA indisponible: aucune option locale ou OpenAI active.");
                return null;
            }

            if (preference == ProviderPreference.OpenAIFirst)
            {
                if (openAiEnabled)
                {
                    return CreateOpenAiProvider(settings);
                }

                if (localEnabled && TryEnsureLocalReady(embeddedRuntimeManager))
                {
                    return embeddedProvider;
                }

                Log.Warning("IA indisponible: aucune option locale ou OpenAI active.");
                return null;
            }

            var provider = resolved switch
            {
                AiProvider.Disabled => null,
                AiProvider.EmbeddedLlama => localEnabled && TryEnsureLocalReady(embeddedRuntimeManager) ? embeddedProvider : null,
                AiProvider.OpenAI => openAiEnabled ? CreateOpenAiProvider(settings) : null,
                _ => null
            };

            if (provider is null)
            {
                Log.Warning("IA indisponible: aucune option locale ou OpenAI active.");
            }

            return provider;
        }

        private IAssistantProvider CreateOpenAiProvider(AppSettings settings)
        {
            var apiKey = _secretStore.LoadOpenAiApiKey();
            return new OpenAiAssistantProvider(
                apiKey,
                settings.OpenAiModel,
                TimeSpan.FromSeconds(settings.OpenAiTimeoutSeconds),
                isProviderEnabled: !string.IsNullOrWhiteSpace(apiKey));
        }

        private static bool TryEnsureLocalReady(ILocalLlmRuntime runtimeManager)
        {
            var modelLocator = new ModelLocator();
            if (!modelLocator.TryResolve(out var modelPath, out _))
            {
                Log.Warning("IA locale indisponible: modèle GGUF non trouvé.");
                return false;
            }

            try
            {
                runtimeManager.SetModelPath(modelPath);
                runtimeManager.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
                var healthy = runtimeManager.HealthCheckAsync(CancellationToken.None).GetAwaiter().GetResult();
                if (!healthy)
                {
                    Log.Warning("IA locale indisponible: runtime non prêt.");
                }

                return healthy;
            }
            catch (AssistantProviderUnavailableException ex)
            {
                Log.Warning($"IA locale indisponible: {ex.Message}");
                return false;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "IA locale indisponible: échec inattendu.");
                return false;
            }
        }
    }
}
