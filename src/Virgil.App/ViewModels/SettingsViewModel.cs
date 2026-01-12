using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Virgil.App.Chat;
using Virgil.App.Commands;
using Virgil.App.Models;
using Virgil.App.Services;
using Virgil.Core.Config;
using Virgil.Services.Assistant;
using Virgil.Services.ModelPacks;

namespace Virgil.App.ViewModels
{
    public class SettingsViewModel : INotifyPropertyChanged
    {
        private const string FullPackDownloadUrl =
            "https://huggingface.co/TheBloke/Meta-Llama-3.1-8B-Instruct-GGUF/resolve/main/llama-3.1-8b-instruct-q4_k_m.gguf";
        private readonly SettingsService _svc;
        private readonly ChatService? _chatService;
        private readonly IAssistantService? _assistantService;
        private readonly ModelLocator _modelLocator = new();
        private CancellationTokenSource? _downloadCts;
        private bool _isDownloadIndeterminate;
        private readonly ModelPackDownloader _packDownloader;
        private readonly ModelPackManifest _packManifest;
        private string _modelStatusText = string.Empty;
        private string _modelPathText = string.Empty;
        private string _modelAgeText = string.Empty;

        public SettingsViewModel(SettingsService svc, ChatService? chatService = null, IAssistantService? assistantService = null)
        {
            _svc = svc;
            _chatService = chatService;
            _assistantService = assistantService;

            // Charger une "copie" (en champs) pour permettre Annuler sans effet de bord
            var s = _svc.Settings;

            _monitoringIntervalMinutesMin = s.MonitoringIntervalMinutesMin;
            _monitoringIntervalMinutesMax = s.MonitoringIntervalMinutesMax;
            _defaultMessageTtlMs = s.DefaultMessageTtlMs;
            _companionTalkative = s.CompanionTalkative;
            _enableBeatPulse = s.EnableBeatPulse;

            _warnTemp = s.Mood.WarnTemp;
            _alertTemp = s.Mood.AlertTemp;
            _warnCpu = s.Mood.WarnCpu;

            _packDownloader = new ModelPackDownloader(_modelLocator);
            _packManifest = ModelPackManifest.FullPack;

            _isPackInstalled = _modelLocator.IsInstalled;
            _downloadStatusText = _isPackInstalled ? "Pack Full installé." : "Pack Full non installé.";
            _downloadSpeedText = "—";
            RefreshModelDetails();

            _installPackCommand = new AsyncRelayCommand(_ => InstallPackAsync(), _ => !IsDownloading);
            _cancelDownloadCommand = new RelayCommand(_ => CancelDownload(), _ => IsDownloading);
            _verifyPackCommand = new AsyncRelayCommand(_ => VerifyPackAsync(), _ => !IsDownloading);
            _testAiCommand = new AsyncRelayCommand(_ => TestAiAsync(), _ => !IsDownloading);
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

        public bool ShowPackInstallPrompt => !_isPackInstalled && _svc.Settings.AiProvider == AiProvider.EmbeddedLlama;

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

        /// <summary>
        /// Applique les valeurs au SettingsService et persiste.
        /// </summary>
        public void Save()
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

            _svc.Save();
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
                var result = await _packDownloader.VerifyAsync().ConfigureAwait(false);
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
            if (_assistantService is null)
            {
                AiTestResponseText = "IA indisponible.";
                return;
            }

            try
            {
                AiTestResponseText = "Test en cours…";
                var reply = await _assistantService.AskAsync("Dis juste: OK", AssistantContext.Empty).ConfigureAwait(false);
                AiTestResponseText = string.IsNullOrWhiteSpace(reply.Text) ? "Réponse vide." : reply.Text;
            }
            catch (Exception ex)
            {
                AiTestResponseText = $"Erreur IA: {ex.Message}";
            }
        }

        private void NotifyChat(string message)
        {
            _chatService?.PostSystemMessage(message, MessageType.Error, ChatKind.Error);
        }

        private void UpdateCommandStates()
        {
            _installPackCommand.RaiseCanExecuteChanged();
            _cancelDownloadCommand.RaiseCanExecuteChanged();
            _verifyPackCommand.RaiseCanExecuteChanged();
            _testAiCommand.RaiseCanExecuteChanged();
        }

        private void RefreshModelDetails()
        {
            if (_modelLocator.TryResolve(out var path, out _))
            {
                ModelStatusText = "Modèle: installé";
                ModelPathText = path;
                ModelAgeText = $"Âge des données: {ModelLocator.FormatAge(ModelLocator.GetModelAge(path))}";
            }
            else
            {
                ModelStatusText = "Modèle manquant";
                ModelPathText = _modelLocator.GetCandidatePaths().First();
                ModelAgeText = string.Empty;
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }
}
