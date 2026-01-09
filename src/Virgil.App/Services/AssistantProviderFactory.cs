using System;
using Virgil.App.Models;
using Virgil.Services.Assistant;

namespace Virgil.App.Services
{
    public sealed class AssistantProviderFactory
    {
        private readonly SettingsService _settingsService;
        private readonly OpenAiKeyStore _keyStore;

        public AssistantProviderFactory(SettingsService settingsService, OpenAiKeyStore keyStore)
        {
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _keyStore = keyStore ?? throw new ArgumentNullException(nameof(keyStore));
        }

        public IAssistantProvider? CreateProvider()
        {
            var settings = _settingsService.Settings;
            var ollamaProvider = new OllamaAssistantProvider(
                settings.OllamaBaseUrl,
                settings.OllamaModel,
                TimeSpan.FromSeconds(settings.OllamaTimeoutSeconds));
            var embeddedRuntimeManager = new LlamaRuntimeManager(settings.EmbeddedLlamaBaseUrl);
            var embeddedProvider = new EmbeddedLlamaProvider(
                embeddedRuntimeManager,
                settings.EmbeddedLlamaBaseUrl,
                TimeSpan.FromSeconds(settings.EmbeddedLlamaTimeoutSeconds));

            return settings.AiProvider switch
            {
                AiProvider.Disabled => null,
                AiProvider.EmbeddedLlama => embeddedProvider,
                AiProvider.OpenAI => CreateOpenAiProvider(settings, ollamaProvider),
                _ => ollamaProvider
            };
        }

        private IAssistantProvider CreateOpenAiProvider(AppSettings settings, IAssistantProvider fallback)
        {
            var apiKey = _keyStore.Load();
            var openAiProvider = new OpenAiAssistantProvider(
                apiKey,
                settings.OpenAiModel,
                TimeSpan.FromSeconds(settings.OpenAiTimeoutSeconds));

            return new FallbackAssistantProvider(
                openAiProvider,
                fallback,
                "OpenAI indisponible, bascule sur IA locale.");
        }
    }
}
