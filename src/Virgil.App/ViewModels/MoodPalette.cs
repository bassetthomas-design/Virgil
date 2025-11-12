using System.Windows.Media;
using Virgil.App.Core;

namespace Virgil.App.ViewModels
{
    public static class MoodPalette
    {
        // Palette centrale pour l’avatar (yeux/aura)
        public static SolidColorBrush For(Mood mood) => mood switch
        {
            Mood.Happy   => From("#00FFC8"),
            Mood.Tired   => From("#6F7A8C"),
            Mood.Angry   => From("#FF4D4D"),
            Mood.Focused => From("#5AD1FF"),
            _            => From("#B0B0B0"),
        };

        private static SolidColorBrush From(string hex)
        {
            var c = (Color)ColorConverter.ConvertFromString(hex);
            return new SolidColorBrush(c);
        }
    }
}
