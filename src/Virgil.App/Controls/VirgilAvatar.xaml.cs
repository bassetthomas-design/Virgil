#nullable enable
using System;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Windows.Media;

namespace Virgil.App.Controls
{
    /// <summary>
    /// Code-behind : set DataContext + animations (clignement & “breathing”).
    /// </summary>
    public partial class VirgilAvatar : UserControl
    {
        private readonly VirgilAvatarViewModel _vm = new();
        private readonly DispatcherTimer _blinkTimer = new() { Interval = TimeSpan.FromSeconds(3) };
        private readonly DispatcherTimer _breathTimer = new() { Interval = TimeSpan.FromMilliseconds(100) };
        private double _phase;

        public VirgilAvatar()
        {
            InitializeComponent();
            DataContext = _vm;

            // Clignement aléatoire
            var rnd = new Random();
            _blinkTimer.Tick += (_, __) =>
            {
                var old = _vm.EyeScale;
                _vm.EyeScale = Math.Max(0.6, old - 0.35);
                // remonte après ~120ms
                _ = Dispatcher.BeginInvoke(async () =>
                {
                    await System.Threading.Tasks.Task.Delay(120);
                    _vm.EyeScale = old;
                });
                _blinkTimer.Interval = TimeSpan.FromSeconds(rnd.Next(2, 5));
            };
            _blinkTimer.Start();

            // Breathing (léger effet de pulsation)
            _breathTimer.Tick += (_, __) =>
            {
                _phase += 0.08;
                var s = 1.0 + 0.02 * Math.Sin(_phase);
                _vm.EyeScale = s;
                var glow = 0.38 + 0.04 * (1 + Math.Sin(_phase + Math.PI / 2));
                _vm.GlowOpacity = glow;
            };
            _breathTimer.Start();
        }

        /// <summary>API appelée depuis MainWindow pour changer d’humeur.</summary>
        public void SetMood(string mood) => _vm.SetMood(mood);

        /// <summary>Expose le ViewModel si tu veux binder depuis XAML externe.</summary>
        public VirgilAvatarViewModel ViewModel => _vm;
    }
}
