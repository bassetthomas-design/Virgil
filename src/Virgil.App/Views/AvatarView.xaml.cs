using System;
using System.Windows.Controls;
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
            var up = new DoubleAnimation { To = 1.04, Duration = TimeSpan.FromMilliseconds(800), AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever };
            var upY = new DoubleAnimation { To = 1.04, Duration = TimeSpan.FromMilliseconds(800), AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever };
            Storyboard.SetTarget(up, Pulse);
            Storyboard.SetTargetProperty(up, new PropertyPath(ScaleTransform.ScaleXProperty));
            Storyboard.SetTarget(upY, Pulse);
            Storyboard.SetTargetProperty(upY, new PropertyPath(ScaleTransform.ScaleYProperty));
            sb.Children.Add(up); sb.Children.Add(upY);
            sb.Begin();
        }
    }
}
