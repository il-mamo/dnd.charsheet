using System.Globalization;
using System.Windows.Data;
using DnD.CharSheet.Domain;

namespace DnD.CharSheet.Desktop;

public sealed class CharacterIdentityEqualsConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) =>
        values.Length == 2 && values[0] is CharacterId first && values[1] is CharacterId second && first == second;

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
