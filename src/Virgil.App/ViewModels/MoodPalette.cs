using System.Windows.Media;
using Virgil.App.Core;

namespace Virgil.App.ViewModels
{
    public static class MoodPalette
    {
        public static SolidColorBrush For(Mood mood) => mood.ToString() switch
        {
            "Happy" => From("#00FFC8"),
            "Angry" => From("#FF4D4D"),
            _ => From("#B0B0B0"),
        };

        private static SolidColorBrush From(string hex)
        {
            var c = (Color)ColorConverter.ConvertFromString(hex);
            return new SolidColorBrush(c);
        }
    }
}
