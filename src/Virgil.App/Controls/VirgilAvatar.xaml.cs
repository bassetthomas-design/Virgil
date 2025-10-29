using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Virgil.App.Controls
{
    public partial class VirgilAvatar : UserControl
    {
        public VirgilAvatar()
        {
            InitializeComponent();
            UpdateVisuals();
        }

        public static readonly DependencyProperty MoodProperty =
            DependencyProperty.Register(
                nameof(Mood),
                typeof(string),
                typeof(VirgilAvatar),
                new PropertyMetadata("happy", OnMoodChanged));

        public string Mood
        {
            get => (string)GetValue(MoodProperty);
            set => SetValue(MoodProperty, value);
        }

        private static void OnMoodChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is VirgilAvatar v) v.UpdateVisuals();
        }

        /// <summary>
        /// Appel pratique depuis MainWindow : AvatarControl?.SetMood("warn")
        /// </summary>
        public void SetMood(string mood)
        {
            try { Mood = mood?.Trim().ToLowerInvariant(); } catch { }
        }

        private void UpdateVisuals()
        {
            // Map humeur -> fichier + couleur d’aura
            var (fileName, color) = Mood switch
            {
                "focused" => ("focused.png", FromHex("#0094FF")),
                "warn"    => ("warn.png",    FromHex("#FFB300")),
                "alert"   => ("alert.png",   FromHex("#FF3B30")),
                "sleepy"  => ("sleepy.png",  FromHex("#8E8E93")),
                "proud"   => ("proud.png",   FromHex("#34C759")),
                "tired"   => ("tired.png",   FromHex("#A2845E")),
                _         => ("happy.png",   FromHex("#34C759")),
            };

            var basePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory ?? "", "assets", "avatar");
            var path = System.IO.Path.Combine(basePath, fileName);

            if (File.Exists(path))
            {
                try
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.UriSource = new Uri(path, UriKind.Absolute);
                    bmp.EndInit();
                    AvatarImage.Source = bmp;
                }
                catch { AvatarImage.Source = null; }
            }
            else
            {
                AvatarImage.Source = null; // fallback si pas d’asset
            }

            if (MoodGlow.Fill is SolidColorBrush b)
            {
                b.Color = color;
            }
        }

        private static Color FromHex(string hex)
        {
            try
            {
                return (Color)ColorConverter.ConvertFromString(hex);
            }
            catch
            {
                return Colors.Black;
            }
        }
    }
}
