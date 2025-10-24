#nullable enable
using System.Windows.Controls;     // WPF UserControl
using System.Windows.Media;

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

        // API utilisé par MainWindow : change l’humeur
        public void SetMood(string mood) => _vm.SetMood(mood);

        // Optionnel : couleurs custom
        public void SetBodyColor(Color c) => _vm.BodyBrush = new SolidColorBrush(c);
        public void SetEyeColor(Color c)  => _vm.EyeBrush  = new SolidColorBrush(c);
    }
}
