// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using TriasDev.Templify.Conditionals;

namespace TriasDev.Templify.Tests;

public sealed class InlineConditionalParserTests
{
    private static string Content(string text, InlineConditionalBranch branch) =>
        text.Substring(branch.ContentStart, branch.ContentEnd - branch.ContentStart);

    [Fact]
    public void Parse_NoConditionals_ReturnsEmpty()
    {
        Assert.Empty(InlineConditionalParser.Parse("Hello {{Name}}"));
    }

    [Fact]
    public void Parse_SingleConditional_ReturnsRangeAndBranch()
    {
        const string text = "A {{#if Show}}B{{/if}} C";

        InlineConditional conditional = Assert.Single(InlineConditionalParser.Parse(text));

        Assert.Equal(2, conditional.StartIndex);
        Assert.Equal("{{#if Show}}B{{/if}}", text[conditional.StartIndex..conditional.EndIndex]);
        InlineConditionalBranch branch = Assert.Single(conditional.Branches);
        Assert.Equal("Show", branch.Condition);
        Assert.Equal("B", Content(text, branch));
    }

    [Fact]
    public void Parse_ElseIfAndElse_ReturnsBranchesInOrder()
    {
        const string text = "{{#if A}}1{{#elseif B}}2{{#else}}3{{/if}}";

        InlineConditional conditional = Assert.Single(InlineConditionalParser.Parse(text));

        Assert.Equal(new string?[] { "A", "B", null }, conditional.Branches.Select(b => b.Condition));
        Assert.Equal(new[] { "1", "2", "3" }, conditional.Branches.Select(b => Content(text, b)));
    }

    [Fact]
    public void Parse_NestedConditional_IsPartOfOuterBranchContent()
    {
        const string text = "{{#if A}}x{{#if B}}y{{#else}}z{{/if}}{{#else}}w{{/if}}";

        InlineConditional outer = Assert.Single(InlineConditionalParser.Parse(text));

        Assert.Equal(text.Length, outer.EndIndex);
        Assert.Equal(2, outer.Branches.Count);
        Assert.Equal("x{{#if B}}y{{#else}}z{{/if}}", Content(text, outer.Branches[0]));
        Assert.Equal("w", Content(text, outer.Branches[1]));
    }

    [Fact]
    public void Parse_MultipleConditionals_ReturnsLeftToRight()
    {
        const string text = "{{#if A}}1{{/if}} and {{#if B}}2{{/if}}";

        List<InlineConditional> conditionals = InlineConditionalParser.Parse(text);

        Assert.Equal(2, conditionals.Count);
        Assert.True(conditionals[0].EndIndex <= conditionals[1].StartIndex);
    }

    [Fact]
    public void Parse_UnmatchedIf_IsSkipped()
    {
        Assert.Empty(InlineConditionalParser.Parse("{{#if A}} never closed"));
    }

    [Fact]
    public void Parse_ElseIfAfterElse_Throws()
    {
        Assert.Throws<Core.TemplateSyntaxException>(
            () => InlineConditionalParser.Parse("{{#if A}}1{{#else}}2{{#elseif B}}3{{/if}}"));
    }
}
