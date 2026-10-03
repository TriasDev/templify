// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.IO.Compression;
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
        Assert.Empty(result.Errors);
        Assert.Empty(result.Warnings);
        Assert.Empty(result.MissingVariables);
        Assert.Equal(new[] { "@index", "Company", "Count", "IsVip", "Items", "Name", "Split", "Title" }, result.AllPlaceholders);
    }

    [Theory]
    [InlineData("{{#if A}}", ValidationErrorType.UnmatchedConditionalStart, "Conditional start marker '{{#if A}}' has no matching '{{/if}}'.")]
    [InlineData("{{#foreach Items}}", ValidationErrorType.UnmatchedLoopStart, "Loop start marker '{{#foreach Items}}' has no matching '{{/foreach}}'.")]
    [InlineData("{{#if Count >}}x{{/if}}", ValidationErrorType.InvalidConditionalExpression, "Invalid condition 'Count >': ")]
    public void SyntaxErrors_AreReported(string paragraph, ValidationErrorType type, string message)
    {
        ValidationResult result = Validate(Paragraphs(paragraph, "x"));

        Assert.False(result.IsValid);
        ValidationError error = Assert.Single(result.Errors);
        Assert.Equal(type, error.Type);
        Assert.StartsWith(message, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void InvalidCondition_ReportsLocation_AndEachExpressionOnce()
    {
        ValidationResult result = Validate(Paragraphs("{{#if Count >}}", "a", "{{/if}}", "{{#if Count >}}", "b", "{{/if}}"));

        ValidationError error = Assert.Single(result.Errors);
        Assert.Equal(ValidationErrorType.InvalidConditionalExpression, error.Type);
        Assert.Equal("{{#if Count >}}", error.Location);
    }

    [Fact]
    public void ElseIfAfterElse_IsReported()
    {
        ValidationResult result = Validate(Paragraphs("{{#if A}}", "a", "{{#else}}", "b", "{{#elseif B}}", "c", "{{/if}}"));

        ValidationError error = Assert.Single(result.Errors);
        Assert.Equal(ValidationErrorType.InvalidConditionalExpression, error.Type);
        Assert.Contains("{{#elseif}}", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReservedWordAsVariable_AddsWarning_OnlyWhenTheDataHasIt()
    {
        OdtDocumentBuilder template = Paragraphs("{{#if contains}}", "x", "{{/if}}");

        ValidationResult withoutData = Validate(template);
        ValidationResult withData = Validate(template, new Dictionary<string, object> { ["contains"] = true });

        Assert.Empty(withoutData.Warnings);
        Assert.True(withData.IsValid);
        ValidationWarning warning = Assert.Single(withData.Warnings);
        Assert.Equal(ValidationWarningType.ReservedWordAsVariable, warning.Type);
        Assert.Equal(
            "Condition 'contains' uses 'contains', which is also a keyword, as a variable. Write '[contains]' to reference the variable unambiguously.",
            warning.Message);
        Assert.Equal("{{#if contains}}", warning.Location);
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
        Assert.Equal(
            new[]
            {
                "UnmatchedConditionalStart: Conditional start marker '{{#if B}}' has no matching '{{/if}}'.",
                "UnmatchedConditionalStart: List item conditional start marker '{{#if A}}' has no matching '{{/if}}'.",
                "UnmatchedLoopStart: Table row loop start marker '{{#foreach Rows}}' has no matching '{{/foreach}}'.",
            },
            result.Errors.Select(e => $"{e.Type}: {e.Message}").Order(StringComparer.Ordinal));
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
        Assert.Equal(
            new[]
            {
                "Variable 'Gone' is referenced in the template but not provided in the data.",
                "Variable 'Missing' is referenced in the template but not provided in the data.",
                "Variable 'item.Nope' is referenced in the template but not provided in the data.",
            },
            result.Errors.Select(e => e.Message).Order(StringComparer.Ordinal));
        Assert.All(result.Errors, e => Assert.Equal(ValidationErrorType.MissingVariable, e.Type));
        Assert.Equal(
            new[] { "Cell", "Gone", "Items", "Missing", "Name", "Rows", "item.Nope", "item.Title" },
            result.AllPlaceholders.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void EmptyLoopCollection_AddsWarning_AndMissingCollectionIsError()
    {
        OdtDocumentBuilder template = Paragraphs("{{#foreach Empty}}", "{{X}}", "{{/foreach}}", "{{#foreach Absent}}", "{{Y}}", "{{/foreach}}");

        ValidationResult result = Validate(template, new Dictionary<string, object> { ["Empty"] = new List<object>() });

        ValidationWarning warning = Assert.Single(result.Warnings);
        Assert.Equal(ValidationWarningType.EmptyLoopCollection, warning.Type);
        Assert.Equal("Collection 'Empty' is empty. Variables inside this loop could not be validated.", warning.Message);
        Assert.Equal(new[] { "Absent" }, result.MissingVariables);
        ValidationError error = Assert.Single(result.Errors);
        Assert.Equal(ValidationErrorType.MissingVariable, error.Type);
        Assert.Equal("Collection 'Absent' is referenced in a loop but not provided in the data.", error.Message);
    }

    [Fact]
    public void EmptyLoopCollection_WarningCanBeDisabled()
    {
        OdtTemplateProcessor processor = new OdtTemplateProcessor(new PlaceholderReplacementOptions { WarnOnEmptyLoopCollections = false });
        using MemoryStream stream = Paragraphs("{{#foreach Empty}}", "{{X}}", "{{/foreach}}").ToStream();

        ValidationResult result = processor.ValidateTemplate(stream, new Dictionary<string, object> { ["Empty"] = new List<object>() });

        Assert.True(result.IsValid);
        Assert.Empty(result.Warnings);
        Assert.Empty(result.MissingVariables);
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
        Assert.Empty(withData.Warnings);
        Assert.Equal(new[] { "@index", "Cell", "Flag", "Items", "Rows", "item" }, result.AllPlaceholders.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void TableRowLoop_UnmatchedInsideCell_IsStillReported()
    {
        OdtDocumentBuilder template = new OdtDocumentBuilder()
            .AddTable(new[] { "{{#foreach Rows}}" }, new[] { "{{#if A}}" }, new[] { "{{/foreach}}" });

        ValidationResult result = Validate(template);

        // Reported once, with the row-level message that processing fails with (as for Word templates).
        ValidationError error = Assert.Single(result.Errors);
        Assert.Equal(ValidationErrorType.UnmatchedConditionalStart, error.Type);
        Assert.Equal("Table row conditional start marker '{{#if A}}' has no matching '{{/if}}'.", error.Message);
    }

    [Fact]
    public void UnmatchedListItemMarkers_AreReportedOnce_WithTheMessageProcessingFailsWith()
    {
        OdtDocumentBuilder template = new OdtDocumentBuilder()
            .AddXml("<text:list><text:list-item><text:p>{{#if A}}</text:p></text:list-item><text:list-item><text:p>b</text:p></text:list-item></text:list>")
            .AddXml("<text:list><text:list-item><text:p>{{#foreach Items}}</text:p></text:list-item><text:list-item><text:p>b</text:p></text:list-item></text:list>");
        byte[] bytes = template.ToBytes();

        ValidationResult result = new OdtTemplateProcessor().ValidateTemplate(new MemoryStream(bytes));
        ProcessingResult processing = new OdtTemplateProcessor().ProcessTemplate(
            bytes,
            new Dictionary<string, object> { ["A"] = true, ["Items"] = new List<int> { 1 } },
            out _);

        Assert.Equal(
            new[]
            {
                "UnmatchedConditionalStart: List item conditional start marker '{{#if A}}' has no matching '{{/if}}'.",
                "UnmatchedLoopStart: List item loop start marker '{{#foreach Items}}' has no matching '{{/foreach}}'.",
            },
            result.Errors.Select(e => $"{e.Type}: {e.Message}").Order(StringComparer.Ordinal));
        Assert.False(processing.IsSuccess);
        Assert.Contains(processing.ErrorMessage!, result.Errors.Select(e => "Processing failed: " + e.Message));
    }

    [Fact]
    public void SameUnmatchedMarker_InTwoParagraphs_IsReportedOnce()
    {
        ValidationResult result = Validate(new OdtDocumentBuilder()
            .AddTable(new[] { "{{#if A}} x" }, new[] { "y" })
            .AddParagraph("{{#if A}}"));

        ValidationError error = Assert.Single(result.Errors);
        Assert.Equal("Conditional start marker '{{#if A}}' has no matching '{{/if}}'.", error.Message);
    }

    [Fact]
    public void ReadOnlyDictionaryOverload_Works()
    {
        OdtTemplateProcessor processor = new OdtTemplateProcessor();
        using MemoryStream stream = Paragraphs("{{A}}").ToStream();

        ValidationResult result = processor.ValidateTemplate(stream, (IReadOnlyDictionary<string, object?>)new Dictionary<string, object?> { ["A"] = null });

        Assert.True(result.IsValid);
        Assert.Empty(result.MissingVariables);
        Assert.Equal(new[] { "A" }, result.AllPlaceholders);
    }

    [Fact]
    public void InvalidPackage_IsReportedAsError()
    {
        using MemoryStream stream = new MemoryStream(Encoding.UTF8.GetBytes("not a zip"));

        ValidationResult result = new OdtTemplateProcessor().ValidateTemplate(stream);

        Assert.False(result.IsValid);
        ValidationError error = Assert.Single(result.Errors);
        Assert.Equal(ValidationErrorType.InvalidDocument, error.Type);
        Assert.Equal(
            "Invalid document: the template is not an OpenDocument Text package (.odt/.ott). Flat OpenDocument (.fodt) is not supported.",
            error.Message);
        Assert.Empty(result.AllPlaceholders);
    }

    [Theory]
    [InlineData("<office:document-content", "Invalid document: content.xml is not well-formed XML")]
    [InlineData("<root/>", "Invalid document: content.xml has no text body (office:text).")]
    public void CorruptedContent_IsInvalidDocument(string contentXml, string message)
    {
        using MemoryStream stream = new MemoryStream(CreateZip(("mimetype", OdtDocumentBuilder.TextMediaType), ("content.xml", contentXml)));

        ValidationResult result = new OdtTemplateProcessor().ValidateTemplate(stream, new Dictionary<string, object>());

        ValidationError error = Assert.Single(result.Errors);
        Assert.Equal(ValidationErrorType.InvalidDocument, error.Type);
        Assert.StartsWith(message, error.Message, StringComparison.Ordinal);
        Assert.Empty(result.MissingVariables);
    }

    [Fact]
    public void MissingContentXml_IsInvalidDocument()
    {
        using MemoryStream stream = new MemoryStream(CreateZip(("mimetype", OdtDocumentBuilder.TextMediaType), ("styles.xml", "<x/>")));

        ValidationResult result = new OdtTemplateProcessor().ValidateTemplate(stream);

        ValidationError error = Assert.Single(result.Errors);
        Assert.Equal(ValidationErrorType.InvalidDocument, error.Type);
        Assert.Equal("Invalid document: content.xml is missing.", error.Message);
    }

    [Fact]
    public void UnreadableStream_IsInvalidDocument()
    {
        using WriteOnlyStream stream = new WriteOnlyStream();

        ValidationResult result = new OdtTemplateProcessor().ValidateTemplate(stream);

        ValidationError error = Assert.Single(result.Errors);
        Assert.Equal(ValidationErrorType.InvalidDocument, error.Type);
        Assert.Equal("Invalid template stream: the template stream must be readable.", error.Message);
    }

    [Fact]
    public void CorruptedDocx_KeepsInvalidPlaceholderSyntax_ForCompatibility()
    {
        // The Word validator shipped in 1.x; its error type for unreadable input changes only in 2.0 (#156).
        using MemoryStream stream = new MemoryStream(Encoding.UTF8.GetBytes("not a zip"));

        ValidationResult result = new DocumentTemplateProcessor().ValidateTemplate(stream);

        ValidationError error = Assert.Single(result.Errors);
        Assert.Equal(ValidationErrorType.InvalidPlaceholderSyntax, error.Type);
        Assert.StartsWith("Validation failed: ", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void NullArguments_Throw()
    {
        OdtTemplateProcessor processor = new OdtTemplateProcessor();

        Assert.Throws<ArgumentNullException>(() => processor.ValidateTemplate(null!));
        Assert.Throws<ArgumentNullException>(() => processor.ValidateTemplate(new MemoryStream(), (Dictionary<string, object>)null!));
    }

    [Fact]
    public void HeaderRegions_AreValidated()
    {
        OdtDocumentBuilder template = new OdtDocumentBuilder()
            .AddParagraph("Body")
            .AddHeaderXml(
                "<style:region-left><text:p>{{#if B}}</text:p></style:region-left>" +
                "<style:region-right><text:p>{{#foreach Items}}</text:p><text:p>{{Title}}</text:p><text:p>{{/foreach}}</text:p></style:region-right>");

        ValidationResult result = Validate(template, new Dictionary<string, object>
        {
            ["B"] = true,
            ["Items"] = new List<Dictionary<string, object>> { new() { ["Name"] = "a" } },
        });

        Assert.False(result.IsValid);
        Assert.Equal(
            new[]
            {
                "MissingVariable: Variable 'Title' is referenced in the template but not provided in the data.",
                "UnmatchedConditionalStart: Conditional start marker '{{#if B}}' has no matching '{{/if}}'.",
            },
            result.Errors.Select(e => $"{e.Type}: {e.Message}").Order(StringComparer.Ordinal));
        Assert.Equal(new[] { "Title" }, result.MissingVariables);
    }

    private static byte[] CreateZip(params (string Name, string Content)[] entries)
    {
        using MemoryStream stream = new MemoryStream();
        using (ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach ((string name, string content) in entries)
            {
                using Stream entryStream = archive.CreateEntry(name, CompressionLevel.NoCompression).Open();
                entryStream.Write(Encoding.UTF8.GetBytes(content));
            }
        }

        return stream.ToArray();
    }

    private sealed class WriteOnlyStream : MemoryStream
    {
        public override bool CanRead => false;

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override int Read(Span<byte> buffer) => throw new NotSupportedException();
    }
}
