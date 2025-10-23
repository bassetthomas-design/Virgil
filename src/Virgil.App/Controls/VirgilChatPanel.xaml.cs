#nullable enable
using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace Virgil.App.Controls
{
    public partial class VirgilChatPanel : UserControl
    {
        public ObservableCollection<Virgil.App.ChatMessage> Messages { get; } = new();

        public VirgilChatPanel()
        {
            InitializeComponent();
            DataContext = this;
            CompositionTarget.Rendering += (_, __) => Scroller.ScrollToEnd();
        }

        /// <summary>Ajoute un message + planifie sa disparition (effet thanos) après <paramref name="ttlMs"/>.</summary>
        public void Post(string text, string? mood = null, int ttlMs = 60000)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            var msg = new Virgil.App.ChatMessage { Text = text, Mood = mood ?? "neutral", Timestamp = DateTime.Now };
            Messages.Add(msg);

            // Attendre que l’élément visuel existe puis planifier l’auto-destruction
            Dispatcher.BeginInvoke(new Action(() =>
            {
                var container = FindContainerForLast();
                if (container != null)
                    ScheduleVanish(container, msg, ttlMs);
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        private FrameworkElement? FindContainerForLast()
        {
            if (Messages.Count == 0) return null;
            var cont = MessageList.ItemContainerGenerator.ContainerFromIndex(Messages.Count - 1) as FrameworkElement;
            if (cont == null) return null;
            var bubble = cont.FindName("Bubble") as FrameworkElement;
            return bubble ?? cont;
        }

        private async void ScheduleVanish(FrameworkElement bubble, Virgil.App.ChatMessage msg, int ttlMs)
        {
            try
            {
                await System.Threading.Tasks.Task.Delay(Math.Max(1000, ttlMs));
                if (!Messages.Contains(msg)) return;

                PlayThanos(bubble);
                FadeShrink(bubble, 260, () =>
                {
                    Messages.Remove(msg);
                });
            }
            catch { }
        }

        private void FadeShrink(FrameworkElement target, int ms, Action onDone)
        {
            var sb = new Storyboard();

            var opa = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(ms))
            { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn } };
            Storyboard.SetTarget(opa, target);
            Storyboard.SetTargetProperty(opa, new PropertyPath("Opacity"));
            sb.Children.Add(opa);

            var scale = new ScaleTransform(1, 1);
            target.RenderTransformOrigin = new Point(0.5, 0.5);
            target.RenderTransform = scale;

            var s1 = new DoubleAnimation(1, 0.85, TimeSpan.FromMilliseconds(ms));
            Storyboard.SetTarget(s1, target);
            Storyboard.SetTargetProperty(s1, new PropertyPath("RenderTransform.ScaleX"));
            sb.Children.Add(s1);

            var s2 = new DoubleAnimation(1, 0.85, TimeSpan.FromMilliseconds(ms));
            Storyboard.SetTarget(s2, target);
            Storyboard.SetTargetProperty(s2, new PropertyPath("RenderTransform.ScaleY"));
            sb.Children.Add(s2);

            sb.Completed += (_, __) => onDone();
            sb.Begin();
        }

        /// <summary>Effet "Thanos": particules qui se dissipent depuis la bulle.</summary>
        private void PlayThanos(FrameworkElement source)
        {
            if (source.ActualWidth < 10 || source.ActualHeight < 10) return;

            var origin = source.TranslatePoint(new System.Windows.Point(0, 0), DustLayer);
            var rnd = new Random();

            int count = Math.Clamp((int)(source.ActualWidth * source.ActualHeight / 550), 14, 40);
            for (int i = 0; i < count; i++)
            {
                var dot = new Ellipse
                {
                    Width = rnd.Next(2, 5),
                    Height = rnd.Next(2, 5),
                    Fill = new SolidColorBrush(System.Windows.Media.Color.FromArgb(220, 255, 255, 255)),
                    Opacity = 0.0,
                    IsHitTestVisible = false
                };
                Canvas.SetLeft(dot, origin.X + rnd.NextDouble() * source.ActualWidth);
                Canvas.SetTop(dot, origin.Y + rnd.NextDouble() * source.ActualHeight);
                DustLayer.Children.Add(dot);

                // destination aléatoire
                var dx = (rnd.NextDouble() - 0.2) * 120;
                var dy = -(20 + rnd.NextDouble() * 80); // léger vers le haut
                var dur = TimeSpan.FromMilliseconds(400 + rnd.Next(0, 200));

                var ax = new DoubleAnimation(Canvas.GetLeft(dot), Canvas.GetLeft(dot) + dx, dur)
                { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } };
                var ay = new DoubleAnimation(Canvas.GetTop(dot), Canvas.GetTop(dot) + dy, dur)
                { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } };
                var aoIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(80));
                var aoOut = new DoubleAnimation(1, 0, dur) { BeginTime = TimeSpan.FromMilliseconds(80) };

                dot.BeginAnimation(UIElement.OpacityProperty, aoIn);
                dot.BeginAnimation(UIElement.OpacityProperty, aoOut);
                dot.BeginAnimation(Canvas.LeftProperty, ax);
                dot.BeginAnimation(Canvas.TopProperty, ay);

                aoOut.Completed += (_, __) => DustLayer.Children.Remove(dot);
            }
        }
    }
}
