using System;
using System.Windows;
using System.Windows.Media.Animation;

namespace Virgil.App.Controls
{
    public partial class VirgilAvatar : System.Windows.Controls.UserControl
    {
        private VirgilAvatarViewModel VM => (VirgilAvatarViewModel)DataContext;

        public VirgilAvatar()
        {
            InitializeComponent();

            if (DataContext == null)
                DataContext = new VirgilAvatarViewModel();

            ApplyTransforms(animated: false);
        }

        /// <summary>
        /// Définit l’humeur (et ses variantes visuelles). 
        /// </summary>
        public void SetMood(string mood)
        {
            VM.SetMood(mood);

            // Afficher / masquer les éléments optionnels (si présents dans le XAML)
            SafeSetVisibility(nameof(RoundEyes), VM.UseRoundEyes);
            SafeSetVisibility(nameof(Tear),      VM.ShowTear);
            SafeSetVisibility(nameof(Hearts),    VM.ShowHearts);
            SafeSetVisibility(nameof(CatAddons), VM.ShowCat);
            SafeSetVisibility(nameof(DevilHorns),VM.ShowDevil);

            // Yeux amande vs ronds : si yeux ronds ON, on masque les amandes
            if (FindName(nameof(LeftEye))  is FrameworkElement le)  le.Visibility  = VM.UseRoundEyes ? Visibility.Collapsed : Visibility.Visible;
            if (FindName(nameof(RightEye)) is FrameworkElement re)  re.Visibility  = VM.UseRoundEyes ? Visibility.Collapsed : Visibility.Visible;

            ApplyTransforms(animated: true);
        }

        public void SetExpression(string expr) => SetMood(expr);

        /// <summary>
        /// Affiche une progression (0–100). Passer une valeur négative pour masquer.
        /// </summary>
        public void SetProgress(double percent)
        {
            if (FindName(nameof(AvatarProgress)) is not System.Windows.Controls.ProgressBar bar)
                return;

            if (percent < 0)
            {
                bar.Visibility = Visibility.Collapsed;
                bar.IsIndeterminate = false;
                return;
            }

            bar.Visibility = Visibility.Visible;
            bar.IsIndeterminate = false;
            bar.Value = Math.Max(0, Math.Min(100, percent));
        }

        /// <summary>
        /// Active/Désactive le mode indéterminé (spinner).
        /// </summary>
        public void SetProgressIndeterminate(bool on)
        {
            if (FindName(nameof(AvatarProgress)) is not System.Windows.Controls.ProgressBar bar)
                return;

            bar.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
            bar.IsIndeterminate = on;
        }

        /// <summary>
        /// Applique les transformations sur les yeux (scale/rotation/déplacement),
        /// sans timer custom : on utilise DoubleAnimation (WPF natif).
        /// </summary>
        private void ApplyTransforms(bool animated)
        {
            // Raccourci animation : anime une DP double si l’élément et la prop existent
            void Anim(DependencyObject target, DependencyProperty dp, double to, int ms = 220)
            {
                if (target is null || dp is null) return;
                if (!animated)
                {
                    target.SetValue(dp, to);
                    return;
                }
                var da = new DoubleAnimation
                {
                    To = to,
                    Duration = TimeSpan.FromMilliseconds(ms),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                };
                (target as IAnimatable)?.BeginAnimation(dp, da);
            }

            // Les transforms doivent exister dans le XAML :
            // LE_S / RE_S : ScaleTransform
            // LE_R / RE_R : RotateTransform
            // LE_T / RE_T : TranslateTransform
            // Si un nom est absent, on ignore proprement.

            var eyeScale = VM.EyeScale;
            var eyeTilt  = VM.EyeTilt;
            var eyeSep   = VM.EyeSeparation;
            var eyeY     = VM.EyeY;

            // Left eye (scale/rot/translate)
            if (FindName(nameof(LE_S)) is System.Windows.Media.ScaleTransform LE_S)
            {
                Anim(LE_S, System.Windows.Media.ScaleTransform.ScaleXProperty, eyeScale);
                Anim(LE_S, System.Windows.Media.ScaleTransform.ScaleYProperty, eyeScale);
            }
            if (FindName(nameof(LE_R)) is System.Windows.Media.RotateTransform LE_R)
            {
                Anim(LE_R, System.Windows.Media.RotateTransform.AngleProperty, -eyeTilt);
            }
            if (FindName(nameof(LE_T)) is System.Windows.Media.TranslateTransform LE_T)
            {
                Anim(LE_T, System.Windows.Media.TranslateTransform.XProperty, -eyeSep);
                Anim(LE_T, System.Windows.Media.TranslateTransform.YProperty,  eyeY);
            }

            // Right eye (scale inversé en X pour symétrie, rot/translate)
            if (FindName(nameof(RE_S)) is System.Windows.Media.ScaleTransform RE_S)
            {
                Anim(RE_S, System.Windows.Media.ScaleTransform.ScaleXProperty, -eyeScale);
                Anim(RE_S, System.Windows.Media.ScaleTransform.ScaleYProperty,  eyeScale);
            }
            if (FindName(nameof(RE_R)) is System.Windows.Media.RotateTransform RE_R)
            {
                Anim(RE_R, System.Windows.Media.RotateTransform.AngleProperty, eyeTilt);
            }
            if (FindName(nameof(RE_T)) is System.Windows.Media.TranslateTransform RE_T)
            {
                Anim(RE_T, System.Windows.Media.TranslateTransform.XProperty, eyeSep);
                Anim(RE_T, System.Windows.Media.TranslateTransform.YProperty, eyeY);
            }
        }

        /// <summary>
        /// Utilitaire : applique Visibility sur un élément nommé s’il existe.
        /// </summary>
        private void SafeSetVisibility(string elementName, bool visible)
        {
            if (FindName(elementName) is FrameworkElement fe)
                fe.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}
