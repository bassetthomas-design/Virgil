using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Virgil.App.Chat;
using Virgil.App.Commands;
using Virgil.App.Models;
using Virgil.App.Services;
using Virgil.Core.Config;
using Virgil.Services.Assistant;

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
        private readonly HttpClient _httpClient = new();
        private CancellationTokenSource? _downloadCts;
        private bool _isDownloadIndeterminate;

        public SettingsViewModel(SettingsService svc, ChatService? chatService = null, IAssistantService? assistantService = null)
        {
            _svc = svc;
            _chatService = chatService;
            _assistantService = assistantService;

            // Charger une "copie" (en champs) pour permettre Annuler sans effet de bord
            var s = _svc.Settings;

            _monitoringIntervalMs = s.MonitoringIntervalMs;
            _defaultMessageTtlMs = s.DefaultMessageTtlMs;
            _companionTalkative = s.CompanionTalkative;
            _enableBeatPulse = s.EnableBeatPulse;

            _warnTemp = s.Mood.WarnTemp;
            _alertTemp = s.Mood.AlertTemp;
            _warnCpu = s.Mood.WarnCpu;

            _isPackInstalled = _modelLocator.IsInstalled;
            _downloadStatusText = _isPackInstalled ? "Pack Full installé." : "Pack Full non installé.";
            _downloadSpeedText = "—";

            _installPackCommand = new AsyncRelayCommand(_ => InstallPackAsync(), _ => !IsDownloading);
            _cancelDownloadCommand = new RelayCommand(_ => CancelDownload(), _ => IsDownloading);
            _verifyPackCommand = new AsyncRelayCommand(_ => VerifyPackAsync(), _ => !IsDownloading);
            _testAiCommand = new AsyncRelayCommand(_ => TestAiAsync(), _ => !IsDownloading);
        }

        private int _monitoringIntervalMs;
        public int MonitoringIntervalMs
        {
            get => _monitoringIntervalMs;
            set { _monitoringIntervalMs = value; OnPropertyChanged(); }
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

            s.MonitoringIntervalMs = _monitoringIntervalMs;
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

            if (string.IsNullOrWhiteSpace(FullPackDownloadUrl))
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

            var modelDirectory = _modelLocator.ModelDirectory;
            Directory.CreateDirectory(modelDirectory);

            var tempPath = Path.Combine(modelDirectory, $"{ModelLocator.ExpectedFileName}.tmp");

            try
            {
                using var response = await _httpClient.GetAsync(
                    FullPackDownloadUrl,
                    HttpCompletionOption.ResponseHeadersRead,
                    ct).ConfigureAwait(false);

                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength;
                IsDownloadIndeterminate = totalBytes is null or <= 0;

                await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
                await using var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, useAsync: true);

                var buffer = new byte[8192];
                long totalRead = 0;
                var stopwatch = Stopwatch.StartNew();

                while (true)
                {
                    var read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), ct).ConfigureAwait(false);
                    if (read <= 0)
                    {
                        break;
                    }

                    await fileStream.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
                    totalRead += read;

                    if (totalBytes.HasValue && totalBytes.Value > 0)
                    {
                        DownloadProgressPercent = totalRead / (double)totalBytes.Value * 100;
                    }

                    DownloadSpeedText = FormatSpeed(totalRead, stopwatch.Elapsed);
                }

                fileStream.Close();

                var destinationPath = _modelLocator.ModelPath;
                if (File.Exists(destinationPath))
                {
                    File.Delete(destinationPath);
                }

                File.Move(tempPath, destinationPath);

                var hash = await ComputeSha256Async(destinationPath, ct).ConfigureAwait(false);
                await File.WriteAllTextAsync(_modelLocator.ModelHashPath, hash, ct).ConfigureAwait(false);

                IsPackInstalled = _modelLocator.IsInstalled;
                DownloadProgressPercent = 100;
                DownloadStatusText = "Terminé.";
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

                if (File.Exists(tempPath))
                {
                    try
                    {
                        File.Delete(tempPath);
                    }
                    catch
                    {
                    }
                }
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
                var hashPath = _modelLocator.ModelHashPath;
                if (!File.Exists(hashPath))
                {
                    DownloadStatusText = "Hash attendu manquant.";
                    return;
                }

                var expected = (await File.ReadAllTextAsync(hashPath).ConfigureAwait(false)).Trim();
                var actual = await ComputeSha256Async(_modelLocator.ModelPath, CancellationToken.None).ConfigureAwait(false);
                if (string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
                {
                    DownloadStatusText = "Vérification OK.";
                    IsPackInstalled = true;
                }
                else
                {
                    DownloadStatusText = "Hash incorrect.";
                    IsPackInstalled = false;
                    NotifyChat("Pack Full corrompu: hash incorrect.");
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

        private static async Task<string> ComputeSha256Async(string filePath, CancellationToken ct)
        {
            await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 8192, useAsync: true);
            using var sha256 = SHA256.Create();
            var hash = await sha256.ComputeHashAsync(stream, ct).ConfigureAwait(false);
            return Convert.ToHexString(hash);
        }

        private static string FormatSpeed(long bytes, TimeSpan elapsed)
        {
            if (elapsed.TotalSeconds <= 0.5)
            {
                return "—";
            }

            var bytesPerSecond = bytes / elapsed.TotalSeconds;
            return $"{FormatBytes(bytesPerSecond)}/s";
        }

        private static string FormatBytes(double bytes)
        {
            string[] suffixes = { "o", "Ko", "Mo", "Go" };
            var order = 0;
            while (bytes >= 1024 && order < suffixes.Length - 1)
            {
                order++;
                bytes /= 1024;
            }

            return $"{bytes:0.0} {suffixes[order]}";
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

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }
}
