#nullable enable
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Virgil.App.Controls
{
    public class ChatMessage
    {
        public string Text { get; set; } = "";
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
        public int TtlMs { get; set; } = 5000;
        public FrameworkElement? Container { get; set; }
    }

    public partial class VirgilChatPanel : System.Windows.Controls.UserControl
    {
        public ObservableCollection<ChatMessage> Messages { get; } = new();

        public VirgilChatPanel()
        {
            InitializeComponent();
            DataContext = this;

            // autoscroll à chaque rendu (léger mais efficace)
            CompositionTarget.Rendering += (_, __) => ScrollToEnd();
        }

        private void ScrollToEnd()
        {
            if (MessageList == null) return;

            // 1) Si c'est un ListBox → ScrollIntoView dispo
            if (MessageList is ListBox lb && lb.Items.Count > 0)
            {
                try { lb.ScrollIntoView(lb.Items[lb.Items.Count - 1]); }
                catch { /* ignore */ }
                return;
            }

            // 2) Sinon on cherche le ScrollViewer parent et on scrolle en bas
            var sv = FindScrollViewer(MessageList);
            if (sv != null)
            {
                sv.ScrollToEnd();
            }
        }

        private static ScrollViewer? FindScrollViewer(DependencyObject root)
        {
            if (root is ScrollViewer sv) return sv;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                var found = FindScrollViewer(child);
                if (found != null) return found;
            }
            return null;
        }

        public void Post(string text, string? mood = null, int? ttlMs = null)
        {
            if (string.IsNullOrWhiteSpace(text)) return;

            var msg = new ChatMessage { Text = text, TtlMs = ttlMs ?? 5000 };
            Messages.Add(msg);
            ScrollToEnd();

            Dispatcher.BeginInvoke(new Action(() =>
            {
                var container = FindContainerFor(msg);
                if (container != null)
                {
                    msg.Container = container;
                    ScheduleVanish(msg);
                }
            }), DispatcherPriority.Loaded);
        }

        private FrameworkElement? FindContainerFor(ChatMessage msg)
        {
            if (MessageList == null || MessageList.Items.Count == 0) return null;

            var idx = MessageList.Items.Count - 1;
            var itemContainer = MessageList.ItemContainerGenerator.ContainerFromIndex(idx) as FrameworkElement;
            return itemContainer?.FindName("Bubble") as FrameworkElement ?? itemContainer;
        }

        private async void ScheduleVanish(ChatMessage msg)
        {
            try
            {
                await System.Threading.Tasks.Task.Delay(msg.TtlMs);
                if (!Messages.Contains(msg) || msg.Container == null) return;

                PlayThanos(msg.Container);
                FadeShrink(msg.Container, 240, () => Messages.Remove(msg));
            }
            catch { /* ignore */ }
        }

        private void FadeShrink(FrameworkElement target, int ms, Action onDone)
        {
            if (target == null) { onDone?.Invoke(); return; }

            var sb = new Storyboard();

            var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(ms))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
            };
            Storyboard.SetTarget(fade, target);
            Storyboard.SetTargetProperty(fade, new PropertyPath("Opacity"));
            sb.Children.Add(fade);

            var scale = new ScaleTransform(1, 1);
            target.RenderTransformOrigin = new System.Windows.Point(0.5, 0.5);
            target.RenderTransform = scale;

            var sx = new DoubleAnimation(1, 0.85, TimeSpan.FromMilliseconds(ms));
            Storyboard.SetTarget(sx, target);
            Storyboard.SetTargetProperty(sx, new PropertyPath("RenderTransform.ScaleX"));
            sb.Children.Add(sx);

            var sy = new DoubleAnimation(1, 0.85, TimeSpan.FromMilliseconds(ms));
            Storyboard.SetTarget(sy, target);
            Storyboard.SetTargetProperty(sy, new PropertyPath("RenderTransform.ScaleY"));
            sb.Children.Add(sy);

            sb.Completed += (_, __) => onDone?.Invoke();
            sb.Begin();
        }

        private void PlayThanos(FrameworkElement source)
        {
            if (DustLayer == null || source.ActualWidth < 10 || source.ActualHeight < 10) return;

            var origin = source.TranslatePoint(new System.Windows.Point(0, 0), DustLayer);
            var rnd = new Random();

            int count = Math.Clamp((int)(source.ActualWidth * source.ActualHeight / 550), 14, 40);
            for (int i = 0; i < count; i++)
            {
                var dot = new Ellipse
                {
                    Width = rnd.Next(2, 5),
                    Height = rnd.Next(2, 5),
                    Fill = new SolidColorBrush(Color.FromArgb(220, 255, 255, 255)),
                    Opacity = 0.0
                };

                Canvas.SetLeft(dot, origin.X + rnd.NextDouble() * source.ActualWidth);
                Canvas.SetTop(dot, origin.Y + rnd.NextDouble() * source.ActualHeight);
                DustLayer.Children.Add(dot);

                var dx = (rnd.NextDouble() - 0.2) * 120;
                var dy = -(20 + rnd.NextDouble() * 80);
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
