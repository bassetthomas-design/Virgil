#nullable enable
using System;
using System.Collections;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;                // WPF controls
using System.Windows.Media;                   // CompositionTarget, Brushes, Color, VisualTreeHelper
using System.Windows.Media.Animation;         // Storyboard, DoubleAnimation, Easing
using System.Windows.Shapes;                  // Ellipse

namespace Virgil.App.Controls
{
    public class ChatBubble
    {
        public string Text { get; set; } = "";
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
        public int TtlMs { get; set; } = 60000;
        public FrameworkElement? Container { get; set; }
    }

    public partial class VirgilChatPanel : System.Windows.Controls.UserControl
    {
        // Source interne (utilisée par Post()) si aucune source externe n’est fournie
        public ObservableCollection<ChatBubble> Items { get; } = new();

        // Option : binder une source externe (ex: ChatMessages) si tu ne veux pas utiliser Post()
        public static readonly DependencyProperty ChatItemsSourceProperty =
            DependencyProperty.Register(
                nameof(ChatItemsSource),
                typeof(IEnumerable),
                typeof(VirgilChatPanel),
                new PropertyMetadata(null, OnChatItemsSourceChanged));

        public IEnumerable? ChatItemsSource
        {
            get => (IEnumerable?)GetValue(ChatItemsSourceProperty);
            set => SetValue(ChatItemsSourceProperty, value);
        }

        private static void OnChatItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is VirgilChatPanel p && p.FindName("MessageList") is ListBox lb)
            {
                lb.ItemsSource = e.NewValue as IEnumerable;
            }
        }

        public VirgilChatPanel()
        {
            InitializeComponent();

            // Si aucune source externe n’est bindée, on alimente la liste avec Items
            if (FindName("MessageList") is ListBox lb && ChatItemsSource is null)
                lb.ItemsSource = Items;

            // Autoscroll fluide à chaque frame rendue
            CompositionTarget.Rendering += (_, __) => ScrollToEnd();
        }

        /// <summary>Affiche un message + planifie sa disparition (utilise la collection interne).</summary>
        public void Post(string text, string? mood = null, int ttlMs = 60000)
        {
            if (string.IsNullOrWhiteSpace(text)) return;

            var m = new ChatBubble { Text = text, TtlMs = ttlMs };
            Items.Add(m);

            // Attendre que le conteneur visuel soit créé
            Dispatcher.BeginInvoke(new Action(() =>
            {
                m.Container = GetContainerForLastItem();
                if (m.Container != null)
                    _ = ScheduleVanishAsync(m);
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        // ===== utilitaires =====

        private FrameworkElement? GetContainerForLastItem()
        {
            if (FindName("MessageList") is not ListBox lb) return null;
            var idx = lb.Items.Count - 1;
            if (idx < 0) return null;

            var container = lb.ItemContainerGenerator.ContainerFromIndex(idx) as FrameworkElement;
            if (container == null) return null;

            var bubble = container.FindName("Bubble") as FrameworkElement;
            return bubble ?? container;
        }

        private async Task ScheduleVanishAsync(ChatBubble msg)
        {
            try
            {
                await Task.Delay(Math.Max(1000, msg.TtlMs)); // minimum 1s
                if (!Items.Contains(msg) || msg.Container == null) return;

                // effet “Thanos”
                PlayThanos(msg.Container);

                // fondu + léger shrink puis suppression de l’item
                FadeShrink(msg.Container, 240, () =>
                {
                    Items.Remove(msg);
                });
            }
            catch { /* ignore */ }
        }

        private void FadeShrink(FrameworkElement target, int ms, Action onDone)
        {
            var sb = new Storyboard();

            var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(ms))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
            };
            Storyboard.SetTarget(fade, target);
            Storyboard.SetTargetProperty(fade, new PropertyPath("Opacity"));
            sb.Children.Add(fade);

            var st = new ScaleTransform(1, 1);
            target.RenderTransformOrigin = new Point(0.5, 0.5);
            target.RenderTransform = st;

            var s1 = new DoubleAnimation(1, 0.85, TimeSpan.FromMilliseconds(ms));
            Storyboard.SetTarget(s1, target);
            Storyboard.SetTargetProperty(s1, new PropertyPath("RenderTransform.ScaleX"));
            sb.Children.Add(s1);

            var s2 = s1.Clone();
            Storyboard.SetTargetProperty(s2, new PropertyPath("RenderTransform.ScaleY"));
            sb.Children.Add(s2);

            sb.Completed += (_, __) => onDone();
            sb.Begin();
        }

        /// <summary>Effet “Thanos” sans shader : particules blanches qui se dissipent.</summary>
        private void PlayThanos(FrameworkElement source)
        {
            if (FindName("DustLayer") is not Canvas dust) return;
            if (source.ActualWidth < 8 || source.ActualHeight < 8) return;

            var origin = source.TranslatePoint(new Point(0, 0), dust);
            var rnd = new Random();

            int count = Math.Clamp((int)(source.ActualWidth * source.ActualHeight / 550), 14, 42);
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
                dust.Children.Add(dot);

                var dx = (rnd.NextDouble() - 0.2) * 120;
                var dy = -(20 + rnd.NextDouble() * 80);
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

                ao2.Completed += (_, __) => dust.Children.Remove(dot);
            }
        }

        private void ScrollToEnd()
        {
            if (FindName("MessageList") is not ListBox lb) return;
            if (lb.Items.Count == 0) return;
            try { lb.ScrollIntoView(lb.Items[lb.Items.Count - 1]); }
            catch { /* ignore */ }
        }
    }
}
