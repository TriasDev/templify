using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace TriasDev.Templify.Tests.Helpers;

/// <summary>
/// Helper class for verifying Word document content in tests.
/// </summary>
public sealed class DocumentVerifier : IDisposable
{
    private readonly MemoryStream _stream;
    private readonly WordprocessingDocument _document;
    private readonly Body _body;

    public DocumentVerifier(MemoryStream stream)
    {
        // Create a copy of the stream to avoid disposal issues
        _stream = new MemoryStream();
        stream.Position = 0;
        stream.CopyTo(_stream);
        _stream.Position = 0;

        _document = WordprocessingDocument.Open(_stream, false);
        _body = _document.MainDocumentPart?.Document?.Body
            ?? throw new InvalidOperationException("Document body not found");
    }

    /// <summary>
    /// Gets the total number of paragraphs in the document body.
    /// </summary>
    public int GetParagraphCount()
    {
        return _body.Elements<Paragraph>().Count();
    }

    /// <summary>
    /// Gets the text content of a specific paragraph (excluding table paragraphs).
    /// </summary>
    public string GetParagraphText(int index)
    {
        IEnumerable<Paragraph> paragraphs = _body.Elements<Paragraph>();

        if (index < 0 || index >= paragraphs.Count())
        {
            throw new ArgumentOutOfRangeException(nameof(index),
                $"Paragraph index {index} is out of range (0-{paragraphs.Count() - 1})");
        }

        Paragraph paragraph = paragraphs.ElementAt(index);
        return paragraph.InnerText;
    }

    /// <summary>
    /// Gets all paragraph texts in the document body (excluding table paragraphs).
    /// </summary>
    public List<string> GetAllParagraphTexts()
    {
        return _body.Elements<Paragraph>()
            .Select(p => p.InnerText)
            .ToList();
    }

    /// <summary>
    /// Gets all runs from a specific paragraph.
    /// </summary>
    public List<Run> GetRuns(int paragraphIndex)
    {
        IEnumerable<Paragraph> paragraphs = _body.Elements<Paragraph>();

        if (paragraphIndex < 0 || paragraphIndex >= paragraphs.Count())
        {
            throw new ArgumentOutOfRangeException(nameof(paragraphIndex));
        }

        Paragraph paragraph = paragraphs.ElementAt(paragraphIndex);
        return paragraph.Elements<Run>().ToList();
    }

    /// <summary>
    /// Gets the RunProperties from a specific run in a specific paragraph.
    /// </summary>
    public RunProperties? GetRunProperties(int paragraphIndex, int runIndex = 0)
    {
        IEnumerable<Paragraph> paragraphs = _body.Elements<Paragraph>();

        if (paragraphIndex < 0 || paragraphIndex >= paragraphs.Count())
        {
            throw new ArgumentOutOfRangeException(nameof(paragraphIndex));
        }

        Paragraph paragraph = paragraphs.ElementAt(paragraphIndex);
        List<Run> runs = paragraph.Elements<Run>().ToList();

        if (runIndex < 0 || runIndex >= runs.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(runIndex),
                $"Run index {runIndex} is out of range (0-{runs.Count - 1})");
        }

        return runs[runIndex].RunProperties;
    }

    /// <summary>
    /// Gets the number of tables in the document.
    /// </summary>
    public int GetTableCount()
    {
        return _body.Elements<Table>().Count();
    }

    /// <summary>
    /// Gets the text content of a specific table cell.
    /// </summary>
    public string GetTableCellText(int tableIndex, int rowIndex, int columnIndex)
    {
        IEnumerable<Table> tables = _body.Elements<Table>();

        if (tableIndex < 0 || tableIndex >= tables.Count())
        {
            throw new ArgumentOutOfRangeException(nameof(tableIndex));
        }

        Table table = tables.ElementAt(tableIndex);
        List<TableRow> rows = table.Elements<TableRow>().ToList();

        if (rowIndex < 0 || rowIndex >= rows.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(rowIndex));
        }

        TableRow row = rows[rowIndex];
        List<TableCell> cells = row.Elements<TableCell>().ToList();

        if (columnIndex < 0 || columnIndex >= cells.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(columnIndex));
        }

        TableCell cell = cells[columnIndex];
        return cell.InnerText;
    }

    /// <summary>
    /// Gets all cell texts from a specific table.
    /// Returns a 2D list [row][column].
    /// </summary>
    public List<List<string>> GetTableCellTexts(int tableIndex)
    {
        IEnumerable<Table> tables = _body.Elements<Table>();

        if (tableIndex < 0 || tableIndex >= tables.Count())
        {
            throw new ArgumentOutOfRangeException(nameof(tableIndex));
        }

        Table table = tables.ElementAt(tableIndex);
        List<List<string>> result = new List<List<string>>();

        foreach (TableRow row in table.Elements<TableRow>())
        {
            List<string> rowTexts = row.Elements<TableCell>()
                .Select(cell => cell.InnerText)
                .ToList();

            result.Add(rowTexts);
        }

        return result;
    }

    /// <summary>
    /// Gets the RunProperties from a specific cell in a table.
    /// </summary>
    public RunProperties? GetTableCellRunProperties(
        int tableIndex,
        int rowIndex,
        int columnIndex,
        int runIndex = 0)
    {
        IEnumerable<Table> tables = _body.Elements<Table>();

        if (tableIndex < 0 || tableIndex >= tables.Count())
        {
            throw new ArgumentOutOfRangeException(nameof(tableIndex));
        }

        Table table = tables.ElementAt(tableIndex);
        List<TableRow> rows = table.Elements<TableRow>().ToList();

        if (rowIndex < 0 || rowIndex >= rows.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(rowIndex));
        }

        TableRow row = rows[rowIndex];
        List<TableCell> cells = row.Elements<TableCell>().ToList();

        if (columnIndex < 0 || columnIndex >= cells.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(columnIndex));
        }

        TableCell cell = cells[columnIndex];
        Paragraph? paragraph = cell.Elements<Paragraph>().FirstOrDefault();

        if (paragraph == null)
        {
            return null;
        }

        List<Run> runs = paragraph.Elements<Run>().ToList();

        if (runIndex < 0 || runIndex >= runs.Count)
        {
            return null;
        }

        return runs[runIndex].RunProperties;
    }

    /// <summary>
    /// Gets the number of rows in a specific table.
    /// </summary>
    public int GetTableRowCount(int tableIndex)
    {
        IEnumerable<Table> tables = _body.Elements<Table>();

        if (tableIndex < 0 || tableIndex >= tables.Count())
        {
            throw new ArgumentOutOfRangeException(nameof(tableIndex));
        }

        Table table = tables.ElementAt(tableIndex);
        return table.Elements<TableRow>().Count();
    }

    /// <summary>
    /// Verifies that RunProperties match the expected formatting.
    /// </summary>
    public static void VerifyFormatting(
        RunProperties? properties,
        bool? expectedBold = null,
        bool? expectedItalic = null,
        string? expectedColor = null,
        string? expectedFontFamily = null,
        string? expectedFontSize = null)
    {
        if (expectedBold.HasValue)
        {
            bool actualBold = properties?.Elements<Bold>().Any() == true;
            if (actualBold != expectedBold.Value)
            {
                throw new InvalidOperationException(
                    $"Expected bold={expectedBold.Value}, but was {actualBold}");
            }
        }

        if (expectedItalic.HasValue)
        {
            bool actualItalic = properties?.Elements<Italic>().Any() == true;
            if (actualItalic != expectedItalic.Value)
            {
                throw new InvalidOperationException(
                    $"Expected italic={expectedItalic.Value}, but was {actualItalic}");
            }
        }

        if (expectedColor != null)
        {
            string? actualColor = properties?.Elements<Color>().FirstOrDefault()?.Val?.Value;
            if (actualColor != expectedColor)
            {
                throw new InvalidOperationException(
                    $"Expected color={expectedColor}, but was {actualColor ?? "null"}");
            }
        }

        if (expectedFontFamily != null)
        {
            string? actualFont = properties?.Elements<RunFonts>().FirstOrDefault()?.Ascii?.Value;
            if (actualFont != expectedFontFamily)
            {
                throw new InvalidOperationException(
                    $"Expected font={expectedFontFamily}, but was {actualFont ?? "null"}");
            }
        }

        if (expectedFontSize != null)
        {
            string? actualSize = properties?.Elements<FontSize>().FirstOrDefault()?.Val?.Value;
            if (actualSize != expectedFontSize)
            {
                throw new InvalidOperationException(
                    $"Expected fontSize={expectedFontSize}, but was {actualSize ?? "null"}");
            }
        }
    }

    /// <summary>
    /// Checks if a specific paragraph is a bullet list item.
    /// </summary>
    public bool IsBulletListItem(int paragraphIndex)
    {
        IEnumerable<Paragraph> paragraphs = _body.Elements<Paragraph>();

        if (paragraphIndex < 0 || paragraphIndex >= paragraphs.Count())
        {
            throw new ArgumentOutOfRangeException(nameof(paragraphIndex));
        }

        Paragraph paragraph = paragraphs.ElementAt(paragraphIndex);
        ParagraphProperties? props = paragraph.ParagraphProperties;

        if (props == null)
        {
            return false;
        }

        NumberingProperties? numProps = props.NumberingProperties;
        if (numProps == null)
        {
            return false;
        }

        // Bullet lists typically use NumberingId = 1 (as defined in DocumentBuilder)
        int? numId = numProps.NumberingId?.Val?.Value;
        return numId == 1;
    }

    /// <summary>
    /// Checks if a specific paragraph is a numbered list item.
    /// </summary>
    public bool IsNumberedListItem(int paragraphIndex)
    {
        IEnumerable<Paragraph> paragraphs = _body.Elements<Paragraph>();

        if (paragraphIndex < 0 || paragraphIndex >= paragraphs.Count())
        {
            throw new ArgumentOutOfRangeException(nameof(paragraphIndex));
        }

        Paragraph paragraph = paragraphs.ElementAt(paragraphIndex);
        ParagraphProperties? props = paragraph.ParagraphProperties;

        if (props == null)
        {
            return false;
        }

        NumberingProperties? numProps = props.NumberingProperties;
        if (numProps == null)
        {
            return false;
        }

        // Numbered lists typically use NumberingId = 2 (as defined in DocumentBuilder)
        int? numId = numProps.NumberingId?.Val?.Value;
        return numId == 2;
    }

    /// <summary>
    /// Checks if a specific paragraph has any list formatting (bullet or numbered).
    /// </summary>
    public bool IsListItem(int paragraphIndex)
    {
        IEnumerable<Paragraph> paragraphs = _body.Elements<Paragraph>();

        if (paragraphIndex < 0 || paragraphIndex >= paragraphs.Count())
        {
            throw new ArgumentOutOfRangeException(nameof(paragraphIndex));
        }

        Paragraph paragraph = paragraphs.ElementAt(paragraphIndex);
        ParagraphProperties? props = paragraph.ParagraphProperties;

        if (props == null)
        {
            return false;
        }

        NumberingProperties? numProps = props.NumberingProperties;
        return numProps != null && numProps.NumberingId != null;
    }

    public void Dispose()
    {
        _document?.Dispose();
        _stream?.Dispose();
    }
}
