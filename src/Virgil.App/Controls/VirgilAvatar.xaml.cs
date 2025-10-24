#nullable enable
using System.Windows.Controls;

namespace Virgil.App.Controls
{
    public partial class VirgilAvatar : UserControl
    {
        private readonly VirgilAvatarViewModel _vm = new();

        public VirgilAvatar()
        {
            InitializeComponent();
            DataContext = _vm;
        }

        public void SetMood(string mood) => _vm.SetMood(mood);
    }
}
