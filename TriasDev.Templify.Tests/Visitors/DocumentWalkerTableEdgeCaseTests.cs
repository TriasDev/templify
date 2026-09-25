// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;
using TriasDev.Templify.Conditionals;
using TriasDev.Templify.Core;
using TriasDev.Templify.Loops;
using TriasDev.Templify.Placeholders;
using TriasDev.Templify.Tests.Helpers;
using TriasDev.Templify.Visitors;

namespace TriasDev.Templify.Tests.Visitors;

/// <summary>
/// DocumentWalker handling of malformed table rows and of tables that lose all their rows.
/// </summary>
public sealed class DocumentWalkerTableEdgeCaseTests
{
    [Fact]
    public void WalkElements_ParagraphDirectlyInRow_IsVisitedUnlessItIsAMarker()
    {
        // Arrange: a <w:p> directly inside <w:tr> is malformed. It only exists in memory (for example after
        // content controls are unwrapped). When a file is loaded, the SDK reads it as an unknown element.
        Paragraph cellParagraph = new Paragraph(new Run(new Text("Cell")));
        Paragraph rowLevel = new Paragraph(new Run(new Text("Row {{Name}}")));
        Paragraph rowLevelMarker = new Paragraph(new Run(new Text("{{/foreach}}")));
        Body body = new Body(new Table(new TableRow(new TableCell(cellParagraph), rowLevel, rowLevelMarker)));
        RecordingVisitor visitor = new RecordingVisitor();

        // Act
        new DocumentWalker().WalkElements(
            body.Elements<OpenXmlElement>().ToList(),
            visitor,
            new GlobalEvaluationContext(new Dictionary<string, object>()));

        // Assert: the walker visits the cell paragraph and the row-level paragraph, but not the marker paragraph
        Assert.Equal(new[] { cellParagraph, rowLevel }, visitor.VisitedParagraphs);
    }

    [Fact]
    public void ProcessTemplate_HeaderWithOnlyATableWhoseRowsAreAllRemoved_KeepsHeaderValid()
    {
        // Arrange: a header containing nothing but a table with an empty row loop
        DocumentBuilder builder = new DocumentBuilder().AddParagraph("Body");
        builder.AddHeaderWithParagraphs(HeaderFooterValues.Default);
        Header header = builder.MainPart.HeaderParts.Single().Header!;
        header.Append(new Table(
            Row("{{#foreach Items}}"),
            Row("{{.}}"),
            Row("{{/foreach}}")));

        // Act
        using TemplateTestRun run = TemplateTestHarness.Process(
            builder,
            new Dictionary<string, object> { ["Items"] = new List<string>() });

        // Assert: the table is gone and an empty paragraph keeps the header schema-valid
        Header outputHeader = run.Verifier.Document.MainDocumentPart!.HeaderParts.Single().Header!;
        Assert.Empty(outputHeader.Elements<Table>());
        Paragraph paragraph = Assert.Single(outputHeader.Elements<Paragraph>());
        Assert.Equal(string.Empty, paragraph.InnerText);
        Assert.Empty(run.Verifier.GetValidationErrors());
    }

    [Fact]
    public void ProcessTemplate_HeaderWithParagraphAndTableWhoseRowsAreAllRemoved_DoesNotAddParagraph()
    {
        DocumentBuilder builder = new DocumentBuilder().AddParagraph("Body");
        builder.AddHeaderWithParagraphs(HeaderFooterValues.Default, "Header text");
        Header header = builder.MainPart.HeaderParts.Single().Header!;
        header.Append(new Table(
            Row("{{#foreach Items}}"),
            Row("{{.}}"),
            Row("{{/foreach}}")));

        using TemplateTestRun run = TemplateTestHarness.Process(
            builder,
            new Dictionary<string, object> { ["Items"] = new List<string>() });

        Header outputHeader = run.Verifier.Document.MainDocumentPart!.HeaderParts.Single().Header!;
        Assert.Empty(outputHeader.Elements<Table>());
        Paragraph paragraph = Assert.Single(outputHeader.Elements<Paragraph>());
        Assert.Equal("Header text", paragraph.InnerText);
    }

    private static TableRow Row(string text) =>
        new TableRow(new TableCell(new Paragraph(new Run(new Text(text)))));

    private sealed class RecordingVisitor : ITemplateElementVisitor
    {
        public List<Paragraph> VisitedParagraphs { get; } = new List<Paragraph>();

        public void VisitConditional(ConditionalBlock conditional, IEvaluationContext context)
        {
        }

        public void VisitLoop(LoopBlock loop, IEvaluationContext context)
        {
        }

        public void VisitPlaceholder(PlaceholderToken placeholder, Paragraph paragraph, IEvaluationContext context)
        {
        }

        public void VisitParagraph(Paragraph paragraph, IEvaluationContext context)
        {
            VisitedParagraphs.Add(paragraph);
        }
    }
}
