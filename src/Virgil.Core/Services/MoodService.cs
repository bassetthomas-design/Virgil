namespace Virgil.Core.Services
{
    /// <summary>
    /// Couleur simple (pas de dépendance WPF).
    /// </summary>
    public readonly struct Rgb
    {
        public readonly byte R;
        public readonly byte G;
        public readonly byte B;

        public Rgb(byte r, byte g, byte b)
        {
            R = r; G = g; B = b;
        }
    }

    /// <summary>
    /// Map d’humeurs -> couleurs + phrase par défaut.
    /// (Pas de référence à System.Windows.* ici.)
    /// </summary>
    public sealed class MoodService
    {
        public Rgb ResolveColor(string? moodKey)
        {
            if (string.IsNullOrWhiteSpace(moodKey)) return Neutral;

            switch (moodKey.Trim().ToLowerInvariant())
            {
                case "neutral":
                case "ok":
                    return Neutral;

                case "vigilant":
                case "warn":
                    return Vigilant;

                case "alert":
                case "hot":
                    return Alert;

                case "rest":
                case "resting":
                    return Resting;

                case "proud":
                case "success":
                    return Proud;

                default:
                    return Neutral;
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

        // Palette (RGB)
        private static Rgb Neutral  => new(0x40, 0xA0, 0xFF); // bleu clair
        private static Rgb Vigilant => new(0xFF, 0xB0, 0x2D); // orange
        private static Rgb Alert    => new(0xFF, 0x45, 0x45); // rouge
        private static Rgb Resting  => new(0x3C, 0xE0, 0xD0); // cyan doux
        private static Rgb Proud    => new(0x32, 0xD0, 0x5F); // vert
    }
}
