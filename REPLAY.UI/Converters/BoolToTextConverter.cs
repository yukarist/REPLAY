using System;
using System.Globalization;
using System.Windows.Data;

namespace REPLAY.UI.Converters
{
    public class BoolToTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (bool)value ? "ON" : "OFF";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value.ToString() == "ON";
        }
    }
}