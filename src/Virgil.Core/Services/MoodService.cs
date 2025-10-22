using System.Windows.Media;

namespace Virgil.Core.Services
{
    /// <summary>
    /// Cartographie des humeurs -> couleurs. Simple et locale.
    /// </summary>
    public sealed class MoodService
    {
        public Color ResolveColor(string moodKey)
        {
            if (string.IsNullOrWhiteSpace(moodKey)) return Neutral;
            switch (moodKey.Trim().ToLowerInvariant())
            {
                case "neutral":
                case "ok":        return Neutral;
                case "vigilant":
                case "warn":      return Vigilant;
                case "alert":
                case "hot":       return Alert;
                case "rest":
                case "resting":   return Resting;
                case "proud":
                case "success":   return Proud;
                default:          return Neutral;
            }
        }

        public string DefaultLine(string moodKey)
        {
            switch (moodKey.Trim().ToLowerInvariant())
            {
                case "vigilant": return "Je garde un œil 👀";
                case "alert":    return "Attention, ça monte !";
                case "resting":  return "Repos…";
                case "proud":    return "Mission accomplie ✅";
                default:         return "Tout roule.";
            }
        }

        // Palette
        private static Color Neutral  => Color.FromRgb(0x40, 0xA0, 0xFF); // bleu clair
        private static Color Vigilant => Color.FromRgb(0xFF, 0xB0, 0x2D); // orange
        private static Color Alert    => Color.FromRgb(0xFF, 0x45, 0x45); // rouge
        private static Color Resting  => Color.FromRgb(0x3C, 0xE0, 0xD0); // cyan doux
        private static Color Proud    => Color.FromRgb(0x32, 0xD0, 0x5F); // vert
    }
}
