using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Virgil.App.ViewModels
{
    /// <summary>
    /// BaseViewModel minimal pour ViewModels Virgil.
    /// Fournit INotifyPropertyChanged + helpers Set/Raise.
    /// </summary>
    internal abstract class BaseViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Déclenche PropertyChanged pour <paramref name="propertyName"/>.
        /// </summary>
        protected void Raise([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        /// <summary>
        /// Affecte la valeur si différente et déclenche PropertyChanged.
        /// </summary>
        protected bool Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value)) return false;
            field = value!;
            Raise(propertyName);
            return true;
        }
    }
}
