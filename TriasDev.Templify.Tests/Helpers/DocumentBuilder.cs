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
    private int _nextAbstractNumId = 0;
    private int _nextNumId = 1;

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
    public static RunProperties CreateFormatting(
        bool bold = false,
        bool italic = false,
        string? color = null,
        string? fontFamily = null,
        string? fontSize = null)
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

        return properties;
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
