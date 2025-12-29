using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Virgil.App.Commands
{
    public class AsyncRelayCommand : ICommand
    {
        private readonly Func<object?, Task> _executeAsync;
        private readonly Func<object?, bool>? _canExecute;
        private readonly SynchronizationContext? _synchronizationContext;
        private bool _isRunning;

        public AsyncRelayCommand(Func<object?, Task> executeAsync, Func<object?, bool>? canExecute = null)
        {
            _executeAsync = executeAsync ?? throw new ArgumentNullException(nameof(executeAsync));
            _canExecute = canExecute;
            _synchronizationContext = SynchronizationContext.Current;
        }

        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter) => !_isRunning && (_canExecute?.Invoke(parameter) ?? true);

        public async void Execute(object? parameter)
        {
            if (!CanExecute(parameter))
            {
                return;
            }

            try
            {
                _isRunning = true;
                RaiseCanExecuteChanged();
                await _executeAsync(parameter).ConfigureAwait(false);
            }
            finally
            {
                _isRunning = false;
                RaiseCanExecuteChanged();
            }
        }

        public void RaiseCanExecuteChanged()
        {
            if (_synchronizationContext == null)
            {
                CanExecuteChanged?.Invoke(this, EventArgs.Empty);
                return;
            }

            if (SynchronizationContext.Current == _synchronizationContext)
            {
                CanExecuteChanged?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                _synchronizationContext.Post(_ => CanExecuteChanged?.Invoke(this, EventArgs.Empty), null);
            }
        }
    }
}
