#nullable enable
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

// Alias WPF pour éviter System.Drawing / WinForms
using SW = System.Windows;
using SWC = System.Windows.Controls;
using SWM = System.Windows.Media;
using SWMA = System.Windows.Media.Animation;
using SWShapes = System.Windows.Shapes;

namespace Virgil.App.Controls
{
    /// <summary>Un message visuel dans le panel.</summary>
    public sealed class ChatBubble
    {
        public string Text { get; set; } = "";
        public DateTime Created { get; set; } = DateTime.UtcNow;
        public int TtlMs { get; set; } = 60_000; // 1 minute par défaut
        public double Opacity { get; set; } = 1.0;

        // Couleur de fond déjà calculée ; on reste en Media.Brush
        public SWM.Brush Background { get; set; } =
            new SWM.SolidColorBrush(SWM.Color.FromArgb(0x22, 0xFF, 0xFF, 0xFF));

        // Référence au conteneur visuel (Border "Bubble") une fois généré
        public SW.FrameworkElement? Container { get; set; }
    }

    /// <summary>Panneau de chat + effet "Thanos".</summary>
    public partial class VirgilChatPanel : SWC.UserControl
    {
        public ObservableCollection<ChatBubble> Messages { get; } = new();

        public VirgilChatPanel()
        {
            InitializeComponent();
            DataContext = this;

            // Auto-scroll à chaque frame si la liste change
            SWM.CompositionTarget.Rendering += (_, __) =>
            {
                try { MessageList?.ScrollIntoView(MessageList.Items.Cast<object>().LastOrDefault()); }
                catch { /* ignore */ }
            };
        }

        /// <summary>Ajoute un message et programme sa disparition.</summary>
        public void Post(string text, string? mood = null, int? ttlMs = null)
        {
            if (string.IsNullOrWhiteSpace(text)) return;

            var bubble = new ChatBubble
            {
                Text = text.Trim(),
                TtlMs = ttlMs ?? 60_000,
                Background = MoodToBrush(mood ?? "neutral")
            };

            Messages.Add(bubble);

            // Quand le conteneur est prêt, on le retrouve pour animer la disparition
            Dispatcher.BeginInvoke(new Action(() =>
            {
                AttachContainerFor(bubble);
                _ = ScheduleVanishAsync(bubble);
            }), SW.Threading.DispatcherPriority.Loaded);
        }

        /// <summary>Scroll jusqu’au bas.</summary>
        public void ScrollToEnd()
        {
            try { MessageList?.ScrollIntoView(MessageList.Items.Cast<object>().LastOrDefault()); } catch { }
        }

        /// <summary>Supprime tous les messages.</summary>
        public void Clear()
        {
            Messages.Clear();
            DustLayer.Children.Clear();
        }

        // ---------------- internals ----------------

        private SWM.Brush MoodToBrush(string mood)
        {
            return mood.ToLowerInvariant() switch
            {
                "proud"    => new SWM.SolidColorBrush(SWM.Color.FromArgb(0x33, 0x46, 0xFF, 0x7A)),
                "vigilant" => new SWM.SolidColorBrush(SWM.Color.FromArgb(0x33, 0xFF, 0xE4, 0x6B)),
                "alert"    => new SWM.SolidColorBrush(SWM.Color.FromArgb(0x33, 0xFF, 0x69, 0x61)),
                _          => new SWM.SolidColorBrush(SWM.Color.FromArgb(0x22, 0xFF, 0xFF, 0xFF)),
            };
        }

        private void AttachContainerFor(ChatBubble bubble)
        {
            var idx = Messages.IndexOf(bubble);
            if (idx < 0) return;

            var container = MessageList.ItemContainerGenerator.ContainerFromIndex(idx) as SW.FrameworkElement;
            if (container == null) return;

            // Le Border dans le DataTemplate s’appelle "Bubble"
            var bubbleBorder = container.FindName("Bubble") as SW.FrameworkElement ?? container;
            bubble.Container = bubbleBorder;
        }

        private async Task ScheduleVanishAsync(ChatBubble bubble)
        {
            try
            {
                await Task.Delay(bubble.TtlMs);
                if (!Messages.Contains(bubble)) return;

                if (bubble.Container != null)
                {
                    PlayThanos(bubble.Container);
                    FadeShrink(bubble.Container, 240, () =>
                    {
                        Messages.Remove(bubble);
                    });
                }
                else
                {
                    Messages.Remove(bubble);
                }
            }
            catch { /* ignore */ }
        }

        private void FadeShrink(SW.FrameworkElement target, int ms, Action onDone)
        {
            var sb = new SWMA.Storyboard();

            // Opacity 1 -> 0
            var daOpacity = new SWMA.DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(ms))
            {
                EasingFunction = new SWMA.QuadraticEase { EasingMode = SWMA.EasingMode.EaseIn }
            };
            SWMA.Storyboard.SetTarget(daOpacity, target);
            SWMA.Storyboard.SetTargetProperty(daOpacity, new SW.PropertyPath("Opacity"));
            sb.Children.Add(daOpacity);

            // Scale 1 -> 0.85
            var scale = new SWM.ScaleTransform(1, 1);
            target.RenderTransformOrigin = new SW.Point(0.5, 0.5);
            target.RenderTransform = scale;

            var daX = new SWMA.DoubleAnimation(1, 0.85, TimeSpan.FromMilliseconds(ms));
            SWMA.Storyboard.SetTarget(daX, target);
            SWMA.Storyboard.SetTargetProperty(daX, new SW.PropertyPath("RenderTransform.ScaleX"));
            sb.Children.Add(daX);

            var daY = new SWMA.DoubleAnimation(1, 0.85, TimeSpan.FromMilliseconds(ms));
            SWMA.Storyboard.SetTarget(daY, target);
            SWMA.Storyboard.SetTargetProperty(daY, new SW.PropertyPath("RenderTransform.ScaleY"));
            sb.Children.Add(daY);

            sb.Completed += (_, __) => onDone();
            sb.Begin();
        }

        /// <summary>Effet poussière ("Thanos") simple sans shader.</summary>
        private void PlayThanos(SW.FrameworkElement source)
        {
            if (source.ActualWidth < 8 || source.ActualHeight < 8) return;

            // position absolue du coin haut-gauche dans coord du DustLayer
            var origin = source.TranslatePoint(new SW.Point(0, 0), DustLayer);
            var rnd = new Random();

            int count = Math.Clamp((int)(source.ActualWidth * source.ActualHeight / 550), 14, 40);
            for (int i = 0; i < count; i++)
            {
                var dot = new SWShapes.Ellipse
                {
                    Width = rnd.Next(2, 5),
                    Height = rnd.Next(2, 5),
                    Fill = new SWM.SolidColorBrush(SWM.Color.FromArgb(220, 255, 255, 255)),
                    Opacity = 0.0
                };

                SWC.Canvas.SetLeft(dot, origin.X + rnd.NextDouble() * source.ActualWidth);
                SWC.Canvas.SetTop(dot, origin.Y + rnd.NextDouble() * source.ActualHeight);
                DustLayer.Children.Add(dot);

                // Trajectoire
                var dx = (rnd.NextDouble() - 0.2) * 120;
                var dy = -(20 + rnd.NextDouble() * 80);
                var dur = TimeSpan.FromMilliseconds(400 + rnd.Next(0, 200));

                var ax = new SWMA.DoubleAnimation(SWC.Canvas.GetLeft(dot), SWC.Canvas.GetLeft(dot) + dx, dur)
                { EasingFunction = new SWMA.QuadraticEase { EasingMode = SWMA.EasingMode.EaseOut } };

                var ay = new SWMA.DoubleAnimation(SWC.Canvas.GetTop(dot), SWC.Canvas.GetTop(dot) + dy, dur)
                { EasingFunction = new SWMA.QuadraticEase { EasingMode = SWMA.EasingMode.EaseOut } };

                var aoIn = new SWMA.DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(80));
                var aoOut = new SWMA.DoubleAnimation(1, 0, dur) { BeginTime = TimeSpan.FromMilliseconds(80) };

                dot.BeginAnimation(SW.UIElement.OpacityProperty, aoIn);
                dot.BeginAnimation(SW.UIElement.OpacityProperty, aoOut);
                dot.BeginAnimation(SWC.Canvas.LeftProperty, ax);
                dot.BeginAnimation(SWC.Canvas.TopProperty, ay);

                aoOut.Completed += (_, __) => DustLayer.Children.Remove(dot);
            }
        }
    }
}
