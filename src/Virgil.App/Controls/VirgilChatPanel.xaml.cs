#nullable enable
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
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
        public FrameworkElement? Container { get; set; }   // attaché au visuel réel
    }

    // On qualifie la base pour être sûr d'utiliser WPF (et pas WinForms).
    public partial class VirgilChatPanel : System.Windows.Controls.UserControl
    {
        public ObservableCollection<ChatMessage> Messages { get; } = new();

        public VirgilChatPanel()
        {
            InitializeComponent();
            DataContext = this;

            // auto-scroll vers le bas quand on rend une frame (si un Scroller existe dans le XAML)
            CompositionTarget.Rendering += (_, __) =>
            {
                var prop = GetType().GetProperty("Scroller");
                if (prop?.GetValue(this) is System.Windows.Controls.ScrollViewer sv)
                    sv.ScrollToEnd();
            };
        }

        /// <summary>
        /// Ajoute une bulle et programme sa disparition (effet "Thanos").
        /// </summary>
        public void Post(string text, string? mood = null, int? ttlMs = null)
        {
            if (string.IsNullOrWhiteSpace(text)) return;

            var msg = new ChatMessage { Text = text, TtlMs = ttlMs ?? 5000 };
            Messages.Add(msg);

            // On attend que l'ItemContainer soit matérialisé par l'ItemsControl.
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
            // Requiert un ItemsControl nommé "List" dans le XAML.
            // On récupère le container de l'item tout juste ajouté (dernier index).
            var listProp = GetType().GetField("List") ?? GetType().GetProperty("List");
            var list = listProp?.GetValue(this) as System.Windows.Controls.ItemsControl;
            if (list == null) return null;

            var idx = Messages.Count - 1;
            var container = list.ItemContainerGenerator.ContainerFromIndex(idx) as FrameworkElement;

            // Idéalement tes DataTemplate contiennent un élément nommé "Bubble".
            var bubble = container?.FindName("Bubble") as FrameworkElement;
            return bubble ?? container;
        }

        private async void ScheduleVanish(ChatMessage msg)
        {
            await Task.Delay(msg.TtlMs);
            if (!Messages.Contains(msg) || msg.Container == null) return;

            // Effet particules + fondu/réduction
            PlayThanos(msg.Container);
            FadeShrink(msg.Container, 240, () =>
            {
                Messages.Remove(msg);
            });
        }

        private void FadeShrink(FrameworkElement target, int ms, Action onDone)
        {
            var sb = new Storyboard();

            // Opacity 1 -> 0
            var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(ms))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
            };
            Storyboard.SetTarget(fade, target);
            Storyboard.SetTargetProperty(fade, new PropertyPath("Opacity"));
            sb.Children.Add(fade);

            // Scale 1 -> 0.85
            var scale = new ScaleTransform(1, 1);
            target.RenderTransformOrigin = new Point(0.5, 0.5);
            target.RenderTransform = scale;

            var sx = new DoubleAnimation(1, 0.85, TimeSpan.FromMilliseconds(ms));
            Storyboard.SetTarget(sx, target);
            Storyboard.SetTargetProperty(sx, new PropertyPath("RenderTransform.ScaleX"));
            sb.Children.Add(sx);

            var sy = new DoubleAnimation(1, 0.85, TimeSpan.FromMilliseconds(ms));
            Storyboard.SetTarget(sy, target);
            Storyboard.SetTargetProperty(sy, new PropertyPath("RenderTransform.ScaleY"));
            sb.Children.Add(sy);

            sb.Completed += (_, __) => onDone();
            sb.Begin();
        }

        /// <summary>
        /// Effet "Thanos" sans shader : petites particules qui s’éparpillent.
        /// Requiert un Canvas nommé "DustLayer" dans le XAML parent.
        /// </summary>
        private void PlayThanos(FrameworkElement source)
        {
            if (source.ActualWidth < 10 || source.ActualHeight < 10) return;

            // Cherche un Canvas "DustLayer" exposé par code-behind (field or prop).
            var dustMember = GetType().GetField("DustLayer") ?? GetType().GetProperty("DustLayer");
            var dust = dustMember?.GetValue(this) as System.Windows.Controls.Canvas;
            if (dust == null) return;

            var origin = source.TranslatePoint(new Point(0, 0), dust);
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
                System.Windows.Controls.Canvas.SetLeft(dot, origin.X + rnd.NextDouble() * source.ActualWidth);
                System.Windows.Controls.Canvas.SetTop(dot, origin.Y + rnd.NextDouble() * source.ActualHeight);
                dust.Children.Add(dot);

                // destination aléatoire
                var dx = (rnd.NextDouble() - 0.2) * 120;
                var dy = -(20 + rnd.NextDouble() * 80); // un peu vers le haut
                var dur = TimeSpan.FromMilliseconds(400 + rnd.Next(0, 200));

                var ax = new DoubleAnimation(System.Windows.Controls.Canvas.GetLeft(dot),
                                             System.Windows.Controls.Canvas.GetLeft(dot) + dx, dur)
                { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } };

                var ay = new DoubleAnimation(System.Windows.Controls.Canvas.GetTop(dot),
                                             System.Windows.Controls.Canvas.GetTop(dot) + dy, dur)
                { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } };

                var aoIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(80));
                var aoOut = new DoubleAnimation(1, 0, dur) { BeginTime = TimeSpan.FromMilliseconds(80) };

                dot.BeginAnimation(UIElement.OpacityProperty, aoIn);
                dot.BeginAnimation(UIElement.OpacityProperty, aoOut);
                dot.BeginAnimation(System.Windows.Controls.Canvas.LeftProperty, ax);
                dot.BeginAnimation(System.Windows.Controls.Canvas.TopProperty, ay);

                // nettoyage
                aoOut.Completed += (_, __) => dust.Children.Remove(dot);
            }
        }
    }
}
