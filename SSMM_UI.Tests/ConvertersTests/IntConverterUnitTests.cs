using SSMM_UI.Converters;
using System.Globalization;

namespace SSMM_UI.Tests.ConvertersTests;

public class IntConverterUnitTests
{
    [Theory]
    [InlineData(5, 5, true)]    // samma värden
    [InlineData(5, 10, false)]  // olika värden
    [InlineData(0, 0, true)]    // noll värden
    public void Equals_WithIntParameter_ShouldCompareCorrectly(int value, int parameter, bool expected)
    {
        // Act
#pragma warning disable CS8605 // Unboxing a possibly null value.
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
        var result = (bool)IntConverters.Equals.Convert(value, null, parameter, CultureInfo.InvariantCulture);
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
#pragma warning restore CS8605 // Unboxing a possibly null value.

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(5, "5", true)]     // giltig string
    [InlineData(5, "10", false)]   // giltig string
    [InlineData(5, "abc", false)]  // ogiltig string
    [InlineData(5, "", false)]     // tom string
#pragma warning disable xUnit1012 // Null should only be used for nullable parameters
    [InlineData(5, null, false)]   // null string
#pragma warning restore xUnit1012 // Null should only be used for nullable parameters
    public void Equals_WithStringParameter_ShouldParseAndCompare(int value, string parameter, bool expected)
    {
        // Act
#pragma warning disable CS8605 // Unboxing a possibly null value.
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
        var result = (bool)IntConverters.Equals.Convert(value, null, parameter, CultureInfo.InvariantCulture);
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
#pragma warning restore CS8605 // Unboxing a possibly null value.

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(5, 5.5)]    // double
    [InlineData(5, true)]   // bool
    [InlineData(5, 'a')]    // char
    public void Equals_WithInvalidParameterType_ShouldReturnFalse(int value, object parameter)
    {
        // Act
#pragma warning disable CS8605 // Unboxing a possibly null value.
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
        var result = (bool)IntConverters.Equals.Convert(value, null, parameter, CultureInfo.InvariantCulture);
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
#pragma warning restore CS8605 // Unboxing a possibly null value.

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void Equals_WithNullParameter_ShouldReturnFalse()
    {
        // Act
#pragma warning disable CS8605 // Unboxing a possibly null value.
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
        var result = (bool)IntConverters.Equals.Convert(5, null, null, CultureInfo.InvariantCulture);
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
#pragma warning restore CS8605 // Unboxing a possibly null value.

        // Assert
        Assert.False(result);
    }

    [Theory]
    [InlineData(int.MaxValue, int.MaxValue, true)]  // max value
    [InlineData(int.MinValue, int.MinValue, true)]  // min value
    [InlineData(-5, "-5", true)]                    // negativa tal
    public void Equals_EdgeCases_ShouldWorkCorrectly(int value, object parameter, bool expected)
    {
        // Act
#pragma warning disable CS8605 // Unboxing a possibly null value.
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
        var result = (bool)IntConverters.Equals.Convert(value, null, parameter, CultureInfo.InvariantCulture);
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
#pragma warning restore CS8605 // Unboxing a possibly null value.

        // Assert
        Assert.Equal(expected, result);
    }

}
