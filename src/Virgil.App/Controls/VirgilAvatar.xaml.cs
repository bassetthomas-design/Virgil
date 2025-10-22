#nullable enable
using System.Windows;                  // WPF
using System.Windows.Controls;         // WPF Controls
using System.Windows.Media;            // (si besoin dans le futur)
                                        // ⚠️ NE PAS importer System.Windows.Forms ici

namespace Virgil.App.Controls
{
    /// <summary>
    /// Code-behind du contrôle d’avatar Virgil (WPF).
    /// </summary>
    public partial class VirgilAvatar : System.Windows.Controls.UserControl
    {
        public VirgilAvatar()
        {
            InitializeComponent();

            // Si le DataContext n’est pas défini dans XAML, on met un VM par défaut
            if (DataContext is null)
                DataContext = new VirgilAvatarViewModel();
        }
    }
}
