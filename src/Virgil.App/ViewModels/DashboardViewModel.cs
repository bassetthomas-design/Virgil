using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Virgil.App.ViewModels
{
    // ---- Base INotifyPropertyChanged (simple, neutre) ----
    internal abstract class BaseViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        protected void Raise([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        protected bool Set<T>(ref T storage, T value, [CallerMemberName] string? name = null)
        {
            if (Equals(storage, value)) return false;
            storage = value;
            Raise(name);
            return true;
        }
    }

    // ---- RelayCommand basique (sync/async) ----
    internal sealed class RelayCommand : ICommand
    {
        private readonly Func<bool>? _canExecute;
        private readonly Action? _execute;
        private readonly Func<Task>? _executeAsync;

        public event EventHandler? CanExecuteChanged;

        public RelayCommand(Action execute, Func<bool>? canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public RelayCommand(Func<Task> executeAsync, Func<bool>? canExecute = null)
        {
            _executeAsync = executeAsync;
            _canExecute = canExecute;
        }

        public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;

        public async void Execute(object? parameter)
        {
            if (_execute != null) { _execute(); return; }
            if (_executeAsync != null) await _executeAsync();
        }

        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }

    // ---- ViewModel principal du tableau de bord ----
    // NOTE: pas "partial" volontairement : on garde une seule source d’autorité pour éviter les collisions.
    internal sealed class DashboardViewModel : BaseViewModel
    {
        // ----- ÉTAT -----
        private bool _isSurveillanceEnabled;
        public bool IsSurveillanceEnabled
        {
            get => _isSurveillanceEnabled;
            set => Set(ref _isSurveillanceEnabled, value);
        }

        private string _status = "Prêt.";
        public string Status
        {
            get => _status;
            set => Set(ref _status, value);
        }

        public ObservableCollection<string> ChatMessages { get; } = new();

        // ----- COMMANDES (liées au XAML / MainWindow.xaml.cs) -----
        public ICommand ToggleSurveillanceCommand { get; }
        public ICommand RunMaintenanceCommand { get; }
        public ICommand CleanTempFilesCommand { get; }
        public ICommand CleanBrowsersCommand { get; }
        public ICommand UpdateAllCommand { get; }
        public ICommand RunDefenderScanCommand { get; }
        public ICommand OpenConfigurationCommand { get; }

        public DashboardViewModel()
        {
            ToggleSurveillanceCommand = new RelayCommand(ToggleSurveillance);
            RunMaintenanceCommand     = new RelayCommand(RunMaintenanceAsync);
            CleanTempFilesCommand     = new RelayCommand(CleanTempFilesAsync);
            CleanBrowsersCommand      = new RelayCommand(CleanBrowsersAsync);
            UpdateAllCommand          = new RelayCommand(UpdateAllAsync);
            RunDefenderScanCommand    = new RelayCommand(RunDefenderScanAsync);
            OpenConfigurationCommand  = new RelayCommand(OpenConfiguration);
        }

        // ----- MÉTHODES APPELÉES PAR MainWindow.xaml.cs -----
        // IMPORTANT : ces signatures correspondent à ce que ton code-behind appelle actuellement.
        public void ToggleSurveillance()
        {
            IsSurveillanceEnabled = !IsSurveillanceEnabled;
            var msg = IsSurveillanceEnabled
                ? "🔍 Surveillance ACTIVÉE. Je garde un œil sur tout."
                : "😴 Surveillance arrêtée. J’me repose une minute…";
            AppendChat(msg);
            Status = msg;
        }

        public async Task RunMaintenanceAsync()
        {
            AppendChat("🛠️ Maintenance complète : démarrage…");
            Status = "Maintenance en cours…";

            // Ici tu enchaîneras : nettoyage intelligent → navigateurs → MAJ globales.
            await Task.Delay(300); // placeholder

            AppendChat("✅ Maintenance terminée.");
            Status = "Maintenance terminée.";
        }

        public async Task CleanTempFilesAsync()
        {
            AppendChat("🧹 Nettoyage des temporaires…");
            Status = "Nettoyage temporaires…";
            await Task.Delay(200); // placeholder
            AppendChat("✅ Temporaires nettoyés.");
            Status = "Temporaires nettoyés.";
        }

        public async Task CleanBrowsersAsync()
        {
            AppendChat("🧼 Nettoyage des navigateurs (caches)…");
            Status = "Nettoyage navigateurs…";
            await Task.Delay(200); // placeholder
            AppendChat("✅ Navigateurs nettoyés.");
            Status = "Navigateurs nettoyés.";
        }

        public async Task UpdateAllAsync()
        {
            AppendChat("⬆️ Mises à jour globales (apps/jeux/Windows/drivers/Defender)…");
            Status = "Mises à jour…";
            await Task.Delay(300); // placeholder
            AppendChat("✅ Tout est à jour.");
            Status = "Tout est à jour.";
        }

        public async Task RunDefenderScanAsync()
        {
            AppendChat("🛡️ Microsoft Defender : MAJ signatures + scan rapide…");
            Status = "Defender en cours…";
            await Task.Delay(200); // placeholder
            AppendChat("✅ Defender OK.");
            Status = "Defender OK.";
        }

        public void OpenConfiguration()
        {
            AppendChat("⚙️ Ouverture de la configuration…");
            Status = "Configuration ouverte.";
            // ouvre/affiche ta fenêtre/onglet de config ici (VM, message, event, etc.)
        }

        // ----- Utils -----
        private void AppendChat(string text)
        {
            ChatMessages.Add($"[{DateTime.Now:HH:mm:ss}] {text}");
            Raise(nameof(ChatMessages));
        }
    }
}
