using System.IO.Compression;
using System.Security;
using System.Text;
using TriasDev.Templify.Core;

namespace TriasDev.Templify.DocumentGenerator.Generators;

/// <summary>
/// Generates an OpenDocument Text (.odt) template, as LibreOffice Writer would save it, and processes it with the
/// format-detecting <see cref="TemplateProcessor"/>. Shows placeholders, a conditional, a table-row loop, a list-item
/// loop, markdown in values and a footer placeholder.
/// </summary>
/// <remarks>
/// The package is written programmatically and deterministically: fixed entry order and timestamps, so the committed
/// template and output only change when the example changes.
/// </remarks>
public class LibreOfficeLetterGenerator : IExampleGenerator
{
    private const string TextMediaType = "application/vnd.oasis.opendocument.text";

    private const string Namespaces =
        "xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" " +
        "xmlns:style=\"urn:oasis:names:tc:opendocument:xmlns:style:1.0\" " +
        "xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\" " +
        "xmlns:table=\"urn:oasis:names:tc:opendocument:xmlns:table:1.0\" " +
        "xmlns:fo=\"urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0\" " +
        "xmlns:dc=\"http://purl.org/dc/elements/1.1/\" " +
        "xmlns:meta=\"urn:oasis:names:tc:opendocument:xmlns:meta:1.0\"";

    public string Name => "libreoffice-letter";

    public string Description => "OpenDocument (.odt) template for LibreOffice: placeholders, conditionals, table and list loops";

    public string GenerateTemplate(string outputDirectory)
    {
        string templatePath = Path.Combine(outputDirectory, $"{Name}-template.odt");
        File.WriteAllBytes(templatePath, BuildPackage());
        return templatePath;
    }

    public Dictionary<string, object> GetSampleData()
    {
        return new Dictionary<string, object>
        {
            ["Customer"] = new Dictionary<string, object>
            {
                ["Name"] = "Alice Johnson",
                ["City"] = "Springfield",
            },
            ["Date"] = ExampleGenerators.SampleDate,
            ["IsPremium"] = true,
            ["Message"] = "Your order has **shipped** and will arrive *within two days*.",
            ["Items"] = new List<object>
            {
                new Dictionary<string, object> { ["Product"] = "Desk lamp", ["Quantity"] = 2, ["Price"] = 39.90m },
                new Dictionary<string, object> { ["Product"] = "Office chair", ["Quantity"] = 1, ["Price"] = 249.00m },
                new Dictionary<string, object> { ["Product"] = "Notebook", ["Quantity"] = 5, ["Price"] = 4.50m },
            },
            ["Benefits"] = new List<object> { "Free shipping", "Extended warranty", "Priority support" },
            ["Company"] = "Acme Corporation",
        };
    }

    public string ProcessTemplate(string templatePath, string outputDirectory)
    {
        // The facade detects the format from the content and hands the template to OdtTemplateProcessor.
        TemplateProcessor processor = new TemplateProcessor(new PlaceholderReplacementOptions
        {
            Culture = System.Globalization.CultureInfo.GetCultureInfo("en-US"),
        });

        string outputPath = Path.Combine(outputDirectory, $"{Name}-output.odt");
        ProcessingResult result = processor.ProcessTemplateFile(templatePath, outputPath, GetSampleData());

        if (!result.IsSuccess)
        {
            throw new InvalidOperationException($"Failed to process template: {result.ErrorMessage}");
        }

        Console.WriteLine($"  ✓ Processed template: {result.ReplacementCount} placeholders replaced");
        if (result.MissingVariables.Any())
        {
            Console.WriteLine($"  ⚠ Missing variables: {string.Join(", ", result.MissingVariables)}");
        }

        if (result.HasWarnings)
        {
            Console.WriteLine($"  ⚠ {result.Warnings.Count} processing warning(s)");
        }

        return outputPath;
    }

    private static byte[] BuildPackage()
    {
        DateTimeOffset timestamp = new DateTimeOffset(ExampleGenerators.SampleDate, TimeSpan.Zero);

        using MemoryStream stream = new MemoryStream();
        using (ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            // mimetype first and stored, as the OpenDocument specification requires.
            AddEntry(archive, "mimetype", TextMediaType, CompressionLevel.NoCompression, timestamp);
            AddEntry(archive, "content.xml", ContentXml(), CompressionLevel.Optimal, timestamp);
            AddEntry(archive, "styles.xml", StylesXml(), CompressionLevel.Optimal, timestamp);
            AddEntry(archive, "meta.xml", MetaXml(), CompressionLevel.Optimal, timestamp);
            AddEntry(archive, "META-INF/manifest.xml", ManifestXml(), CompressionLevel.Optimal, timestamp);
        }

        return stream.ToArray();
    }

    private static void AddEntry(ZipArchive archive, string name, string content, CompressionLevel level, DateTimeOffset timestamp)
    {
        ZipArchiveEntry entry = archive.CreateEntry(name, level);
        entry.LastWriteTime = timestamp;
        using Stream entryStream = entry.Open();
        entryStream.Write(Encoding.UTF8.GetBytes(content));
    }

    private static string P(string text, string style = "Standard") =>
        $"<text:p text:style-name=\"{style}\">{SecurityElement.Escape(text)}</text:p>";

    private static string Cell(string text, string style = "Table_20_Contents") =>
        $"<table:table-cell table:style-name=\"Cell\" office:value-type=\"string\">{P(text, style)}</table:table-cell>";

    private static string Row(params string[] cells) =>
        "<table:table-row>" + string.Concat(cells.Select(c => Cell(c))) + "</table:table-row>";

    private static string MarkerRow(string marker) =>
        "<table:table-row>" + Cell(marker) + Cell(string.Empty) + Cell(string.Empty) + "</table:table-row>";

    private static string ListItem(string text) =>
        $"<text:list-item>{P(text, "List_20_Paragraph")}</text:list-item>";

    private static string ContentXml()
    {
        string body =
            "<text:h text:style-name=\"Heading_20_1\" text:outline-level=\"1\">Order Confirmation</text:h>" +
            P("Dear {{Customer.Name}},") +
            P("thank you for your order of {{Date:date:MMMM d, yyyy}}. {{Message}}") +
            P("{{#if IsPremium}}") +
            P("As a premium customer from {{Customer.City}}, you enjoy these benefits:") +
            "<text:list text:style-name=\"L1\">" +
            ListItem("{{#foreach Benefits}}") +
            ListItem("{{.}}") +
            ListItem("{{/foreach}}") +
            "</text:list>" +
            P("{{#else}}") +
            P("Become a premium customer to enjoy free shipping.") +
            P("{{/if}}") +
            "<text:h text:style-name=\"Heading_20_2\" text:outline-level=\"2\">Your Items</text:h>" +
            "<table:table table:name=\"Items\" table:style-name=\"Items\">" +
            "<table:table-column table:style-name=\"Items.A\" table:number-columns-repeated=\"3\"/>" +
            "<table:table-header-rows>" +
            "<table:table-row>" + Cell("Product", "Table_20_Heading") + Cell("Quantity", "Table_20_Heading") + Cell("Price", "Table_20_Heading") + "</table:table-row>" +
            "</table:table-header-rows>" +
            MarkerRow("{{#foreach Items}}") +
            Row("{{Product}}", "{{Quantity}}", "{{Price:currency}}") +
            MarkerRow("{{/foreach}}") +
            "</table:table>" +
            P("Kind regards,") +
            P("{{Company}}");

        return "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
               $"<office:document-content {Namespaces} office:version=\"1.3\">" +
               "<office:automatic-styles>" +
               "<style:style style:name=\"Items\" style:family=\"table\"><style:table-properties style:width=\"16cm\" table:align=\"left\"/></style:style>" +
               "<style:style style:name=\"Items.A\" style:family=\"table-column\"><style:table-column-properties style:column-width=\"5.333cm\"/></style:style>" +
               "<style:style style:name=\"Cell\" style:family=\"table-cell\"><style:table-cell-properties fo:padding=\"0.1cm\" fo:border=\"0.5pt solid #000000\"/></style:style>" +
               "<text:list-style style:name=\"L1\"><text:list-level-style-bullet text:level=\"1\" text:bullet-char=\"•\">" +
               "<style:list-level-properties text:list-level-position-and-space-mode=\"label-alignment\">" +
               "<style:list-level-label-alignment text:label-followed-by=\"listtab\" fo:text-indent=\"-0.635cm\" fo:margin-left=\"1.27cm\"/>" +
               "</style:list-level-properties></text:list-level-style-bullet></text:list-style>" +
               "</office:automatic-styles>" +
               "<office:body><office:text>" + body + "</office:text></office:body>" +
               "</office:document-content>";
    }

    private static string StylesXml() =>
        "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
        $"<office:document-styles {Namespaces} office:version=\"1.3\">" +
        "<office:styles>" +
        "<style:default-style style:family=\"paragraph\"><style:text-properties fo:font-size=\"11pt\"/></style:default-style>" +
        "<style:style style:name=\"Standard\" style:family=\"paragraph\" style:class=\"text\"><style:paragraph-properties fo:margin-bottom=\"0.2cm\"/></style:style>" +
        "<style:style style:name=\"Heading_20_1\" style:display-name=\"Heading 1\" style:family=\"paragraph\" style:parent-style-name=\"Standard\" style:class=\"text\">" +
        "<style:text-properties fo:font-size=\"18pt\" fo:font-weight=\"bold\" fo:color=\"#1f3864\"/></style:style>" +
        "<style:style style:name=\"Heading_20_2\" style:display-name=\"Heading 2\" style:family=\"paragraph\" style:parent-style-name=\"Standard\" style:class=\"text\">" +
        "<style:paragraph-properties fo:margin-top=\"0.4cm\"/><style:text-properties fo:font-size=\"14pt\" fo:font-weight=\"bold\"/></style:style>" +
        "<style:style style:name=\"List_20_Paragraph\" style:display-name=\"List Paragraph\" style:family=\"paragraph\" style:parent-style-name=\"Standard\" style:class=\"list\"/>" +
        "<style:style style:name=\"Table_20_Contents\" style:display-name=\"Table Contents\" style:family=\"paragraph\" style:parent-style-name=\"Standard\" style:class=\"extra\"/>" +
        "<style:style style:name=\"Table_20_Heading\" style:display-name=\"Table Heading\" style:family=\"paragraph\" style:parent-style-name=\"Table_20_Contents\" style:class=\"extra\">" +
        "<style:text-properties fo:font-weight=\"bold\"/></style:style>" +
        "<style:style style:name=\"Footer\" style:family=\"paragraph\" style:parent-style-name=\"Standard\" style:class=\"extra\">" +
        "<style:text-properties fo:font-size=\"9pt\" fo:color=\"#666666\"/></style:style>" +
        "</office:styles>" +
        "<office:automatic-styles>" +
        "<style:page-layout style:name=\"pm1\"><style:page-layout-properties fo:page-width=\"21cm\" fo:page-height=\"29.7cm\" " +
        "fo:margin-top=\"2cm\" fo:margin-bottom=\"1.5cm\" fo:margin-left=\"2.5cm\" fo:margin-right=\"2.5cm\"/>" +
        "<style:footer-style><style:header-footer-properties fo:min-height=\"0.6cm\"/></style:footer-style></style:page-layout>" +
        "</office:automatic-styles>" +
        "<office:master-styles>" +
        "<style:master-page style:name=\"Standard\" style:page-layout-name=\"pm1\">" +
        "<style:footer>" + P("{{Company}} · Order confirmation for {{Customer.Name}}", "Footer") + "</style:footer>" +
        "</style:master-page>" +
        "</office:master-styles>" +
        "</office:document-styles>";

    private static string MetaXml() =>
        "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
        $"<office:document-meta {Namespaces} office:version=\"1.3\">" +
        "<office:meta><meta:generator>TriasDev.Templify.DocumentGenerator</meta:generator>" +
        "<dc:title>Order Confirmation</dc:title></office:meta>" +
        "</office:document-meta>";

    private static string ManifestXml() =>
        "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
        "<manifest:manifest xmlns:manifest=\"urn:oasis:names:tc:opendocument:xmlns:manifest:1.0\" manifest:version=\"1.3\">" +
        $"<manifest:file-entry manifest:full-path=\"/\" manifest:version=\"1.3\" manifest:media-type=\"{TextMediaType}\"/>" +
        "<manifest:file-entry manifest:full-path=\"content.xml\" manifest:media-type=\"text/xml\"/>" +
        "<manifest:file-entry manifest:full-path=\"styles.xml\" manifest:media-type=\"text/xml\"/>" +
        "<manifest:file-entry manifest:full-path=\"meta.xml\" manifest:media-type=\"text/xml\"/>" +
        "</manifest:manifest>";
}
