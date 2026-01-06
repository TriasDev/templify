// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace TriasDev.Templify.Tests.Helpers;

/// <summary>
/// Helper class for building Word documents programmatically in tests.
/// </summary>
public sealed class DocumentBuilder
{
    private readonly MemoryStream _stream;
    private readonly WordprocessingDocument _document;
    private readonly Body _body;

    public DocumentBuilder()
    {
        _stream = new MemoryStream();
        _document = WordprocessingDocument.Create(_stream, WordprocessingDocumentType.Document);

        MainDocumentPart mainPart = _document.AddMainDocumentPart();
        mainPart.Document = new Document();
        _body = mainPart.Document.AppendChild(new Body());
    }

    /// <summary>
    /// Adds a paragraph with the specified text and optional formatting.
    /// </summary>
    public DocumentBuilder AddParagraph(string text, RunProperties? formatting = null)
    {
        Paragraph paragraph = new Paragraph();
        Run run = new Run();
        Text textElement = new Text(text);
        textElement.Space = SpaceProcessingModeValues.Preserve;

        run.Append(textElement);

        if (formatting != null)
        {
            run.RunProperties = (RunProperties)formatting.CloneNode(true);
        }

        paragraph.Append(run);
        _body.Append(paragraph);

        return this;
    }

    /// <summary>
    /// Adds a paragraph with multiple runs (for mixed formatting).
    /// </summary>
    public DocumentBuilder AddParagraphWithRuns(params (string text, RunProperties? formatting)[] runs)
    {
        Paragraph paragraph = new Paragraph();

        foreach ((string text, RunProperties? formatting) in runs)
        {
            Run run = new Run();
            Text textElement = new Text(text);
            textElement.Space = SpaceProcessingModeValues.Preserve;

            run.Append(textElement);

            if (formatting != null)
            {
                run.RunProperties = (RunProperties)formatting.CloneNode(true);
            }

            paragraph.Append(run);
        }

        _body.Append(paragraph);

        return this;
    }

    /// <summary>
    /// Adds a bullet list item with the specified text.
    /// </summary>
    public DocumentBuilder AddBulletListItem(string text, int level = 0, RunProperties? formatting = null)
    {
        EnsureNumberingPart();

        Paragraph paragraph = new Paragraph();
        Run run = new Run();
        Text textElement = new Text(text);
        textElement.Space = SpaceProcessingModeValues.Preserve;

        run.Append(textElement);

        if (formatting != null)
        {
            run.RunProperties = (RunProperties)formatting.CloneNode(true);
        }

        paragraph.Append(run);

        // Apply bullet numbering
        ParagraphProperties paraProps = new ParagraphProperties();
        NumberingProperties numProps = new NumberingProperties(
            new NumberingLevelReference() { Val = level },
            new NumberingId() { Val = 1 } // Bullet list ID
        );
        paraProps.Append(numProps);
        paragraph.PrependChild(paraProps);

        _body.Append(paragraph);

        return this;
    }

    /// <summary>
    /// Adds a numbered list item with the specified text.
    /// </summary>
    public DocumentBuilder AddNumberedListItem(string text, int level = 0, RunProperties? formatting = null)
    {
        EnsureNumberingPart();

        Paragraph paragraph = new Paragraph();
        Run run = new Run();
        Text textElement = new Text(text);
        textElement.Space = SpaceProcessingModeValues.Preserve;

        run.Append(textElement);

        if (formatting != null)
        {
            run.RunProperties = (RunProperties)formatting.CloneNode(true);
        }

        paragraph.Append(run);

        // Apply numbered list numbering
        ParagraphProperties paraProps = new ParagraphProperties();
        NumberingProperties numProps = new NumberingProperties(
            new NumberingLevelReference() { Val = level },
            new NumberingId() { Val = 2 } // Numbered list ID
        );
        paraProps.Append(numProps);
        paragraph.PrependChild(paraProps);

        _body.Append(paragraph);

        return this;
    }

    /// <summary>
    /// Ensures the NumberingDefinitionsPart exists and has bullet/numbered list definitions.
    /// </summary>
    private void EnsureNumberingPart()
    {
        MainDocumentPart mainPart = _document.MainDocumentPart!;

        if (mainPart.NumberingDefinitionsPart == null)
        {
            NumberingDefinitionsPart numberingPart = mainPart.AddNewPart<NumberingDefinitionsPart>();
            Numbering numbering = new Numbering();

            // Create bullet list definition (AbstractNum ID 0, Num ID 1)
            AbstractNum bulletAbstractNum = CreateBulletListDefinition(0);
            numbering.Append(bulletAbstractNum);

            NumberingInstance bulletNum = new NumberingInstance() { NumberID = 1 };
            bulletNum.Append(new AbstractNumId() { Val = 0 });
            numbering.Append(bulletNum);

            // Create numbered list definition (AbstractNum ID 1, Num ID 2)
            AbstractNum numberedAbstractNum = CreateNumberedListDefinition(1);
            numbering.Append(numberedAbstractNum);

            NumberingInstance numberedNum = new NumberingInstance() { NumberID = 2 };
            numberedNum.Append(new AbstractNumId() { Val = 1 });
            numbering.Append(numberedNum);

            numberingPart.Numbering = numbering;
            numberingPart.Numbering.Save();
        }
    }

    /// <summary>
    /// Creates a bullet list definition.
    /// </summary>
    private AbstractNum CreateBulletListDefinition(int abstractNumId)
    {
        AbstractNum abstractNum = new AbstractNum() { AbstractNumberId = abstractNumId };

        Level level = new Level() { LevelIndex = 0 };
        level.Append(new NumberingFormat() { Val = NumberFormatValues.Bullet });
        level.Append(new LevelText() { Val = "·" });
        level.Append(new LevelJustification() { Val = LevelJustificationValues.Left });

        PreviousParagraphProperties pPr = new PreviousParagraphProperties();
        Indentation indent = new Indentation() { Left = "720", Hanging = "360" };
        pPr.Append(indent);
        level.Append(pPr);

        abstractNum.Append(level);

        return abstractNum;
    }

    /// <summary>
    /// Creates a numbered list definition.
    /// </summary>
    private AbstractNum CreateNumberedListDefinition(int abstractNumId)
    {
        AbstractNum abstractNum = new AbstractNum() { AbstractNumberId = abstractNumId };

        Level level = new Level() { LevelIndex = 0 };
        level.Append(new StartNumberingValue() { Val = 1 });
        level.Append(new NumberingFormat() { Val = NumberFormatValues.Decimal });
        level.Append(new LevelText() { Val = "%1." });
        level.Append(new LevelJustification() { Val = LevelJustificationValues.Left });

        PreviousParagraphProperties pPr = new PreviousParagraphProperties();
        Indentation indent = new Indentation() { Left = "720", Hanging = "360" };
        pPr.Append(indent);
        level.Append(pPr);

        abstractNum.Append(level);

        return abstractNum;
    }

    /// <summary>
    /// Adds a table with the specified number of rows and columns.
    /// </summary>
    public DocumentBuilder AddTable(int rows, int columns, Func<int, int, string> cellTextProvider)
    {
        Table table = new Table();

        // Add table properties
        TableProperties tableProperties = new TableProperties();
        TableBorders tableBorders = new TableBorders(
            new TopBorder { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 4 },
            new BottomBorder { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 4 },
            new LeftBorder { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 4 },
            new RightBorder { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 4 },
            new InsideHorizontalBorder { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 4 },
            new InsideVerticalBorder { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 4 }
        );
        tableProperties.Append(tableBorders);
        table.Append(tableProperties);

        // Add rows and cells
        for (int row = 0; row < rows; row++)
        {
            TableRow tableRow = new TableRow();

            for (int col = 0; col < columns; col++)
            {
                TableCell cell = new TableCell();
                Paragraph paragraph = new Paragraph();
                Run run = new Run();
                Text text = new Text(cellTextProvider(row, col));
                text.Space = SpaceProcessingModeValues.Preserve;

                run.Append(text);
                paragraph.Append(run);
                cell.Append(paragraph);
                tableRow.Append(cell);
            }

            table.Append(tableRow);
        }

        _body.Append(table);

        return this;
    }

    /// <summary>
    /// Adds a table with custom cell content and optional formatting.
    /// </summary>
    public DocumentBuilder AddTableWithFormatting(
        int rows,
        int columns,
        Func<int, int, (string text, RunProperties? formatting)> cellProvider)
    {
        Table table = new Table();

        // Add table properties
        TableProperties tableProperties = new TableProperties();
        TableBorders tableBorders = new TableBorders(
            new TopBorder { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 4 },
            new BottomBorder { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 4 },
            new LeftBorder { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 4 },
            new RightBorder { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 4 },
            new InsideHorizontalBorder { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 4 },
            new InsideVerticalBorder { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 4 }
        );
        tableProperties.Append(tableBorders);
        table.Append(tableProperties);

        // Add rows and cells
        for (int row = 0; row < rows; row++)
        {
            TableRow tableRow = new TableRow();

            for (int col = 0; col < columns; col++)
            {
                (string text, RunProperties? formatting) = cellProvider(row, col);

                TableCell cell = new TableCell();
                Paragraph paragraph = new Paragraph();
                Run run = new Run();
                Text textElement = new Text(text);
                textElement.Space = SpaceProcessingModeValues.Preserve;

                run.Append(textElement);

                if (formatting != null)
                {
                    run.RunProperties = (RunProperties)formatting.CloneNode(true);
                }

                paragraph.Append(run);
                cell.Append(paragraph);
                tableRow.Append(cell);
            }

            table.Append(tableRow);
        }

        _body.Append(table);

        return this;
    }

    /// <summary>
    /// Creates RunProperties with the specified formatting options.
    /// </summary>
    /// <param name="bold">Whether to apply bold formatting.</param>
    /// <param name="italic">Whether to apply italic formatting.</param>
    /// <param name="color">Text color as a hex string (e.g., "FF0000" for red).</param>
    /// <param name="fontFamily">Font family name (e.g., "Arial").</param>
    /// <param name="fontSize">Font size in half-points (e.g., "24" for 12pt).</param>
    /// <param name="highlight">Highlight color from predefined Word highlight colors.</param>
    /// <param name="shadingFill">Background shading fill color as a hex string (e.g., "000000" for black).</param>
    /// <returns>A new RunProperties instance with the specified formatting.</returns>
    public static RunProperties CreateFormatting(
        bool bold = false,
        bool italic = false,
        string? color = null,
        string? fontFamily = null,
        string? fontSize = null,
        HighlightColorValues? highlight = null,
        string? shadingFill = null)
    {
        RunProperties properties = new RunProperties();

        if (bold)
        {
            properties.Append(new Bold());
        }

        if (italic)
        {
            properties.Append(new Italic());
        }

        if (color != null)
        {
            properties.Append(new Color { Val = color });
        }

        if (fontFamily != null)
        {
            properties.Append(new RunFonts { Ascii = fontFamily });
        }

        if (fontSize != null)
        {
            properties.Append(new FontSize { Val = fontSize });
        }

        if (highlight != null)
        {
            properties.Append(new Highlight { Val = highlight.Value });
        }

        if (shadingFill != null)
        {
            properties.Append(new Shading { Val = ShadingPatternValues.Clear, Fill = shadingFill });
        }

        return properties;
    }

    /// <summary>
    /// Adds a simple Table of Contents field to the document.
    /// This creates a TOC field that Word can update but Templify cannot.
    /// The TOC will contain entries pointing to page numbers that become stale after processing.
    /// </summary>
    /// <param name="tocEntries">List of entries to show in the TOC, each with (text, pageNumber)</param>
    public DocumentBuilder AddTableOfContents(params (string text, int pageNumber)[] tocEntries)
    {
        // Add TOC title
        Paragraph titlePara = new Paragraph();
        Run titleRun = new Run();
        titleRun.RunProperties = new RunProperties(new Bold());
        titleRun.Append(new Text("Table of Contents"));
        titlePara.Append(titleRun);
        _body.Append(titlePara);

        // Add TOC field begin
        Paragraph tocPara = new Paragraph();

        // Field begin character
        Run beginRun = new Run();
        beginRun.Append(new FieldChar { FieldCharType = FieldCharValues.Begin });
        tocPara.Append(beginRun);

        // Field instruction (TOC command)
        Run instrRun = new Run();
        instrRun.Append(new FieldCode(" TOC \\o \"1-3\" \\h \\z \\u ") { Space = SpaceProcessingModeValues.Preserve });
        tocPara.Append(instrRun);

        // Field separator
        Run sepRun = new Run();
        sepRun.Append(new FieldChar { FieldCharType = FieldCharValues.Separate });
        tocPara.Append(sepRun);

        _body.Append(tocPara);

        // Add TOC entries (these are the cached/displayed values)
        foreach ((string text, int pageNumber) in tocEntries)
        {
            Paragraph entryPara = new Paragraph();

            // Hyperlink-like entry text
            Run textRun = new Run();
            textRun.Append(new Text(text) { Space = SpaceProcessingModeValues.Preserve });
            entryPara.Append(textRun);

            // Tab character to separate text from page number
            Run tabRun = new Run();
            tabRun.Append(new TabChar());
            entryPara.Append(tabRun);

            // Page number (this is the cached value that becomes stale)
            Run pageRun = new Run();
            pageRun.Append(new Text(pageNumber.ToString()));
            entryPara.Append(pageRun);

            _body.Append(entryPara);
        }

        // Field end character
        Paragraph endPara = new Paragraph();
        Run endRun = new Run();
        endRun.Append(new FieldChar { FieldCharType = FieldCharValues.End });
        endPara.Append(endRun);
        _body.Append(endPara);

        return this;
    }

    /// <summary>
    /// Adds a heading paragraph with the specified style level (1-3).
    /// These are typically what TOC entries reference.
    /// </summary>
    public DocumentBuilder AddHeading(string text, int level = 1)
    {
        Paragraph paragraph = new Paragraph();

        // Add paragraph properties with heading style
        ParagraphProperties paraProps = new ParagraphProperties();
        paraProps.Append(new ParagraphStyleId { Val = $"Heading{level}" });
        paragraph.Append(paraProps);

        Run run = new Run();
        RunProperties runProps = new RunProperties(new Bold());
        if (level == 1)
        {
            runProps.Append(new FontSize { Val = "32" }); // 16pt
        }
        else if (level == 2)
        {
            runProps.Append(new FontSize { Val = "28" }); // 14pt
        }
        else
        {
            runProps.Append(new FontSize { Val = "24" }); // 12pt
        }
        run.RunProperties = runProps;
        run.Append(new Text(text) { Space = SpaceProcessingModeValues.Preserve });

        paragraph.Append(run);
        _body.Append(paragraph);

        return this;
    }

    /// <summary>
    /// Adds a page break to simulate multi-page documents.
    /// </summary>
    public DocumentBuilder AddPageBreak()
    {
        Paragraph paragraph = new Paragraph();
        Run run = new Run();
        run.Append(new Break { Type = BreakValues.Page });
        paragraph.Append(run);
        _body.Append(paragraph);

        return this;
    }

    /// <summary>
    /// Returns the document as a MemoryStream for processing.
    /// </summary>
    public MemoryStream ToStream()
    {
        // Save and close the document
        _document.MainDocumentPart!.Document.Save();
        _document.Dispose();

        // Reset stream position for reading
        _stream.Position = 0;

        return _stream;
    }

    /// <summary>
    /// Disposes the document and stream if not yet converted to stream.
    /// </summary>
    public void Dispose()
    {
        _document?.Dispose();
        _stream?.Dispose();
    }
}
