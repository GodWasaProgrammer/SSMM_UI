using System;
using Avalonia.Data.Converters;

namespace SSMM_UI.Converters;

public class FuncValueConverter<TFrom, TTo> : IValueConverter
{
    private readonly Func<TFrom, object?, TTo> _convert;

    public FuncValueConverter(Func<TFrom, object?, TTo> convert)
    {
        _convert = convert;
    }

    public object Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        if (value is TFrom typedValue)
        {
            return _convert(typedValue, parameter)!;
        }
        return default(TTo)!;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
public static class StringConverters
{
    public static readonly IValueConverter IsNullOrEmpty =
        new FuncValueConverter<string, bool>((value, parameter) => string.IsNullOrEmpty(value));
}