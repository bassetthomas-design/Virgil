using System.Windows.Controls;

namespace Virgil.App.Views
{
    public partial class AvatarView : UserControl
    {
        public AvatarView()
        {
            InitializeComponent();
            // Any animation is now driven from XAML bindings/converters.
            // Keeping code-behind minimal avoids invalid PropertyPath/ScaleTransform static usage errors.
        }
    }
}
