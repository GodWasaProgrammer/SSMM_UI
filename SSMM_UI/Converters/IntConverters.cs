using System;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace SSMM_UI.Converters;

public static class IntConverters
{
    public static readonly IValueConverter Equals =
        new FuncValueConverter<int, bool>((value, parameter) =>
        {
            if (parameter is int intParam)
                return value == intParam;

            if (parameter is string stringParam && int.TryParse(stringParam, out int parsedParam))
                return value == parsedParam;

            return false;
        });
}
