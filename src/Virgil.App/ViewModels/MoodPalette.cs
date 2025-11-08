using System.Windows.Media;

namespace Virgil.App.ViewModels;

public static class MoodPalette
{
    public static SolidColorBrush For(Mood m) => m switch
    {
        Mood.Happy => new SolidColorBrush(Color.FromRgb(0x5B,0x6B,0xFF)),
        Mood.Focused => new SolidColorBrush(Color.FromRgb(0x7D,0xB3,0xFF)),
        Mood.Warn => new SolidColorBrush(Color.FromRgb(0xFF,0xA8,0x49)),
        Mood.Alert => new SolidColorBrush(Color.FromRgb(0xFF,0x5C,0x5C)),
        Mood.Proud => new SolidColorBrush(Color.FromRgb(0x9A,0x7B,0xFF)),
        Mood.Tired => new SolidColorBrush(Color.FromRgb(0x9A,0x9A,0x9A)),
        _ => new SolidColorBrush(Color.FromRgb(0xA9,0xB4,0xFF))
    };
}