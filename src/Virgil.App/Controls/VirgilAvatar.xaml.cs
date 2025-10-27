using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Virgil.App.Controls
{
    public partial class VirgilAvatar : UserControl
    {
        public VirgilAvatar()
        {
            InitializeComponent();
            // Sécurité : on force les yeux par défaut au démarrage
            ShowEyes("default");
        }

        // Couleur du visage (liaison XAML)
        public static readonly DependencyProperty FaceFillProperty =
            DependencyProperty.Register(
                nameof(FaceFill),
                typeof(Brush),
                typeof(VirgilAvatar),
                new PropertyMetadata(new SolidColorBrush(Color.FromRgb(0x4F, 0x6D, 0x3A)))); // vert doux

        public Brush FaceFill
        {
            get => (Brush)GetValue(FaceFillProperty);
            set => SetValue(FaceFillProperty, value);
        }

        // Helpers pour basculer les groupes d’yeux et décos
        private IEnumerable<UIElement> AllEyeGroups()
        {
            yield return EyesDefault;
            yield return EyesAngry;
            yield return EyesLove;
            yield return EyesTear;
            yield return EyesSleepy;
            yield return EyesWink;
            yield return EyesCat;
        }

        private void ShowEyes(string preset)
        {
            foreach (var g in AllEyeGroups()) g.Visibility = Visibility.Collapsed;

            switch ((preset ?? "default").ToLowerInvariant())
            {
                case "angry":  EyesAngry.Visibility  = Visibility.Visible;  break;
                case "love":   EyesLove.Visibility   = Visibility.Visible;  break;
                case "tear":   EyesTear.Visibility   = Visibility.Visible;  break;
                case "sleepy": EyesSleepy.Visibility = Visibility.Visible;  break;
                case "wink":   EyesWink.Visibility   = Visibility.Visible;  break;
                case "cat":    EyesCat.Visibility    = Visibility.Visible;  break;
                default:       EyesDefault.Visibility = Visibility.Visible;  break;
            }
        }

        private void ShowCat(bool on)
        {
            CatDecor.Visibility   = on ? Visibility.Visible : Visibility.Collapsed;
        }

        private void ShowDevil(bool on)
        {
            DevilDecor.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>
        /// Humeurs supportées (mapping visuel collé à l’image) :
        /// - "neutral"  → yeux ronds blancs
        /// - "angry"    → yeux plissés (obliques)
        /// - "love"     → cœurs roses
        /// - "tear"     → larme à droite
        /// - "sleepy"   → demi-lunes
        /// - "wink"     → clin d'œil
        /// - "cat"      → oreilles+moustaches + yeux ronds blancs
        /// - "devil"    → cornes + yeux plissés ; disque rouge
        /// </summary>
        public void SetMood(string mood)
        {
            var key = (mood ?? "neutral").Trim().ToLowerInvariant();

            // Couleur du visage + halo selon la “famille”
            Color face = Color.FromRgb(0x4F, 0x6D, 0x3A); // vert de base
            if (key == "devil") face = Color.FromRgb(0xD9, 0x3D, 0x3D); // rouge
            if (key == "love")  face = Color.FromRgb(0x4F, 0x6D, 0x3A); // reste vert sur la planche

            FaceFill = new SolidColorBrush(face);
            if (Glow != null)
            {
                var halo = Color.FromArgb(0x55, face.R, face.G, face.B);
                Glow.Fill = new SolidColorBrush(halo);
            }

            // Décors spéciaux
            ShowCat(key == "cat");
            ShowDevil(key == "devil");

            // Choix des yeux (toujours blancs, sauf "love" → cœurs roses)
            var eyesPreset = key switch
            {
                "angry"  => "angry",
                "love"   => "love",
                "tear"   => "tear",
                "sleepy" => "sleepy",
                "wink"   => "wink",
                "cat"    => "cat",
                "devil"  => "angry", // comme sur la planche : yeux plissés + cornes
                _        => "default"
            };
            ShowEyes(eyesPreset);

            // Animation de halo
            try { if (FindResource("MoodPulse") is Storyboard sb) sb.Begin(); } catch { }
        }
    }
}
