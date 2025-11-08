using System;
using System.Windows.Input;

namespace Virgil.App.Infrastructure;

public class SimpleCommand : ICommand
{
    private readonly Action _action;
    private readonly Func<bool>? _can;
    public event EventHandler? CanExecuteChanged;
    public SimpleCommand(Action action, Func<bool>? canExecute = null) { _action = action; _can = canExecute; }
    public bool CanExecute(object? parameter) => _can?.Invoke() ?? true;
    public void Execute(object? parameter) => _action();
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
