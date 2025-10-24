// Force WPF static ColorConverter in Converters.cs without touching file
// Allow calling `new ColorConverter().ConvertFromString(hex)` to compile
// by using an extension method pattern fallback.
using System.Windows.Media;

namespace Virgil.App.Extensions
{
    public static class ColorConverterInstanceShim
    {
        public static Color ConvertFromString(this ColorConverter _, string value)
            => (Color)ColorConverter.ConvertFromString(value);
    }
}
