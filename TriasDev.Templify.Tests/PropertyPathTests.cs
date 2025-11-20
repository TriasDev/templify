// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using TriasDev.Templify.Core;
using TriasDev.Templify.Conditionals;
using TriasDev.Templify.Loops;
using TriasDev.Templify.Placeholders;
using TriasDev.Templify.PropertyPaths;
using TriasDev.Templify.Utilities;
using System.Reflection;

namespace TriasDev.Templify.Tests;

public class PropertyPathTests
{
    private static readonly Type _propertyPathType = typeof(DocumentTemplateProcessor).Assembly
        .GetType("TriasDev.Templify.PropertyPaths.PropertyPath")!;

    private static readonly Type _propertyPathSegmentType = typeof(DocumentTemplateProcessor).Assembly
        .GetType("TriasDev.Templify.PropertyPaths.PropertyPathSegment")!;

    private static readonly MethodInfo _parseMethod = _propertyPathType
        .GetMethod("Parse", BindingFlags.Static | BindingFlags.Public)!;

    private static readonly MethodInfo _tryParseMethod = _propertyPathType
        .GetMethod("TryParse", BindingFlags.Static | BindingFlags.Public)!;

    private static readonly PropertyInfo _segmentsProp = _propertyPathType.GetProperty("Segments")!;
    private static readonly PropertyInfo _isSimpleProp = _propertyPathType.GetProperty("IsSimple")!;
    private static readonly PropertyInfo _segmentNameProp = _propertyPathSegmentType.GetProperty("Name")!;
    private static readonly PropertyInfo _segmentIsIndexerProp = _propertyPathSegmentType.GetProperty("IsIndexer")!;

    private static System.Collections.IList GetSegments(object propertyPath)
    {
        object segments = _segmentsProp.GetValue(propertyPath)!;
        return (System.Collections.IList)segments;
    }

    private static bool GetIsSimple(object propertyPath) => (bool)_isSimpleProp.GetValue(propertyPath)!;

    private static string GetSegmentName(object segment) => (string)_segmentNameProp.GetValue(segment)!;

    private static bool GetSegmentIsIndexer(object segment) => (bool)_segmentIsIndexerProp.GetValue(segment)!;

    [Fact]
    public void Parse_WithSimplePath_ReturnsCorrectSegments()
    {
        // Arrange
        string path = "Name";

        // Act
        object result = _parseMethod.Invoke(null, new object[] { path })!;
        System.Collections.IList segments = GetSegments(result);

        // Assert
        Assert.NotNull(result);
        Assert.Single(segments);
        Assert.Equal("Name", GetSegmentName(segments[0]!));
        Assert.False(GetSegmentIsIndexer(segments[0]!));
        Assert.True(GetIsSimple(result));
    }

    [Fact]
    public void Parse_WithNestedPath_ReturnsCorrectSegments()
    {
        // Arrange
        string path = "Customer.Address.City";

        // Act
        object result = _parseMethod.Invoke(null, new object[] { path })!;
        System.Collections.IList segments = GetSegments(result);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, segments.Count);
        Assert.Equal("Customer", GetSegmentName(segments[0]!));
        Assert.Equal("Address", GetSegmentName(segments[1]!));
        Assert.Equal("City", GetSegmentName(segments[2]!));
        Assert.False(GetIsSimple(result));
    }

    [Fact]
    public void Parse_WithArrayIndexer_ReturnsCorrectSegments()
    {
        // Arrange
        string path = "Items[0]";

        // Act
        object result = _parseMethod.Invoke(null, new object[] { path })!;
        System.Collections.IList segments = GetSegments(result);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, segments.Count);
        Assert.Equal("Items", GetSegmentName(segments[0]!));
        Assert.False(GetSegmentIsIndexer(segments[0]!));
        Assert.Equal("0", GetSegmentName(segments[1]!));
        Assert.True(GetSegmentIsIndexer(segments[1]!));
    }

    [Fact]
    public void Parse_WithMixedNotation_ReturnsCorrectSegments()
    {
        // Arrange
        string path = "Orders[0].Customer.Address";

        // Act
        object result = _parseMethod.Invoke(null, new object[] { path })!;
        System.Collections.IList segments = GetSegments(result);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(4, segments.Count);
        Assert.Equal("Orders", GetSegmentName(segments[0]!));
        Assert.Equal("0", GetSegmentName(segments[1]!));
        Assert.True(GetSegmentIsIndexer(segments[1]!));
        Assert.Equal("Customer", GetSegmentName(segments[2]!));
        Assert.Equal("Address", GetSegmentName(segments[3]!));
    }

    [Fact]
    public void Parse_WithDictionaryKeyIndexer_ReturnsCorrectSegments()
    {
        // Arrange
        string path = "Settings[Theme]";

        // Act
        object result = _parseMethod.Invoke(null, new object[] { path })!;
        System.Collections.IList segments = GetSegments(result);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, segments.Count);
        Assert.Equal("Settings", GetSegmentName(segments[0]!));
        Assert.Equal("Theme", GetSegmentName(segments[1]!));
        Assert.True(GetSegmentIsIndexer(segments[1]!));
    }

    [Fact]
    public void Parse_WithEmptyString_ThrowsException()
    {
        // Arrange
        string path = "";

        // Act & Assert
        TargetInvocationException ex = Assert.Throws<TargetInvocationException>(() =>
            _parseMethod.Invoke(null, new object[] { path }));

        Assert.IsType<ArgumentException>(ex.InnerException);
    }

    [Fact]
    public void Parse_WithNullString_ThrowsException()
    {
        // Act & Assert
        TargetInvocationException ex = Assert.Throws<TargetInvocationException>(() =>
            _parseMethod.Invoke(null, new object?[] { null }));

        Assert.IsType<ArgumentException>(ex.InnerException);
    }

    [Fact]
    public void Parse_WithEmptyBrackets_ThrowsException()
    {
        // Arrange
        string path = "Items[]";

        // Act & Assert
        TargetInvocationException ex = Assert.Throws<TargetInvocationException>(() =>
            _parseMethod.Invoke(null, new object[] { path }));

        Assert.IsType<ArgumentException>(ex.InnerException);
    }

    [Fact]
    public void Parse_WithUnclosedBracket_ThrowsException()
    {
        // Arrange
        string path = "Items[0";

        // Act & Assert
        TargetInvocationException ex = Assert.Throws<TargetInvocationException>(() =>
            _parseMethod.Invoke(null, new object[] { path }));

        Assert.IsType<ArgumentException>(ex.InnerException);
    }

    [Fact]
    public void Parse_WithDoubleDot_ThrowsException()
    {
        // Arrange
        string path = "Customer..Address";

        // Act & Assert
        TargetInvocationException ex = Assert.Throws<TargetInvocationException>(() =>
            _parseMethod.Invoke(null, new object[] { path }));

        Assert.IsType<ArgumentException>(ex.InnerException);
    }

    [Fact]
    public void Parse_WithInvalidCharacter_ThrowsException()
    {
        // Arrange
        string path = "Customer.@Address";

        // Act & Assert
        TargetInvocationException ex = Assert.Throws<TargetInvocationException>(() =>
            _parseMethod.Invoke(null, new object[] { path }));

        Assert.IsType<ArgumentException>(ex.InnerException);
    }

    [Fact]
    public void TryParse_WithValidPath_ReturnsTrue()
    {
        // Arrange
        string path = "Customer.Address.City";
        object[] parameters = new object[] { path, null! };

        // Act
        object? result = _tryParseMethod.Invoke(null, parameters);

        // Assert
        Assert.True((bool)result!);
        Assert.NotNull(parameters[1]);
    }

    [Fact]
    public void TryParse_WithInvalidPath_ReturnsFalse()
    {
        // Arrange
        string path = "Customer..Address";
        object[] parameters = new object[] { path, null! };

        // Act
        object? result = _tryParseMethod.Invoke(null, parameters);

        // Assert
        Assert.False((bool)result!);
        Assert.Null(parameters[1]);
    }

    [Fact]
    public void Parse_WithComplexMixedPath_ReturnsCorrectSegments()
    {
        // Arrange
        string path = "Data[Items][0].Properties[Name]";

        // Act
        object result = _parseMethod.Invoke(null, new object[] { path })!;
        System.Collections.IList segments = GetSegments(result);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(5, segments.Count);
        Assert.Equal("Data", GetSegmentName(segments[0]!));
        Assert.Equal("Items", GetSegmentName(segments[1]!));
        Assert.True(GetSegmentIsIndexer(segments[1]!));
        Assert.Equal("0", GetSegmentName(segments[2]!));
        Assert.True(GetSegmentIsIndexer(segments[2]!));
        Assert.Equal("Properties", GetSegmentName(segments[3]!));
        Assert.Equal("Name", GetSegmentName(segments[4]!));
        Assert.True(GetSegmentIsIndexer(segments[4]!));
    }
}
