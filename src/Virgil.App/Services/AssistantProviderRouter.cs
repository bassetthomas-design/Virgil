using System;
using Virgil.App.Models;
using Virgil.Services.Assistant;

namespace Virgil.App.Services
{
    public sealed class AssistantProviderRouter
    {
        public IAssistantProvider? SelectProvider(
            ProviderPreference preference,
            bool localEnabled,
            bool openAiEnabled,
            Func<bool> ensureLocalReady,
            Func<IAssistantProvider> createLocalProvider,
            Func<IAssistantProvider> createOpenAiProvider)
        {
            if (ensureLocalReady is null)
            {
                throw new ArgumentNullException(nameof(ensureLocalReady));
            }

            if (createLocalProvider is null)
            {
                throw new ArgumentNullException(nameof(createLocalProvider));
            }

            if (createOpenAiProvider is null)
            {
                throw new ArgumentNullException(nameof(createOpenAiProvider));
            }

            return preference switch
            {
                ProviderPreference.LocalFirst => SelectLocalFirst(localEnabled, openAiEnabled, ensureLocalReady, createLocalProvider, createOpenAiProvider),
                ProviderPreference.OpenAIFirst => SelectOpenAiFirst(localEnabled, openAiEnabled, ensureLocalReady, createLocalProvider, createOpenAiProvider),
                ProviderPreference.LocalOnly => createLocalProvider(),
                ProviderPreference.OpenAIOnly => openAiEnabled ? createOpenAiProvider() : null,
                _ => null
            };
        }

        private static IAssistantProvider SelectLocalFirst(
            bool localEnabled,
            bool openAiEnabled,
            Func<bool> ensureLocalReady,
            Func<IAssistantProvider> createLocalProvider,
            Func<IAssistantProvider> createOpenAiProvider)
        {
            if (localEnabled && ensureLocalReady())
            {
                return createLocalProvider();
            }

            if (openAiEnabled)
            {
                return createOpenAiProvider();
            }

            return createLocalProvider();
        }

        private static IAssistantProvider SelectOpenAiFirst(
            bool localEnabled,
            bool openAiEnabled,
            Func<bool> ensureLocalReady,
            Func<IAssistantProvider> createLocalProvider,
            Func<IAssistantProvider> createOpenAiProvider)
        {
            if (openAiEnabled)
            {
                return createOpenAiProvider();
            }

            if (localEnabled)
            {
                ensureLocalReady();
            }

            return createLocalProvider();
        }
    }
}
