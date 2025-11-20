// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using TriasDev.Templify.Markdown;

namespace TriasDev.Templify.Tests.Markdown;

public class MarkdownParserTests
{
    [Fact]
    public void Parse_PlainText_ReturnsSingleSegment()
    {
        // Arrange
        string text = "This is plain text";

        // Act
        List<MarkdownSegment> segments = MarkdownParser.Parse(text);

        // Assert
        Assert.Single(segments);
        Assert.Equal("This is plain text", segments[0].Text);
        Assert.False(segments[0].IsBold);
        Assert.False(segments[0].IsItalic);
        Assert.False(segments[0].IsStrikethrough);
    }

    [Fact]
    public void Parse_BoldWithDoubleAsterisks_ParsesCorrectly()
    {
        // Arrange
        string text = "My name is **Alice**";

        // Act
        List<MarkdownSegment> segments = MarkdownParser.Parse(text);

        // Assert
        Assert.Equal(2, segments.Count);
        Assert.Equal("My name is ", segments[0].Text);
        Assert.False(segments[0].IsBold);
        Assert.Equal("Alice", segments[1].Text);
        Assert.True(segments[1].IsBold);
        Assert.False(segments[1].IsItalic);
    }

    [Fact]
    public void Parse_BoldWithDoubleUnderscores_ParsesCorrectly()
    {
        // Arrange
        string text = "My name is __Alice__";

        // Act
        List<MarkdownSegment> segments = MarkdownParser.Parse(text);

        // Assert
        Assert.Equal(2, segments.Count);
        Assert.Equal("My name is ", segments[0].Text);
        Assert.False(segments[0].IsBold);
        Assert.Equal("Alice", segments[1].Text);
        Assert.True(segments[1].IsBold);
        Assert.False(segments[1].IsItalic);
    }

    [Fact]
    public void Parse_ItalicWithSingleAsterisk_ParsesCorrectly()
    {
        // Arrange
        string text = "This is *italic* text";

        // Act
        List<MarkdownSegment> segments = MarkdownParser.Parse(text);

        // Assert
        Assert.Equal(3, segments.Count);
        Assert.Equal("This is ", segments[0].Text);
        Assert.Equal("italic", segments[1].Text);
        Assert.True(segments[1].IsItalic);
        Assert.False(segments[1].IsBold);
        Assert.Equal(" text", segments[2].Text);
    }

    [Fact]
    public void Parse_ItalicWithSingleUnderscore_ParsesCorrectly()
    {
        // Arrange
        string text = "This is _italic_ text";

        // Act
        List<MarkdownSegment> segments = MarkdownParser.Parse(text);

        // Assert
        Assert.Equal(3, segments.Count);
        Assert.Equal("This is ", segments[0].Text);
        Assert.Equal("italic", segments[1].Text);
        Assert.True(segments[1].IsItalic);
        Assert.False(segments[1].IsBold);
        Assert.Equal(" text", segments[2].Text);
    }

    [Fact]
    public void Parse_Strikethrough_ParsesCorrectly()
    {
        // Arrange
        string text = "This is ~~strikethrough~~ text";

        // Act
        List<MarkdownSegment> segments = MarkdownParser.Parse(text);

        // Assert
        Assert.Equal(3, segments.Count);
        Assert.Equal("This is ", segments[0].Text);
        Assert.Equal("strikethrough", segments[1].Text);
        Assert.True(segments[1].IsStrikethrough);
        Assert.False(segments[1].IsBold);
        Assert.False(segments[1].IsItalic);
        Assert.Equal(" text", segments[2].Text);
    }

    [Fact]
    public void Parse_BoldAndItalic_ParsesCorrectly()
    {
        // Arrange
        string text = "This is ***bold and italic*** text";

        // Act
        List<MarkdownSegment> segments = MarkdownParser.Parse(text);

        // Assert
        Assert.Equal(3, segments.Count);
        Assert.Equal("This is ", segments[0].Text);
        Assert.Equal("bold and italic", segments[1].Text);
        Assert.True(segments[1].IsBold);
        Assert.True(segments[1].IsItalic);
        Assert.False(segments[1].IsStrikethrough);
        Assert.Equal(" text", segments[2].Text);
    }

    [Fact]
    public void Parse_MixedFormatting_ParsesAllSegments()
    {
        // Arrange
        string text = "Normal **bold** and *italic* and ~~strike~~";

        // Act
        List<MarkdownSegment> segments = MarkdownParser.Parse(text);

        // Assert
        Assert.Equal(6, segments.Count);

        Assert.Equal("Normal ", segments[0].Text);
        Assert.False(segments[0].IsBold);

        Assert.Equal("bold", segments[1].Text);
        Assert.True(segments[1].IsBold);

        Assert.Equal(" and ", segments[2].Text);

        Assert.Equal("italic", segments[3].Text);
        Assert.True(segments[3].IsItalic);

        Assert.Equal(" and ", segments[4].Text);

        Assert.Equal("strike", segments[5].Text);
        Assert.True(segments[5].IsStrikethrough);
    }

    [Fact]
    public void Parse_ConsecutiveBold_ParsesEachSeparately()
    {
        // Arrange
        string text = "**First** **Second**";

        // Act
        List<MarkdownSegment> segments = MarkdownParser.Parse(text);

        // Assert
        Assert.Equal(3, segments.Count);
        Assert.Equal("First", segments[0].Text);
        Assert.True(segments[0].IsBold);
        Assert.Equal(" ", segments[1].Text);
        Assert.Equal("Second", segments[2].Text);
        Assert.True(segments[2].IsBold);
    }

    [Fact]
    public void Parse_EmptyString_ReturnsEmptyList()
    {
        // Arrange
        string text = "";

        // Act
        List<MarkdownSegment> segments = MarkdownParser.Parse(text);

        // Assert
        Assert.Empty(segments);
    }

    [Fact]
    public void Parse_NullString_ReturnsEmptyList()
    {
        // Arrange
        string? text = null;

        // Act
        List<MarkdownSegment> segments = MarkdownParser.Parse(text!);

        // Assert
        Assert.Empty(segments);
    }

    [Fact]
    public void Parse_MalformedMarkdown_UnclosedBold_RendersLiterally()
    {
        // Arrange
        string text = "Hello **world";

        // Act
        List<MarkdownSegment> segments = MarkdownParser.Parse(text);

        // Assert
        Assert.Single(segments);
        Assert.Equal("Hello **world", segments[0].Text);
        Assert.False(segments[0].IsBold);
    }

    [Fact]
    public void Parse_MalformedMarkdown_UnclosedItalic_RendersLiterally()
    {
        // Arrange
        string text = "Hello *world";

        // Act
        List<MarkdownSegment> segments = MarkdownParser.Parse(text);

        // Assert
        Assert.Single(segments);
        Assert.Equal("Hello *world", segments[0].Text);
        Assert.False(segments[0].IsItalic);
    }

    [Fact]
    public void Parse_MalformedMarkdown_UnclosedStrikethrough_RendersLiterally()
    {
        // Arrange
        string text = "Hello ~~world";

        // Act
        List<MarkdownSegment> segments = MarkdownParser.Parse(text);

        // Assert
        Assert.Single(segments);
        Assert.Equal("Hello ~~world", segments[0].Text);
        Assert.False(segments[0].IsStrikethrough);
    }

    [Fact]
    public void ContainsMarkdown_WithBold_ReturnsTrue()
    {
        // Arrange
        string text = "Hello **world**";

        // Act
        bool result = MarkdownParser.ContainsMarkdown(text);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ContainsMarkdown_WithItalic_ReturnsTrue()
    {
        // Arrange
        string text = "Hello *world*";

        // Act
        bool result = MarkdownParser.ContainsMarkdown(text);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ContainsMarkdown_WithStrikethrough_ReturnsTrue()
    {
        // Arrange
        string text = "Hello ~~world~~";

        // Act
        bool result = MarkdownParser.ContainsMarkdown(text);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ContainsMarkdown_PlainText_ReturnsFalse()
    {
        // Arrange
        string text = "Hello world";

        // Act
        bool result = MarkdownParser.ContainsMarkdown(text);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ContainsMarkdown_EmptyString_ReturnsFalse()
    {
        // Arrange
        string text = "";

        // Act
        bool result = MarkdownParser.ContainsMarkdown(text);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ContainsMarkdown_NullString_ReturnsFalse()
    {
        // Arrange
        string? text = null;

        // Act
        bool result = MarkdownParser.ContainsMarkdown(text);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void Parse_BoldAtStart_ParsesCorrectly()
    {
        // Arrange
        string text = "**Bold** at start";

        // Act
        List<MarkdownSegment> segments = MarkdownParser.Parse(text);

        // Assert
        Assert.Equal(2, segments.Count);
        Assert.Equal("Bold", segments[0].Text);
        Assert.True(segments[0].IsBold);
        Assert.Equal(" at start", segments[1].Text);
        Assert.False(segments[1].IsBold);
    }

    [Fact]
    public void Parse_BoldAtEnd_ParsesCorrectly()
    {
        // Arrange
        string text = "At end **Bold**";

        // Act
        List<MarkdownSegment> segments = MarkdownParser.Parse(text);

        // Assert
        Assert.Equal(2, segments.Count);
        Assert.Equal("At end ", segments[0].Text);
        Assert.False(segments[0].IsBold);
        Assert.Equal("Bold", segments[1].Text);
        Assert.True(segments[1].IsBold);
    }

    [Fact]
    public void Parse_OnlyBold_ParsesCorrectly()
    {
        // Arrange
        string text = "**OnlyBold**";

        // Act
        List<MarkdownSegment> segments = MarkdownParser.Parse(text);

        // Assert
        Assert.Single(segments);
        Assert.Equal("OnlyBold", segments[0].Text);
        Assert.True(segments[0].IsBold);
    }
}
