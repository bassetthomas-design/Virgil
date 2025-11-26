using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Virgil.App.Core;
using Virgil.App.Services;
using Virgil.Services.Narration;

namespace Virgil.App.ViewModels
{
    public class ActionsInvokerViewModel : BaseViewModel
    {
        private readonly IQuickActionsService _quickActions;
        private readonly VirgilNarrationService _narration;

        public ActionsInvokerViewModel(IQuickActionsService quickActions, VirgilNarrationService narration)
        {
            _quickActions = quickActions ?? throw new ArgumentNullException(nameof(quickActions));
            _narration = narration ?? throw new ArgumentNullException(nameof(narration));

            InvokeActionCommand = new RelayCommand(p => InvokeAction(p as string));
        }

        public ICommand InvokeActionCommand { get; }

        public void InvokeAction(string actionId)
        {
            if (string.IsNullOrWhiteSpace(actionId))
                return;

            _ = InvokeActionInternalAsync(actionId);
        }

        private async Task InvokeActionInternalAsync(string actionId)
        {
            var token = CancellationToken.None;
            var success = false;

            try
            {
                await _narration.OnActionStartedAsync(actionId, token);
                await _quickActions.ExecuteAsync(actionId, token);
                success = true;
            }
            catch
            {
                success = false;
            }
            finally
            {
                await _narration.OnActionCompletedAsync(actionId, success, token);
            }
        }
    }
}
