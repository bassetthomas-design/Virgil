#nullable enable
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using Media = System.Windows.Media;                 // Brush/Color/etc.
using WpfControls = System.Windows.Controls;       // ListBox, UserControl
using Wpf = System.Windows; // pour forcer Point de WPF

namespace Virgil.App.Controls
{
    public class ChatMessage
    {
        public string Text { get; set; } = "";
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
        public int TtlMs { get; set; } = 5000;
        public FrameworkElement? Container { get; set; }   // attaché au visuel réel
    }

    public partial class VirgilChatPanel : WpfControls.UserControl
    {
        // ItemsSource bindée depuis XAML (ChatItemsSource="{Binding ChatMessages}")
        public static readonly DependencyProperty ChatItemsSourceProperty =
            DependencyProperty.Register(
                nameof(ChatItemsSource),
                typeof(ObservableCollection<ChatMessage>),
                typeof(VirgilChatPanel),
                new PropertyMetadata(null, OnItemsSourceChanged));

        public ObservableCollection<ChatMessage>? ChatItemsSource
        {
            get => (ObservableCollection<ChatMessage>?)GetValue(ChatItemsSourceProperty);
            set => SetValue(ChatItemsSourceProperty, value);
        }

        private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var panel = (VirgilChatPanel)d;
            panel.MessageList.ItemsSource = (ObservableCollection<ChatMessage>?)e.NewValue;
        }

        public VirgilChatPanel()
        {
            InitializeComponent();

            // Auto-scroll vers le bas à chaque frame si de nouveaux items arrivent
            Media.CompositionTarget.Rendering += (_, __) =>
            {
                var lb = MessageList;
                if (lb?.Items?.Count > 0)
                    lb.ScrollIntoView(lb.Items[lb.Items.Count - 1]);
            };
        }

        /// <summary>API pratique pour garder ton code appelant (MainWindow → ChatArea.Post)</summary>
        public void Post(string text, string? mood = null, int? ttlMs = null)
        {
            if (string.IsNullOrWhiteSpace(text)) return;

            var target = ChatItemsSource ??= new ObservableCollection<ChatMessage>();
            var msg = new ChatMessage { Text = text, TtlMs = ttlMs ?? 5000 };
            target.Add(msg);

            // attendre que le conteneur visuel existe puis programmer la disparition
            Dispatcher.BeginInvoke(new Action(() =>
            {
                var container = FindContainerFor(msg);
                if (container != null)
                {
                    msg.Container = container;
                    _ = ScheduleVanish(msg);
                }
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        private FrameworkElement? FindContainerFor(ChatMessage msg)
        {
            // on récupère l’item container correspondant au message
            var container = MessageList.ItemContainerGenerator.ContainerFromItem(msg) as FrameworkElement;
            if (container == null && MessageList.Items.Count > 0)
                container = MessageList.ItemContainerGenerator.ContainerFromIndex(MessageList.Items.Count - 1) as FrameworkElement;

            // si le DataTemplate contient un élément nommé "Bubble", on le prend sinon le container
            return container?.FindName("Bubble") as FrameworkElement ?? container;
        }

        private async Task ScheduleVanish(ChatMessage msg)
        {
            await Task.Delay(msg.TtlMs);
            var src = ChatItemsSource;
            if (src == null || !src.Contains(msg) || msg.Container == null) return;

            PlayThanos(msg.Container);
            FadeShrink(msg.Container, 240, () =>
            {
                src.Remove(msg);
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
            target.RenderTransformOrigin = new Wpf.Point(0.5, 0.5);
            target.RenderTransform = scale;

            var sx = new DoubleAnimation(1, 0.85, TimeSpan.FromMilliseconds(ms));
            Storyboard.SetTarget(sx, target);
            Storyboard.SetTargetProperty(sx, new PropertyPath("RenderTransform.ScaleX"));
            sb.Children.Add(sx);

            var sy = sx.Clone();
            Storyboard.SetTargetProperty(sy, new PropertyPath("RenderTransform.ScaleY"));
            sb.Children.Add(sy);

            sb.Completed += (_, __) => onDone();
            sb.Begin();
        }

        /// <summary>Effet "Thanos" (poussière) sans shader.</summary>
        private void PlayThanos(FrameworkElement source)
        {
            if (source.ActualWidth < 10 || source.ActualHeight < 10) return;

            var origin = source.TranslatePoint(new Wpf.Point(0, 0), DustLayer);
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
                WpfControls.Canvas.SetLeft(dot, origin.X + rnd.NextDouble() * source.ActualWidth);
                WpfControls.Canvas.SetTop(dot, origin.Y + rnd.NextDouble() * source.ActualHeight);
                DustLayer.Children.Add(dot);

                // destination aléatoire
                var dx = (rnd.NextDouble() - 0.2) * 120;
                var dy = -(20 + rnd.NextDouble() * 80); // un peu vers le haut
                var dur = TimeSpan.FromMilliseconds(400 + rnd.Next(0, 200));

                var ax = new DoubleAnimation(
                    WpfControls.Canvas.GetLeft(dot),
                    WpfControls.Canvas.GetLeft(dot) + dx, dur)
                { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } };

                var ay = new DoubleAnimation(
                    WpfControls.Canvas.GetTop(dot),
                    WpfControls.Canvas.GetTop(dot) + dy, dur)
                { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } };

                var ao = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(80));
                var ao2 = new DoubleAnimation(1, 0, dur) { BeginTime = TimeSpan.FromMilliseconds(80) };

                dot.BeginAnimation(UIElement.OpacityProperty, ao);
                dot.BeginAnimation(UIElement.OpacityProperty, ao2);
                dot.BeginAnimation(WpfControls.Canvas.LeftProperty, ax);
                dot.BeginAnimation(WpfControls.Canvas.TopProperty, ay);

                // nettoyage
                ao2.Completed += (_, __) => DustLayer.Children.Remove(dot);
            }
        }
    }
}
