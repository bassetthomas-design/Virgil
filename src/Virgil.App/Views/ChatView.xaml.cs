using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Collections.Specialized;
using System.Windows.Threading;
using Virgil.App.ViewModels;

namespace Virgil.App.Views
{
    public partial class ChatView : UserControl
    {
        private readonly Random _snapRandom = new();
        private const double AutoScrollThresholdPx = 40;
        private ChatViewModel? _viewModel;
        private bool _isNearBottom = true;

        public ChatView()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
            Unloaded += OnUnloaded;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (_viewModel is null)
            {
                return;
            }

            _viewModel.SnapRequested -= OnSnapRequestedAsync;
            _viewModel.Messages.CollectionChanged -= OnMessagesCollectionChanged;
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (_viewModel is not null)
            {
                _viewModel.SnapRequested -= OnSnapRequestedAsync;
                _viewModel.Messages.CollectionChanged -= OnMessagesCollectionChanged;
            }

            _viewModel = DataContext as ChatViewModel;

            if (_viewModel is not null)
            {
                _viewModel.SnapRequested += OnSnapRequestedAsync;
                _viewModel.Messages.CollectionChanged += OnMessagesCollectionChanged;
                _isNearBottom = IsNearBottom();
            }
        }

        private void OnChatScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            _isNearBottom = IsNearBottom();
        }

        private void OnMessagesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action != NotifyCollectionChangedAction.Add || !_isNearBottom)
            {
                return;
            }

            _ = Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
            {
                if (ChatScroll is null)
                {
                    return;
                }

                ChatScroll.UpdateLayout();
                ChatScroll.ScrollToEnd();
                _isNearBottom = true;
            }));
        }

        private bool IsNearBottom()
        {
            if (ChatScroll is null)
            {
                return true;
            }

            return ChatScroll.ScrollableHeight - ChatScroll.VerticalOffset <= AutoScrollThresholdPx;
        }

        private void OnInputPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            {
                if (DataContext is ChatViewModel vm && vm.SendCommand.CanExecute(null))
                {
                    vm.SendCommand.Execute(null);
                    e.Handled = true;
                }
            }
        }

        private Task OnSnapRequestedAsync(int durationMs)
        {
            return SnapWithParticlesAsync(Math.Max(700, Math.Min(durationMs, 1200)));
        }

        private async Task SnapWithParticlesAsync(int durationMs)
        {
            if (ChatScroll is null || SnapOverlay is null || SnapImage is null || SnapDustLayer is null)
            {
                return;
            }

            if (ChatScroll.ActualWidth <= 0 || ChatScroll.ActualHeight <= 0)
            {
                return;
            }

            int width = (int)Math.Ceiling(ChatScroll.ActualWidth);
            int height = (int)Math.Ceiling(ChatScroll.ActualHeight);

            var rtb = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(ChatScroll);

            SnapImage.Source = rtb;
            SnapImage.Width = width;
            SnapImage.Height = height;

            SnapOverlay.Visibility = Visibility.Visible;
            ChatScroll.Opacity = 0;

            SnapDustLayer.Children.Clear();

            SnapImage.Opacity = 1;

            var tcs = new TaskCompletionSource<bool>();
            var particles = CreateParticles(rtb, width, height);
            var duration = TimeSpan.FromMilliseconds(durationMs);
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var lastTick = stopwatch.Elapsed;
            var gravity = _snapRandom.Next(250, 451);

            void OnRendering(object? sender, EventArgs args)
            {
                var elapsed = stopwatch.Elapsed;
                var delta = elapsed - lastTick;
                lastTick = elapsed;

                double dt = Math.Max(0.0, delta.TotalSeconds);
                double progress = Math.Min(1.0, elapsed.TotalMilliseconds / durationMs);
                double opacity = 1.0 - progress;

                SnapImage.Opacity = opacity;

                foreach (var particle in particles)
                {
                    particle.Update(dt, gravity, progress);
                }

                if (elapsed >= duration)
                {
                    CompositionTarget.Rendering -= OnRendering;
                    tcs.TrySetResult(true);
                }
            }

            CompositionTarget.Rendering += OnRendering;

            await tcs.Task;

            SnapOverlay.Visibility = Visibility.Collapsed;
            ChatScroll.Opacity = 1;
            SnapImage.Source = null;
            SnapDustLayer.Children.Clear();
        }

        private List<DustParticle> CreateParticles(RenderTargetBitmap rtb, int width, int height)
        {
            int particleCount = Math.Clamp((int)(width * height / 1400.0), 180, 450);
            particleCount = Math.Min(particleCount, 600);

            int stride = width * 4;
            var pixels = new byte[height * stride];
            rtb.CopyPixels(pixels, stride, 0);

            int gridColumns = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(particleCount * (width / (double)Math.Max(1, height)))));
            int gridRows = Math.Max(1, (int)Math.Ceiling(particleCount / (double)gridColumns));
            double cellWidth = width / (double)gridColumns;
            double cellHeight = height / (double)gridRows;

            var particles = new List<DustParticle>(particleCount);

            for (int i = 0; i < particleCount; i++)
            {
                int col = i % gridColumns;
                int row = i / gridColumns;
                double x = Math.Min(width - 1, col * cellWidth + _snapRandom.NextDouble() * cellWidth);
                double y = Math.Min(height - 1, row * cellHeight + _snapRandom.NextDouble() * cellHeight);

                int px = Math.Clamp((int)x, 0, width - 1);
                int py = Math.Clamp((int)y, 0, height - 1);
                int index = py * stride + px * 4;

                byte b = pixels[index];
                byte g = pixels[index + 1];
                byte r = pixels[index + 2];
                byte a = pixels[index + 3];

                if (a > 0)
                {
                    r = (byte)Math.Min(255, r * 255 / a);
                    g = (byte)Math.Min(255, g * 255 / a);
                    b = (byte)Math.Min(255, b * 255 / a);
                }

                double size = _snapRandom.Next(2, 7);
                double baseOpacity = 0.4 + _snapRandom.NextDouble() * 0.5;
                double vx = _snapRandom.NextDouble() * 240 - 120;
                double vy = _snapRandom.NextDouble() * 160 - 140;
                double drag = _snapRandom.NextDouble() * 0.02 + 0.97;
                double rotationSpeed = _snapRandom.NextDouble() * 120 - 60;

                var rect = new Rectangle
                {
                    Width = size,
                    Height = size,
                    Fill = new SolidColorBrush(Color.FromArgb(Math.Max((byte)40, a), r, g, b)),
                    Opacity = baseOpacity,
                    RadiusX = 0.5,
                    RadiusY = 0.5
                };

                var scale = new ScaleTransform(1, 1);
                var rotate = new RotateTransform(_snapRandom.NextDouble() * 360);
                var translate = new TranslateTransform(x, y);
                var group = new TransformGroup();
                group.Children.Add(scale);
                group.Children.Add(rotate);
                group.Children.Add(translate);
                rect.RenderTransform = group;

                SnapDustLayer.Children.Add(rect);
                particles.Add(new DustParticle(rect, scale, rotate, translate, baseOpacity, x, y, vx, vy, drag, rotationSpeed));
            }

            return particles;
        }

        private sealed class DustParticle
        {
            private readonly Rectangle _element;
            private readonly ScaleTransform _scale;
            private readonly RotateTransform _rotate;
            private readonly TranslateTransform _translate;
            private readonly double _baseOpacity;
            private readonly double _drag;
            private readonly double _rotationSpeed;

            private double _x;
            private double _y;
            private double _vx;
            private double _vy;
            private double _rotation;

            public DustParticle(Rectangle element, ScaleTransform scale, RotateTransform rotate, TranslateTransform translate, double baseOpacity, double x, double y, double vx, double vy, double drag, double rotationSpeed)
            {
                _element = element;
                _scale = scale;
                _rotate = rotate;
                _translate = translate;
                _baseOpacity = baseOpacity;
                _x = x;
                _y = y;
                _vx = vx;
                _vy = vy;
                _drag = drag;
                _rotationSpeed = rotationSpeed;
                _rotation = _rotate.Angle;
            }

            public void Update(double dt, double gravity, double progress)
            {
                _vx *= _drag;
                _vy = (_vy * _drag) + gravity * dt;
                _x += _vx * dt;
                _y += _vy * dt;
                _rotation += _rotationSpeed * dt;

                double scale = 1.0 - 0.2 * progress;
                _scale.ScaleX = scale;
                _scale.ScaleY = scale;
                _rotate.Angle = _rotation;
                _translate.X = _x;
                _translate.Y = _y;
                _element.Opacity = _baseOpacity * (1.0 - progress);
            }
        }
    }
}
