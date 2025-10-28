using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Virgil.App.Controls
{
    public partial class AvatarControl : UserControl
    {
        public AvatarControl() { InitializeComponent(); UpdateMoodBrush(); }

        public static readonly DependencyProperty MoodProperty =
            DependencyProperty.Register(nameof(Mood), typeof(string), typeof(AvatarControl),
                new PropertyMetadata("happy", OnMoodChanged));

        public string Mood
        {
            get => (string)GetValue(MoodProperty);
            set => SetValue(MoodProperty, value);
        }

        private static void OnMoodChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is AvatarControl ac) ac.UpdateMoodBrush();
        }

        private void UpdateMoodBrush()
        {
            // mapping simple (tu brancheras sur tes humeurs réelles)
            var color = Mood switch
            {
                "happy"   => Color.FromRgb( 88, 201,  72),
                "focused" => Color.FromRgb( 72, 155, 201),
                "warn"    => Color.FromRgb(232, 176,  58),
                "alert"   => Color.FromRgb(224,  80,  80),
                "sleepy"  => Color.FromRgb(145, 145, 145),
                _         => Color.FromRgb( 58,  70,  99),
            };
            (FindName("MoodBrush") as SolidColorBrush)!.Color = color;
        }
    }
}
