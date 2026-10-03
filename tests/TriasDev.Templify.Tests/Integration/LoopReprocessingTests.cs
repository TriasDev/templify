// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using TriasDev.Templify.Core;
using TriasDev.Templify.Tests.Helpers;

namespace TriasDev.Templify.Tests.Integration;

/// <summary>
/// Regression tests for issue #140: content produced by loop expansion must not be
/// processed a second time (e.g. with the global context). Otherwise data values that
/// contain placeholder syntax are evaluated as template code (template injection) and
/// warnings are reported more than once.
/// </summary>
public sealed class LoopReprocessingTests
{
    private static Dictionary<string, object> InjectionData() => new Dictionary<string, object>
    {
        ["Secret"] = "LEAKED",
        ["Items"] = new List<Dictionary<string, object>>
        {
            new Dictionary<string, object> { ["Name"] = "{{Secret}}" }
        }
    };

    [Fact]
    public void ProcessTemplate_TableRowLoop_ValueContainingPlaceholder_IsNotReprocessed()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddTable(3, 1, (row, _) => row switch
        {
            0 => "{{#foreach Items}}",
            1 => "{{Name}}",
            _ => "{{/foreach}}"
        });

        // Act
        (ProcessingResult result, MemoryStream output) = Process(builder.ToStream(), InjectionData());

        // Assert
        Assert.True(result.IsSuccess, result.ErrorMessage);
        using DocumentVerifier verifier = new DocumentVerifier(output);
        Assert.Equal(1, verifier.GetTableRowCount(0));
        Assert.Equal("{{Secret}}", verifier.GetTableCellText(0, 0, 0));
    }

    [Fact]
    public void ProcessTemplate_TableRowLoop_MissingVariableWarningsNotDuplicated()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddTable(3, 1, (row, _) => row switch
        {
            0 => "{{#foreach Items}}",
            1 => "{{Name}} {{Missing}}",
            _ => "{{/foreach}}"
        });

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Items"] = new List<Dictionary<string, object>>
            {
                new Dictionary<string, object> { ["Name"] = "a" }
            }
        };

        // Act
        (ProcessingResult result, MemoryStream output) = Process(builder.ToStream(), data);

        // Assert
        Assert.True(result.IsSuccess, result.ErrorMessage);
        ProcessingWarning warning = Assert.Single(result.Warnings);
        Assert.Equal(ProcessingWarningType.MissingVariable, warning.Type);
        output.Dispose();
    }

    [Fact]
    public void ProcessTemplate_BodyLoop_ValueContainingPlaceholder_IsNotReprocessed()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{#foreach Items}}");
        builder.AddParagraph("{{Name}}");
        builder.AddParagraph("{{/foreach}}");

        // Act
        (ProcessingResult result, MemoryStream output) = Process(builder.ToStream(), InjectionData());

        // Assert
        Assert.True(result.IsSuccess, result.ErrorMessage);
        using DocumentVerifier verifier = new DocumentVerifier(output);
        Assert.Equal(new List<string> { "{{Secret}}" }, verifier.GetAllParagraphTexts());
    }

    [Fact]
    public void ProcessTemplate_NestedTableInTableRowLoop_ValueContainingPlaceholder_IsNotReprocessed()
    {
        // Arrange: outer table-row loop whose content row contains a nested table with {{Name}}
        MemoryStream template = CreateTemplate(body =>
        {
            Table nested = new Table(new TableRow(Cell("{{Name}}")));
            TableCell cellWithNestedTable = new TableCell(nested, Para(string.Empty));

            body.Append(new Table(
                new TableRow(Cell("{{#foreach Items}}")),
                new TableRow(cellWithNestedTable),
                new TableRow(Cell("{{/foreach}}"))));
        });

        // Act
        (ProcessingResult result, MemoryStream output) = Process(template, InjectionData());

        // Assert
        Assert.True(result.IsSuccess, result.ErrorMessage);
        string text = BodyText(output);
        Assert.Contains("{{Secret}}", text);
        Assert.DoesNotContain("LEAKED", text);
    }

    [Fact]
    public void ProcessTemplate_TableRowLoopInsideBodyLoop_ValueContainingPlaceholder_IsNotReprocessed()
    {
        // Arrange: body-level loop over Groups containing a table with a table-row loop over group.Items.
        // Rows produced by the inner loop must not be re-walked with the outer (Group) context.
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{#foreach group in Groups}}");
        builder.AddTable(3, 1, (row, _) => row switch
        {
            0 => "{{#foreach item in group.Items}}",
            1 => "{{item.Name}}",
            _ => "{{/foreach}}"
        });
        builder.AddParagraph("{{/foreach}}");

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Groups"] = new List<Dictionary<string, object>>
            {
                new Dictionary<string, object>
                {
                    ["Secret"] = "LEAKED",
                    ["Items"] = new List<Dictionary<string, object>>
                    {
                        new Dictionary<string, object> { ["Name"] = "{{group.Secret}}" }
                    }
                }
            }
        };

        // Act
        (ProcessingResult result, MemoryStream output) = Process(builder.ToStream(), data);

        // Assert
        Assert.True(result.IsSuccess, result.ErrorMessage);
        using DocumentVerifier verifier = new DocumentVerifier(output);
        Assert.Equal(1, verifier.GetTableRowCount(0));
        Assert.Equal("{{group.Secret}}", verifier.GetTableCellText(0, 0, 0));
    }

    [Fact]
    public void ProcessTemplate_TableRowLoopInHeader_ValueContainingPlaceholder_IsNotReprocessed()
    {
        // Arrange
        MemoryStream template = CreateTemplate(
            body => body.Append(Para("Body")),
            header => header.Append(new Table(
                new TableRow(Cell("{{#foreach Items}}")),
                new TableRow(Cell("{{Name}}")),
                new TableRow(Cell("{{/foreach}}")))));

        // Act
        (ProcessingResult result, MemoryStream output) = Process(template, InjectionData());

        // Assert
        Assert.True(result.IsSuccess, result.ErrorMessage);
        using WordprocessingDocument doc = WordprocessingDocument.Open(output, false);
        string headerText = string.Join("|", doc.MainDocumentPart!.HeaderParts
            .SelectMany(h => h.Header!.Descendants<Paragraph>())
            .Select(p => p.InnerText));
        Assert.Equal("{{Secret}}", headerText);
    }

    private static (ProcessingResult Result, MemoryStream Output) Process(
        MemoryStream template,
        Dictionary<string, object> data)
    {
        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        MemoryStream output = new MemoryStream();
        ProcessingResult result = processor.ProcessTemplate(template, output, data);
        output.Position = 0;
        return (result, output);
    }

    private static string BodyText(MemoryStream output)
    {
        using WordprocessingDocument doc = WordprocessingDocument.Open(output, false);
        return string.Join("|", doc.MainDocumentPart!.Document!.Body!
            .Descendants<Paragraph>()
            .Select(p => p.InnerText));
    }

    private static Paragraph Para(string text) =>
        new Paragraph(new Run(new Text(text) { Space = SpaceProcessingModeValues.Preserve }));

    private static TableCell Cell(string text) => new TableCell(Para(text));

    private static MemoryStream CreateTemplate(Action<Body> buildBody, Action<Header>? buildHeader = null)
    {
        MemoryStream stream = new MemoryStream();
        using (WordprocessingDocument doc = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            MainDocumentPart mainPart = doc.AddMainDocumentPart();
            mainPart.Document = new Document(new Body());
            Body body = mainPart.Document.Body!;
            buildBody(body);

            if (buildHeader != null)
            {
                HeaderPart headerPart = mainPart.AddNewPart<HeaderPart>();
                headerPart.Header = new Header();
                buildHeader(headerPart.Header);
                headerPart.Header.Save();

                body.Append(new SectionProperties(new HeaderReference
                {
                    Type = HeaderFooterValues.Default,
                    Id = mainPart.GetIdOfPart(headerPart)
                }));
            }

            mainPart.Document.Save();
        }

        stream.Position = 0;
        return stream;
    }
}
