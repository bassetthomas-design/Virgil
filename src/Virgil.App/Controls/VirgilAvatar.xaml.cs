#nullable enable
using System;
using System.Windows;                    // WPF
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Virgil.App.Controls
{
    // ⚠️ on force la classe de base WPF pour éviter tout conflit avec WinForms
    public partial class VirgilAvatar : System.Windows.Controls.UserControl
    {
        public VirgilAvatar()
        {
            InitializeComponent();

            // Si aucun DataContext défini côté XAML, on instancie le ViewModel par défaut
            if (DataContext == null)
                DataContext = new VirgilAvatarViewModel();
        }

        // Méthode utilitaire appelée depuis MainWindow pour changer l’humeur
        public void SetMood(string mood)
        {
            if (DataContext is VirgilAvatarViewModel vm)
                vm.SetMood(mood);
        }

        // (Optionnel) Expose un petit “nudge” visuel
        public void Nudge()
        {
            try
            {
                var sb = new Storyboard();
                var anim = new DoubleAnimation
                {
                    From = 1.0,
                    To = 1.06,
                    Duration = TimeSpan.FromMilliseconds(120),
                    AutoReverse = true,
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                };
                Storyboard.SetTarget(anim, this);
                Storyboard.SetTargetProperty(anim, new PropertyPath("LayoutTransform.ScaleX"));
                sb.Children.Add(anim);

                var animY = anim.Clone();
                Storyboard.SetTargetProperty(animY, new PropertyPath("LayoutTransform.ScaleY"));
                sb.Children.Add(animY);

                if (LayoutTransform == Transform.Identity)
                    LayoutTransform = new ScaleTransform(1, 1);

                sb.Begin();
            }
            catch { /* effet non bloquant */ }
        }
    }
}
