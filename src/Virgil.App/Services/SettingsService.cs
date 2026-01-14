using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Virgil.App.Models;
using Virgil.Core.Config;
using Virgil.Services;
using Virgil.Services.Assistant;

namespace Virgil.App.Services
{
    public class SettingsService
    {
        private static readonly string SettingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Virgil", "settings.json");
        private readonly ISecretStore _secretStore;
        private readonly ModelLocator _modelLocator;
        private AiProvider _effectiveAiProvider = AiProvider.EmbeddedLlama;

        public AppSettings Settings { get; private set; } = new AppSettings();
        public AiProvider EffectiveAiProvider => _effectiveAiProvider;

        public ProviderPreference EffectiveProviderPreference
            => Settings.ProviderPreference ?? ProviderPreference.LocalFirst;

        public bool IsLocalEnabled
        {
            get
            {
                var availability = ModelAvailability.Check(_modelLocator, Settings.GetActiveFullManifest());
                var runtimePath = LlamaRuntimeManager.DefaultRuntimePath;
                return availability.CanRunOffline && File.Exists(runtimePath);
            }
        }

        public bool IsOpenAiEnabled => !string.IsNullOrWhiteSpace(_secretStore.LoadOpenAiApiKey());

        public SettingsService(ISecretStore? secretStore = null, ModelLocator? modelLocator = null)
        {
            _secretStore = secretStore ?? new OpenAiKeyStore();
            _modelLocator = modelLocator ?? new ModelLocator();
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            Load();
        }

        public void Save()
        {
            var json = JsonSerializer.Serialize(Settings, new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });
            File.WriteAllText(SettingsPath, json);
            _effectiveAiProvider = ResolveAiProvider(Settings.AiProvider);
        }

        /// <summary>
        /// Recharge les paramètres depuis le fichier de configuration.
        /// </summary>
        public void Reload()
        {
            Load();
        }

        private void Load()
        {
            if (File.Exists(SettingsPath))
            {
                try { Settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath)) ?? new AppSettings(); }
                catch { Settings = new AppSettings(); }
            }
            else
            {
                Settings = new AppSettings();
            }

            Settings.HasOpenAiKey = !string.IsNullOrWhiteSpace(_secretStore.LoadOpenAiApiKey());
            _effectiveAiProvider = ResolveAiProvider(Settings.AiProvider);
        }

        private AiProvider ResolveAiProvider(AiProvider? configuredProvider)
        {
            if (Settings.ProviderPreference is null && configuredProvider is not null)
            {
                var hasOpenAiKey = !string.IsNullOrWhiteSpace(_secretStore.LoadOpenAiApiKey());
                return configuredProvider.Value switch
                {
                    AiProvider.OpenAI => hasOpenAiKey ? AiProvider.OpenAI : AiProvider.Disabled,
                    AiProvider.EmbeddedLlama => AiProvider.EmbeddedLlama,
                    AiProvider.Disabled => AiProvider.Disabled,
                    _ => AiProvider.Disabled
                };
            }

            return ProviderPreferenceResolver.Resolve(
                EffectiveProviderPreference,
                IsLocalEnabled,
                IsOpenAiEnabled);
        }
    }
}
