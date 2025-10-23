// Uses WPF namespaces explicitly to avoid conflicts with System.Windows.Forms / System.Drawing
#nullable enable
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using SW = System.Windows;
using SWC = System.Windows.Controls;
using SWM = System.Windows.Media;
using SWS = System.Windows.Shapes;

namespace Virgil.App.Controls
{
    public class ChatBubble
    {
        public string Text { get; set; } = "";
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
        public int TtlMs { get; set; } = 60_000; // 60s par défaut
        public SW.FrameworkElement? Visual { get; set; }
    }

    public partial class VirgilChatPanel : SWC.UserControl
    {
        // Ces noms doivent exister dans VirgilChatPanel.xaml :
        // <ScrollViewer x:Name="Scroller">, <ListBox x:Name="MessageList">, <Canvas x:Name="DustLayer">
        public ObservableCollection<ChatBubble> Messages { get; } = new();

        public VirgilChatPanel()
        {
            InitializeComponent();
            DataContext = this;

            // Scroll automatique sur nouveau contenu
            CompositionTarget.Rendering += (_, __) => Scroller?.ScrollToEnd();
        }

        /// <summary>Ajoute un message dans la zone de chat (avec TTL en ms, défaut 60s).</summary>
        public void Post(string text, string? mood = null, int? ttlMs = null)
        {
            if (string.IsNullOrWhiteSpace(text))
                return;

            var bubble = new ChatBubble
            {
                Text = text,
                TtlMs = ttlMs ?? 60_000,
                Created = DateTime.UtcNow
            };
            Messages.Add(bubble);

            // Laisse WPF matérialiser l’item, puis on accroche la bulle et on programme sa disparition.
            Dispatcher.BeginInvoke(new Action(() =>
            {
                var container = MessageList.ItemContainerGenerator.ContainerForItem(bubble) as SWC.ListBoxItem;
                if (container != null)
                {
                    // On essaie de retrouver l'élément visuel nommé "Bubble" dans le template de l'item
                    var bubbleVisual = FindElementByName(container, "Bubble") as SW.FrameworkElement ?? container;
                    bubble.Visual = bubbleVisual;

                    ScheduleVanish(bubble);
                    Scroller?.ScrollToEnd();
                }
            }), DispatcherPriority.Loaded);
        }

        public void ClearAll()
        {
            // Efface tout (sans effet visuel)
            foreach (var child in DustLayer.Children.OfType<SW.FrameworkElement>().ToList())
                DustLayer.Children.Remove(child);
            Messages.Clear();
        }

        public void ScrollToEnd()
        {
            Scroller?.ScrollToEnd();
        }

        // ---------- Helpers internes ----------

        private SW.DependencyObject? FindElementByName(SW.DependencyObject root, string name)
        {
            if (root is FrameworkElement fe && fe.Name == name)
                return fe;

            int count = SW.Media.VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < count; i++)
            {
                var child = SW.Media.VisualTreeHelper.GetChild(root, i);
                var found = FindElementByName(child, name);
                if (found != null) return found;
            }
            return null;
        }

        private async void ScheduleVanish(ChatBubble cb)
        {
            await Task.Delay(cb.TtlMs);
            if (!Messages.Contains(cb) || cb.Visual is null)
                return;

            // Effet "poussière" + fade/shrink puis suppression
            PlayThanos(cb.Visual);
            FadeAndShrink(cb.Visual, 240, () =>
            {
                Messages.Remove(cb);
            });
        }

        private void FadeAndShrink(SW.FrameworkElement target, int ms, Action onDone)
        {
            var sb = new SWM.Animation.Storyboard();

            var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(ms))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
            };
            SWM.Animation.Storyboard.SetTarget(fade, target);
            SWM.Animation.Storyboard.SetTargetProperty(fade, new PropertyPath("Opacity"));
            sb.Children.Add(fade);

            var scale = new SWM.ScaleTransform(1, 1);
            target.RenderTransformOrigin = new SW.Point(0.5, 0.5);
            target.RenderTransform = scale;

            var sx = new DoubleAnimation(1, 0.85, TimeSpan.FromMilliseconds(ms));
            var sy = new DoubleAnimation(1, 0.85, TimeSpan.FromMilliseconds(ms));
            SWM.Animation.Storyboard.SetTarget(sx, target);
            SWM.Animation.Storyboard.SetTargetProperty(sx, new PropertyPath("RenderTransform.ScaleX"));
            SWM.Animation.Storyboard.SetTarget(sy, target);
            SWM.Animation.Storyboard.SetTargetProperty(sy, new PropertyPath("RenderTransform.ScaleY"));
            sb.children.Add(sx);
            sb.children.Add(sy);

            sb.Completed += (_, __) => onDone();
            sb.Begin();
        }

        /// <summary>Effet "Thanos" : petites particules blanches qui s’éparpillent depuis la bulle.</summary>
        private void PlayThanos(SW.FrameworkElement source)
        {
            if (source.ActualWidth <= 4 || source.ActualHeight <= 4 || DustLayer == null)
                return;

            var origin = source.TranslatePoint(new SW.Point(0, 0), DustLayer);
            var rnd = new Random();

            int count = Math.Clamp((int)((source.ActualWidth * source.ActualHeight) / 550.0), 12, 42);

            for (int i = 0; i < count; i++)
            {
                var dot = new SWS.Ellipse
                {
                    Width = rnd.Next(2, 5),
                    Height = rnd.Next(2, 5),
                    Fill = new SWM.SolidColorBrush(SWM.Color.FromArgb(220, 255, 255, 255)),
                    Opacity = 0.0
                };

                SWC.Canvas.SetLeft(dot, origin.X + rnd.NextDouble() * source.ActualWidth);
                SWC.Canvas.SetTop(dot,  origin.Y + rnd.NextDouble() * source.ActualHeight);
                DustLayer.Children.Add(dot);

                // Déplacement aléatoire vers le haut / côté
                double dx = (rnd.NextDouble() - 0.2) * 120.0;
                double dy = -(20 + rnd.NextDouble() * 80.0);
                var dur = TimeSpan.FromMilliseconds(400 + rnd.Next(0, 220));

                var ax = new DoubleAnimation(SWC.Canvas.GetLeft(dot), SWC.Canvas.GetLeft(dot) + dx, dur)
                { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } };
                var ay = new DoubleAnimation(SWC.Canvas.GetTop(dot), SWC.Canvas.GetTop(dot) + dy, dur)
                { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } };
                var aIn  = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(80));
                var aOut = new DoubleAnimation(1, 0, dur) { BeginTime = TimeSpan.FromMilliseconds(80) };

                dot.BeginAnimation(SW.UIElement.OpacityProperty, aIn);
                dot.BeginAnimation(SW.UIElement.OpacityProperty, aOut);
                dot.BeginAnimation(SWC.Canvas.LeftProperty, ax);
                dot.BeginAnimation(SWC.Canvas.TopProperty,  ay);

                aOut.Completed += (_, __) => DustLayer.Children.Remove(dot);
            }
        }
    }
}
