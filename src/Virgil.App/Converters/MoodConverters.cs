using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Virgil.App.Converters
{
    public enum Mood { Neutral = 0, Happy = 1, Tired = 2, Angry = 3, Focused = 4 }

    public abstract class MoodConverterBase : IValueConverter
    {
        public virtual object Convert(object value, Type targetType, object parameter, CultureInfo culture) => DependencyProperty.UnsetValue;
        public virtual object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
        protected static Mood AsMood(object value) => value is Mood m ? m : Mood.Neutral;
    }

    public class MoodToScaleYConverter : MoodConverterBase
    {
        public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var m = AsMood(value);
            return m switch { Mood.Happy => 1.2, Mood.Tired => 0.9, Mood.Angry => 1.1, Mood.Focused => 1.0, _ => 1.0 };
        }
    }

    public class MoodToScaleXConverter : MoodConverterBase
    {
        public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var m = AsMood(value);
            return m switch { Mood.Happy => 1.2, Mood.Tired => 0.95, Mood.Angry => 1.05, Mood.Focused => 1.0, _ => 1.0 };
        }
    }

    public class MoodToAngleLeftConverter : MoodConverterBase
    {
        public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var m = AsMood(value);
            return m switch { Mood.Happy => -5.0, Mood.Tired => 5.0, Mood.Angry => -8.0, Mood.Focused => 0.0, _ => 0.0 };
        }
    }

    public class MoodToAngleRightConverter : MoodConverterBase
    {
        public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var m = AsMood(value);
            return m switch { Mood.Happy => 5.0, Mood.Tired => -5.0, Mood.Angry => 8.0, Mood.Focused => 0.0, _ => 0.0 };
        }
    }
}
