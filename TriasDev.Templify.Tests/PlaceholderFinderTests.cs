using TriasDev.Templify.Core;
using TriasDev.Templify.Conditionals;
using TriasDev.Templify.Loops;
using TriasDev.Templify.Placeholders;
using TriasDev.Templify.PropertyPaths;
using TriasDev.Templify.Utilities;

namespace TriasDev.Templify.Tests;

public class PlaceholderFinderTests
{
    private readonly PlaceholderFinder _finder;

    public PlaceholderFinderTests()
    {
        _finder = new PlaceholderFinder();
    }

    [Fact]
    public void FindPlaceholders_WithValidPlaceholders_ReturnsMatches()
    {
        // Arrange
        string text = "Hello {{Name}}, your order {{OrderNumber}} is ready.";

        // Act
        List<PlaceholderMatch> matches = _finder.FindPlaceholders(text).ToList();

        // Assert
        Assert.Equal(2, matches.Count);
        Assert.Equal("{{Name}}", matches[0].FullMatch);
        Assert.Equal("Name", matches[0].VariableName);
        Assert.Equal("{{OrderNumber}}", matches[1].FullMatch);
        Assert.Equal("OrderNumber", matches[1].VariableName);
    }

    [Fact]
    public void FindPlaceholders_WithNoPlaceholders_ReturnsEmpty()
    {
        // Arrange
        string text = "This text has no placeholders.";

        // Act
        List<PlaceholderMatch> matches = _finder.FindPlaceholders(text).ToList();

        // Assert
        Assert.Empty(matches);
    }

    [Fact]
    public void FindPlaceholders_WithEmptyString_ReturnsEmpty()
    {
        // Arrange
        string text = string.Empty;

        // Act
        List<PlaceholderMatch> matches = _finder.FindPlaceholders(text).ToList();

        // Assert
        Assert.Empty(matches);
    }

    [Fact]
    public void FindPlaceholders_WithMultiplePlaceholdersInRow_ReturnsAll()
    {
        // Arrange
        string text = "{{First}}{{Second}}{{Third}}";

        // Act
        List<PlaceholderMatch> matches = _finder.FindPlaceholders(text).ToList();

        // Assert
        Assert.Equal(3, matches.Count);
        Assert.Equal("First", matches[0].VariableName);
        Assert.Equal("Second", matches[1].VariableName);
        Assert.Equal("Third", matches[2].VariableName);
    }

    [Fact]
    public void FindPlaceholders_WithUnderscoresAndNumbers_ReturnsMatches()
    {
        // Arrange
        string text = "{{Variable_Name_123}} and {{Item2}}";

        // Act
        List<PlaceholderMatch> matches = _finder.FindPlaceholders(text).ToList();

        // Assert
        Assert.Equal(2, matches.Count);
        Assert.Equal("Variable_Name_123", matches[0].VariableName);
        Assert.Equal("Item2", matches[1].VariableName);
    }

    [Fact]
    public void IsValidPlaceholder_WithValidPlaceholder_ReturnsTrue()
    {
        // Arrange
        string placeholder = "{{ValidName}}";

        // Act
        bool isValid = _finder.IsValidPlaceholder(placeholder);

        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public void IsValidPlaceholder_WithInvalidPlaceholder_ReturnsFalse()
    {
        // Arrange
        string placeholder = "{InvalidName}";

        // Act
        bool isValid = _finder.IsValidPlaceholder(placeholder);

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public void IsValidPlaceholder_WithSpacesInside_ReturnsFalse()
    {
        // Arrange
        string placeholder = "{{Invalid Name}}";

        // Act
        bool isValid = _finder.IsValidPlaceholder(placeholder);

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public void ExtractVariableName_WithValidPlaceholder_ReturnsName()
    {
        // Arrange
        string placeholder = "{{CustomerName}}";

        // Act
        string? variableName = _finder.ExtractVariableName(placeholder);

        // Assert
        Assert.Equal("CustomerName", variableName);
    }

    [Fact]
    public void ExtractVariableName_WithInvalidPlaceholder_ReturnsNull()
    {
        // Arrange
        string placeholder = "{InvalidFormat}";

        // Act
        string? variableName = _finder.ExtractVariableName(placeholder);

        // Assert
        Assert.Null(variableName);
    }

    [Fact]
    public void GetUniqueVariableNames_WithDuplicates_ReturnsDistinct()
    {
        // Arrange
        string text = "{{Name}} is {{Name}} and {{Age}} is {{Age}}.";

        // Act
        List<string> variableNames = _finder.GetUniqueVariableNames(text).ToList();

        // Assert
        Assert.Equal(2, variableNames.Count);
        Assert.Contains("Age", variableNames);
        Assert.Contains("Name", variableNames);
    }

    [Fact]
    public void GetUniqueVariableNames_ReturnsInAlphabeticalOrder()
    {
        // Arrange
        string text = "{{Zebra}} {{Apple}} {{Banana}}";

        // Act
        List<string> variableNames = _finder.GetUniqueVariableNames(text).ToList();

        // Assert
        Assert.Equal(3, variableNames.Count);
        Assert.Equal("Apple", variableNames[0]);
        Assert.Equal("Banana", variableNames[1]);
        Assert.Equal("Zebra", variableNames[2]);
    }

    #region Format Specifier Tests

    [Fact]
    public void FindPlaceholders_WithFormatSpecifier_ParsesFormat()
    {
        // Arrange
        string text = "{{IsActive:checkbox}}";

        // Act
        List<PlaceholderMatch> matches = _finder.FindPlaceholders(text).ToList();

        // Assert
        Assert.Single(matches);
        Assert.Equal("{{IsActive:checkbox}}", matches[0].FullMatch);
        Assert.Equal("IsActive", matches[0].VariableName);
        Assert.Equal("checkbox", matches[0].Format);
    }

    [Fact]
    public void FindPlaceholders_WithoutFormatSpecifier_FormatIsNull()
    {
        // Arrange
        string text = "{{IsActive}}";

        // Act
        List<PlaceholderMatch> matches = _finder.FindPlaceholders(text).ToList();

        // Assert
        Assert.Single(matches);
        Assert.Equal("{{IsActive}}", matches[0].FullMatch);
        Assert.Equal("IsActive", matches[0].VariableName);
        Assert.Null(matches[0].Format);
    }

    [Fact]
    public void FindPlaceholders_WithMultipleFormats_ParsesAllFormats()
    {
        // Arrange
        string text = "{{IsActive:checkbox}} {{IsEnabled:yesno}} {{Status:checkmark}}";

        // Act
        List<PlaceholderMatch> matches = _finder.FindPlaceholders(text).ToList();

        // Assert
        Assert.Equal(3, matches.Count);
        Assert.Equal("checkbox", matches[0].Format);
        Assert.Equal("yesno", matches[1].Format);
        Assert.Equal("checkmark", matches[2].Format);
    }

    [Fact]
    public void FindPlaceholders_WithMixedFormatAndNoFormat_ParsesCorrectly()
    {
        // Arrange
        string text = "{{Name}} is {{IsActive:checkbox}} and {{Age}}";

        // Act
        List<PlaceholderMatch> matches = _finder.FindPlaceholders(text).ToList();

        // Assert
        Assert.Equal(3, matches.Count);
        Assert.Null(matches[0].Format);
        Assert.Equal("checkbox", matches[1].Format);
        Assert.Null(matches[2].Format);
    }

    [Fact]
    public void FindPlaceholders_WithNestedPropertyAndFormat_ParsesBoth()
    {
        // Arrange
        string text = "{{Customer.IsActive:yesno}}";

        // Act
        List<PlaceholderMatch> matches = _finder.FindPlaceholders(text).ToList();

        // Assert
        Assert.Single(matches);
        Assert.Equal("Customer.IsActive", matches[0].VariableName);
        Assert.Equal("yesno", matches[0].Format);
    }

    [Fact]
    public void FindPlaceholders_WithIndexerAndFormat_ParsesBoth()
    {
        // Arrange
        string text = "{{Items[0]:checkbox}}";

        // Act
        List<PlaceholderMatch> matches = _finder.FindPlaceholders(text).ToList();

        // Assert
        Assert.Single(matches);
        Assert.Equal("Items[0]", matches[0].VariableName);
        Assert.Equal("checkbox", matches[0].Format);
    }

    [Fact]
    public void FindPlaceholders_WithComplexPathAndFormat_ParsesBoth()
    {
        // Arrange
        string text = "{{Orders[0].Customer.IsActive:yesno}}";

        // Act
        List<PlaceholderMatch> matches = _finder.FindPlaceholders(text).ToList();

        // Assert
        Assert.Single(matches);
        Assert.Equal("Orders[0].Customer.IsActive", matches[0].VariableName);
        Assert.Equal("yesno", matches[0].Format);
    }

    [Fact]
    public void FindPlaceholders_WithLoopMetadataAndFormat_ParsesBoth()
    {
        // Arrange
        string text = "{{@first:checkbox}} {{@last:yesno}}";

        // Act
        List<PlaceholderMatch> matches = _finder.FindPlaceholders(text).ToList();

        // Assert
        Assert.Equal(2, matches.Count);
        Assert.Equal("@first", matches[0].VariableName);
        Assert.Equal("checkbox", matches[0].Format);
        Assert.Equal("@last", matches[1].VariableName);
        Assert.Equal("yesno", matches[1].Format);
    }

    [Fact]
    public void FindPlaceholders_WithCurrentItemAndFormat_ParsesBoth()
    {
        // Arrange
        string text = "{{.:checkbox}} {{this:yesno}}";

        // Act
        List<PlaceholderMatch> matches = _finder.FindPlaceholders(text).ToList();

        // Assert
        Assert.Equal(2, matches.Count);
        Assert.Equal(".", matches[0].VariableName);
        Assert.Equal("checkbox", matches[0].Format);
        Assert.Equal("this", matches[1].VariableName);
        Assert.Equal("yesno", matches[1].Format);
    }

    [Fact]
    public void IsValidPlaceholder_WithFormatSpecifier_ReturnsTrue()
    {
        // Arrange
        string placeholder = "{{IsActive:checkbox}}";

        // Act
        bool isValid = _finder.IsValidPlaceholder(placeholder);

        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public void ExtractVariableName_WithFormatSpecifier_ReturnsVariableName()
    {
        // Arrange
        string placeholder = "{{IsActive:checkbox}}";

        // Act
        string? variableName = _finder.ExtractVariableName(placeholder);

        // Assert
        Assert.Equal("IsActive", variableName);
    }

    [Fact]
    public void FindPlaceholders_WithUppercaseFormat_ParsesCorrectly()
    {
        // Arrange
        string text = "{{IsActive:CHECKBOX}}";

        // Act
        List<PlaceholderMatch> matches = _finder.FindPlaceholders(text).ToList();

        // Assert
        Assert.Single(matches);
        Assert.Equal("CHECKBOX", matches[0].Format);
    }

    [Fact]
    public void FindPlaceholders_WithNumbersInFormat_ParsesCorrectly()
    {
        // Arrange
        string text = "{{Value:format123}}";

        // Act
        List<PlaceholderMatch> matches = _finder.FindPlaceholders(text).ToList();

        // Assert
        Assert.Single(matches);
        Assert.Equal("format123", matches[0].Format);
    }

    [Theory]
    [InlineData("{{IsActive:checkbox}}", "checkbox")]
    [InlineData("{{IsActive:yesno}}", "yesno")]
    [InlineData("{{IsActive:truefalse}}", "truefalse")]
    [InlineData("{{IsActive:onoff}}", "onoff")]
    [InlineData("{{IsActive:enabled}}", "enabled")]
    [InlineData("{{IsActive:active}}", "active")]
    [InlineData("{{IsActive:checkmark}}", "checkmark")]
    [InlineData("{{IsActive:check}}", "check")]
    public void FindPlaceholders_WithBuiltInFormats_ParsesCorrectly(string text, string expectedFormat)
    {
        // Act
        List<PlaceholderMatch> matches = _finder.FindPlaceholders(text).ToList();

        // Assert
        Assert.Single(matches);
        Assert.Equal(expectedFormat, matches[0].Format);
    }

    #endregion
}
