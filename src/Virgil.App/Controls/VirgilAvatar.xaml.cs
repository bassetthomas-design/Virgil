using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

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
                AvatarImage.Source = null;
            }
        }
    }
}
