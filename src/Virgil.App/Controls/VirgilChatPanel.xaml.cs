#nullable enable
using System;
using System.Collections;
using System.Collections.Specialized;
using System.Windows;
using Controls = System.Windows.Controls;
using Media = System.Windows.Media;
using Shapes = System.Windows.Shapes;

namespace Virgil.App.Controls
{
    public partial class VirgilChatPanel : Controls.UserControl
    {
        public VirgilChatPanel()
        {
            InitializeComponent();
            Loaded += (_, __) =>
            {
                // auto-scroll à l’arrivée d’éléments
                CompositionTarget.Rendering += (_, __) => Scroller?.ScrollToEnd();
                HookCollectionChanged();
            };
        }

        // ItemsSource (on laisse MainWindow posséder la collection)
        public static readonly DependencyProperty ItemsSourceProperty =
            DependencyProperty.Register(nameof(ItemsSource), typeof(IEnumerable),
                typeof(VirgilChatPanel),
                new PropertyMetadata(null, OnItemsSourceChanged));

        public IEnumerable? ItemsSource
        {
            get => (IEnumerable?)GetValue(ItemsSourceProperty);
            set => SetValue(ItemsSourceProperty, value);
        }

        private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var self = (VirgilChatPanel)d;
            self.List.ItemsSource = self.ItemsSource;
            self.HookCollectionChanged();
        }

        private void HookCollectionChanged()
        {
            if (ItemsSource is INotifyCollectionChanged ncc)
            {
                ncc.CollectionChanged -= Items_CollectionChanged;
                ncc.CollectionChanged += Items_CollectionChanged;
            }
        }

        private void Items_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            // rien d’obligatoire ici; l’effet est déclenché par MainWindow via TriggerDustFor(...)
        }

        public void ScrollToEnd() => Scroller?.ScrollToEnd();

        /// <summary>Déclenche l’effet poussière (“Thanos”) pour un item existant.</summary>
        public void TriggerDustFor(object item)
        {
            var container = (FrameworkElement?)List.ItemContainerGenerator.ContainerFromItem(item);
            if (container == null) return;

            var bubble = FindDescendantByName(container, "Bubble") as FrameworkElement ?? container;
            if (bubble == null) return;

            PlayThanos(bubble);
        }

        private static FrameworkElement? FindDescendantByName(FrameworkElement root, string name)
        {
            if (root.Name == name) return root;
            int count = Media.VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < count; i++)
            {
                var child = Media.VisualTreeHelper.GetChild(root, i) as FrameworkElement;
                var r = child != null ? FindDescendantByName(child, name) : null;
                if (r != null) return r;
            }
            return null;
        }

        private void PlayThanos(FrameworkElement source)
        {
            if (source.ActualWidth < 8 || source.ActualHeight < 8) return;

            var origin = source.TranslatePoint(new Point(0, 0), DustLayer);
            var rnd = new Random();

            int count = Math.Clamp((int)(source.ActualWidth * source.ActualHeight / 550), 12, 40);
            for (int i = 0; i < count; i++)
            {
                var dot = new Shapes.Ellipse
                {
                    Width = rnd.Next(2, 5),
                    Height = rnd.Next(2, 5),
                    Fill = new Media.SolidColorBrush(Media.Color.FromArgb(220, 255, 255, 255)),
                    Opacity = 0.0
                };
                Controls.Canvas.SetLeft(dot, origin.X + rnd.NextDouble() * source.ActualWidth);
                Controls.Canvas.SetTop(dot, origin.Y + rnd.NextDouble() * source.ActualHeight);
                DustLayer.Children.Add(dot);

                var dx = (rnd.NextDouble() - 0.2) * 120;
                var dy = -(20 + rnd.NextDouble() * 80);
                var dur = TimeSpan.FromMilliseconds(400 + rnd.Next(0, 200));

                var ax = new System.Windows.Media.Animation.DoubleAnimation(
                    Controls.Canvas.GetLeft(dot), Controls.Canvas.GetLeft(dot) + dx, dur)
                { EasingFunction = new System.Windows.Media.Animation.QuadraticEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut } };

                var ay = new System.Windows.Media.Animation.DoubleAnimation(
                    Controls.Canvas.GetTop(dot), Controls.Canvas.GetTop(dot) + dy, dur)
                { EasingFunction = new System.Windows.Media.Animation.QuadraticEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut } };

                var ao = new System.Windows.Media.Animation.DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(80));
                var ao2 = new System.Windows.Media.Animation.DoubleAnimation(1, 0, dur) { BeginTime = TimeSpan.FromMilliseconds(80) };

                dot.BeginAnimation(UIElement.OpacityProperty, ao);
                dot.BeginAnimation(UIElement.OpacityProperty, ao2);
                dot.BeginAnimation(Controls.Canvas.LeftProperty, ax);
                dot.BeginAnimation(Controls.Canvas.TopProperty, ay);

                ao2.Completed += (_, __) => DustLayer.Children.Remove(dot);
            }
        }
    }
}
