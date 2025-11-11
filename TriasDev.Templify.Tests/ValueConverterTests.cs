using TriasDev.Templify.Core;
using TriasDev.Templify.Conditionals;
using TriasDev.Templify.Loops;
using TriasDev.Templify.Placeholders;
using TriasDev.Templify.PropertyPaths;
using TriasDev.Templify.Utilities;
using System.Globalization;
using System.Reflection;

namespace TriasDev.Templify.Tests;

public class ValueConverterTests
{
    // Use reflection to access the internal static class
    private static readonly Type ValueConverterType = typeof(DocumentTemplateProcessor).Assembly
        .GetType("TriasDev.Templify.Placeholders.ValueConverter")!;

    private static readonly MethodInfo ConvertToStringMethod = ValueConverterType
        .GetMethod("ConvertToString", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!;

    private static string ConvertToString(object? value)
    {
        return (string)ConvertToStringMethod.Invoke(null, new[] { value, CultureInfo.InvariantCulture })!;
    }

    [Fact]
    public void ConvertToString_WithNull_ReturnsEmptyString()
    {
        // Act
        string result = ConvertToString(null);

        // Assert
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void ConvertToString_WithString_ReturnsSameString()
    {
        // Arrange
        string value = "Hello World";

        // Act
        string result = ConvertToString(value);

        // Assert
        Assert.Equal("Hello World", result);
    }

    [Fact]
    public void ConvertToString_WithInteger_ReturnsStringRepresentation()
    {
        // Arrange
        int value = 42;

        // Act
        string result = ConvertToString(value);

        // Assert
        Assert.Equal("42", result);
    }

    [Fact]
    public void ConvertToString_WithDecimal_ReturnsStringRepresentation()
    {
        // Arrange
        decimal value = 123.45m;

        // Act
        string result = ConvertToString(value);

        // Assert (culture-independent check)
        Assert.NotEmpty(result);
        Assert.Contains("123", result);
        Assert.Contains("45", result);
    }

    [Fact]
    public void ConvertToString_WithDouble_ReturnsStringRepresentation()
    {
        // Arrange
        double value = 3.14159;

        // Act
        string result = ConvertToString(value);

        // Assert (culture-independent check)
        Assert.NotEmpty(result);
        Assert.Contains("3", result);
        Assert.Contains("14159", result);
    }

    [Fact]
    public void ConvertToString_WithBoolean_ReturnsStringRepresentation()
    {
        // Arrange
        bool trueValue = true;
        bool falseValue = false;

        // Act
        string trueResult = ConvertToString(trueValue);
        string falseResult = ConvertToString(falseValue);

        // Assert
        Assert.Equal("True", trueResult);
        Assert.Equal("False", falseResult);
    }

    [Fact]
    public void ConvertToString_WithDateTime_ReturnsStringRepresentation()
    {
        // Arrange
        DateTime value = new DateTime(2025, 11, 7, 10, 30, 0);

        // Act
        string result = ConvertToString(value);

        // Assert
        Assert.NotEmpty(result);
        Assert.Contains("2025", result);
    }

    [Fact]
    public void ConvertToString_WithDateTimeOffset_ReturnsStringRepresentation()
    {
        // Arrange
        DateTimeOffset value = new DateTimeOffset(2025, 11, 7, 10, 30, 0, TimeSpan.Zero);

        // Act
        string result = ConvertToString(value);

        // Assert
        Assert.NotEmpty(result);
        Assert.Contains("2025", result);
    }

    [Fact]
    public void ConvertToString_WithLong_ReturnsStringRepresentation()
    {
        // Arrange
        long value = 9876543210L;

        // Act
        string result = ConvertToString(value);

        // Assert
        Assert.Equal("9876543210", result);
    }

    [Fact]
    public void ConvertToString_WithFloat_ReturnsStringRepresentation()
    {
        // Arrange
        float value = 2.5f;

        // Act
        string result = ConvertToString(value);

        // Assert (culture-independent check)
        Assert.NotEmpty(result);
        Assert.Contains("2", result);
        Assert.Contains("5", result);
    }

    [Fact]
    public void ConvertToString_WithCustomObject_CallsToString()
    {
        // Arrange
        CustomObject value = new CustomObject { Name = "Test", Value = 123 };

        // Act
        string result = ConvertToString(value);

        // Assert
        Assert.Equal("Test: 123", result);
    }

    private class CustomObject
    {
        public string Name { get; set; } = string.Empty;
        public int Value { get; set; }

        public override string ToString()
        {
            return $"{Name}: {Value}";
        }
    }
}
