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

        public Task<RamboErrorDialogResult> AskRamboErrorDecisionAsync(string friendlyMessage, CancellationToken ct = default)
        {
            var dispatcher = Application.Current.Dispatcher;
            var result = dispatcher.Invoke(() =>
            {
                var checkbox = new CheckBox
                {
                    Content = "Continuer automatiquement pour les prochaines erreurs similaires",
                    Margin = new Thickness(0, 4, 0, 16)
                };

                var message = new TextBlock
                {
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 0, 0, 12),
                    Text = $"Une étape a rencontré un problème :\n{friendlyMessage}\n\nVoulez-vous continuer ou arrêter l'opération ?"
                };

                var root = new DockPanel { Margin = new Thickness(16) };
                DockPanel.SetDock(message, Dock.Top);
                root.Children.Add(message);

                DockPanel.SetDock(checkbox, Dock.Top);
                root.Children.Add(checkbox);

                var actions = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                };
                DockPanel.SetDock(actions, Dock.Bottom);

                var stop = new Button { Content = "Arrêter", MinWidth = 100, Margin = new Thickness(0, 0, 8, 0), IsCancel = true };
                var cont = new Button { Content = "Continuer", MinWidth = 110, IsDefault = true };

                var dialogResult = new RamboErrorDialogResult
                {
                    Decision = RamboErrorDecision.Stop,
                    AutoContinueSimilarErrors = false
                };

                stop.Click += (_, _) =>
                {
                    dialogResult.Decision = RamboErrorDecision.Stop;
                    dialogResult.AutoContinueSimilarErrors = checkbox.IsChecked == true;
                    var window = Window.GetWindow(stop);
                    if (window is not null)
                    {
                        window.DialogResult = false;
                        window.Close();
                    }
                };

                cont.Click += (_, _) =>
                {
                    dialogResult.Decision = RamboErrorDecision.Continue;
                    dialogResult.AutoContinueSimilarErrors = checkbox.IsChecked == true;
                    var window = Window.GetWindow(cont);
                    if (window is not null)
                    {
                        window.DialogResult = true;
                        window.Close();
                    }
                };

                actions.Children.Add(stop);
                actions.Children.Add(cont);
                root.Children.Add(actions);

                var dialog = new Window
                {
                    Title = "Mode RAMBO",
                    Width = 560,
                    Height = 260,
                    ResizeMode = ResizeMode.NoResize,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    WindowStyle = WindowStyle.ToolWindow,
                    Content = root,
                    Owner = Application.Current.MainWindow
                };

                _ = dialog.ShowDialog();
                return dialogResult;
            });

            return Task.FromResult(result);
        }

        private static UIElement BuildContent()
        {
            var root = new DockPanel { Margin = new Thickness(16) };

            var body = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 16),
                Text = "• Nettoyage profond des fichiers temporaires et caches\n• Nettoyage Windows Update / logs / miniatures / shaders\n• Nettoyage caches navigateurs\n• Optimisation mémoire et refresh système\n• Analyse des gros dossiers, démarrage et RAM\n• Recherche de dossiers inactifs et fichiers dupliqués\n\nCertaines étapes avancées peuvent demander confirmation."
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
