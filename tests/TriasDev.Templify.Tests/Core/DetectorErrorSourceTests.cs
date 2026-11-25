// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Xml.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;
using TriasDev.Templify.Conditionals;
using TriasDev.Templify.Loops;
using TriasDev.Templify.OpenDocument;
using TriasDev.Templify.Tests.Helpers;
using TriasDev.Templify.Tests.Odt;

namespace TriasDev.Templify.Tests.Core;

/// <summary>
/// Detector syntax errors name the element they are about, so validation can locate them (#255); a table row loop
/// with both markers in one row explains that this form is not supported (#255, R5).
/// </summary>
public sealed class DetectorErrorSourceTests
{
    private const string SameRowSentence =
        " Loop markers in a table must be in rows of their own; a row with both '{{#foreach}}' and '{{/foreach}}' is not supported.";

    [Fact]
    public void Word_UnmatchedLoopStart_SourceIsStartParagraph()
    {
        List<OpenXmlElement> elements = [P("{{#foreach Items}}"), P("x")];

        TriasDev.Templify.Core.TemplateSyntaxException ex = Assert.Throws<TriasDev.Templify.Core.TemplateSyntaxException>(
            () => LoopDetector.DetectLoopsInElements(elements));

        Assert.Same(elements[0], ex.SourceElement);
    }

    [Fact]
    public void Word_InvalidIterationVariable_SourceIsStartParagraph()
    {
        List<OpenXmlElement> elements = [P("{{#foreach in in Items}}"), P("x"), P("{{/foreach}}")];

        TriasDev.Templify.Core.TemplateSyntaxException ex = Assert.Throws<TriasDev.Templify.Core.TemplateSyntaxException>(
            () => LoopDetector.DetectLoopsInElements(elements));

        Assert.Same(elements[0], ex.SourceElement);
    }

    [Fact]
    public void Word_UnmatchedConditionalStart_SourceIsStartParagraph()
    {
        List<OpenXmlElement> elements = [P("x"), P("{{#if A}}"), P("y")];

        TriasDev.Templify.Core.TemplateSyntaxException ex = Assert.Throws<TriasDev.Templify.Core.TemplateSyntaxException>(
            () => ConditionalDetector.DetectConditionalsInElements(elements));

        Assert.Same(elements[1], ex.SourceElement);
    }

    [Fact]
    public void Word_ElseIfAfterElse_SourceIsElseIfParagraph()
    {
        List<OpenXmlElement> elements = [P("{{#if A}}"), P("a"), P("{{#else}}"), P("b"), P("{{#elseif B}}"), P("c"), P("{{/if}}")];

        TriasDev.Templify.Core.TemplateSyntaxException ex = Assert.Throws<TriasDev.Templify.Core.TemplateSyntaxException>(
            () => ConditionalDetector.DetectConditionalsInElements(elements));

        Assert.Same(elements[4], ex.SourceElement);
    }

    [Fact]
    public void Word_UnmatchedTableRowLoop_SourceIsRow()
    {
        List<TableRow> rows = [Row("{{#foreach Items}}"), Row("x")];

        TriasDev.Templify.Core.TemplateSyntaxException ex = Assert.Throws<TriasDev.Templify.Core.TemplateSyntaxException>(
            () => LoopDetector.DetectTableRowLoops(rows));

        Assert.Same(rows[0], ex.SourceElement);
    }

    [Fact]
    public void Odt_UnmatchedLoopStart_SourceIsStartParagraph()
    {
        List<XElement> elements = [OdtP("{{#foreach Items}}"), OdtP("x")];

        TriasDev.Templify.Core.TemplateSyntaxException ex = Assert.Throws<TriasDev.Templify.Core.TemplateSyntaxException>(
            () => OdtLoopDetector.DetectLoops(elements));

        Assert.Same(elements[0], ex.SourceElement);
    }

    [Fact]
    public void Odt_ElseIfAfterElse_SourceIsElseIfParagraph()
    {
        List<XElement> elements = [OdtP("{{#if A}}"), OdtP("a"), OdtP("{{#else}}"), OdtP("b"), OdtP("{{#elseif B}}"), OdtP("c"), OdtP("{{/if}}")];

        TriasDev.Templify.Core.TemplateSyntaxException ex = Assert.Throws<TriasDev.Templify.Core.TemplateSyntaxException>(
            () => OdtConditionalDetector.DetectConditionals(elements));

        Assert.Same(elements[4], ex.SourceElement);
    }

    [Fact]
    public void Word_SameRowLoop_ExplainsMarkersBelongInOwnRows()
    {
        DocumentBuilder builder = new DocumentBuilder().AddTableWithCellParagraphs(2, 2, (r, c) => (r, c) switch
        {
            (0, 0) => ["{{#foreach Items}}"],
            (0, 1) => ["{{/foreach}}"],
            _ => ["x"],
        });
        byte[] template = builder.ToStream().ToArray();

        TriasDev.Templify.Core.ProcessingResult result = TemplateTestHarness.CreateInvariantProcessor()
            .ProcessTemplate(new MemoryStream(template), new MemoryStream(), Data());
        TriasDev.Templify.Core.ValidationResult validation = TemplateTestHarness.CreateInvariantProcessor().ValidateTemplate(new MemoryStream(template));

        Assert.False(result.IsSuccess);
        Assert.EndsWith(SameRowSentence, result.ErrorMessage, StringComparison.Ordinal);
        TriasDev.Templify.Core.ValidationError error = Assert.Single(validation.Errors, e => e.Message.EndsWith(SameRowSentence, StringComparison.Ordinal));
        Assert.Equal(TriasDev.Templify.Core.ValidationErrorType.UnmatchedLoopStart, error.Type);
    }

    [Fact]
    public void Odt_SameRowLoop_ExplainsMarkersBelongInOwnRows()
    {
        OdtDocumentBuilder builder = new OdtDocumentBuilder().AddTable(["{{#foreach Items}}", "{{/foreach}}"], ["x", "x"]);

        TriasDev.Templify.Core.ProcessingResult result = new TriasDev.Templify.Core.OdtTemplateProcessor(OdtTestHelper.InvariantOptions())
            .ProcessTemplate(builder.ToBytes(), Data(), out _);
        TriasDev.Templify.Core.ValidationResult validation = new TriasDev.Templify.Core.OdtTemplateProcessor(OdtTestHelper.InvariantOptions())
            .ValidateTemplate(builder.ToStream());

        Assert.False(result.IsSuccess);
        Assert.EndsWith(SameRowSentence, result.ErrorMessage, StringComparison.Ordinal);
        TriasDev.Templify.Core.ValidationError error = Assert.Single(validation.Errors, e => e.Message.EndsWith(SameRowSentence, StringComparison.Ordinal));
        Assert.Equal(TriasDev.Templify.Core.ValidationErrorType.UnmatchedLoopStart, error.Type);
    }

    [Fact]
    public void Word_StartRowWithoutEnd_MessageUnchanged()
    {
        List<TableRow> rows = [Row("{{#foreach Items}}"), Row("x")];

        TriasDev.Templify.Core.TemplateSyntaxException ex = Assert.Throws<TriasDev.Templify.Core.TemplateSyntaxException>(
            () => LoopDetector.DetectTableRowLoops(rows));

        Assert.Equal("Table row loop start marker '{{#foreach Items}}' has no matching '{{/foreach}}'.", ex.Message);
    }

    private static Dictionary<string, object> Data() => new() { ["Items"] = new List<string> { "a" } };

    private static Paragraph P(string text) => new Paragraph(new Run(new Text(text) { Space = SpaceProcessingModeValues.Preserve }));

    private static TableRow Row(string text) => new TableRow(new TableCell(P(text)));

    private static XElement OdtP(string text) => new XElement(OdfNames.Text + "p", text);
}
