using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Virgil.App.Controls
{
    public partial class AvatarControl : UserControl
    {
        public AvatarControl()
        {
            InitializeComponent();
            UpdateMoodVisuals();
        }

        #region Dependency Properties

        public static readonly DependencyProperty MoodProperty =
            DependencyProperty.Register(
                nameof(Mood),
                typeof(string),
                typeof(AvatarControl),
                new PropertyMetadata("happy", OnMoodChanged));

        /// <summary>
        /// Humeur courante: happy|focused|warn|alert|sleepy|proud|tired
        /// </summary>
        public string Mood
        {
            get => (string)GetValue(MoodProperty);
            set => SetValue(MoodProperty, value);
        }

        public static readonly DependencyProperty AssetsRootProperty =
            DependencyProperty.Register(
                nameof(AssetsRoot),
                typeof(string),
                typeof(AvatarControl),
                new PropertyMetadata("assets/avatar", OnMoodChanged));

        /// <summary>
        /// Dossier racine des sprites (ex: assets/avatar)
        /// Attendu: {AssetsRoot}/{mood}.png  (ou .svg si tu as un renderer)
        /// </summary>
        public string AssetsRoot
        {
            get => (string)GetValue(AssetsRootProperty);
            set => SetValue(AssetsRootProperty, value);
        }

        #endregion

        #region Bindable (for XAML bindings in this control)

        public Brush AvatarBackgroundBrush
        {
            get
            {
                // Couleur de fond selon humeur
                switch ((Mood ?? string.Empty).ToLowerInvariant())
                {
                    case "happy":   return new SolidColorBrush(Color.FromRgb(0xC8, 0xFF, 0xC8));
                    case "focused": return new SolidColorBrush(Color.FromRgb(0xC8, 0xE8, 0xFF));
                    case "warn":    return new SolidColorBrush(Color.FromRgb(0xFF, 0xF1, 0xC8));
                    case "alert":   return new SolidColorBrush(Color.FromRgb(0xFF, 0xC8, 0xC8));
                    case "sleepy":  return new SolidColorBrush(Color.FromRgb(0xE8, 0xE8, 0xE8));
                    case "proud":   return new SolidColorBrush(Color.FromRgb(0xE9, 0xDA, 0xFF));
                    case "tired":   return new SolidColorBrush(Color.FromRgb(0xDD, 0xEE, 0xF4));
                    default:        return new SolidColorBrush(Color.FromRgb(0xF0, 0xF0, 0xF0));
                }
            }
        }

        public string MoodGlyph
        {
            get
            {
                // Emoji fallback si pas d’image trouvée
                switch ((Mood ?? string.Empty).ToLowerInvariant())
                {
                    case "happy":   return "🙂";
                    case "focused": return "🧐";
                    case "warn":    return "😬";
                    case "alert":   return "😱";
                    case "sleepy":  return "😴";
                    case "proud":   return "😎";
                    case "tired":   return "🥱";
                    default:        return "🤖";
                }
            }
        }

        public string MoodText
        {
            get
            {
                switch ((Mood ?? string.Empty).ToLowerInvariant())
                {
                    case "happy":   return "Humeur : Happy";
                    case "focused": return "Humeur : Focused";
                    case "warn":    return "Humeur : Pré-alerte";
                    case "alert":   return "Humeur : Alerte";
                    case "sleepy":  return "Humeur : Sleepy";
                    case "proud":   return "Humeur : Proud";
                    case "tired":   return "Humeur : Tired";
                    default:        return $"Humeur : {Mood}";
                }
            }
        }

        #endregion

        private static void OnMoodChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is AvatarControl ctrl)
                ctrl.UpdateMoodVisuals();
        }

        private void UpdateMoodVisuals()
        {
            // Met à jour brosses/labels liés
            AvatarBorder.Background = AvatarBackgroundBrush;
            MoodLabel.DataContext = this;

            // Tente de charger une image spécifique à l’humeur
            // Convention: {AssetsRoot}/{mood}.png
            var mood = (Mood ?? "happy").ToLowerInvariant();
            var root = AssetsRoot ?? "assets/avatar";
            var candidate = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory ?? "", root, $"{mood}.png");

            if (File.Exists(candidate))
            {
                try
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.UriSource = new Uri(candidate, UriKind.Absolute);
                    bmp.EndInit();
                    bmp.Freeze();

                    MoodImage.Source = bmp;
                    MoodImage.Visibility = Visibility.Visible;
                    MoodGlyph.Visibility = Visibility.Collapsed;
                }
                catch
                {
                    // fallback emoji
                    MoodImage.Source = null;
                    MoodImage.Visibility = Visibility.Collapsed;
                    MoodGlyph.Visibility = Visibility.Visible;
                }
            }
            else
            {
                // Pas d’image => fallback
                MoodImage.Source = null;
                MoodImage.Visibility = Visibility.Collapsed;
                MoodGlyph.Visibility = Visibility.Visible;
            }
        }

        /// <summary>
        /// API simple appelée depuis MainWindow: AvatarControl.SetMood("happy")
        /// </summary>
        public void SetMood(string mood)
        {
            Mood = mood;
            UpdateMoodVisuals();
        }
    }
}
