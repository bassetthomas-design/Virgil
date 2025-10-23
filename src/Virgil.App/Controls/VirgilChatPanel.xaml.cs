#nullable enable
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;                // WPF
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
        public FrameworkElement? Container { get; set; } // bulle réelle dans l’ItemsControl
    }

    public partial class VirgilChatPanel : System.Windows.Controls.UserControl
    {
        public ObservableCollection<ChatBubble> Items { get; } = new();

        public VirgilChatPanel()
        {
            InitializeComponent();

            // Si l’ItemsControl s’appelle MessageList dans ton XAML, on lui donne la source :
            if (FindName("MessageList") is ItemsControl ic)
                ic.ItemsSource = Items;

            // Autoscroll fluide à chaque frame rendue
            CompositionTarget.Rendering += (_, __) => ScrollToEnd();
        }

        /// <summary>Affiche un message + planifie sa disparition.</summary>
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
            if (FindName("MessageList") is not ItemsControl ic) return null;
            var idx = Items.Count - 1;
            if (idx < 0) return null;

            var container = ic.ItemContainerGenerator.ContainerFromIndex(idx) as FrameworkElement;
            if (container == null) return null;

            // si ton DataTemplate nomme l’élément bulle "Bubble", on le récupère, sinon on garde le container
            var bubble = container.FindName("Bubble") as FrameworkElement;
            return bubble ?? container;
        }

        private async Task ScheduleVanishAsync(ChatBubble msg)
        {
            try
            {
                await Task.Delay(Math.Max(1000, msg.TtlMs)); // garde un minimum d’1s
                if (!Items.Contains(msg) || msg.Container == null) return;

                // petit effet “Thanos”
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
            if (FindName("MessageList") is not ItemsControl ic) return;

            // 1) Si c’est un ListBox → ScrollIntoView du dernier item
            if (ic is ListBox lb && lb.Items.Count > 0)
            {
                try { lb.ScrollIntoView(lb.Items[lb.Items.Count - 1]); }
                catch { /* ignore */ }
                return;
            }

            // 2) Sinon, on récupère le ScrollViewer parent et on ScrollToEnd
            var sv = FindScrollViewer(ic);
            sv?.ScrollToEnd();
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
    }
}
