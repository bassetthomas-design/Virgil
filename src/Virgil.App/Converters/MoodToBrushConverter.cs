using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Virgil.App.Converters
{
    public class MoodToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var mood = (value as string)?.ToLowerInvariant() ?? string.Empty;
            // default calm mint from the photo palette
            Color c = (Color)ColorConverter.ConvertFromString("#A6FEE1");
            if (mood.Contains("alert") || mood.Contains("overheat") || mood.Contains("angry")) c = Color.FromRgb(0xFF, 0x55, 0x55);
            else if (mood.Contains("warn") || mood.Contains("hot") || mood.Contains("stress")) c = Color.FromRgb(0xFF, 0xA6, 0x3A);
            else if (mood.Contains("happy") || mood.Contains("cool") || mood.Contains("chill")) c = (Color)ColorConverter.ConvertFromString("#B8FFE8");
            return new SolidColorBrush(c);
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
