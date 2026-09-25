// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;
using TriasDev.Templify.Core;

namespace TriasDev.Templify.Tests.Core;

/// <summary>
/// Tests for TextTemplateProcessor feature parity with the Word processor (#150).
/// </summary>
public sealed class TextTemplateParityTests
{
    private static TextProcessingResult Process(string template, Dictionary<string, object> data, PlaceholderReplacementOptions? options = null)
        => new TextTemplateProcessor(options ?? new PlaceholderReplacementOptions { Culture = CultureInfo.InvariantCulture })
            .ProcessTemplate(template, data);

    private sealed class Product
    {
        public string Name { get; init; } = string.Empty;
        public decimal Price { get; init; }
    }

    private sealed class Category
    {
        public string Name { get; init; } = string.Empty;
        public List<Product> Products { get; init; } = new();
    }

    #region Named iteration variables and nested loops

    [Fact]
    public void Foreach_NamedIterationVariable_IsSupported()
    {
        TextProcessingResult result = Process(
            "{{#foreach item in Items}}[{{item.Name}}]{{/foreach}}",
            new Dictionary<string, object>
            {
                ["Items"] = new List<Product> { new() { Name = "A" }, new() { Name = "B" } }
            });

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Equal("[A][B]", result.ProcessedText);
        Assert.Empty(result.MissingVariables);
    }

    [Fact]
    public void Foreach_NamedIterationVariable_DirectReference()
    {
        TextProcessingResult result = Process(
            "{{#foreach tag in Tags}}{{tag}};{{/foreach}}",
            new Dictionary<string, object> { ["Tags"] = new[] { "x", "y" } });

        Assert.Equal("x;y;", result.ProcessedText);
    }

    [Fact]
    public void NestedNamedLoops_AccessParentLoopVariable()
    {
        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Categories"] = new List<Category>
            {
                new() { Name = "Fruit", Products = new() { new() { Name = "Apple" }, new() { Name = "Pear" } } },
                new() { Name = "Veg", Products = new() { new() { Name = "Leek" } } },
            }
        };

        TextProcessingResult result = Process(
            "{{#foreach category in Categories}}{{#foreach product in category.Products}}{{category.Name}}:{{product.Name}}({{@index}}/{{@count}}) {{/foreach}}{{/foreach}}",
            data);

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Equal("Fruit:Apple(0/2) Fruit:Pear(1/2) Veg:Leek(0/1) ", result.ProcessedText);
    }

    [Fact]
    public void NestedImplicitLoops_ResolveOuterItemPropertiesThroughParentChain()
    {
        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Categories"] = new List<Category>
            {
                new() { Name = "Fruit", Products = new() { new() { Name = "Apple", Price = 1 } } },
            }
        };

        TextProcessingResult result = Process(
            "{{#foreach Categories}}{{#foreach Products}}{{Name}} {{Price}}{{/foreach}}{{/foreach}}",
            data);

        Assert.Equal("Apple 1", result.ProcessedText);
    }

    [Fact]
    public void Foreach_ReservedIterationVariable_ReturnsFailure()
    {
        TextProcessingResult result = Process("{{#foreach in in Items}}x{{/foreach}}",
            new Dictionary<string, object> { ["Items"] = new[] { 1 } });

        Assert.False(result.IsSuccess);
        Assert.Contains("reserved keyword", result.ErrorMessage);
    }

    [Fact]
    public void Foreach_MetadataPrefixIterationVariable_ReturnsFailure()
    {
        TextProcessingResult result = Process("{{#foreach @item in Items}}x{{/foreach}}",
            new Dictionary<string, object> { ["Items"] = new[] { 1 } });

        Assert.False(result.IsSuccess);
        Assert.Contains("reserved for loop metadata", result.ErrorMessage);
    }

    [Fact]
    public void Foreach_BracketPathCollection_IsSupported()
    {
        TextProcessingResult result = Process(
            "{{#foreach Groups[0].Products}}{{Name}}{{/foreach}}",
            new Dictionary<string, object>
            {
                ["Groups"] = new List<Category> { new() { Products = new() { new() { Name = "A" } } } }
            });

        Assert.Equal("A", result.ProcessedText);
    }

    [Fact]
    public void Foreach_OverString_ReturnsFailure()
    {
        TextProcessingResult result = Process("{{#foreach Name}}x{{/foreach}}",
            new Dictionary<string, object> { ["Name"] = "abc" });

        Assert.False(result.IsSuccess);
        Assert.Contains("is not a collection", result.ErrorMessage);
    }

    [Fact]
    public void Foreach_NullCollection_RendersNothingAndWarns()
    {
        TextProcessingResult result = Process("a{{#foreach Items}}x{{/foreach}}b",
            new Dictionary<string, object> { ["Items"] = null! });

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Equal("ab", result.ProcessedText);
        ProcessingWarning warning = Assert.Single(result.Warnings);
        Assert.Equal(ProcessingWarningType.NullLoopCollection, warning.Type);
    }

    [Fact]
    public void Foreach_MissingCollection_RendersNothingAndWarns()
    {
        TextProcessingResult result = Process("a{{#foreach Items}}x{{/foreach}}b", new Dictionary<string, object>());

        Assert.True(result.IsSuccess);
        Assert.Equal("ab", result.ProcessedText);
        Assert.Contains("Items", result.MissingVariables);
        Assert.Equal(ProcessingWarningType.MissingLoopCollection, Assert.Single(result.Warnings).Type);
    }

    [Fact]
    public void Foreach_MissingCollection_ThrowException_ReturnsFailure()
    {
        TextProcessingResult result = Process("{{#foreach Items}}x{{/foreach}}", new Dictionary<string, object>(),
            new PlaceholderReplacementOptions { MissingVariableBehavior = MissingVariableBehavior.ThrowException });

        Assert.False(result.IsSuccess);
        Assert.Contains("Collection not found: Items", result.ErrorMessage);
    }

    #endregion

    #region Elseif and conditionals

    [Theory]
    [InlineData("Active", "A")]
    [InlineData("Pending", "P")]
    [InlineData("Closed", "C")]
    [InlineData("Other", "X")]
    public void ElseIf_Chain_PicksFirstMatchingBranch(string status, string expected)
    {
        TextProcessingResult result = Process(
            "{{#if Status = \"Active\"}}A{{#elseif Status = \"Pending\"}}P{{#elseif Status = \"Closed\"}}C{{#else}}X{{/if}}",
            new Dictionary<string, object> { ["Status"] = status });

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Equal(expected, result.ProcessedText);
    }

    [Fact]
    public void ElseIf_WithoutElse_NoMatch_RendersNothing()
    {
        TextProcessingResult result = Process("[{{#if A}}a{{#elseif B}}b{{/if}}]",
            new Dictionary<string, object> { ["A"] = false, ["B"] = false });

        Assert.Equal("[]", result.ProcessedText);
    }

    [Fact]
    public void ElseIf_AfterElse_ReturnsFailure()
    {
        TextProcessingResult result = Process("{{#if A}}1{{#else}}2{{#elseif B}}3{{/if}}",
            new Dictionary<string, object> { ["A"] = false, ["B"] = true });

        Assert.False(result.IsSuccess);
        Assert.Contains("cannot appear after", result.ErrorMessage);
    }

    [Fact]
    public void NestedIfElse_AreMatchedByDepth()
    {
        const string template = "{{#if A}}{{#if B}}1{{#else}}2{{/if}}{{#elseif C}}{{#if B}}3{{#else}}4{{/if}}{{#else}}5{{/if}}";

        Assert.Equal("1", Process(template, new() { ["A"] = true, ["B"] = true, ["C"] = false }).ProcessedText);
        Assert.Equal("2", Process(template, new() { ["A"] = true, ["B"] = false, ["C"] = false }).ProcessedText);
        Assert.Equal("3", Process(template, new() { ["A"] = false, ["B"] = true, ["C"] = true }).ProcessedText);
        Assert.Equal("4", Process(template, new() { ["A"] = false, ["B"] = false, ["C"] = true }).ProcessedText);
        Assert.Equal("5", Process(template, new() { ["A"] = false, ["B"] = false, ["C"] = false }).ProcessedText);
    }

    [Fact]
    public void ConditionInsideLoop_UsesLoopContext()
    {
        TextProcessingResult result = Process(
            "{{#foreach Items}}{{#if @first}}first {{#elseif @last}}last{{#else}}mid {{/if}}{{/foreach}}",
            new Dictionary<string, object> { ["Items"] = new[] { 1, 2, 3 } });

        Assert.Equal("first mid last", result.ProcessedText);
    }

    [Fact]
    public void MalformedCondition_WarnsAndTakesElse()
    {
        TextProcessingResult result = Process("{{#if A && B}}y{{#elseif A}}a{{/if}}",
            new Dictionary<string, object> { ["A"] = true, ["B"] = true });

        Assert.Equal("a", result.ProcessedText);
        Assert.Equal(ProcessingWarningType.ExpressionFailed, Assert.Single(result.Warnings).Type);
    }

    [Fact]
    public void MultiLineCondition_IsSupported()
    {
        TextProcessingResult result = Process("{{#if A and\n B}}yes{{/if}}",
            new Dictionary<string, object> { ["A"] = true, ["B"] = true });

        Assert.Equal("yes", result.ProcessedText);
    }

    #endregion

    #region Syntax errors and literal markers

    [Fact]
    public void MalformedIfMarker_ReturnsFailure()
    {
        TextProcessingResult result = Process("Text {{#if A", new Dictionary<string, object> { ["A"] = true });

        Assert.False(result.IsSuccess);
        Assert.Contains("Malformed {{#if}} tag at position 5", result.ErrorMessage);
    }

    [Fact]
    public void MalformedForeachMarker_ReturnsFailure()
    {
        TextProcessingResult result = Process("{{#foreach a b c}}x{{/foreach}}", new Dictionary<string, object>());

        Assert.False(result.IsSuccess);
        Assert.Contains("Malformed {{#foreach}} tag at position 0", result.ErrorMessage);
    }

    [Fact]
    public void UnmatchedIf_ReportsOutermostPosition()
    {
        TextProcessingResult result = Process("ab{{#if A}}{{#if B}}x{{/if}}", new Dictionary<string, object>());

        Assert.False(result.IsSuccess);
        Assert.Equal("Processing failed: Unmatched {{#if}} tag at position 2", result.ErrorMessage);
    }

    [Fact]
    public void UnmatchedForeach_ReturnsFailure()
    {
        TextProcessingResult result = Process("{{#foreach Items}}x", new Dictionary<string, object> { ["Items"] = new[] { 1 } });

        Assert.False(result.IsSuccess);
        Assert.Contains("Unmatched {{#foreach}} tag at position 0", result.ErrorMessage);
    }

    [Theory]
    [InlineData("{{#if A}}{{#foreach Items}}x{{/if}}{{/foreach}}", "Unmatched {{#foreach}} tag at position 9")]
    [InlineData("{{#foreach Items}}{{#if A}}x{{/foreach}}{{/if}}", "Unmatched {{#if}} tag at position 18")]
    public void MisnestedBlocks_ReturnFailure(string template, string expected)
    {
        TextProcessingResult result = Process(template, new Dictionary<string, object> { ["A"] = true, ["Items"] = new[] { 1 } });

        Assert.False(result.IsSuccess);
        Assert.Contains(expected, result.ErrorMessage);
    }

    [Theory]
    [InlineData("a {{/if}} b")]
    [InlineData("a {{/foreach}} b")]
    [InlineData("a {{#else}} b")]
    [InlineData("a {{#elseif X}} b")]
    [InlineData("a {{#if}} b")]
    public void StrayMarkers_AreKeptAsText(string template)
    {
        TextProcessingResult result = Process(template, new Dictionary<string, object>());

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Equal(template, result.ProcessedText);
    }

    #endregion

    #region Case-insensitive markers

    [Fact]
    public void Markers_AreCaseInsensitive()
    {
        TextProcessingResult result = Process(
            "{{#IF A}}a{{#ElseIf B}}b{{#ELSE}}c{{/If}}|{{#ForEach x IN Items}}{{x}}{{/FOREACH}}",
            new Dictionary<string, object> { ["A"] = false, ["B"] = true, ["Items"] = new[] { 1, 2 } });

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Equal("b|12", result.ProcessedText);
    }

    [Fact]
    public void Markers_AreCultureInvariant_TurkishCulture()
    {
        CultureInfo original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("tr-TR");
            TextProcessingResult result = Process("{{#IF IsActive}}yes{{/IF}}",
                new Dictionary<string, object> { ["IsActive"] = true });

            Assert.Equal("yes", result.ProcessedText);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    #endregion

    #region Inline expressions, TextReplacements, formats

    [Fact]
    public void InlineExpression_IsEvaluated()
    {
        TextProcessingResult result = Process("{{(A and B)}} {{(Count > 3):yesno}}",
            new Dictionary<string, object> { ["A"] = true, ["B"] = false, ["Count"] = 5 });

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Equal("False Yes", result.ProcessedText);
        Assert.Equal(2, result.ReplacementCount);
    }

    [Fact]
    public void InlineExpression_Invalid_WarnsAndIsMissing()
    {
        TextProcessingResult result = Process("[{{(A === B)}}]", new Dictionary<string, object> { ["A"] = 1, ["B"] = 1 });

        Assert.True(result.IsSuccess);
        Assert.Equal("[{{(A === B)}}]", result.ProcessedText);
        Assert.Contains("(A === B)", result.MissingVariables);
        Assert.Contains(result.Warnings, w => w.Type == ProcessingWarningType.ExpressionFailed);
        Assert.Contains(result.Warnings, w => w.Type == ProcessingWarningType.MissingVariable);
    }

    [Fact]
    public void TextReplacements_AreAppliedToValues()
    {
        PlaceholderReplacementOptions options = new PlaceholderReplacementOptions
        {
            TextReplacements = new Dictionary<string, string> { ["<br>"] = "\n", ["&amp;"] = "&" }
        };

        TextProcessingResult result = Process("{{Text}} <br>", new Dictionary<string, object> { ["Text"] = "a<br>b &amp; c" }, options);

        // Only values are affected, not the template text.
        Assert.Equal("a\nb & c <br>", result.ProcessedText);
    }

    [Fact]
    public void RawFormat_IsAccepted()
    {
        TextProcessingResult result = Process("{{Text:raw}}", new Dictionary<string, object> { ["Text"] = "**bold**" });

        Assert.Equal("**bold**", result.ProcessedText);
    }

    [Fact]
    public void ValuesContainingMarkers_AreNotReprocessed()
    {
        TextProcessingResult result = Process("{{#foreach Items}}{{.}}{{/foreach}}",
            new Dictionary<string, object> { ["Items"] = new[] { "{{Secret}}" }, ["Secret"] = "leaked" });

        Assert.Equal("{{Secret}}", result.ProcessedText);
    }

    #endregion

    #region Warnings

    [Fact]
    public void MissingPlaceholder_AddsMissingVariableWarning()
    {
        TextProcessingResult result = Process("Hi {{Name}}", new Dictionary<string, object>());

        Assert.True(result.HasWarnings);
        ProcessingWarning warning = Assert.Single(result.Warnings);
        Assert.Equal(ProcessingWarningType.MissingVariable, warning.Type);
        Assert.Equal("Name", warning.VariableName);
    }

    #endregion

    #region Processing order

    [Fact]
    public void FalseConditional_ContentIsNotEvaluated()
    {
        // Conditionals are evaluated first (like the Word processor): a loop over a non-collection and
        // missing variables in a branch that is not taken do not fail or show up as missing.
        TextProcessingResult result = Process(
            "{{#if HasItems}}{{#foreach Items}}{{Missing}}{{/foreach}}{{#else}}none{{/if}}",
            new Dictionary<string, object> { ["HasItems"] = false, ["Items"] = 42 });

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Equal("none", result.ProcessedText);
        Assert.Empty(result.MissingVariables);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void FalseConditional_ThrowException_DoesNotThrowForDeadBranch()
    {
        TextProcessingResult result = Process(
            "{{#if Show}}{{#foreach Items}}{{Name}}{{/foreach}}{{/if}}ok",
            new Dictionary<string, object> { ["Show"] = false, ["Items"] = new[] { new { Other = 1 } } },
            new PlaceholderReplacementOptions { MissingVariableBehavior = MissingVariableBehavior.ThrowException });

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Equal("ok", result.ProcessedText);
    }

    #endregion

    #region No block limit, scalability

    [Fact]
    public void MoreThan100Blocks_AreAllProcessed()
    {
        StringBuilder template = new StringBuilder();
        StringBuilder expected = new StringBuilder();
        for (int i = 0; i < 150; i++)
        {
            template.Append("{{#if A}}a{{/if}}{{#foreach Items}}{{.}}{{/foreach}},");
            expected.Append("a12,");
        }

        TextProcessingResult result = Process(template.ToString(),
            new Dictionary<string, object> { ["A"] = true, ["Items"] = new[] { 1, 2 } });

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Equal(expected.ToString(), result.ProcessedText);
        Assert.DoesNotContain("{{", result.ProcessedText);
    }

    [Fact]
    public void DeepNesting_BeyondOldLimit_IsProcessed()
    {
        StringBuilder template = new StringBuilder();
        for (int i = 0; i < 120; i++)
        {
            template.Append("{{#if A}}");
        }

        template.Append("deep");
        for (int i = 0; i < 120; i++)
        {
            template.Append("{{/if}}");
        }

        TextProcessingResult result = Process(template.ToString(), new Dictionary<string, object> { ["A"] = true });

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Equal("deep", result.ProcessedText);
    }

    [Fact]
    public void LargeTemplate_IsProcessed()
    {
        StringBuilder template = new StringBuilder();
        for (int i = 0; i < 20000; i++)
        {
            template.Append("{{#if A}}x{{#else}}y{{/if}}{{Name}} ");
        }

        TextProcessingResult result = Process(template.ToString(),
            new Dictionary<string, object> { ["A"] = true, ["Name"] = "n" });

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Equal(20000, result.ReplacementCount);
        Assert.StartsWith("xn xn ", result.ProcessedText);
    }

    #endregion
}
