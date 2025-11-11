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
            // default calm blue
            Color c = Color.FromRgb(0xD0, 0xF0, 0xFF);
            if (mood.Contains("alert")) c = Color.FromRgb(0xFF, 0x55, 0x55);
            else if (mood.Contains("warn") || mood.Contains("hot")) c = Color.FromRgb(0xFF, 0xA6, 0x3A);
            else if (mood.Contains("happy") || mood.Contains("cool") ) c = Color.FromRgb(0x9A, 0xFF, 0x9A);
            return new SolidColorBrush(c);
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
