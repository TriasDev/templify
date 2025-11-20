// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using TriasDev.Templify.Formatting;

namespace TriasDev.Templify.Tests.Formatting;

public class BooleanFormatterTests
{
    [Fact]
    public void Constructor_WithValidValues_InitializesProperties()
    {
        // Arrange & Act
        var formatter = new BooleanFormatter("Yes", "No");

        // Assert
        Assert.Equal("Yes", formatter.TrueValue);
        Assert.Equal("No", formatter.FalseValue);
    }

    [Fact]
    public void Constructor_WithNullTrueValue_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new BooleanFormatter(null!, "No"));
    }

    [Fact]
    public void Constructor_WithNullFalseValue_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new BooleanFormatter("Yes", null!));
    }

    [Fact]
    public void Format_WithTrueValue_ReturnsTrueString()
    {
        // Arrange
        var formatter = new BooleanFormatter("Yes", "No");

        // Act
        string result = formatter.Format(true);

        // Assert
        Assert.Equal("Yes", result);
    }

    [Fact]
    public void Format_WithFalseValue_ReturnsFalseString()
    {
        // Arrange
        var formatter = new BooleanFormatter("Yes", "No");

        // Act
        string result = formatter.Format(false);

        // Assert
        Assert.Equal("No", result);
    }

    [Fact]
    public void Format_WithSymbols_ReturnsCorrectSymbol()
    {
        // Arrange
        var formatter = new BooleanFormatter("☑", "☐");

        // Act
        string trueResult = formatter.Format(true);
        string falseResult = formatter.Format(false);

        // Assert
        Assert.Equal("☑", trueResult);
        Assert.Equal("☐", falseResult);
    }

    [Fact]
    public void Format_WithUnicodeCharacters_ReturnsCorrectCharacter()
    {
        // Arrange
        var formatter = new BooleanFormatter("✓", "✗");

        // Act
        string trueResult = formatter.Format(true);
        string falseResult = formatter.Format(false);

        // Assert
        Assert.Equal("✓", trueResult);
        Assert.Equal("✗", falseResult);
    }

    [Theory]
    [InlineData("Active", "Inactive", true, "Active")]
    [InlineData("Active", "Inactive", false, "Inactive")]
    [InlineData("On", "Off", true, "On")]
    [InlineData("On", "Off", false, "Off")]
    [InlineData("Enabled", "Disabled", true, "Enabled")]
    [InlineData("Enabled", "Disabled", false, "Disabled")]
    public void Format_WithVariousStrings_ReturnsCorrectValue(string trueValue, string falseValue, bool input, string expected)
    {
        // Arrange
        var formatter = new BooleanFormatter(trueValue, falseValue);

        // Act
        string result = formatter.Format(input);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Format_WithEmptyStrings_ReturnsEmptyString()
    {
        // Arrange
        var formatter = new BooleanFormatter(string.Empty, string.Empty);

        // Act
        string trueResult = formatter.Format(true);
        string falseResult = formatter.Format(false);

        // Assert
        Assert.Equal(string.Empty, trueResult);
        Assert.Equal(string.Empty, falseResult);
    }

    [Fact]
    public void Format_WithLocalizedStrings_ReturnsLocalizedValue()
    {
        // Arrange - German
        var formatter = new BooleanFormatter("Ja", "Nein");

        // Act
        string trueResult = formatter.Format(true);
        string falseResult = formatter.Format(false);

        // Assert
        Assert.Equal("Ja", trueResult);
        Assert.Equal("Nein", falseResult);
    }

    [Fact]
    public void Format_WithLongStrings_ReturnsFullString()
    {
        // Arrange
        var formatter = new BooleanFormatter(
            "This is a very long true value",
            "This is a very long false value");

        // Act
        string trueResult = formatter.Format(true);
        string falseResult = formatter.Format(false);

        // Assert
        Assert.Equal("This is a very long true value", trueResult);
        Assert.Equal("This is a very long false value", falseResult);
    }
}
