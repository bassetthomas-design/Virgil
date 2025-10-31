using System.Windows.Controls;
using Virgil.Core;

namespace Virgil.App.Controls
{
    public partial class AvatarControl : UserControl
    {
        public AvatarControl()
        {
            InitializeComponent();
        }

        public void SetMood(Mood mood)
        {
            // TODO: switch mood -> sprite (happy, focused, warn, alert, sleepy, proud, tired, etc.)
            // Exemple:
            // switch (mood)
            // {
            //     case Mood.Neutral:   Sprite.Source = ...; break;
            //     case Mood.Focused:   Sprite.Source = ...; break;
            //     // ...
            // }
        }
    }
}
