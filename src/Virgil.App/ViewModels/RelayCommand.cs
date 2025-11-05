using System;
using System.Windows.Input;

namespace Virgil.App.ViewModels
{
    /// <summary>
    /// Implémentation simple d'un ICommand (style MVVM).
    /// </summary>
    internal sealed class RelayCommand : ICommand
    {
        private readonly Action<object?> _execute;
        private readonly Func<object?, bool>? _canExecute;

        public RelayCommand(Action execute)
        {
            _execute = _ => execute();
        }

        public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public RelayCommand(Action execute, Func<bool> canExecute)
        {
            if (execute == null) throw new ArgumentNullException(nameof(execute));
            if (canExecute == null) throw new ArgumentNullException(nameof(canExecute));

            _execute = _ => execute();
            _canExecute = _ => canExecute();
        }

        public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;

        public void Execute(object? parameter) => _execute(parameter);

        public event EventHandler? CanExecuteChanged;

        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
