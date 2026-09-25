// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using TriasDev.Templify.Core;
using TriasDev.Templify.Conditionals;
using TriasDev.Templify.Loops;
using TriasDev.Templify.Placeholders;
using TriasDev.Templify.PropertyPaths;
using TriasDev.Templify.Utilities;

namespace TriasDev.Templify.Tests;

public class PropertyPathTests
{
    [Fact]
    public void Parse_WithSimplePath_ReturnsCorrectSegments()
    {
        // Arrange
        string path = "Name";

        // Act
        PropertyPath result = PropertyPath.Parse(path);
        IReadOnlyList<PropertyPathSegment> segments = result.Segments;

        // Assert
        Assert.NotNull(result);
        Assert.Single(segments);
        Assert.Equal("Name", segments[0].Name);
        Assert.False(segments[0].IsIndexer);
        Assert.True(result.IsSimple);
    }

    [Fact]
    public void Parse_WithNestedPath_ReturnsCorrectSegments()
    {
        // Arrange
        string path = "Customer.Address.City";

        // Act
        PropertyPath result = PropertyPath.Parse(path);
        IReadOnlyList<PropertyPathSegment> segments = result.Segments;

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, segments.Count);
        Assert.Equal("Customer", segments[0].Name);
        Assert.Equal("Address", segments[1].Name);
        Assert.Equal("City", segments[2].Name);
        Assert.False(result.IsSimple);
    }

    [Fact]
    public void Parse_WithArrayIndexer_ReturnsCorrectSegments()
    {
        // Arrange
        string path = "Items[0]";

        // Act
        PropertyPath result = PropertyPath.Parse(path);
        IReadOnlyList<PropertyPathSegment> segments = result.Segments;

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, segments.Count);
        Assert.Equal("Items", segments[0].Name);
        Assert.False(segments[0].IsIndexer);
        Assert.Equal("0", segments[1].Name);
        Assert.True(segments[1].IsIndexer);
    }

    [Fact]
    public void Parse_WithMixedNotation_ReturnsCorrectSegments()
    {
        // Arrange
        string path = "Orders[0].Customer.Address";

        // Act
        PropertyPath result = PropertyPath.Parse(path);
        IReadOnlyList<PropertyPathSegment> segments = result.Segments;

        // Assert
        Assert.NotNull(result);
        Assert.Equal(4, segments.Count);
        Assert.Equal("Orders", segments[0].Name);
        Assert.Equal("0", segments[1].Name);
        Assert.True(segments[1].IsIndexer);
        Assert.Equal("Customer", segments[2].Name);
        Assert.Equal("Address", segments[3].Name);
    }

    [Fact]
    public void Parse_WithDictionaryKeyIndexer_ReturnsCorrectSegments()
    {
        // Arrange
        string path = "Settings[Theme]";

        // Act
        PropertyPath result = PropertyPath.Parse(path);
        IReadOnlyList<PropertyPathSegment> segments = result.Segments;

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, segments.Count);
        Assert.Equal("Settings", segments[0].Name);
        Assert.Equal("Theme", segments[1].Name);
        Assert.True(segments[1].IsIndexer);
    }

    [Fact]
    public void Parse_WithEmptyString_ThrowsException()
    {
        // Arrange
        string path = "";

        // Act & Assert
        Assert.Throws<ArgumentException>(() => PropertyPath.Parse(path));
    }

    [Fact]
    public void Parse_WithNullString_ThrowsException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => PropertyPath.Parse(null!));
    }

    [Fact]
    public void Parse_WithEmptyBrackets_ThrowsException()
    {
        // Arrange
        string path = "Items[]";

        // Act & Assert
        Assert.Throws<ArgumentException>(() => PropertyPath.Parse(path));
    }

    [Fact]
    public void Parse_WithUnclosedBracket_ThrowsException()
    {
        // Arrange
        string path = "Items[0";

        // Act & Assert
        Assert.Throws<ArgumentException>(() => PropertyPath.Parse(path));
    }

    [Fact]
    public void Parse_WithDoubleDot_ThrowsException()
    {
        // Arrange
        string path = "Customer..Address";

        // Act & Assert
        Assert.Throws<ArgumentException>(() => PropertyPath.Parse(path));
    }

    [Fact]
    public void Parse_WithInvalidCharacter_ThrowsException()
    {
        // Arrange
        string path = "Customer.@Address";

        // Act & Assert
        Assert.Throws<ArgumentException>(() => PropertyPath.Parse(path));
    }

    [Fact]
    public void TryParse_WithValidPath_ReturnsTrue()
    {
        // Arrange
        string path = "Customer.Address.City";

        // Act
        bool result = PropertyPath.TryParse(path, out PropertyPath? parsed);

        // Assert
        Assert.True(result);
        Assert.NotNull(parsed);
    }

    [Fact]
    public void TryParse_WithInvalidPath_ReturnsFalse()
    {
        // Arrange
        string path = "Customer..Address";

        // Act
        bool result = PropertyPath.TryParse(path, out PropertyPath? parsed);

        // Assert
        Assert.False(result);
        Assert.Null(parsed);
    }

    [Fact]
    public void Parse_WithComplexMixedPath_ReturnsCorrectSegments()
    {
        // Arrange
        string path = "Data[Items][0].Properties[Name]";

        // Act
        PropertyPath result = PropertyPath.Parse(path);
        IReadOnlyList<PropertyPathSegment> segments = result.Segments;

        // Assert
        Assert.NotNull(result);
        Assert.Equal(5, segments.Count);
        Assert.Equal("Data", segments[0].Name);
        Assert.Equal("Items", segments[1].Name);
        Assert.True(segments[1].IsIndexer);
        Assert.Equal("0", segments[2].Name);
        Assert.True(segments[2].IsIndexer);
        Assert.Equal("Properties", segments[3].Name);
        Assert.Equal("Name", segments[4].Name);
        Assert.True(segments[4].IsIndexer);
    }
}
