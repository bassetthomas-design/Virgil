using System;
using System.IO;
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
                ? new LlamaRuntimeManager(settings.EmbeddedLlamaBaseUrl, defaultRuntimePath)
                : new NoRuntimeLocalLlmRuntime(defaultRuntimePath);
            var embeddedProvider = new EmbeddedLlamaProvider(
                embeddedRuntimeManager,
                settings.EmbeddedLlamaBaseUrl,
                TimeSpan.FromSeconds(settings.EmbeddedLlamaTimeoutSeconds));

            return _settingsService.EffectiveAiProvider switch
            {
                AiProvider.Disabled => null,
                AiProvider.EmbeddedLlama => embeddedProvider,
                AiProvider.OpenAI => CreateOpenAiProvider(settings, embeddedProvider),
                _ => embeddedProvider
            };
        }

        private IAssistantProvider CreateOpenAiProvider(AppSettings settings, IAssistantProvider fallback)
        {
            var apiKey = _secretStore.LoadOpenAiApiKey();
            var openAiProvider = new OpenAiAssistantProvider(
                apiKey,
                settings.OpenAiModel,
                TimeSpan.FromSeconds(settings.OpenAiTimeoutSeconds),
                isProviderEnabled: _settingsService.EffectiveAiProvider == AiProvider.OpenAI
                    && !string.IsNullOrWhiteSpace(apiKey));

            return new FallbackAssistantProvider(
                openAiProvider,
                fallback,
                "OpenAI indisponible, bascule sur IA locale.");
        }
    }
}
