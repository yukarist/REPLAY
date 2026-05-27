using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;

namespace REPLAY.UI.Behaviors
{

public class HighlightConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        var text = values[0] as string;
        var keyword = values[1] as string;

        var span = new Span();

        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(keyword))
        {
            span.Inlines.Add(new Run(text));
            return span;
        }

        int index = text.IndexOf(keyword, StringComparison.OrdinalIgnoreCase);

        if (index < 0)
        {
            span.Inlines.Add(new Run(text));
            return span;
        }

        span.Inlines.Add(new Run(text.Substring(0, index)));
        span.Inlines.Add(new Run(text.Substring(index, keyword.Length))
        {
            Foreground = Brushes.Cyan
        });
        span.Inlines.Add(new Run(text.Substring(index + keyword.Length)));

        return span;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
}