// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Text;
using TriasDev.Templify.Core;
using TriasDev.Templify.Tests.Helpers;

namespace TriasDev.Templify.Tests.Odt;

/// <summary>
/// <see cref="OdtTemplateProcessor.ValidateTemplate(Stream)"/> and its data overloads.
/// </summary>
public sealed class OdtValidationTests
{
    private static ValidationResult Validate(OdtDocumentBuilder template, Dictionary<string, object>? data = null)
    {
        OdtTemplateProcessor processor = new OdtTemplateProcessor(OdtTestHelper.InvariantOptions());
        using MemoryStream stream = template.ToStream();
        return data == null ? processor.ValidateTemplate(stream) : processor.ValidateTemplate(stream, data);
    }

    private static OdtDocumentBuilder Paragraphs(params string[] texts)
    {
        OdtDocumentBuilder builder = new OdtDocumentBuilder();
        foreach (string text in texts)
        {
            builder.AddParagraph(text);
        }

        return builder;
    }

    [Fact]
    public void ValidTemplate_ListsAllPlaceholders()
    {
        OdtDocumentBuilder template = Paragraphs("Hello {{Name}}", "{{#if IsVip and Count > 2}}", "vip", "{{/if}}",
                "{{#foreach Items}}", "{{Title}} {{@index}}", "{{/foreach}}")
            .AddXml("<text:p>{{Spl<text:span text:style-name=\"T1\">it}}</text:span></text:p>")
            .AddHeaderParagraph("{{Company}}");

        ValidationResult result = Validate(template);

        Assert.True(result.IsValid);
        Assert.Equal(new[] { "@index", "Company", "Count", "IsVip", "Items", "Name", "Split", "Title" }, result.AllPlaceholders);
    }

    [Theory]
    [InlineData("{{#if A}}", "has no matching '{{/if}}'")]
    [InlineData("{{#foreach Items}}", "has no matching '{{/foreach}}'")]
    [InlineData("{{#if Count >}}x{{/if}}", "Invalid condition 'Count >'")]
    public void SyntaxErrors_AreReported(string paragraph, string message)
    {
        ValidationResult result = Validate(Paragraphs(paragraph, "x"));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Message.Contains(message, StringComparison.Ordinal));
    }

    [Fact]
    public void SyntaxErrors_InTableCellsListsAndHeaders_AreReportedOnce()
    {
        OdtDocumentBuilder template = new OdtDocumentBuilder()
            .AddTable(new[] { "{{#foreach Rows}}" }, new[] { "x" })
            .AddXml("<text:list><text:list-item><text:p>{{#if A}}</text:p></text:list-item><text:list-item><text:p>b</text:p></text:list-item></text:list>")
            .AddHeaderParagraph("{{#if B}}");

        ValidationResult result = Validate(template);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Message.Contains("Table row loop start marker '{{#foreach Rows}}'", StringComparison.Ordinal));
        Assert.Contains(result.Errors, e => e.Message.Contains("List item conditional start marker '{{#if A}}'", StringComparison.Ordinal));
        Assert.Contains(result.Errors, e => e.Message.Contains("'{{#if B}}' has no matching", StringComparison.Ordinal));
        Assert.Equal(result.Errors.Count, result.Errors.Select(e => e.Message).Distinct().Count());
    }

    [Fact]
    public void MissingVariables_AreReported_WithLoopScopes()
    {
        OdtDocumentBuilder template = Paragraphs("{{Name}} {{Missing}}", "{{#foreach item in Items}}", "{{item.Title}} {{item.Nope}} {{Name}}", "{{/foreach}}")
            .AddTable(new[] { "{{#foreach Rows}}" }, new[] { "{{Cell}} {{Gone}}" }, new[] { "{{/foreach}}" });

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Name"] = "x",
            ["Items"] = new List<Dictionary<string, object>> { new() { ["Title"] = "t" } },
            ["Rows"] = new List<Dictionary<string, object>> { new() { ["Cell"] = "c" } },
        };

        ValidationResult result = Validate(template, data);

        Assert.False(result.IsValid);
        Assert.Equal(new[] { "Gone", "Missing", "item.Nope" }, result.MissingVariables.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void EmptyLoopCollection_AddsWarning_AndMissingCollectionIsError()
    {
        OdtDocumentBuilder template = Paragraphs("{{#foreach Empty}}", "{{X}}", "{{/foreach}}", "{{#foreach Absent}}", "{{Y}}", "{{/foreach}}");

        ValidationResult result = Validate(template, new Dictionary<string, object> { ["Empty"] = new List<object>() });

        Assert.Contains(result.Warnings, w => w.Type == ValidationWarningType.EmptyLoopCollection);
        Assert.Equal(new[] { "Absent" }, result.MissingVariables);
    }

    [Fact]
    public void TableRowAndListItemBlocks_AreValid()
    {
        // The marker rows and items hold the markers in their own paragraphs; those paragraphs are not markers of
        // a loop or conditional inside the cell or item.
        OdtDocumentBuilder template = new OdtDocumentBuilder()
            .AddTable(new[] { "{{#foreach Rows}}", "" }, new[] { "{{Cell}}", "{{@index}}" }, new[] { "{{/foreach}}", "" })
            .AddTable(new[] { "{{#if Flag}}" }, new[] { "x" }, new[] { "{{#else}}" }, new[] { "y" }, new[] { "{{/if}}" })
            .AddXml("<text:list><text:list-item><text:p>{{#foreach item in Items}}</text:p></text:list-item>"
                + "<text:list-item><text:p>{{item}}</text:p></text:list-item>"
                + "<text:list-item><text:p>{{/foreach}}</text:p></text:list-item></text:list>")
            .AddXml("<text:list><text:list-item><text:p>{{#if Flag}}</text:p></text:list-item>"
                + "<text:list-item><text:p>a</text:p></text:list-item>"
                + "<text:list-item><text:p>{{/if}}</text:p></text:list-item></text:list>");

        ValidationResult result = Validate(template);
        ValidationResult withData = Validate(template, new Dictionary<string, object>
        {
            ["Rows"] = new List<Dictionary<string, object>> { new() { ["Cell"] = "c" } },
            ["Items"] = new List<string> { "a" },
            ["Flag"] = true,
        });

        Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(e => e.Message)));
        Assert.True(withData.IsValid, string.Join("; ", withData.Errors.Select(e => e.Message)));
        Assert.Empty(withData.MissingVariables);
    }

    [Fact]
    public void TableRowLoop_UnmatchedInsideCell_IsStillReported()
    {
        OdtDocumentBuilder template = new OdtDocumentBuilder()
            .AddTable(new[] { "{{#foreach Rows}}" }, new[] { "{{#if A}}" }, new[] { "{{/foreach}}" });

        ValidationResult result = Validate(template);

        Assert.Contains(result.Errors, e => e.Message.Contains("'{{#if A}}'", StringComparison.Ordinal));
    }

    [Fact]
    public void ReadOnlyDictionaryOverload_Works()
    {
        OdtTemplateProcessor processor = new OdtTemplateProcessor();
        using MemoryStream stream = Paragraphs("{{A}}").ToStream();

        ValidationResult result = processor.ValidateTemplate(stream, (IReadOnlyDictionary<string, object?>)new Dictionary<string, object?> { ["A"] = null });

        Assert.True(result.IsValid);
    }

    [Fact]
    public void InvalidPackage_IsReportedAsError()
    {
        using MemoryStream stream = new MemoryStream(Encoding.UTF8.GetBytes("not a zip"));

        ValidationResult result = new OdtTemplateProcessor().ValidateTemplate(stream);

        Assert.False(result.IsValid);
        Assert.Contains("not an OpenDocument Text package", result.Errors[0].Message, StringComparison.Ordinal);
    }

    [Fact]
    public void NullArguments_Throw()
    {
        OdtTemplateProcessor processor = new OdtTemplateProcessor();

        Assert.Throws<ArgumentNullException>(() => processor.ValidateTemplate(null!));
        Assert.Throws<ArgumentNullException>(() => processor.ValidateTemplate(new MemoryStream(), (Dictionary<string, object>)null!));
    }
}
