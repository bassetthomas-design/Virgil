using System.Windows.Media;

namespace Virgil.App.ViewModels;

public static class MoodPalette
{
    public static SolidColorBrush For(Mood m) => m switch
    {
        Mood.Happy => Brush(0x5B,0x6B,0xFF),
        Mood.Focused => Brush(0x7D,0xB3,0xFF),
        Mood.Warn => Brush(0xFF,0xA8,0x49),
        Mood.Alert => Brush(0xFF,0x5C,0x5C),
        Mood.Proud => Brush(0x9A,0x7B,0xFF),
        Mood.Tired => Brush(0x9A,0x9A,0x9A),
        Mood.Angry => Brush(0xE9,0x2E,0x2E),
        Mood.Love  => Brush(0xFF,0x6B,0xB6),
        Mood.Chat  => Brush(0x4D,0xD0,0xE1),
        _ => Brush(0xA9,0xB4,0xFF)
    };

    private static SolidColorBrush Brush(byte r, byte g, byte b) => new(Color.FromRgb(r,g,b));
}