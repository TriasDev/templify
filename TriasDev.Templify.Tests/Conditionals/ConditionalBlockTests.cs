// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;
using TriasDev.Templify.Conditionals;

namespace TriasDev.Templify.Tests.Conditionals;

/// <summary>
/// Unit tests for <see cref="ConditionalBlock"/> construction and its branch-derived properties.
/// </summary>
public sealed class ConditionalBlockTests
{
    private static Paragraph P(string text) => new Paragraph(new Run(new Text(text)));

    private static ConditionalBranch Branch(string? condition, string marker, params string[] content) =>
        new ConditionalBranch(condition, content.Select(c => (OpenXmlElement)P(c)).ToList(), P(marker));

    [Fact]
    public void Constructor_IfOnly_HasNoElseOrElseIf()
    {
        ConditionalBlock block = new ConditionalBlock(new[] { Branch("A", "{{#if A}}", "a") }, P("{{/if}}"));

        Assert.Equal("A", block.ConditionExpression);
        Assert.False(block.HasElseBranch);
        Assert.False(block.HasElseIfBranches);
        Assert.Null(block.ElseMarker);
        Assert.Empty(block.ElseContentElements);
        Assert.Equal("a", Assert.Single(block.IfContentElements).InnerText);
    }

    [Fact]
    public void Constructor_IfElse_HasElseButNoElseIf()
    {
        ConditionalBranch elseBranch = Branch(null, "{{#else}}", "b");
        ConditionalBlock block = new ConditionalBlock(new[] { Branch("A", "{{#if A}}", "a"), elseBranch }, P("{{/if}}"));

        Assert.True(block.HasElseBranch);
        Assert.False(block.HasElseIfBranches);
        Assert.Same(elseBranch.Marker, block.ElseMarker);
        Assert.Equal("b", Assert.Single(block.ElseContentElements).InnerText);
    }

    [Fact]
    public void Constructor_IfElseIf_HasElseIfButNoElse()
    {
        ConditionalBlock block = new ConditionalBlock(
            new[] { Branch("A", "{{#if A}}", "a"), Branch("B", "{{#elseif B}}", "b") },
            P("{{/if}}"));

        Assert.False(block.HasElseBranch);
        Assert.True(block.HasElseIfBranches);
        Assert.Null(block.ElseMarker);
    }

    [Fact]
    public void Constructor_IfElseIfElse_HasBoth()
    {
        ConditionalBlock block = new ConditionalBlock(
            new[] { Branch("A", "{{#if A}}", "a"), Branch("B", "{{#elseif B}}", "b"), Branch(null, "{{#else}}", "c") },
            P("{{/if}}"),
            isTableRowConditional: true,
            nestingLevel: 2);

        Assert.True(block.HasElseBranch);
        Assert.True(block.HasElseIfBranches);
        Assert.Equal("c", Assert.Single(block.ElseContentElements).InnerText);
        Assert.True(block.IsTableRowConditional);
        Assert.Equal(2, block.NestingLevel);
    }

    [Fact]
    public void Constructor_NoBranches_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new ConditionalBlock(Array.Empty<ConditionalBranch>(), P("{{/if}}")));
        Assert.Throws<ArgumentException>(() => new ConditionalBlock((IReadOnlyList<ConditionalBranch>)null!, P("{{/if}}")));
    }

    [Fact]
    public void Constructor_FirstBranchIsElse_ThrowsArgumentException()
    {
        ArgumentException ex = Assert.Throws<ArgumentException>(
            () => new ConditionalBlock(new[] { Branch(null, "{{#else}}", "x") }, P("{{/if}}")));
        Assert.Equal("branches", ex.ParamName);
    }

    [Fact]
    public void Constructor_NullEndMarker_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new ConditionalBlock(new[] { Branch("A", "{{#if A}}") }, null!));
    }

    [Fact]
    public void LegacyConstructor_WithElseMarker_CreatesElseBranch()
    {
        Paragraph start = P("{{#if A}}");
        Paragraph elseMarker = P("{{#else}}");
        ConditionalBlock block = new ConditionalBlock(
            "A", new List<OpenXmlElement> { P("a") }, new List<OpenXmlElement> { P("b") }, start, elseMarker, P("{{/if}}"));

        Assert.Equal(2, block.Branches.Count);
        Assert.Same(start, block.StartMarker);
        Assert.Same(elseMarker, block.ElseMarker);
        Assert.Equal("b", Assert.Single(block.ElseContentElements).InnerText);
    }

    [Fact]
    public void LegacyConstructor_ElseContentWithoutMarker_FallsBackToStartMarker()
    {
        Paragraph start = P("{{#if A}}");
        ConditionalBlock block = new ConditionalBlock(
            "A", new List<OpenXmlElement> { P("a") }, new List<OpenXmlElement> { P("b") }, start, null, P("{{/if}}"));

        Assert.True(block.HasElseBranch);
        Assert.Same(start, block.ElseMarker);
    }

    [Fact]
    public void LegacyConstructor_ElseMarkerWithoutContent_HasNoElseBranch()
    {
        ConditionalBlock block = new ConditionalBlock(
            "A", new List<OpenXmlElement> { P("a") }, new List<OpenXmlElement>(), P("{{#if A}}"), P("{{#else}}"), P("{{/if}}"));

        Assert.False(block.HasElseBranch);
        Assert.Single(block.Branches);
    }

    [Fact]
    public void LegacyConstructor_NullArguments_Throw()
    {
        List<OpenXmlElement> content = new List<OpenXmlElement>();
        Assert.Throws<ArgumentNullException>(() => new ConditionalBlock(null!, content, content, P("s"), null, P("e")));
        Assert.Throws<ArgumentNullException>(() => new ConditionalBlock("A", null!, content, P("s"), null, P("e")));
        Assert.Throws<ArgumentNullException>(() => new ConditionalBlock("A", content, content, null!, null, P("e")));
        Assert.Throws<ArgumentNullException>(() => new ConditionalBlock("A", content, content, P("s"), null, null!));
    }
}
