using System.Windows.Media;

namespace Virgil.App.Controls;

public partial class VirgilAvatar
{
    public void SetMood(string mood)
    {
        var key = (mood ?? "").Trim().ToLowerInvariant();
        FaceFill = key switch
        {
            "happy"   => new SolidColorBrush(Color.FromRgb(0x54,0xC5,0x6C)),
            "alert"   => new SolidColorBrush(Color.FromRgb(0xD9,0x3D,0x3D)),
            "playful" => new SolidColorBrush(Color.FromRgb(0x9B,0x59,0xB6)),
            _            => new SolidColorBrush(Color.FromRgb(0x44,0x55,0x66)),
        };
    }
}
