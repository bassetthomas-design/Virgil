using System.Windows;
using Virgil.App.Interfaces;

namespace Virgil.App.Services
{
    public sealed class ConfirmationService : IConfirmationService
    {
        public bool Confirm(string message, string title = "Virgil", MessageBoxImage icon = MessageBoxImage.Warning)
        {
            var result = MessageBox.Show(message, title, MessageBoxButton.OKCancel, icon);
            return result == MessageBoxResult.OK;
        }
    }
}
