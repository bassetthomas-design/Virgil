using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Virgil.App.Interfaces;
using Virgil.Services;

namespace Virgil.App.Services
{
    public sealed class UiConfirmationPrompt : IConfirmationPrompt
    {
        private readonly IConfirmationService _confirmation;

        public UiConfirmationPrompt(IConfirmationService confirmation)
        {
            _confirmation = confirmation;
        }

        public Task<bool> ConfirmAsync(string message, CancellationToken ct = default)
        {
            var confirmed = _confirmation.Confirm(message, "Confirmation", MessageBoxImage.Warning);
            return Task.FromResult(confirmed);
        }

        public Task<bool> ConfirmRamboAsync(CancellationToken ct = default)
        {
            var dispatcher = Application.Current.Dispatcher;
            var confirmed = dispatcher.Invoke(() =>
            {
                var dialog = new Window
                {
                    Title = "Mode RAMBO",
                    Width = 520,
                    Height = 320,
                    ResizeMode = ResizeMode.NoResize,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    WindowStyle = WindowStyle.ToolWindow,
                    Content = BuildContent(),
                    Owner = Application.Current.MainWindow
                };

                return dialog.ShowDialog() == true;
            });

            return Task.FromResult(confirmed);
        }

        private static UIElement BuildContent()
        {
            var root = new DockPanel { Margin = new Thickness(16) };

            var body = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 16),
                Text = "Mode RAMBO va exécuter un nettoyage profond: nettoyage des temporaires, cache navigateurs, purge mémoire standby, analyse disque/démarrage/RAM, nettoyage ciblé des processus lourds, puis redémarrage de l'explorateur.\n\nCertaines applications en arrière-plan peuvent être fermées. Les actions sont ciblées et sécurisées."
            };
            DockPanel.SetDock(body, Dock.Top);
            root.Children.Add(body);

            var actions = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
            };
            DockPanel.SetDock(actions, Dock.Bottom);

            var launch = new Button { Content = "Lancer RAMBO", MinWidth = 130, Margin = new Thickness(0, 0, 8, 0), IsDefault = true };
            var cancel = new Button { Content = "Annuler", MinWidth = 100, IsCancel = true };

            launch.Click += (_, _) =>
            {
                var window = Window.GetWindow(launch);
                if (window is not null)
                {
                    window.DialogResult = true;
                    window.Close();
                }
            };

            cancel.Click += (_, _) =>
            {
                var window = Window.GetWindow(cancel);
                if (window is not null)
                {
                    window.DialogResult = false;
                    window.Close();
                }
            };

            actions.Children.Add(launch);
            actions.Children.Add(cancel);
            root.Children.Add(actions);
            return root;
        }
    }
}
