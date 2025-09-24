using SSMM_UI.Converters;
using System.Globalization;

namespace SSMM_UI.Tests.ConvertersTests;

public class FuncValueUnitTests
{
#pragma warning disable CS8605 // Unboxing a possibly null value.
    [Fact]
    public void Convert_ShouldInvokeProvidedFunction()
    {
        // Arrange
        var converter = new FuncValueConverter<string, int>((value, param) => value.Length);

        // Act
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
        var result = converter.Convert("test", null, null, CultureInfo.InvariantCulture);
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.

        // Assert
        Assert.Equal(4, result);
    }

    [Fact]
    public void Convert_ShouldPassParameterToFunction()
    {
        // Arrange
        var converter = new FuncValueConverter<string, string>((value, param) => $"{value}_{param}");

        // Act
        var result = converter.Convert("hello", typeof(string), "world", CultureInfo.InvariantCulture);

        // Assert
        Assert.Equal("hello_world", result);
    }

    [Fact]
    public void Convert_ShouldReturnDefaultWhenWrongType()
    {
        // Arrange
        var converter = new FuncValueConverter<string, int>((value, param) => value.Length);

        // Act 
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
        var result = converter.Convert(123, null, null, CultureInfo.InvariantCulture);
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.

        // Assert
        Assert.Equal(0, result); 
    }

    [Fact]
    public void Convert_ShouldHandleNullValue()
    {
        // Arrange
        var converter = new FuncValueConverter<string, bool>((value, param) => value == "test");

        // Act
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
        var result = converter.Convert(null, null, null, CultureInfo.InvariantCulture);
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.

        // Assert
        Assert.False((bool)result); 
    }

    [Fact]
    public void ConvertBack_ShouldAlwaysThrowNotSupportedException()
    {
        // Arrange
        var converter = new FuncValueConverter<string, int>((value, param) => value.Length);

        // Act & Assert
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
        Assert.Throws<NotSupportedException>(() =>
            converter.ConvertBack(123, null, null, CultureInfo.InvariantCulture));
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
    }

    [Fact]
    public void StringConverters_IsNullOrEmpty_ShouldWorkCorrectly()
    {
        // Arrange
        var converter = StringConverters.IsNullOrEmpty;

        // Act & Assert
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
        Assert.True((bool)converter.Convert("", null, null, CultureInfo.InvariantCulture));

        // Testa med en tom string istället för null
        Assert.True((bool)converter.Convert("", null, null, CultureInfo.InvariantCulture));

        Assert.False((bool)converter.Convert("test", null, null, CultureInfo.InvariantCulture));
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
    }

    [Theory]
    [InlineData(123.0, 246)] // must be double
    [InlineData(1.5, 3)]     // double to int
    public void Convert_ShouldWorkWithDoubleInput(object input, object expected)
    {
        // Arrange
        var converter = new FuncValueConverter<double, int>((value, param) => (int)(value * 2));

        // Act
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
        var result = converter.Convert(input, null, null, CultureInfo.InvariantCulture);
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Convert_ShouldReturnDefaultForWrongType()
    {
        // Arrange
        var converter = new FuncValueConverter<double, int>((value, param) => (int)(value * 2));

        // Act - Skicka string istället för double
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
        var result = converter.Convert("hello", null, null, CultureInfo.InvariantCulture);
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.

        // Assert - Borde returnera default(int) = 0
        Assert.Equal(0, result);
    }
#pragma warning restore CS8605 // Unboxing a possibly null value.
}
