using System.Windows;

namespace Virgil.App.Interfaces
{
    public interface IConfirmationService
    {
        bool Confirm(string message, string title = "Virgil", MessageBoxImage icon = MessageBoxImage.Warning);
    }
}
