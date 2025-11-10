using System;
using System.Threading.Tasks;
using System.Windows.Input;
using Virgil.App.Chat;
using Virgil.App.Commands;
using Virgil.App.Services;
using Virgil.App.Utils;

namespace Virgil.App.ViewModels
{
    public class ActionsViewModel
    {
        private readonly ActionsService _actions;
        private readonly ChatService _chat;
        private readonly MonitoringService _mon;
        private readonly SettingsService _settings;
        private readonly LoggerService _logger = new();

        public ActionsViewModel(ActionsService actions, ChatService chat, MonitoringService mon, SettingsService settings)
        {
            _actions = actions; _chat = chat; _mon = mon; _settings = settings;
            SurveillanceCommand = new RelayCommand(async _ => await SurveillanceAsync());
            MaintenanceCommand = new RelayCommand(async _ => await RunAsync(_actions.MaintenanceCompleteAsync, "Maintenance complète", requiresAdmin: true));
            SmartCleanupCommand = new RelayCommand(async _ => await RunAsync(_actions.SmartCleanupAsync, "Nettoyage intelligent"));
            CleanBrowsersCommand = new RelayCommand(async _ => await RunAsync(_actions.CleanBrowsersAsync, "Nettoyer navigateurs"));
            UpdateAllCommand = new RelayCommand(async _ => await RunAsync(_actions.UpdateAllAsync, "Tout mettre à jour"));
            DefenderCommand = new RelayCommand(async _ => await RunAsync(_actions.DefenderUpdateAndScanAsync, "Defender (MAJ + Scan)", requiresAdmin: true));
            OpenConfigCommand = new RelayCommand(async _ => await RunAsync(_actions.OpenConfigAsync, "Ouvrir configuration"));
        }

        public ICommand SurveillanceCommand { get; }
        public ICommand MaintenanceCommand { get; }
        public ICommand SmartCleanupCommand { get; }
        public ICommand CleanBrowsersCommand { get; }
        public ICommand UpdateAllCommand { get; }
        public ICommand DefenderCommand { get; }
        public ICommand OpenConfigCommand { get; }

        private async Task SurveillanceAsync()
        {
            await _actions.ToggleSurveillanceAsync(
                onStart: async () => { _chat.Post("Surveillance ON", MessageType.Success, pinned: true); _mon.Start(); await Task.CompletedTask; },
                onStop: async () => { _chat.Post("Surveillance OFF", MessageType.Warning, pinned: true); _mon.Stop(); await Task.CompletedTask; }
            );
        }

        private async Task RunAsync(Func<Task<ProcessResult?>> action, string label, bool requiresAdmin = false)
        {
            if (requiresAdmin && !Admin.IsElevated()) { _chat.Post(label + " — nécessite les droits administrateur", MessageType.Warning); return; }

            _chat.Post(label + " — démarré", MessageType.Info);
            var res = await action();
            if (res == null) { _chat.Post(label + " — script introuvable", MessageType.Warning); return; }

            var log = _logger.Save(label, res);
            var status = res.ExitCode == 0 ? MessageType.Success : MessageType.Error;
            _chat.Post($"{label} — terminé (code {res.ExitCode}). Log: {log}", status, ttlMs: 8000);
        }
    }
}
