using System;

namespace Virgil.Core.Services
{
    /// <summary>
    /// Maps mood keywords to simple RGB colours and default phrases. This version has
    /// been extended with additional moods such as excited and anxious (see issue #150).
    /// No WPF dependencies are introduced here.
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
                case "happy":
                    return Proud;
                case "tired":
                case "sleepy":
                    return Tired;
                case "excited":
                    return Excited;
                case "anxious":
                    return Anxious;
                case "focused":
                    return Neutral;
                default:
                    return Neutral;
            }
        }

        public string DefaultLine(string moodKey)
        {
            switch (moodKey.Trim().ToLowerInvariant())
            {
                case "vigilant": return "Je garde un œil 👀";
                case "alert":    return "Attention, ça chauffe !";
                case "resting":  return "Repos…";
                case "proud":    return "Mission accomplie ✅";
                case "happy":    return "Quelle belle journée !";
                case "tired":    return "Un petit somme s'impose…";
                case "excited":  return "Ça bouge !";
                case "anxious":  return "Je reste prudent…";
                case "focused":  return "Concentré sur la tâche.";
                default:          return "Tout roule.";
            }
        }

        // Palette (RGB)
        private static Rgb Neutral  => new(0x40, 0xA0, 0xFF); // bleu clair
        private static Rgb Vigilant => new(0xFF, 0xB0, 0x2D); // orange
        private static Rgb Alert    => new(0xFF, 0x45, 0x45); // rouge
        private static Rgb Resting  => new(0x3C, 0xE0, 0xD0); // cyan doux
        private static Rgb Proud    => new(0x32, 0xD0, 0x5F); // vert
        private static Rgb Tired    => new(0xB0, 0xC4, 0xDE); // acier léger
        private static Rgb Excited  => new(0xFF, 0xD7, 0x00); // or
        private static Rgb Anxious  => new(0x8B, 0x00, 0x8B); // magenta sombre
    }
}
