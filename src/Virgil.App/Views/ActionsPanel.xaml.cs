using System.Windows;
using System.Windows.Controls;
uusing Virgil.App.Interfaces;

namespace Virgil.App.Views
{
    public partial class ActionsPanel : UserControl
    {
        public ActionsPanel()
        {
            InitializeComponent();
        }

        private void ActionButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string actionId)
            {
                if (this.DataContext is IActionInvoker invoker)
                {
                    invoker.InvokeAction(actionId);
                }
            }
        }
    }
}
