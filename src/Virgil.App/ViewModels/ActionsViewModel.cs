using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Virgil.App.Commands;
using Virgil.App.Models;

namespace Virgil.App.ViewModels
{
    /// <summary>
    /// ViewModel pour le panneau d'actions rapides.
    /// Reçoit un identifiant d'action (Tag / CommandParameter) et délègue au backend.
    /// </summary>
    public class ActionsViewModel : BaseViewModel
    {
        private readonly Func<string, CancellationToken, Task<ActionResult>> _runner;
        private string _performanceModeStatus = "Mode performance: Inactif";

        /// <summary>
        /// Commande appelée par les boutons d'ActionsPanel.xaml, avec l'identifiant d'action en paramètre.
        /// </summary>
        public ICommand InvokeActionCommand { get; }

        public string PerformanceModeStatus
        {
            get => _performanceModeStatus;
            set => Set(ref _performanceModeStatus, value);
        }

        public ActionsViewModel(Func<string, CancellationToken, Task<ActionResult>> runner)
        {
            _runner = runner ?? throw new ArgumentNullException(nameof(runner));
            InvokeActionCommand = new AsyncRelayCommand(async param =>
            {
                var actionId = param as string;
                if (!string.IsNullOrWhiteSpace(actionId))
                {
                    await _runner(actionId!, CancellationToken.None).ConfigureAwait(false);
                }
            });
        }
    }
}
