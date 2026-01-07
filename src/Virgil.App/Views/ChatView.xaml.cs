using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using Virgil.App.ViewModels;

namespace Virgil.App.Views
{
    public partial class ChatView : UserControl
    {
        private readonly Random _snapRandom = new();
        private ChatViewModel? _viewModel;

        public ChatView()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (_viewModel is not null)
            {
                _viewModel.SnapRequested -= OnSnapRequestedAsync;
            }

            _viewModel = DataContext as ChatViewModel;

            if (_viewModel is not null)
            {
                _viewModel.SnapRequested += OnSnapRequestedAsync;
            }
        }

        private void OnInputPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            {
                if (DataContext is ChatViewModel vm && vm.SendCommand.CanExecute(null))
                {
                    vm.SendCommand.Execute(null);
                    e.Handled = true;
                }
            }
        }

        private Task OnSnapRequestedAsync(int durationMs)
        {
            return PlaySnapAsync(Math.Max(600, Math.Min(durationMs, 900)));
        }

        private async Task PlaySnapAsync(int durationMs)
        {
            if (ChatScroll is null || SnapOverlay is null || SnapImage is null || SnapDustLayer is null)
            {
                return;
            }

            if (ChatScroll.ActualWidth <= 0 || ChatScroll.ActualHeight <= 0)
            {
                return;
            }

            int width = (int)Math.Ceiling(ChatScroll.ActualWidth);
            int height = (int)Math.Ceiling(ChatScroll.ActualHeight);

            var rtb = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(ChatScroll);

            SnapImage.Source = rtb;
            SnapImage.Width = width;
            SnapImage.Height = height;

            SnapOverlay.Visibility = Visibility.Visible;

            SnapDustLayer.Children.Clear();

            var blur = new BlurEffect { Radius = 0 };
            SnapImage.Effect = blur;
            var scale = new ScaleTransform(1, 1);
            SnapImage.RenderTransformOrigin = new Point(0.5, 0.5);
            SnapImage.RenderTransform = scale;
            SnapImage.Opacity = 1;

            var duration = TimeSpan.FromMilliseconds(durationMs);
            var storyboard = new Storyboard { Duration = duration };

            var opacityAnimation = new DoubleAnimation(1, 0, duration)
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
            };
            Storyboard.SetTarget(opacityAnimation, SnapImage);
            Storyboard.SetTargetProperty(opacityAnimation, new PropertyPath(UIElement.OpacityProperty));
            storyboard.Children.Add(opacityAnimation);

            var blurAnimation = new DoubleAnimation(0, 10, duration)
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
            };
            Storyboard.SetTarget(blurAnimation, blur);
            Storyboard.SetTargetProperty(blurAnimation, new PropertyPath(BlurEffect.RadiusProperty));
            storyboard.Children.Add(blurAnimation);

            var scaleXAnimation = new DoubleAnimation(1, 0.98, duration)
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
            };
            Storyboard.SetTarget(scaleXAnimation, scale);
            Storyboard.SetTargetProperty(scaleXAnimation, new PropertyPath(ScaleTransform.ScaleXProperty));
            storyboard.Children.Add(scaleXAnimation);

            var scaleYAnimation = new DoubleAnimation(1, 0.98, duration)
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
            };
            Storyboard.SetTarget(scaleYAnimation, scale);
            Storyboard.SetTargetProperty(scaleYAnimation, new PropertyPath(ScaleTransform.ScaleYProperty));
            storyboard.Children.Add(scaleYAnimation);

            AddDustParticles(width, height, duration, storyboard);

            var tcs = new TaskCompletionSource<bool>();
            void OnCompleted(object? sender, EventArgs args)
            {
                storyboard.Completed -= OnCompleted;
                tcs.TrySetResult(true);
            }

            storyboard.Completed += OnCompleted;
            storyboard.Begin();

            await tcs.Task;

            SnapOverlay.Visibility = Visibility.Collapsed;
            SnapImage.Source = null;
            SnapImage.Effect = null;
            SnapDustLayer.Children.Clear();
        }

        private void AddDustParticles(int width, int height, TimeSpan duration, Storyboard storyboard)
        {
            int particleCount = _snapRandom.Next(80, 161);
            for (int i = 0; i < particleCount; i++)
            {
                double size = _snapRandom.Next(2, 5);
                double left = _snapRandom.NextDouble() * Math.Max(1, width - size);
                double top = _snapRandom.NextDouble() * Math.Max(1, height - size);

                var ellipse = new Ellipse
                {
                    Width = size,
                    Height = size,
                    Fill = new SolidColorBrush(Colors.White),
                    Opacity = 0.6
                };

                Canvas.SetLeft(ellipse, left);
                Canvas.SetTop(ellipse, top);

                var translate = new TranslateTransform();
                ellipse.RenderTransform = translate;

                SnapDustLayer.Children.Add(ellipse);

                var dustOpacity = new DoubleAnimation(0.6, 0, duration);
                Storyboard.SetTarget(dustOpacity, ellipse);
                Storyboard.SetTargetProperty(dustOpacity, new PropertyPath(UIElement.OpacityProperty));
                storyboard.Children.Add(dustOpacity);

                var moveX = new DoubleAnimation(0, _snapRandom.NextDouble() * 12 - 6, duration);
                Storyboard.SetTarget(moveX, translate);
                Storyboard.SetTargetProperty(moveX, new PropertyPath(TranslateTransform.XProperty));
                storyboard.Children.Add(moveX);

                var moveY = new DoubleAnimation(0, _snapRandom.NextDouble() * 16 - 8, duration);
                Storyboard.SetTarget(moveY, translate);
                Storyboard.SetTargetProperty(moveY, new PropertyPath(TranslateTransform.YProperty));
                storyboard.Children.Add(moveY);
            }
        }
    }
}
