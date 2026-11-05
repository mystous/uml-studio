using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace UmlStudio.ViewModels;

public static class Converters
{
    public static readonly IValueConverter BoolToBold =
        new FuncValueConverter<bool, FontWeight>(b => b ? FontWeight.Bold : FontWeight.Normal);

    public static readonly IValueConverter HexToBrush =
        new FuncValueConverter<string?, IBrush>(s =>
        {
            if (!string.IsNullOrWhiteSpace(s) && Color.TryParse(s.Trim(), out var c)) return new SolidColorBrush(c);
            return new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
        });
}
