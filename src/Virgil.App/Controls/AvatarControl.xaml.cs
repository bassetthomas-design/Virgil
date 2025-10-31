// src/Virgil.App/Controls/AvatarControl.xaml.cs
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Virgil.Core; // << important : Mood est dans Virgil.Core

namespace Virgil.App.Controls
{
    public partial class AvatarControl : UserControl
    {
        public static readonly DependencyProperty MoodProperty =
            DependencyProperty.Register(
                nameof(Mood),
                typeof(Mood),
                typeof(AvatarControl),
                new PropertyMetadata(Mood.Focused, OnMoodChanged));

        public Mood Mood
        {
            get => (Mood)GetValue(MoodProperty);
            set => SetValue(MoodProperty, value);
        }

        private readonly DispatcherTimer _blinkTimer = new DispatcherTimer();

        public AvatarControl()
        {
            InitializeComponent();

            // clignement simple (si ton XAML a une Image nommée SpriteImage)
            _blinkTimer.Interval = TimeSpan.FromSeconds(4);
            _blinkTimer.Tick += (_, __) => BlinkOnce();
            _blinkTimer.Start();

            // mood par défaut
            ApplyMoodSprite(Mood);
        }

        private static void OnMoodChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is AvatarControl ctrl && e.NewValue is Mood m)
                ctrl.ApplyMoodSprite(m);
        }

        private void ApplyMoodSprite(Mood mood)
        {
            // Mappe l’énum vers tes sprites (à ajuster selon tes assets)
            // ex. pack URI: "pack://application:,,,/Virgil.App;component/Assets/Avatar/mood_focused.png"
           string key = mood switch
{
    Mood.Happy    => "mood_happy.png",
    Mood.Warn     => "mood_warn.png",
    Mood.Alert    => "mood_alert.png",
    Mood.Sleepy   => "mood_sleepy.png",
    Mood.Proud    => "mood_proud.png",
    Mood.Tired    => "mood_tired.png",
    Mood.Neutral  => "mood_focused.png",  // fallback provisoire
    Mood.Vigilant => "mood_focused.png",  // fallback provisoire
    Mood.Resting  => "mood_sleepy.png",   // fallback provisoire
    _             => "mood_focused.png",
};


            try
            {
                var uri = new Uri($"pack://application:,,,/Virgil.App;component/Assets/Avatar/{key}", UriKind.Absolute);
                SpriteImage.Source = new BitmapImage(uri);
            }
            catch
            {
                // fallback silencieux si le sprite n’existe pas
            }
        }

        private async void BlinkOnce()
        {
            // Si tu as deux images superposées (œil ouvert/fermé), gère l’opacité ici
            // Ici on fait un blink “rapide” en jouant sur l’opacité de SpriteImage (exemple minimal)
            var s = SpriteImage.Opacity;
            SpriteImage.Opacity = 0.2;
            await System.Threading.Tasks.Task.Delay(120);
            SpriteImage.Opacity = s;
        }
    }
}
