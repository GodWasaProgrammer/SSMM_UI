using Avalonia.Data.Converters;

namespace SSMM_UI.Converters;

public static class IntConverters
{
#pragma warning disable CS0108 // Member hides inherited member; missing new keyword
    public static readonly IValueConverter Equals =
#pragma warning restore CS0108 // Member hides inherited member; missing new keyword
        new FuncValueConverter<int, bool>((value, parameter) =>
        {
            if (parameter is int intParam)
                return value == intParam;

            if (parameter is string stringParam && int.TryParse(stringParam, out int parsedParam))
                return value == parsedParam;

            return false;
        });
}
