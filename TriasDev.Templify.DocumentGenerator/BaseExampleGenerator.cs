using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using TriasDev.Templify.Core;

namespace TriasDev.Templify.DocumentGenerator;

/// <summary>
/// Base class for example generators with common document creation utilities
/// </summary>
public abstract class BaseExampleGenerator : IExampleGenerator
{
    public abstract string Name { get; }
    public abstract string Description { get; }

    public abstract string GenerateTemplate(string outputDirectory);
    public abstract Dictionary<string, object> GetSampleData();

    public virtual string ProcessTemplate(string templatePath, string outputDirectory)
    {
        var processor = new DocumentTemplateProcessor();
        var data = GetSampleData();

        var outputPath = Path.Combine(outputDirectory, $"{Name}-output.docx");

        using var templateStream = File.OpenRead(templatePath);
        using var outputStream = File.Create(outputPath);

        var result = processor.ProcessTemplate(templateStream, outputStream, data);

        if (!result.IsSuccess)
        {
            throw new InvalidOperationException(
                $"Failed to process template: {result.ErrorMessage}");
        }

        Console.WriteLine($"  ✓ Processed template: {result.ReplacementCount} placeholders replaced");
        if (result.MissingVariables.Any())
        {
            Console.WriteLine($"  ⚠ Missing variables: {string.Join(", ", result.MissingVariables)}");
        }

        return outputPath;
    }

    /// <summary>
    /// Creates a new Word document with basic structure
    /// </summary>
    protected WordprocessingDocument CreateDocument(string path)
    {
        var doc = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document);

        var mainPart = doc.AddMainDocumentPart();
        mainPart.Document = new Document();
        mainPart.Document.AppendChild(new Body());

        return doc;
    }

    /// <summary>
    /// Adds a paragraph with text to the document body
    /// </summary>
    protected Paragraph AddParagraph(Body body, string text, bool isBold = false)
    {
        var paragraph = new Paragraph();
        var run = new Run();
        var runText = new Text(text);

        if (isBold)
        {
            run.RunProperties = new RunProperties(new Bold());
        }

        run.AppendChild(runText);
        paragraph.AppendChild(run);
        body.AppendChild(paragraph);

        return paragraph;
    }

    /// <summary>
    /// Adds a paragraph with a placeholder to the document body
    /// </summary>
    protected Paragraph AddPlaceholder(Body body, string placeholderName, bool isBold = false)
    {
        return AddParagraph(body, $"{{{{{placeholderName}}}}}", isBold);
    }

    /// <summary>
    /// Creates a table with the specified number of columns
    /// </summary>
    protected Table CreateTable(int columns)
    {
        var table = new Table();

        // Table properties
        var tableProperties = new TableProperties();
        var tableBorders = new TableBorders(
            new TopBorder { Val = BorderValues.Single, Size = 4 },
            new BottomBorder { Val = BorderValues.Single, Size = 4 },
            new LeftBorder { Val = BorderValues.Single, Size = 4 },
            new RightBorder { Val = BorderValues.Single, Size = 4 },
            new InsideHorizontalBorder { Val = BorderValues.Single, Size = 4 },
            new InsideVerticalBorder { Val = BorderValues.Single, Size = 4 }
        );
        tableProperties.AppendChild(tableBorders);
        table.AppendChild(tableProperties);

        return table;
    }

    /// <summary>
    /// Adds a table row with the specified cell texts
    /// </summary>
    protected TableRow AddTableRow(Table table, params string[] cellTexts)
    {
        var row = new TableRow();

        foreach (var text in cellTexts)
        {
            var cell = new TableCell();
            var paragraph = new Paragraph();
            var run = new Run();
            var runText = new Text(text);

            run.AppendChild(runText);
            paragraph.AppendChild(run);
            cell.AppendChild(paragraph);
            row.AppendChild(cell);
        }

        table.AppendChild(row);
        return row;
    }

    /// <summary>
    /// Adds a header row to the table with bold text
    /// </summary>
    protected TableRow AddTableHeaderRow(Table table, params string[] headerTexts)
    {
        var row = new TableRow();

        foreach (var text in headerTexts)
        {
            var cell = new TableCell();
            var paragraph = new Paragraph();
            var run = new Run();
            var runProperties = new RunProperties(new Bold());
            var runText = new Text(text);

            run.RunProperties = runProperties;
            run.AppendChild(runText);
            paragraph.AppendChild(run);
            cell.AppendChild(paragraph);
            row.AppendChild(cell);
        }

        table.AppendChild(row);
        return row;
    }

    /// <summary>
    /// Adds an empty paragraph (spacing)
    /// </summary>
    protected void AddEmptyParagraph(Body body)
    {
        body.AppendChild(new Paragraph());
    }
}
