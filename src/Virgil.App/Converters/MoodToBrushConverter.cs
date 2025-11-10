using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using Virgil.App.Core;

namespace Virgil.App.Converters
{
    public class MoodToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var mood = value is MoodState m ? m : MoodState.Focused;
            return mood switch
            {
                MoodState.Happy => (Brush)new SolidColorBrush(Color.FromRgb(0x54,0xF2,0xA8)),
                MoodState.Warn  => (Brush)new SolidColorBrush(Color.FromRgb(0xFF,0xC1,0x3D)),
                MoodState.Alert => (Brush)new SolidColorBrush(Color.FromRgb(0xFF,0x56,0x56)),
                MoodState.Tired => (Brush)new SolidColorBrush(Color.FromRgb(0x88,0x88,0xAA)),
                _ => (Brush)new SolidColorBrush(Color.FromRgb(0x5A,0x8D,0xFF)),
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
    }
}
