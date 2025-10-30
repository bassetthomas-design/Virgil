using System;
using System.IO;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Virgil.Core;

namespace Virgil.App.Controls
{
    public partial class AvatarControl : UserControl
    {
        public AvatarControl() => InitializeComponent();

        public void SetMood(string mood)
        {
            if (Enum.TryParse<Mood>(mood, true, out var m)) SetMood(m);
            else SetMood(Mood.Happy);
        }

        public void SetMood(Mood mood)
        {
            string fileName = mood switch {
                Mood.Happy   => "happy.png",
                Mood.Focused => "focused.png",
                Mood.Warn    => "warn.png",
                Mood.Alert   => "alert.png",
                Mood.Sleepy  => "sleepy.png",
                Mood.Proud   => "proud.png",
                Mood.Tired   => "tired.png",
                _            => "neutral.png"
            };

            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string path = Path.Combine(baseDir, "assets", "avatar", fileName);

                if (File.Exists(path))
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.UriSource = new Uri(path, UriKind.Absolute);
                    bmp.EndInit();
                    MoodImage.Source = bmp;
                    FallbackGlyph.Visibility = System.Windows.Visibility.Collapsed;
                }
                else
                {
                    // Fallback emoji si l'image est absente
                    MoodImage.Source = null;
                    FallbackGlyph.Text = mood switch {
                        Mood.Happy   => "😊",
                        Mood.Focused => "🧐",
                        Mood.Warn    => "😬",
                        Mood.Alert   => "😱",
                        Mood.Sleepy  => "😴",
                        Mood.Proud   => "😏",
                        Mood.Tired   => "🥱",
                        _            => "😐"
                    };
                    FallbackGlyph.Visibility = System.Windows.Visibility.Visible;
                }
            }
            catch
            {
                MoodImage.Source = null;
                FallbackGlyph.Text = "😐";
                FallbackGlyph.Visibility = System.Windows.Visibility.Visible;
            }
        }
    }
}
