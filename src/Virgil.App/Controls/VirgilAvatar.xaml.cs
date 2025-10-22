#nullable enable
using System.Windows.Controls;

namespace Virgil.App.Controls
{
    public partial class VirgilAvatar : UserControl
    {
        public VirgilAvatar()
        {
            InitializeComponent();
            // L’avatar attend un DataContext = VirgilAvatarViewModel (déjà prêt)
        }
    }
}
