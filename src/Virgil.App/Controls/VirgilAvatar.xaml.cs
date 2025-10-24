#nullable enable
using System.Windows.Controls;

namespace Virgil.App.Controls
{
    public partial class VirgilAvatar : UserControl
    {
        public VirgilAvatar()
        {
            InitializeComponent();

            // Si le DataContext n’a pas été injecté par le parent,
            // on garde celui déclaré dans le XAML (instance par défaut).
        }

        /// <summary>
        /// Accès pratique au VM depuis MainWindow si besoin.
        /// </summary>
        public VirgilAvatarViewModel? ViewModel => DataContext as VirgilAvatarViewModel;
    }
}
