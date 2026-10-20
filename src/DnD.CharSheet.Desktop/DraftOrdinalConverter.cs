using System.Globalization;
using System.Windows.Data;

namespace DnD.CharSheet.Desktop;

public sealed class DraftOrdinalConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is int index ? $"Draft {index + 1}" : "Draft";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
