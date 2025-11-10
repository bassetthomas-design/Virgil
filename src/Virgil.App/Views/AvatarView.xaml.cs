using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace Virgil.App.Views
{
    public partial class AvatarView : UserControl
    {
        public AvatarView() { InitializeComponent(); }

        public void Pulse(double intensity)
        {
            try
            {
                var sb = new Storyboard();
                var animUp = new DoubleAnimation
                {
                    From = 1.0, To = 1.0 + 0.05 + 0.15 * intensity, Duration = TimeSpan.FromMilliseconds(140), AutoReverse = true, EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                };
                Storyboard.SetTarget(animUp, ScaleTransform);
                Storyboard.SetTargetProperty(animUp, new PropertyPath("ScaleX"));
                var animUpY = animUp.Clone();
                Storyboard.SetTarget(animUpY, ScaleTransform);
                Storyboard.SetTargetProperty(animUpY, new PropertyPath("ScaleY"));
                sb.Children.Add(animUp); sb.Children.Add(animUpY);
                sb.Begin();
            }
            catch { }
        }
    }
}
