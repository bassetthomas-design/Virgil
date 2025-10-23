#nullable enable
using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;

// Alias WPF pour éviter tout conflit avec System.Drawing
using Media = System.Windows.Media;
using WpfPoint = System.Windows.Point;

namespace Virgil.App.Controls
{
    public class ChatMessage
    {
        public string Text { get; set; } = "";
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
        public int TtlMs { get; set; } = 5000;
        public FrameworkElement? Container { get; set; } // attaché au visuel réel
    }

    public partial class VirgilChatPanel : UserControl
    {
        public ObservableCollection<ChatMessage> Messages { get; } = new();

        public VirgilChatPanel()
        {
            InitializeComponent();
            DataContext = this;
            CompositionTarget.Rendering += (_, __) => Scroller.ScrollToEnd();
        }

        public void Post(string text, string? mood = null, int? ttlMs = null)
        {
            if (string.IsNullOrWhiteSpace(text)) return;

            var msg = new ChatMessage { Text = text, TtlMs = ttlMs ?? 5000 };
            Messages.Add(msg);

            // On attend que le conteneur visuel existe
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
            // ItemsControl génère un container par item; on prend le dernier
            var container = List.ItemContainerGenerator.ContainerFromIndex(Messages.Count - 1) as FrameworkElement;
            return container?.FindName("Bubble") as FrameworkElement ?? container;
        }

        private async void ScheduleVanish(ChatMessage msg)
        {
            await System.Threading.Tasks.Task.Delay(msg.TtlMs);
            if (!Messages.Contains(msg) || msg.Container == null) return;

            // lancer l’effet Thanos (poussière) à partir des bounds de la bulle
            PlayThanos(msg.Container);
            FadeShrink(msg.Container, 240, () =>
            {
                Messages.Remove(msg);
            });
        }

        private void FadeShrink(FrameworkElement target, int ms, Action onDone)
        {
            var sb = new Storyboard();

            var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(ms))
            { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn } };
            Storyboard.SetTarget(fade, target);
            Storyboard.SetTargetProperty(fade, new PropertyPath("Opacity"));
            sb.Children.Add(fade);

            var scale = new Media.ScaleTransform(1, 1);
            target.RenderTransformOrigin = new WpfPoint(0.5, 0.5);
            target.RenderTransform = scale;

            var daX = new DoubleAnimation(1, 0.85, TimeSpan.FromMilliseconds(ms));
            Storyboard.SetTarget(daX, target);
            Storyboard.SetTargetProperty(daX, new PropertyPath("RenderTransform.ScaleX"));
            sb.Children.Add(daX);

            var daY = new DoubleAnimation(1, 0.85, TimeSpan.FromMilliseconds(ms));
            Storyboard.SetTarget(daY, target);
            Storyboard.SetTargetProperty(daY, new PropertyPath("RenderTransform.ScaleY"));
            sb.Children.Add(daY);

            sb.Completed += (_, __) => onDone();
            sb.Begin();
        }

        /// <summary>Effet "Thanos" sans shader : petites particules qui s’éparpillent.</summary>
        private void PlayThanos(FrameworkElement source)
        {
            if (DustLayer == null) return;
            if (source.ActualWidth < 10 || source.ActualHeight < 10) return;

            var origin = source.TranslatePoint(new WpfPoint(0, 0), DustLayer);
            var rnd = new Random();

            int count = Math.Clamp((int)(source.ActualWidth * source.ActualHeight / 550), 14, 40);
            for (int i = 0; i < count; i++)
            {
                var dot = new Ellipse
                {
                    Width = rnd.Next(2, 5),
                    Height = rnd.Next(2, 5),
                    Fill = new Media.SolidColorBrush(Media.Color.FromArgb(220, 255, 255, 255)),
                    Opacity = 0.0
                };
                Canvas.SetLeft(dot, origin.X + rnd.NextDouble() * source.ActualWidth);
                Canvas.SetTop(dot, origin.Y + rnd.NextDouble() * source.ActualHeight);
                DustLayer.Children.Add(dot);

                // destination aléatoire
                var dx = (rnd.NextDouble() - 0.2) * 120;
                var dy = -(20 + rnd.NextDouble() * 80); // un peu vers le haut
                var dur = TimeSpan.FromMilliseconds(400 + rnd.Next(0, 200));

                var ax = new DoubleAnimation(Canvas.GetLeft(dot), Canvas.GetLeft(dot) + dx, dur)
                { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } };
                var ay = new DoubleAnimation(Canvas.GetTop(dot), Canvas.GetTop(dot) + dy, dur)
                { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } };
                var ao = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(80));
                var ao2 = new DoubleAnimation(1, 0, dur) { BeginTime = TimeSpan.FromMilliseconds(80) };

                dot.BeginAnimation(UIElement.OpacityProperty, ao);
                dot.BeginAnimation(UIElement.OpacityProperty, ao2);
                dot.BeginAnimation(Canvas.LeftProperty, ax);
                dot.BeginAnimation(Canvas.TopProperty, ay);

                // nettoyage
                ao2.Completed += (_, __) => DustLayer.Children.Remove(dot);
            }
        }
    }
}
