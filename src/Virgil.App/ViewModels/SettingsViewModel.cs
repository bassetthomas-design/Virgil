using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using AppChatService = Virgil.App.Chat.ChatService;
using Virgil.App.Commands;
using Virgil.App.Models;
using Virgil.App.Services;
using Virgil.Core.Config;
using Virgil.Services;
using Virgil.Services.Assistant;
using Virgil.Services.ModelPacks;

namespace Virgil.App.ViewModels
{
    public class SettingsViewModel : INotifyPropertyChanged
    {
        private const string FullPackDownloadUrl =
            "https://huggingface.co/TheBloke/Meta-Llama-3.1-8B-Instruct-GGUF/resolve/main/Meta-Llama-3.1-8B-Instruct-Q5_K_M.gguf";
        private const int RuntimeHelpMaxLength = 6000;
        private const string RuntimeHelpLogFileName = "runtime-help.log";
        private readonly SettingsService _svc;
        private readonly AppChatService? _chatService;
        private readonly IAssistantService? _assistantService;
        private readonly ISecretStore _secretStore;
        private readonly ModelLocator _modelLocator = new();
        private CancellationTokenSource? _downloadCts;
        private bool _isDownloadIndeterminate;
        private readonly ModelPackDownloader _packDownloader;
        private readonly LlamaRuntimeInstaller _runtimeInstaller;
        private readonly ModelPackManifest _packManifest;
        private string _modelStatusText = string.Empty;
        private string _modelPathText = string.Empty;
        private string _modelAgeText = string.Empty;
        private string _providerStatusText = string.Empty;
        private string _ggufStatusText = string.Empty;
        private string _runtimeStatusText = string.Empty;
        private string _runtimeProcessStatusText = string.Empty;
        private string _runtimePortStatusText = string.Empty;
        private string _runtimeWarningText = string.Empty;
        private string _runtimeLastErrorText = string.Empty;
        private string _runtimeExitCodeText = string.Empty;
        private string _runtimeCommandLineText = string.Empty;
        private string _runtimeStderrText = string.Empty;
        private string _runtimeSecurityFlagsText = string.Empty;
        private string _runtimeSecurityStrategyText = string.Empty;
        private string _runtimeHelpText = "Cliquez sur \"Aide runtime\" pour afficher l'aide.";
        private string _openAiStatusText = string.Empty;
        private string _openAiKeyStatusText = string.Empty;
        private string _openAiTestResponseText = string.Empty;
        private string _openAiApiKeyInput = string.Empty;
        private bool _openAiEnabled;
        private bool _isOpenAiKeyVisible;
        private bool _isRuntimeHelpLoading;
        private ProviderPreference _selectedProviderPreference;
        private readonly IReadOnlyList<ProviderPreferenceOption> _providerPreferenceOptions;

        public SettingsViewModel(
            SettingsService svc,
            AppChatService? chatService = null,
            IAssistantService? assistantService = null,
            ISecretStore? secretStore = null)
        {
            _svc = svc;
            _chatService = chatService;
            _assistantService = assistantService;
            _secretStore = secretStore ?? new OpenAiKeyStore();

            // Charger une "copie" (en champs) pour permettre Annuler sans effet de bord
            var s = _svc.Settings;

            _monitoringIntervalMinutesMin = s.MonitoringIntervalMinutesMin;
            _monitoringIntervalMinutesMax = s.MonitoringIntervalMinutesMax;
            _defaultMessageTtlMs = s.DefaultMessageTtlMs;
            _companionTalkative = s.CompanionTalkative;
            _enableBeatPulse = s.EnableBeatPulse;
            _selectedProviderPreference = s.ProviderPreference ?? ProviderPreference.LocalFirst;
            _providerPreferenceOptions = new[]
            {
                new ProviderPreferenceOption(ProviderPreference.LocalFirst, "LocalFirst (défaut)"),
                new ProviderPreferenceOption(ProviderPreference.OpenAIFirst, "OpenAIFirst"),
                new ProviderPreferenceOption(ProviderPreference.LocalOnly, "LocalOnly"),
                new ProviderPreferenceOption(ProviderPreference.OpenAIOnly, "OpenAIOnly")
            };

            _warnTemp = s.Mood.WarnTemp;
            _alertTemp = s.Mood.AlertTemp;
            _warnCpu = s.Mood.WarnCpu;
            _openAiEnabled = s.OpenAiEnabled;

            _packDownloader = new ModelPackDownloader(_modelLocator);
            _runtimeInstaller = new LlamaRuntimeInstaller();
            _packManifest = _svc.Settings.GetActiveFullManifest();

            _isPackInstalled = _modelLocator.IsInstalled;
            _downloadStatusText = _isPackInstalled ? "Pack Full installé." : "Pack Full non installé.";
            _downloadSpeedText = "—";
            RefreshModelDetails();
            RefreshAiStatuses();

            _installPackCommand = new AsyncRelayCommand(_ => InstallPackAsync(), _ => !IsDownloading);
            _cancelDownloadCommand = new RelayCommand(_ => CancelDownload(), _ => IsDownloading);
            _verifyPackCommand = new AsyncRelayCommand(_ => VerifyPackAsync(), _ => !IsDownloading);
            _testAiCommand = new AsyncRelayCommand(_ => TestAiAsync(), _ => !IsDownloading);
            _saveAiSettingsCommand = new RelayCommand(_ => SaveAiSettings());
            _testOpenAiCommand = new AsyncRelayCommand(_ => TestOpenAiAsync(), _ => !IsDownloading);
            _runtimeHelpCommand = new AsyncRelayCommand(_ => ShowRuntimeHelpAsync(), _ => !IsRuntimeHelpLoading);
        }

        private int _monitoringIntervalMinutesMin;
        public int MonitoringIntervalMinutesMin
        {
            get => _monitoringIntervalMinutesMin;
            set { _monitoringIntervalMinutesMin = value; OnPropertyChanged(); }
        }

        private int _monitoringIntervalMinutesMax;
        public int MonitoringIntervalMinutesMax
        {
            get => _monitoringIntervalMinutesMax;
            set { _monitoringIntervalMinutesMax = value; OnPropertyChanged(); }
        }

        private int _defaultMessageTtlMs;
        public int DefaultMessageTtlMs
        {
            get => _defaultMessageTtlMs;
            set { _defaultMessageTtlMs = value; OnPropertyChanged(); }
        }

        private bool _companionTalkative;
        public bool CompanionTalkative
        {
            get => _companionTalkative;
            set { _companionTalkative = value; OnPropertyChanged(); }
        }

        private bool _enableBeatPulse;
        public bool EnableBeatPulse
        {
            get => _enableBeatPulse;
            set { _enableBeatPulse = value; OnPropertyChanged(); }
        }

        private double _warnTemp;
        public double WarnTemp
        {
            get => _warnTemp;
            set { _warnTemp = value; OnPropertyChanged(); }
        }

        private double _alertTemp;
        public double AlertTemp
        {
            get => _alertTemp;
            set { _alertTemp = value; OnPropertyChanged(); }
        }

        private double _warnCpu;
        public double WarnCpu
        {
            get => _warnCpu;
            set { _warnCpu = value; OnPropertyChanged(); }
        }

        private bool _isPackInstalled;
        public bool IsPackInstalled
        {
            get => _isPackInstalled;
            private set
            {
                if (_isPackInstalled == value)
                {
                    return;
                }

                _isPackInstalled = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PackStatusText));
                OnPropertyChanged(nameof(ShowPackInstallPrompt));
                RefreshModelDetails();
                RefreshAiStatuses();
            }
        }

        private bool _isDownloading;
        public bool IsDownloading
        {
            get => _isDownloading;
            private set
            {
                if (_isDownloading == value)
                {
                    return;
                }

                _isDownloading = value;
                OnPropertyChanged();
                UpdateCommandStates();
            }
        }

        private double _downloadProgressPercent;
        public double DownloadProgressPercent
        {
            get => _downloadProgressPercent;
            private set { _downloadProgressPercent = value; OnPropertyChanged(); }
        }

        private string _downloadSpeedText = string.Empty;
        public string DownloadSpeedText
        {
            get => _downloadSpeedText;
            private set { _downloadSpeedText = value; OnPropertyChanged(); }
        }

        private string _downloadStatusText = string.Empty;
        public string DownloadStatusText
        {
            get => _downloadStatusText;
            private set { _downloadStatusText = value; OnPropertyChanged(); }
        }

        private string _aiTestResponseText = string.Empty;
        public string AiTestResponseText
        {
            get => _aiTestResponseText;
            private set { _aiTestResponseText = value; OnPropertyChanged(); }
        }

        public string PackStatusText => IsPackInstalled ? "Installé" : "Non installé";

        public bool ShowPackInstallPrompt => !_isPackInstalled && _svc.EffectiveAiProvider == AiProvider.EmbeddedLlama;

        public string ProviderStatusText
        {
            get => _providerStatusText;
            private set { _providerStatusText = value; OnPropertyChanged(); }
        }

        public string GgufStatusText
        {
            get => _ggufStatusText;
            private set { _ggufStatusText = value; OnPropertyChanged(); }
        }

        public string RuntimeStatusText
        {
            get => _runtimeStatusText;
            private set { _runtimeStatusText = value; OnPropertyChanged(); }
        }

        public string RuntimeProcessStatusText
        {
            get => _runtimeProcessStatusText;
            private set { _runtimeProcessStatusText = value; OnPropertyChanged(); }
        }

        public string RuntimePortStatusText
        {
            get => _runtimePortStatusText;
            private set { _runtimePortStatusText = value; OnPropertyChanged(); }
        }

        public string RuntimeWarningText
        {
            get => _runtimeWarningText;
            private set { _runtimeWarningText = value; OnPropertyChanged(); }
        }

        public string RuntimeLastErrorText
        {
            get => _runtimeLastErrorText;
            private set { _runtimeLastErrorText = value; OnPropertyChanged(); }
        }

        public string RuntimeExitCodeText
        {
            get => _runtimeExitCodeText;
            private set { _runtimeExitCodeText = value; OnPropertyChanged(); }
        }

        public string RuntimeCommandLineText
        {
            get => _runtimeCommandLineText;
            private set { _runtimeCommandLineText = value; OnPropertyChanged(); }
        }

        public string RuntimeStderrText
        {
            get => _runtimeStderrText;
            private set { _runtimeStderrText = value; OnPropertyChanged(); }
        }

        public string RuntimeSecurityFlagsText
        {
            get => _runtimeSecurityFlagsText;
            private set { _runtimeSecurityFlagsText = value; OnPropertyChanged(); }
        }

        public string RuntimeSecurityStrategyText
        {
            get => _runtimeSecurityStrategyText;
            private set { _runtimeSecurityStrategyText = value; OnPropertyChanged(); }
        }

        public string RuntimeHelpText
        {
            get => _runtimeHelpText;
            private set { _runtimeHelpText = value; OnPropertyChanged(); }
        }

        public bool IsRuntimeHelpLoading
        {
            get => _isRuntimeHelpLoading;
            private set
            {
                if (_isRuntimeHelpLoading == value)
                {
                    return;
                }

                _isRuntimeHelpLoading = value;
                OnPropertyChanged();
                UpdateCommandStates();
            }
        }

        public string OpenAiStatusText
        {
            get => _openAiStatusText;
            private set { _openAiStatusText = value; OnPropertyChanged(); }
        }

        public string OpenAiKeyStatusText
        {
            get => _openAiKeyStatusText;
            private set { _openAiKeyStatusText = value; OnPropertyChanged(); }
        }

        public string OpenAiTestResponseText
        {
            get => _openAiTestResponseText;
            private set { _openAiTestResponseText = value; OnPropertyChanged(); }
        }

        public string OpenAiApiKeyInput
        {
            get => _openAiApiKeyInput;
            set { _openAiApiKeyInput = value; OnPropertyChanged(); }
        }

        public bool OpenAiEnabled
        {
            get => _openAiEnabled;
            set { _openAiEnabled = value; OnPropertyChanged(); }
        }

        public bool IsOpenAiKeyVisible
        {
            get => _isOpenAiKeyVisible;
            set
            {
                if (_isOpenAiKeyVisible == value)
                {
                    return;
                }

                _isOpenAiKeyVisible = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(OpenAiKeyToggleText));
            }
        }

        public string OpenAiKeyToggleText => IsOpenAiKeyVisible ? "Masquer" : "Afficher";

        public ProviderPreference SelectedProviderPreference
        {
            get => _selectedProviderPreference;
            set { _selectedProviderPreference = value; OnPropertyChanged(); }
        }

        public IReadOnlyList<ProviderPreferenceOption> ProviderPreferenceOptions => _providerPreferenceOptions;

        public string ModelStatusText
        {
            get => _modelStatusText;
            private set { _modelStatusText = value; OnPropertyChanged(); }
        }

        public string ModelPathText
        {
            get => _modelPathText;
            private set { _modelPathText = value; OnPropertyChanged(); }
        }

        public string ModelAgeText
        {
            get => _modelAgeText;
            private set { _modelAgeText = value; OnPropertyChanged(); }
        }

        public bool IsDownloadIndeterminate
        {
            get => _isDownloadIndeterminate;
            private set
            {
                if (_isDownloadIndeterminate == value)
                {
                    return;
                }

                _isDownloadIndeterminate = value;
                OnPropertyChanged();
            }
        }

        private readonly AsyncRelayCommand _installPackCommand;
        public ICommand InstallPackCommand => _installPackCommand;

        private readonly RelayCommand _cancelDownloadCommand;
        public ICommand CancelDownloadCommand => _cancelDownloadCommand;

        private readonly AsyncRelayCommand _verifyPackCommand;
        public ICommand VerifyPackCommand => _verifyPackCommand;

        private readonly AsyncRelayCommand _testAiCommand;
        public ICommand TestAiCommand => _testAiCommand;

        private readonly RelayCommand _saveAiSettingsCommand;
        public ICommand SaveAiSettingsCommand => _saveAiSettingsCommand;

        private readonly AsyncRelayCommand _testOpenAiCommand;
        public ICommand TestOpenAiCommand => _testOpenAiCommand;

        private readonly AsyncRelayCommand _runtimeHelpCommand;
        public ICommand RuntimeHelpCommand => _runtimeHelpCommand;

        /// <summary>
        /// Applique les valeurs au SettingsService et persiste.
        /// </summary>
        public void Save()
        {
            ApplyGeneralSettings();
            ApplyAiSettings();
            _svc.Save();
            RefreshAiStatuses();
        }

        private void ApplyGeneralSettings()
        {
            var s = _svc.Settings;

            var minMinutes = Math.Clamp(_monitoringIntervalMinutesMin, 5, 10);
            var maxMinutes = Math.Clamp(_monitoringIntervalMinutesMax, 5, 10);
            if (maxMinutes < minMinutes)
            {
                (minMinutes, maxMinutes) = (maxMinutes, minMinutes);
            }

            s.MonitoringIntervalMinutesMin = minMinutes;
            s.MonitoringIntervalMinutesMax = maxMinutes;
            s.DefaultMessageTtlMs = _defaultMessageTtlMs;
            s.CompanionTalkative = _companionTalkative;
            s.EnableBeatPulse = _enableBeatPulse;

            s.Mood.WarnTemp = _warnTemp;
            s.Mood.AlertTemp = _alertTemp;
            s.Mood.WarnCpu = _warnCpu;
        }

        private void SaveAiSettings()
        {
            ApplyAiSettings();
            _svc.Save();
            RefreshAiStatuses();
        }

        private void ApplyAiSettings()
        {
            var s = _svc.Settings;
            s.ProviderPreference = _selectedProviderPreference;
            s.AiProvider = null;
            s.OpenAiEnabled = _openAiEnabled;

            var keyInput = _openAiApiKeyInput?.Trim();
            if (!string.IsNullOrWhiteSpace(keyInput))
            {
                _secretStore.SaveOpenAiApiKey(keyInput);
                OpenAiApiKeyInput = string.Empty;
            }

            s.HasOpenAiKey = !string.IsNullOrWhiteSpace(_secretStore.LoadOpenAiApiKey());
        }

        private async Task InstallPackAsync()
        {
            if (IsDownloading)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(_packManifest.DownloadUrl))
            {
                DownloadStatusText = "URL Pack Full manquante.";
                NotifyChat("Installation Pack Full impossible: URL manquante.");
                return;
            }

            _downloadCts = new CancellationTokenSource();
            var ct = _downloadCts.Token;

            IsDownloading = true;
            IsDownloadIndeterminate = false;
            DownloadProgressPercent = 0;
            DownloadSpeedText = "—";
            DownloadStatusText = "Téléchargement…";

            try
            {
                var progress = new Progress<ModelPackDownloadProgress>(update =>
                {
                    DownloadStatusText = update.StatusText;
                    DownloadSpeedText = update.SpeedText;
                    DownloadProgressPercent = update.Percent ?? 0;
                    IsDownloadIndeterminate = update.IsIndeterminate;
                });

                var result = await _packDownloader.DownloadAsync(_packManifest, progress, ct).ConfigureAwait(false);

                IsPackInstalled = _modelLocator.IsInstalled;
                DownloadStatusText = result.StatusText;
                if (!result.Success && !string.Equals(result.StatusText, "Téléchargement annulé.", StringComparison.OrdinalIgnoreCase))
                {
                    NotifyChat(result.ErrorMessage ?? "Téléchargement Pack Full échoué.");
                    return;
                }

                if (result.Success)
                {
                    IsDownloadIndeterminate = true;
                    var runtimeProgress = new Progress<string>(status =>
                    {
                        DownloadStatusText = status;
                    });

                    var runtimeResult = await _runtimeInstaller.InstallAndVerifyAsync(runtimeProgress, ct).ConfigureAwait(false);
                    DownloadStatusText = runtimeResult.StatusText;
                    if (!runtimeResult.Success)
                    {
                        var errorMessage = runtimeResult.ErrorMessage ?? "Installation runtime échouée.";
                        NotifyChat(errorMessage);
                        if (!string.IsNullOrWhiteSpace(runtimeResult.Diagnostics))
                        {
                            NotifyChat(runtimeResult.Diagnostics);
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                DownloadStatusText = "Téléchargement annulé.";
            }
            catch (Exception ex)
            {
                DownloadStatusText = "Échec du téléchargement.";
                NotifyChat($"Téléchargement Pack Full échoué: {ex.Message}");
            }
            finally
            {
                IsDownloading = false;
                IsDownloadIndeterminate = false;
                DownloadSpeedText = "—";
                _downloadCts?.Dispose();
                _downloadCts = null;

                _packDownloader.CleanupTemporaryFiles();
            }
        }

        private void CancelDownload()
        {
            if (!IsDownloading)
            {
                return;
            }

            DownloadStatusText = "Annulation…";
            _downloadCts?.Cancel();
        }

        private async Task VerifyPackAsync()
        {
            if (IsDownloading)
            {
                return;
            }

            if (!_modelLocator.IsInstalled)
            {
                IsPackInstalled = false;
                DownloadStatusText = "Pack Full non installé.";
                return;
            }

            DownloadStatusText = "Vérification…";
            try
            {
                var result = await _packDownloader.VerifyAsync(_packManifest).ConfigureAwait(false);
                DownloadStatusText = result.StatusText;
                IsPackInstalled = result.IsValid;
                if (!result.IsValid && !string.IsNullOrWhiteSpace(result.ErrorMessage))
                {
                    NotifyChat(result.ErrorMessage);
                }
            }
            catch (Exception ex)
            {
                DownloadStatusText = "Erreur de vérification.";
                NotifyChat($"Vérification Pack Full échouée: {ex.Message}");
            }
        }

        private async Task TestAiAsync()
        {
            var assistantService = _assistantService;
            if (assistantService is null)
            {
                var factory = new AssistantProviderFactory(_svc, _secretStore);
                var provider = factory.CreateProvider();
                assistantService = provider is null ? null : new AssistantService(provider);
            }

            if (assistantService is null)
            {
                AiTestResponseText = "IA indisponible.";
                return;
            }

            try
            {
                AiTestResponseText = "Test en cours…";
                var reply = await assistantService.AskAsync("Dis juste: OK", AssistantContext.Empty).ConfigureAwait(false);
                AiTestResponseText = string.IsNullOrWhiteSpace(reply.Text) ? "Réponse vide." : reply.Text;
            }
            catch (Exception ex)
            {
                AiTestResponseText = $"Erreur IA: {ex.Message}";
            }
        }

        private async Task TestOpenAiAsync()
        {
            if (!OpenAiEnabled)
            {
                OpenAiTestResponseText = "OpenAI désactivé.";
                return;
            }

            var apiKey = string.IsNullOrWhiteSpace(_openAiApiKeyInput)
                ? _secretStore.LoadOpenAiApiKey()
                : _openAiApiKeyInput.Trim();
            if (_svc.EffectiveAiProvider != AiProvider.OpenAI || string.IsNullOrWhiteSpace(apiKey))
            {
                OpenAiTestResponseText = "OpenAI non configuré.";
                return;
            }

            try
            {
                OpenAiTestResponseText = "Test OpenAI en cours…";
                using var client = new System.Net.Http.HttpClient
                {
                    BaseAddress = new Uri("https://api.openai.com/v1/"),
                    Timeout = TimeSpan.FromSeconds(_svc.Settings.OpenAiTimeoutSeconds)
                };
                using var request = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Get, "models");
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
                using var response = await client.SendAsync(request).ConfigureAwait(false);

                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    OpenAiTestResponseText = "Clé OpenAI invalide ou expirée.";
                    return;
                }

                if (!response.IsSuccessStatusCode)
                {
                    OpenAiTestResponseText = $"Erreur OpenAI: {response.StatusCode}.";
                    return;
                }

                OpenAiTestResponseText = "OpenAI OK.";
            }
            catch (Exception ex)
            {
                OpenAiTestResponseText = $"Erreur OpenAI: {ex.Message}";
            }
        }

        private void NotifyChat(string message)
        {
            _chatService?.PostSystemMessage(
                message,
                Virgil.App.Chat.MessageType.Error,
                Virgil.App.Chat.ChatKind.Error);
        }

        private void UpdateCommandStates()
        {
            _installPackCommand.RaiseCanExecuteChanged();
            _cancelDownloadCommand.RaiseCanExecuteChanged();
            _verifyPackCommand.RaiseCanExecuteChanged();
            _testAiCommand.RaiseCanExecuteChanged();
            _testOpenAiCommand.RaiseCanExecuteChanged();
            _runtimeHelpCommand.RaiseCanExecuteChanged();
        }

        private void RefreshModelDetails()
        {
            var availability = GetModelAvailability();
            ModelStatusText = availability.UserMessage;
            ModelPathText = availability.ModelPath;
            ModelAgeText = availability.IsModelFilePresent
                ? $"Âge des données: {ModelLocator.FormatAge(ModelLocator.GetModelAge(availability.ModelPath))}"
                : string.Empty;
        }

        private void RefreshAiStatuses()
        {
            var availability = GetModelAvailability();
            var diagnostics = LlamaRuntimeDiagnosticsStore.Latest;
            ProviderStatusText = _svc.EffectiveAiProvider switch
            {
                AiProvider.EmbeddedLlama => "Provider actif: EmbeddedLlama",
                AiProvider.OpenAI => "Provider actif: OpenAI",
                AiProvider.Disabled => "Provider actif: Désactivé",
                _ => "Provider actif: Inconnu"
            };

            GgufStatusText = availability.IsModelFilePresent
                ? "Statut GGUF: installé"
                : "Statut GGUF: manquant";

            RuntimeStatusText = availability.IsRuntimePresent
                ? "Statut runtime: présent"
                : "Statut runtime: manquant";

            RuntimeProcessStatusText = diagnostics.ProcessLaunched
                ? "Process lancé: OK"
                : "Process lancé: KO";

            RuntimePortStatusText = diagnostics.PortOpen
                ? "Port ouvert: OK"
                : "Port ouvert: KO";

            RuntimeWarningText = string.IsNullOrWhiteSpace(diagnostics.WarningMessage)
                ? "Warning runtime: —"
                : $"Warning runtime: {diagnostics.WarningMessage}";

            var lastError = diagnostics.LastErrorMessage;
            RuntimeLastErrorText = string.IsNullOrWhiteSpace(lastError)
                ? "Erreur runtime bloquante: —"
                : $"Erreur runtime bloquante: {lastError}";

            RuntimeExitCodeText = diagnostics.ExitCode.HasValue
                ? $"ExitCode runtime: {diagnostics.ExitCode}"
                : "ExitCode runtime: —";

            RuntimeCommandLineText = string.IsNullOrWhiteSpace(diagnostics.CommandLine)
                ? "Commande runtime: —"
                : $"Commande runtime: {diagnostics.CommandLine}";

            var stderr = string.IsNullOrWhiteSpace(diagnostics.Stderr)
                ? "—"
                : Truncate(diagnostics.Stderr, 2000);
            RuntimeStderrText = $"STDERR runtime: {stderr}";

            RuntimeSecurityFlagsText = string.IsNullOrWhiteSpace(diagnostics.SecurityFlagsDetected)
                ? "Flags détectés: —"
                : $"Flags détectés: {diagnostics.SecurityFlagsDetected}";

            RuntimeSecurityStrategyText = string.IsNullOrWhiteSpace(diagnostics.SecurityStrategy)
                ? "Stratégie sécurité: —"
                : $"Stratégie sécurité: {diagnostics.SecurityStrategy}";

            OpenAiStatusText = _svc.Settings.OpenAiEnabled
                ? "Statut OpenAI: activé"
                : "Statut OpenAI: désactivé";
            OpenAiKeyStatusText = _svc.Settings.HasOpenAiKey ? "Clé enregistrée ✅" : "Aucune clé ❌";
        }

        private static string Truncate(string value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length <= maxLength)
            {
                return value;
            }

            return $"{value.Substring(0, maxLength)}…";
        }

        private async Task ShowRuntimeHelpAsync()
        {
            if (IsRuntimeHelpLoading)
            {
                return;
            }

            var runtimePath = LlamaRuntimeManager.DefaultRuntimePath;
            if (!File.Exists(runtimePath))
            {
                RuntimeHelpText = $"Runtime introuvable: {runtimePath}";
                return;
            }

            IsRuntimeHelpLoading = true;
            RuntimeHelpText = "Récupération de l'aide runtime…";

            try
            {
                var result = await ReadRuntimeHelpAsync(runtimePath).ConfigureAwait(false);
                RuntimeHelpText = Truncate(result.Output, RuntimeHelpMaxLength);
                LogRuntimeHelp(runtimePath, result);
            }
            catch (Exception ex)
            {
                RuntimeHelpText = $"Erreur aide runtime: {ex.Message}";
                LogRuntimeHelp(runtimePath, new RuntimeHelpResult(string.Empty, ex.ToString(), null));
            }
            finally
            {
                IsRuntimeHelpLoading = false;
            }
        }

        private static async Task<RuntimeHelpResult> ReadRuntimeHelpAsync(string runtimePath)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = runtimePath,
                Arguments = "--help",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = startInfo };
            process.Start();

            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync().ConfigureAwait(false);

            var stdout = await stdoutTask.ConfigureAwait(false);
            var stderr = await stderrTask.ConfigureAwait(false);

            var combined = string.Join(
                Environment.NewLine,
                new[] { stdout, stderr }.Where(text => !string.IsNullOrWhiteSpace(text)));

            if (string.IsNullOrWhiteSpace(combined))
            {
                combined = "Aucune sortie retournée.";
            }

            return new RuntimeHelpResult(stdout, stderr, process.ExitCode, combined);
        }

        private static void LogRuntimeHelp(string runtimePath, RuntimeHelpResult result)
        {
            try
            {
                var logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Virgil", "logs");
                Directory.CreateDirectory(logDir);
                var logPath = Path.Combine(logDir, RuntimeHelpLogFileName);
                var builder = new StringBuilder();

                builder.AppendLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Runtime help");
                builder.AppendLine($"Path: {runtimePath}");
                builder.AppendLine($"ExitCode: {(result.ExitCode.HasValue ? result.ExitCode.ToString() : "—")}");
                builder.AppendLine("STDOUT:");
                builder.AppendLine(string.IsNullOrWhiteSpace(result.Stdout) ? "—" : result.Stdout);
                builder.AppendLine("STDERR:");
                builder.AppendLine(string.IsNullOrWhiteSpace(result.Stderr) ? "—" : result.Stderr);
                builder.AppendLine(new string('-', 80));

                File.AppendAllText(logPath, builder.ToString());
            }
            catch
            {
                // Ignore logging errors.
            }
        }

        private ModelAvailabilityResult GetModelAvailability()
        {
            return ModelAvailability.Check(_modelLocator, _packManifest);
        }

        private sealed record RuntimeHelpResult(string Stdout, string Stderr, int? ExitCode, string Output)
        {
            public RuntimeHelpResult(string stdout, string stderr, int? exitCode)
                : this(stdout, stderr, exitCode, string.Empty)
            {
            }
        }

        public sealed record ProviderPreferenceOption(ProviderPreference Value, string Label);

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }
}
