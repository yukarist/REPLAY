using System;
using System.Globalization;
using System.Windows.Data;

namespace REPLAY.UI.Converters
{
        public class LevelConverter : IValueConverter
    {
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
                float level = (float)value;
                return level * 60 + 5; // 高さ調整
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}