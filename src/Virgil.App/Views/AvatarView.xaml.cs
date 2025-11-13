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
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // Simple intro scale animation
            var sb = new Storyboard();

            var scaleX = new DoubleAnimation(0.9, 1.0, new Duration(TimeSpan.FromMilliseconds(350))) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } };
            var scaleY = new DoubleAnimation(0.9, 1.0, new Duration(TimeSpan.FromMilliseconds(350))) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } };

            Storyboard.SetTarget(scaleX, AvatarRoot);
            Storyboard.SetTarget(scaleY, AvatarRoot);
            Storyboard.SetTargetProperty(scaleX, new PropertyPath(ScaleTransform.ScaleXProperty));
            Storyboard.SetTargetProperty(scaleY, new PropertyPath(ScaleTransform.ScaleYProperty));

            sb.Children.Add(scaleX);
            sb.Children.Add(scaleY);
            sb.Begin();
        }
    }
}
