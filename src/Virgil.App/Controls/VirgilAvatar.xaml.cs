#nullable enable
using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Controls;
using SW = System.Windows;

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

        public void SetMood(string mood)
        {
            VM.SetMood(mood);

            if (RoundEyes != null) RoundEyes.Visibility = VM.UseRoundEyes ? Visibility.Visible : Visibility.Collapsed;
            if (Tear != null) Tear.Visibility = VM.ShowTear ? Visibility.Visible : Visibility.Collapsed;
            if (Hearts != null) Hearts.Visibility = VM.ShowHearts ? Visibility.Visible : Visibility.Collapsed;
            if (CatAddons != null) CatAddons.Visibility = VM.ShowCat ? Visibility.Visible : Visibility.Collapsed;
            if (DevilHorns != null) DevilHorns.Visibility = VM.ShowDevil ? Visibility.Visible : Visibility.Collapsed;

            if (LeftEye != null && RightEye != null)
            {
                var vis = VM.UseRoundEyes ? Visibility.Collapsed : Visibility.Visible;
                LeftEye.Visibility = vis;
                RightEye.Visibility = vis;
            }

            ApplyTransforms(animated: true);
        }

        public void SetExpression(string expr) => SetMood(expr);

        public void SetProgress(double percent)
        {
            if (AvatarProgress == null) return;

            if (percent < 0)
            {
                AvatarProgress.Visibility = Visibility.Collapsed;
                AvatarProgress.IsIndeterminate = false;
                return;
            }

            AvatarProgress.Visibility = Visibility.Visible;
            AvatarProgress.IsIndeterminate = false;
            AvatarProgress.Value = Math.Max(0, Math.Min(100, percent));
        }

        public void SetProgressIndeterminate(bool on)
        {
            if (AvatarProgress == null) return;
            AvatarProgress.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
            AvatarProgress.IsIndeterminate = on;
        }

        private void ApplyTransforms(bool animated)
        {
            void Lerp(Action<double> setter, double from, double to, int ms = 220)
            {
                var t0 = from; var t1 = to;
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var timer = new SW.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
                timer.Tick += (_, __) =>
                {
                    var p = Math.Min(1.0, sw.Elapsed.TotalMilliseconds / ms);
                    var eased = 1 - Math.Pow(1 - p, 2);
                    setter(t0 + (t1 - t0) * eased);
                    if (p >= 1) timer.Stop();
                };
                timer.Start();
            }

            if (LE_S == null || RE_S == null || LE_R == null || RE_R == null || LE_T == null || RE_T == null)
                return;

            if (!animated)
            {
                LE_S.ScaleX = VM.EyeScale; LE_S.ScaleY = VM.EyeScale;
                LE_R.Angle = -VM.EyeTilt;
                LE_T.X = -VM.EyeSeparation;
                LE_T.Y = VM.EyeY;

                RE_S.ScaleX = -VM.EyeScale; RE_S.ScaleY = VM.EyeScale;
                RE_R.Angle = VM.EyeTilt;
                RE_T.X = VM.EyeSeparation;
                RE_T.Y = VM.EyeY;
            }
            else
            {
                Lerp(v => LE_S.ScaleX = v, LE_S.ScaleX, VM.EyeScale);
                Lerp(v => LE_S.ScaleY = v, LE_S.ScaleY, VM.EyeScale);
                Lerp(v => LE_R.Angle = v, LE_R.Angle, -VM.EyeTilt);
                Lerp(v => LE_T.X = v, LE_T.X, -VM.EyeSeparation);
                Lerp(v => LE_T.Y = v, LE_T.Y, VM.EyeY);

                Lerp(v => RE_S.ScaleX = v, RE_S.ScaleX, -VM.EyeScale);
                Lerp(v => RE_S.ScaleY = v, RE_S.ScaleY, VM.EyeScale);
                Lerp(v => RE_R.Angle = v, RE_R.Angle, VM.EyeTilt);
                Lerp(v => RE_T.X = v, RE_T.X, VM.EyeSeparation);
                Lerp(v => RE_T.Y = v, RE_T.Y, VM.EyeY);
            }
        }
    }
}
