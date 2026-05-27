using System; // Type, NotImplementedException のために追加
using System.Globalization;
using System.Windows.Data;

namespace REPLAY.UI.Behaviors
{
    public class StarConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // valueがbool型かどうか安全にチェックし、boolなら isStarred に代入する
            if (value is bool isStarred)
            {
                return isStarred ? "★" : "☆";
            }

            // もし value が null や bool 以外の型だった場合のデフォルトの表示
            return "☆";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // 画面(UI)からデータ側への変換（TwoWayバインディング）をしないなら、このままでOKです。
            throw new NotImplementedException();
        }
    }
}