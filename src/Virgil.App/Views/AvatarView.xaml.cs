using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Virgil.App.Views
{
    public partial class AvatarView : UserControl
    {
        public AvatarView()
        {
            InitializeComponent();
            Loaded += (_, __) => StartPulse();
        }

        private void StartPulse()
        {
            var sb = new Storyboard();
            var ease = new SineEase { EasingMode = EasingMode.EaseInOut };
            var up = new DoubleAnimation { To = 1.06, Duration = TimeSpan.FromMilliseconds(950), AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = ease };
            var upY = new DoubleAnimation { To = 1.06, Duration = TimeSpan.FromMilliseconds(950), AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = ease };
            Storyboard.SetTarget(up, Pulse);
            Storyboard.SetTargetProperty(up, new PropertyPath(ScaleTransform.ScaleXProperty));
            Storyboard.SetTarget(upY, Pulse);
            Storyboard.SetTargetProperty(upY, new PropertyPath(ScaleTransform.ScaleYProperty));
            sb.Children.Add(up); sb.Children.Add(upY);
            sb.Begin();
        }
    }
}
