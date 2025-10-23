#nullable enable
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;     // Color, SolidColorBrush, Brushes
using System.Windows.Shapes;    // Ellipse
using System.Windows.Media.Animation;

namespace Virgil.App.Controls
{
    public class ChatMessage
    {
        public string Text { get; set; } = "";
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
        public int TtlMs { get; set; } = 60_000; // 1 minute par défaut
        public FrameworkElement? Container { get; set; }
    }

    public partial class VirgilChatPanel : System.Windows.Controls.UserControl
    {
        public ObservableCollection<ChatMessage> Messages { get; } = new();

        public VirgilChatPanel()
        {
            InitializeComponent();
            DataContext = this;

            // auto-scroll si un ScrollViewer entoure le ListBox côté XAML
            Loaded += (_, __) =>
            {
                var sv = FindDescendant<ScrollViewer>(this);
                if (sv != null)
                {
                    Messages.CollectionChanged += (_, __2) =>
                    {
                        sv.ScrollToEnd();
                    };
                }
            };
        }

        // API publique pour poster un message (avec TTL optionnel)
        public void Post(string text, string? mood = null, int? ttlMs = null)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            var msg = new ChatMessage { Text = text, TtlMs = ttlMs ?? 60_000 };
            Messages.Add(msg);

            // Quand l’item visuel est prêt, on déclenche le timer d’effacement
            Dispatcher.BeginInvoke(new Action(() =>
            {
                var container = FindContainerForLastItem();
                if (container != null)
                {
                    msg.Container = container;
                    ScheduleVanish(msg);
                }
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        // ===== helpers visuels =====
        private FrameworkElement? FindContainerForLastItem()
        {
            if (Messages.Count == 0) return null;
            var list = FindDescendant<ListBox>(this);
            if (list == null) return null;
            var container = list.ItemContainerGenerator.ContainerFromIndex(Messages.Count - 1) as FrameworkElement;
            if (container == null) return null;

            // Si ton DataTemplate nomme l’élément bulle "Bubble", on le récupère, sinon on garde le container
            var bubble = container.FindName("Bubble") as FrameworkElement;
            return bubble ?? container;
        }

        private static T? FindDescendant<T>(DependencyObject root) where T : DependencyObject
        {
            int count = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                if (child is T t) return t;
                var deeper = FindDescendant<T>(child);
                if (deeper != null) return deeper;
            }
            return null;
        }

        // ===== disparition + effet "Thanos" =====
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
            var sb = new Storyboard();

            // Opacity -> 0
            var daOpacity = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(ms))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
            };
            Storyboard.SetTarget(daOpacity, target);
            Storyboard.SetTargetProperty(daOpacity, new PropertyPath("Opacity"));
            sb.Children.Add(daOpacity);

            // Scale 1 -> 0.85
            var scale = new ScaleTransform(1, 1);
            target.RenderTransformOrigin = new Point(0.5, 0.5);
            target.RenderTransform = scale;

            var daX = new DoubleAnimation(1, 0.85, TimeSpan.FromMilliseconds(ms));
            Storyboard.SetTarget(daX, target);
            Storyboard.SetTargetProperty(daX, new PropertyPath("RenderTransform.ScaleX"));
            sb.Children.Add(daX);

            var daY = daX.Clone();
            Storyboard.SetTargetProperty(daY, new PropertyPath("RenderTransform.ScaleY"));
            sb.Children.Add(daY);

            sb.Completed += (_, __) => onDone();
            sb.Begin();
        }

        /// <summary>Effet “poussière” simple (sans shader) : petits points qui s’éparpillent puis s’éteignent.</summary>
        private void PlayThanos(FrameworkElement source)
        {
            if (source.ActualWidth < 6 || source.ActualHeight < 6) return;

            // On cherche un Canvas appelé "DustLayer" dans le template, sinon on en injecte un dans la même parenté visuelle
            var root = Window.GetWindow(this) as FrameworkElement ?? source;
            var layer = FindDescendant<Canvas>(root);
            if (layer == null)
            {
                // fallback : on essaye d’ajouter un Canvas au parent immédiat si c’est un Grid
                if (source.Parent is Grid g)
                {
                    layer = new Canvas { IsHitTestVisible = false };
                    g.Children.Add(layer);
                }
                else return;
            }

            var origin = source.TranslatePoint(new Point(0, 0), layer);
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
                layer.Children.Add(dot);

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

                aoOut.Completed += (_, __) => layer.Children.Remove(dot);
            }
        }
    }
}
