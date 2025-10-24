using System.Windows.Media;

namespace Virgil.App.Extensions
{
    /// <summary>
    /// Provides an instance-style extension for ColorConverter.ConvertFromString so that existing
    /// code written as `new ColorConverter().ConvertFromString(hex)` compiles against WPF.
    /// </summary>
    public static class ColorConverterExtensions
    {
        public static Color ConvertFromString(this ColorConverter _, string value)
            => (Color)ColorConverter.ConvertFromString(value);
    }
}
