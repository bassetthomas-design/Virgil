using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using Virgil.App.Utils;

namespace Virgil.App.Controls
{
    public partial class VirgilAvatar : UserControl
    {
        public VirgilAvatar()
        {
            InitializeComponent();
            // Initialise l'image avec la valeur actuelle de Mood (default: neutral)
            ApplyMood(Mood);
        }

        /// <summary>
        /// Humeur actuelle de l’avatar (ex: "neutral", "happy", "focused", "warn", "alert", "sleepy", ...).
        /// </summary>
        public string Mood
        {
            get => (string)GetValue(MoodProperty);
            set => SetValue(MoodProperty, value);
        }

        public static readonly DependencyProperty MoodProperty =
            DependencyProperty.Register(
                nameof(Mood),
                typeof(string),
                typeof(VirgilAvatar),
                new PropertyMetadata("neutral", OnMoodChanged));

        private static void OnMoodChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is VirgilAvatar control)
            {
                control.ApplyMood(e.NewValue as string ?? "neutral");
            }
        }

        /// <summary>
        /// Applique visuellement l’humeur. 
        /// Ici, on essaie de charger un sprite en fonction du nom (PNG/SVG converti en PNG).
        /// Chemins d’exemple: /assets/avatar/neutral.png, /happy.png, etc.
        /// Adapte les chemins à ton repo si besoin.
        /// </summary>
        private void ApplyMood(string mood)
        {
            try
            {
                // Exemple de recherche simple de sprite dans assets/avatar/
                // Priorité: dossier app (build output) puis dossier du projet si en dev.
                var fileName = $"{mood.ToLowerInvariant()}.png";
                var probing = new[]
                {
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "avatar", fileName),
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "avatar", "default", fileName),
                };

                string? found = null;
                foreach (var p in probing)
                {
                    if (File.Exists(p)) { found = p; break; }
                }

                if (found is not null)
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.UriSource = new Uri(found, UriKind.Absolute);
                    bmp.EndInit();
                    AvatarImage.Source = bmp;
                }
                else
                {
                    // Fallback: on efface l’image si rien trouvé
                    AvatarImage.Source = null;
                }
            }
            catch
            {
                AvatarImage.Source = CreateFallback();
                StartupLog.Write($"Avatar load failed for mood {mood}");
            }
        }

        private static ImageSource CreateFallback()
        {
            var drawing = new DrawingGroup();
            drawing.Children.Add(new GeometryDrawing
            {
                Brush = new SolidColorBrush(Color.FromRgb(239, 68, 68)),
                Geometry = new EllipseGeometry(new Point(32, 32), 32, 32)
            });

            drawing.Children.Add(new GeometryDrawing
            {
                Brush = Brushes.White,
                Geometry = new GeometryGroup
                {
                    Children = new GeometryCollection
                    {
                        new RectangleGeometry(new Rect(14, 24, 12, 8)),
                        new RectangleGeometry(new Rect(38, 24, 12, 8))
                    }
                }
            });

            drawing.Children.Add(new GeometryDrawing
            {
                Pen = new Pen(Brushes.White, 3),
                Geometry = new LineGeometry(new Point(18, 46), new Point(46, 46))
            });

            return new DrawingImage(drawing);
        }
    }
}
