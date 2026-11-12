using System.Globalization;
using System.Windows.Data;
using DnD.CharSheet.Domain;

namespace DnD.CharSheet.Desktop;

public sealed class AdventureMemberCountConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) =>
        values.Length == 2 && values[0] is AdventureId id &&
        values[1] is IReadOnlyDictionary<AdventureId, int> counts && counts.TryGetValue(id, out var count)
            ? $"{count} character{(count == 1 ? "" : "s")}" : "";

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
