// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using TriasDev.Templify.Core;
using TriasDev.Templify.Conditionals;
using TriasDev.Templify.Loops;
using TriasDev.Templify.Placeholders;
using TriasDev.Templify.PropertyPaths;
using TriasDev.Templify.Utilities;
using TriasDev.Templify.Formatting;
using System.Globalization;
using System.Reflection;

namespace TriasDev.Templify.Tests;

public class ValueConverterTests
{
    // Use reflection to access the internal static class
    private static readonly Type _valueConverterType = typeof(DocumentTemplateProcessor).Assembly
        .GetType("TriasDev.Templify.Placeholders.ValueConverter")!;

    private static readonly MethodInfo _convertToStringMethod = _valueConverterType
        .GetMethod("ConvertToString", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
            null, new[] { typeof(object), typeof(CultureInfo) }, null)!;

    private static readonly MethodInfo _convertToStringWithFormatMethod = _valueConverterType
        .GetMethod("ConvertToString", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
            null, new[] { typeof(object), typeof(CultureInfo), typeof(string), typeof(BooleanFormatterRegistry) }, null)!;

    private static string ConvertToString(object? value)
    {
        return (string)_convertToStringMethod.Invoke(null, new[] { value, CultureInfo.InvariantCulture })!;
    }

    private static string ConvertToString(object? value, CultureInfo culture, string? format, BooleanFormatterRegistry? registry)
    {
        return (string)_convertToStringWithFormatMethod.Invoke(null, new object?[] { value, culture, format, registry })!;
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

    #region Format Specifier Tests

    [Theory]
    [InlineData(true, "checkbox", "☑")]
    [InlineData(false, "checkbox", "☐")]
    public void ConvertToString_WithBooleanAndCheckboxFormat_ReturnsCheckboxSymbol(bool value, string format, string expected)
    {
        // Arrange
        var registry = new BooleanFormatterRegistry();

        // Act
        string result = ConvertToString(value, CultureInfo.InvariantCulture, format, registry);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(true, "yesno", "Yes")]
    [InlineData(false, "yesno", "No")]
    public void ConvertToString_WithBooleanAndYesNoFormat_ReturnsYesNo(bool value, string format, string expected)
    {
        // Arrange
        var registry = new BooleanFormatterRegistry();

        // Act
        string result = ConvertToString(value, CultureInfo.InvariantCulture, format, registry);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(true, "checkmark", "✓")]
    [InlineData(false, "checkmark", "✗")]
    public void ConvertToString_WithBooleanAndCheckmarkFormat_ReturnsCheckmarkSymbol(bool value, string format, string expected)
    {
        // Arrange
        var registry = new BooleanFormatterRegistry();

        // Act
        string result = ConvertToString(value, CultureInfo.InvariantCulture, format, registry);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(true, "truefalse", "True")]
    [InlineData(false, "truefalse", "False")]
    public void ConvertToString_WithBooleanAndTrueFalseFormat_ReturnsTrueFalse(bool value, string format, string expected)
    {
        // Arrange
        var registry = new BooleanFormatterRegistry();

        // Act
        string result = ConvertToString(value, CultureInfo.InvariantCulture, format, registry);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(true, "onoff", "On")]
    [InlineData(false, "onoff", "Off")]
    public void ConvertToString_WithBooleanAndOnOffFormat_ReturnsOnOff(bool value, string format, string expected)
    {
        // Arrange
        var registry = new BooleanFormatterRegistry();

        // Act
        string result = ConvertToString(value, CultureInfo.InvariantCulture, format, registry);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(true, "enabled", "Enabled")]
    [InlineData(false, "enabled", "Disabled")]
    public void ConvertToString_WithBooleanAndEnabledFormat_ReturnsEnabledDisabled(bool value, string format, string expected)
    {
        // Arrange
        var registry = new BooleanFormatterRegistry();

        // Act
        string result = ConvertToString(value, CultureInfo.InvariantCulture, format, registry);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(true, "active", "Active")]
    [InlineData(false, "active", "Inactive")]
    public void ConvertToString_WithBooleanAndActiveFormat_ReturnsActiveInactive(bool value, string format, string expected)
    {
        // Arrange
        var registry = new BooleanFormatterRegistry();

        // Act
        string result = ConvertToString(value, CultureInfo.InvariantCulture, format, registry);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ConvertToString_WithBooleanAndNullFormat_ReturnsDefaultBoolean()
    {
        // Arrange
        var registry = new BooleanFormatterRegistry();

        // Act
        string trueResult = ConvertToString(true, CultureInfo.InvariantCulture, null, registry);
        string falseResult = ConvertToString(false, CultureInfo.InvariantCulture, null, registry);

        // Assert
        Assert.Equal("True", trueResult);
        Assert.Equal("False", falseResult);
    }

    [Fact]
    public void ConvertToString_WithBooleanAndEmptyFormat_ReturnsDefaultBoolean()
    {
        // Arrange
        var registry = new BooleanFormatterRegistry();

        // Act
        string trueResult = ConvertToString(true, CultureInfo.InvariantCulture, string.Empty, registry);
        string falseResult = ConvertToString(false, CultureInfo.InvariantCulture, string.Empty, registry);

        // Assert
        Assert.Equal("True", trueResult);
        Assert.Equal("False", falseResult);
    }

    [Fact]
    public void ConvertToString_WithBooleanAndWhitespaceFormat_ReturnsDefaultBoolean()
    {
        // Arrange
        var registry = new BooleanFormatterRegistry();

        // Act
        string trueResult = ConvertToString(true, CultureInfo.InvariantCulture, "   ", registry);
        string falseResult = ConvertToString(false, CultureInfo.InvariantCulture, "   ", registry);

        // Assert
        Assert.Equal("True", trueResult);
        Assert.Equal("False", falseResult);
    }

    [Fact]
    public void ConvertToString_WithBooleanAndUnknownFormat_ReturnsDefaultBoolean()
    {
        // Arrange
        var registry = new BooleanFormatterRegistry();

        // Act
        string result = ConvertToString(true, CultureInfo.InvariantCulture, "unknownformat", registry);

        // Assert
        Assert.Equal("True", result);
    }

    [Fact]
    public void ConvertToString_WithBooleanAndNullRegistry_CreatesDefaultRegistry()
    {
        // Act
        string result = ConvertToString(true, CultureInfo.InvariantCulture, "checkbox", null);

        // Assert
        Assert.Equal("☑", result);
    }

    [Fact]
    public void ConvertToString_WithBooleanAndCustomFormatter_UsesCustomFormatter()
    {
        // Arrange
        var registry = new BooleanFormatterRegistry();
        registry.Register("custom", new BooleanFormatter("👍", "👎"));

        // Act
        string trueResult = ConvertToString(true, CultureInfo.InvariantCulture, "custom", registry);
        string falseResult = ConvertToString(false, CultureInfo.InvariantCulture, "custom", registry);

        // Assert
        Assert.Equal("👍", trueResult);
        Assert.Equal("👎", falseResult);
    }

    [Theory]
    [InlineData("de", true, "yesno", "Ja")]
    [InlineData("de", false, "yesno", "Nein")]
    [InlineData("fr", true, "yesno", "Oui")]
    [InlineData("fr", false, "yesno", "Non")]
    [InlineData("es", true, "yesno", "Sí")]
    [InlineData("es", false, "yesno", "No")]
    public void ConvertToString_WithBooleanAndLocalizedFormat_ReturnsLocalizedValue(string cultureName, bool value, string format, string expected)
    {
        // Arrange
        var culture = new CultureInfo(cultureName);
        var registry = new BooleanFormatterRegistry(culture);

        // Act
        string result = ConvertToString(value, culture, format, registry);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ConvertToString_WithNonBooleanAndFormat_IgnoresFormat()
    {
        // Arrange
        var registry = new BooleanFormatterRegistry();

        // Act
        string stringResult = ConvertToString("text", CultureInfo.InvariantCulture, "checkbox", registry);
        string intResult = ConvertToString(42, CultureInfo.InvariantCulture, "yesno", registry);
        string nullResult = ConvertToString(null, CultureInfo.InvariantCulture, "checkmark", registry);

        // Assert
        Assert.Equal("text", stringResult);
        Assert.Equal("42", intResult);
        Assert.Equal(string.Empty, nullResult);
    }

    [Theory]
    [InlineData("CHECKBOX", "☑")]
    [InlineData("CheckBox", "☑")]
    [InlineData("checkbox", "☑")]
    [InlineData("YESNO", "Yes")]
    [InlineData("YesNo", "Yes")]
    [InlineData("yesno", "Yes")]
    public void ConvertToString_WithBooleanAndCaseVariantFormat_IsCaseInsensitive(string format, string expected)
    {
        // Arrange
        var registry = new BooleanFormatterRegistry();

        // Act
        string result = ConvertToString(true, CultureInfo.InvariantCulture, format, registry);

        // Assert
        Assert.Equal(expected, result);
    }

    #endregion

    #region Number Format Specifier Tests

    [Theory]
    [InlineData(1234.567, "currency", "$1,234.57")]
    [InlineData(42, "currency", "$42.00")]
    [InlineData(42L, "currency", "$42.00")]
    [InlineData(42.0, "currency", "$42.00")]
    [InlineData(42.0f, "currency", "$42.00")]
    public void ConvertToString_WithNumericAndCurrencyFormat_ReturnsCurrencyString(object value, string format, string expected)
    {
        // Act
        string result = ConvertToString(value, new CultureInfo("en-US"), format, null);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ConvertToString_WithCurrencyFormatAndGermanCulture_ReturnsEuroFormatting()
    {
        // Arrange
        var culture = new CultureInfo("de-DE");

        // Act
        string result = ConvertToString(1234.56m, culture, "currency", null);

        // Assert
        Assert.Contains("1.234,56", result);
    }

    [Theory]
    [InlineData("CURRENCY")]
    [InlineData("Currency")]
    [InlineData("currency")]
    public void ConvertToString_WithCurrencyFormatCaseInsensitive_ReturnsCurrencyString(string format)
    {
        // Act
        string result = ConvertToString(42m, new CultureInfo("en-US"), format, null);

        // Assert
        Assert.Equal("$42.00", result);
    }

    [Fact]
    public void ConvertToString_WithNumberFormatN2_ReturnsFormattedNumber()
    {
        // Act
        string result = ConvertToString(1234.5678m, new CultureInfo("en-US"), "number:N2", null);

        // Assert
        Assert.Equal("1,234.57", result);
    }

    [Fact]
    public void ConvertToString_WithNumberFormatN0_ReturnsFormattedNumber()
    {
        // Act
        string result = ConvertToString(1234, new CultureInfo("en-US"), "number:N0", null);

        // Assert
        Assert.Equal("1,234", result);
    }

    [Fact]
    public void ConvertToString_WithNumberFormatF3_ReturnsFormattedNumber()
    {
        // Act
        string result = ConvertToString(3.14159, new CultureInfo("en-US"), "number:F3", null);

        // Assert
        Assert.Equal("3.142", result);
    }

    [Fact]
    public void ConvertToString_WithNumberFormatP_ReturnsPercentage()
    {
        // Act
        string result = ConvertToString(0.1234m, new CultureInfo("en-US"), "number:P", null);

        // Assert
        Assert.Contains("12.34", result);
        Assert.Contains("%", result);
    }

    [Fact]
    public void ConvertToString_WithNumberFormatC_ReturnsCurrency()
    {
        // Act
        string result = ConvertToString(42m, new CultureInfo("en-US"), "number:C", null);

        // Assert
        Assert.Equal("$42.00", result);
    }

    [Fact]
    public void ConvertToString_WithStringAndCurrencyFormat_IgnoresFormat()
    {
        // Act
        string result = ConvertToString("text", new CultureInfo("en-US"), "currency", null);

        // Assert
        Assert.Equal("text", result);
    }

    [Fact]
    public void ConvertToString_WithNullAndCurrencyFormat_ReturnsEmptyString()
    {
        // Act
        string result = ConvertToString(null, new CultureInfo("en-US"), "currency", null);

        // Assert
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void ConvertToString_WithInvalidNumberFormat_FallsThrough()
    {
        // Act
        string result = ConvertToString(42m, new CultureInfo("en-US"), "number:XYZ", null);

        // Assert — should not throw, falls through to default
        Assert.NotEmpty(result);
    }

    #endregion
}
