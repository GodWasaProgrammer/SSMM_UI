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
        var result = (bool)IntConverters.Equals.Convert(value, null, parameter, CultureInfo.InvariantCulture);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(5, "5", true)]     // giltig string
    [InlineData(5, "10", false)]   // giltig string
    [InlineData(5, "abc", false)]  // ogiltig string
    [InlineData(5, "", false)]     // tom string
    [InlineData(5, null, false)]   // null string
    public void Equals_WithStringParameter_ShouldParseAndCompare(int value, string parameter, bool expected)
    {
        // Act
        var result = (bool)IntConverters.Equals.Convert(value, null, parameter, CultureInfo.InvariantCulture);

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
        var result = (bool)IntConverters.Equals.Convert(value, null, parameter, CultureInfo.InvariantCulture);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void Equals_WithNullParameter_ShouldReturnFalse()
    {
        // Act
        var result = (bool)IntConverters.Equals.Convert(5, null, null, CultureInfo.InvariantCulture);

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
        var result = (bool)IntConverters.Equals.Convert(value, null, parameter, CultureInfo.InvariantCulture);

        // Assert
        Assert.Equal(expected, result);
    }

}
