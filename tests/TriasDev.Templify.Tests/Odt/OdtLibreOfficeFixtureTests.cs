// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.Xml.Linq;
using TriasDev.Templify.Core;
using TriasDev.Templify.Tests.Helpers;

namespace TriasDev.Templify.Tests.Odt;

/// <summary>
/// Processes templates written by LibreOffice (Odt/Fixtures/*.odt), so the engine is tested against the XML
/// LibreOffice really produces: its full style set, rsid spans (officeooo:rsid), header and footer styles
/// (<c>MT1</c>), <c>text:s</c> runs, covered table cells, sequence declarations, settings, thumbnails and
/// soft page breaks. These tests do not need LibreOffice and run in CI.
/// </summary>
/// <remarks>
/// The fixtures were saved by LibreOffice 26.8 from the sources in Odt/Fixtures/Sources (.fodt files and a .docx),
/// opened with a layout (so soft page breaks are written) and stored with the <c>writer8</c> filter, e.g. with a
/// Basic macro calling <c>loadComponentFromURL</c>, <c>getViewCursor().jumpToLastPage()</c> and <c>storeToURL</c>.
/// A plain <c>soffice --headless --convert-to odt</c> writes the same XML without the soft page breaks.
/// </remarks>
public sealed class OdtLibreOfficeFixtureTests
{
    public static byte[] LoadFixture(string name) =>
        File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Odt", "Fixtures", name));

    public static Dictionary<string, object> InvoiceData() => new Dictionary<string, object>
    {
        ["CompanyName"] = "ACME GmbH",
        ["InvoiceNumber"] = "INV-7",
        ["Customer"] = new Dictionary<string, object> { ["Name"] = "Ada Lovelace" },
        ["InvoiceDate"] = new DateTime(2026, 9, 1),
        ["Reference"] = "PO-42",
        ["Items"] = new List<Dictionary<string, object>>
        {
            new() { ["Name"] = "Widget", ["Quantity"] = 2, ["Price"] = 9.5m },
            new() { ["Name"] = "Gadget", ["Quantity"] = 1, ["Price"] = 1200m },
        },
        ["Total"] = 1219m,
        ["IsPaid"] = false,
        ["PaidOn"] = "never",
        ["Notes"] = "Deliver **before** noon",
    };

    public static Dictionary<string, object> ContainersData() => new Dictionary<string, object>
    {
        ["Project"] = "Templify",
        ["Features"] = new List<Dictionary<string, object>>
        {
            new() { ["Name"] = "ODT", ["IsNew"] = true },
            new() { ["Name"] = "DOCX", ["IsNew"] = false },
        },
        ["NeedsConfig"] = false,
        ["ConfigFile"] = "app.json",
        ["Contact"] = new Dictionary<string, object>
        {
            ["Name"] = "Bob",
            ["Phones"] = new List<string> { "+49 1", "+49 2" },
        },
        ["FootnoteText"] = "Offer",
        ["ValidUntil"] = "2026-12-31",
        ["ShowAppendix"] = true,
    };

    public static Dictionary<string, object> LongDocumentData() => new Dictionary<string, object>
    {
        ["ReportName"] = "Q3",
        ["Chapters"] = new List<Dictionary<string, object>> { new() { ["Title"] = "Alpha" }, new() { ["Title"] = "Beta" } },
        ["Rows"] = new List<Dictionary<string, object>>
        {
            new() { ["Key"] = "k1", ["Value"] = "v1" },
            new() { ["Key"] = "k2", ["Value"] = "v2" },
        },
    };

    public static Dictionary<string, object> FromWordData() => new Dictionary<string, object>
    {
        ["FirstName"] = "Ada",
        ["LastName"] = "**Love**lace",
        ["IsMember"] = true,
        ["MemberSince"] = new DateTime(2019, 5, 1),
        ["Orders"] = new List<Dictionary<string, object>>
        {
            new() { ["Id"] = 1, ["Amount"] = 10.5m },
            new() { ["Id"] = 2, ["Amount"] = 3m },
        },
    };

    public static OdtDocumentVerifier Process(string fixture, Dictionary<string, object> data, out ProcessingResult result)
    {
        OdtTemplateProcessor processor = new OdtTemplateProcessor(new PlaceholderReplacementOptions { Culture = CultureInfo.InvariantCulture });
        result = processor.ProcessTemplate(LoadFixture(fixture), data, out byte[] output);

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Empty(result.Warnings);
        OdtDocumentVerifier verifier = new OdtDocumentVerifier(output);
        verifier.AssertValidOdtPackage();
        return verifier;
    }

    [Fact]
    public void Invoice_IsProcessed()
    {
        OdtDocumentVerifier output = Process("lo-invoice.odt", InvoiceData(), out ProcessingResult result);

        Assert.Equal(
            new[]
            {
                "Invoice INV-7",
                "Dear Ada Lovelace,",
                "Date:\t2026-09-01   Ref: PO-42",
                "Item", "Qty", "Price",
                "1. Widget", "2", "9.50",
                "2. Gadget", "1", "1200.00",
                "Total", "1219.00",
                "Please pay 1219.00 within 14 days.",
                "Notes: Deliver before noon",
            },
            output.GetParagraphTexts());
        Assert.Equal(new[] { "ACME GmbH – Invoice INV-7" }, output.GetHeaderTexts());
        Assert.Equal(new[] { "Page  · ACME GMBH" }, output.GetFooterTexts()); // the page number is a field
        Assert.Single(output.StylesXml!.Descendants(OdtDocumentVerifier.Text + "page-number"));
        Assert.Empty(result.MissingVariables);

        // The split placeholders keep LibreOffice's rsid spans (the value takes the first span's style).
        XElement dear = OdtDocumentVerifier.GetParagraphs(output.Body).ElementAt(1);
        Assert.Equal("Ada Lovelace", dear.Elements(OdtDocumentVerifier.Text + "span").Select(s => s.Value).Aggregate(string.Concat));
        Assert.Equal("T1", (string?)dear.Elements(OdtDocumentVerifier.Text + "span").First(s => s.Value.Length > 0).Attribute(OdtDocumentVerifier.Text + "style-name"));

        // The header/footer part keeps its own automatic styles (MT1) and the covered cell of the total row stays.
        Assert.Contains("MT1", output.StylesXml!.ToString(), StringComparison.Ordinal);
        XElement table = output.Body.Descendants(OdtDocumentVerifier.Table + "table").Single();
        Assert.Single(table.Descendants(OdtDocumentVerifier.Table + "covered-table-cell"));
        Assert.Equal(4, table.Descendants(OdtDocumentVerifier.Table + "table-row").Count());

        // Markdown in a LibreOffice document: a new automatic style that collides with none of LibreOffice's.
        XElement notes = OdtDocumentVerifier.GetParagraphs(output.Body).Last();
        XElement bold = notes.Elements(OdtDocumentVerifier.Text + "span").Single(s => s.Value == "before");
        string styleName = (string)bold.Attribute(OdtDocumentVerifier.Text + "style-name")!;
        Assert.Equal("T3", styleName);
        XElement style = output.ContentXml.Descendants(OdtDocumentVerifier.Style + "style")
            .Single(s => (string?)s.Attribute(OdtDocumentVerifier.Style + "name") == styleName);
        Assert.Equal("bold", (string?)style.Element(OdtDocumentVerifier.Style + "text-properties")!.Attribute(OdtDocumentVerifier.Fo + "font-weight"));
    }

    [Fact]
    public void Invoice_PaidBranch_IsProcessed()
    {
        Dictionary<string, object> data = InvoiceData();
        data["IsPaid"] = true;
        data["PaidOn"] = "2026-09-10";

        OdtDocumentVerifier output = Process("lo-invoice.odt", data, out _);

        List<string> texts = output.GetParagraphTexts();
        Assert.Equal("Paid on 2026-09-10. Thank you!", texts[^2]);
        Assert.DoesNotContain(texts, t => t.Contains("Please pay", StringComparison.Ordinal));
    }

    [Fact]
    public void Invoice_Validation_ListsPlaceholders_AndMissingVariables()
    {
        OdtTemplateProcessor processor = new OdtTemplateProcessor();
        Dictionary<string, object> data = InvoiceData();
        data.Remove("PaidOn");
        data.Remove("CompanyName");

        ValidationResult syntax = processor.ValidateTemplate(new MemoryStream(LoadFixture("lo-invoice.odt")));
        ValidationResult withData = processor.ValidateTemplate(new MemoryStream(LoadFixture("lo-invoice.odt")), data);

        Assert.True(syntax.IsValid, string.Join("; ", syntax.Errors.Select(e => e.Message)));
        Assert.Equal(
            new[]
            {
                "@number", "CompanyName", "Customer.Name", "InvoiceDate", "InvoiceNumber", "IsPaid", "Items", "Notes",
                "PaidOn", "Reference", "Total", "item.Name", "item.Price", "item.Quantity",
            },
            syntax.AllPlaceholders.Order(StringComparer.Ordinal));
        Assert.Equal(new[] { "CompanyName", "PaidOn" }, withData.MissingVariables.Order(StringComparer.Ordinal));

        // One error per paragraph that references a missing variable (as for Word): CompanyName is in the header
        // and the footer.
        Assert.Equal(
            new[]
            {
                "Variable 'CompanyName' is referenced in the template but not provided in the data.",
                "Variable 'CompanyName' is referenced in the template but not provided in the data.",
                "Variable 'PaidOn' is referenced in the template but not provided in the data.",
            },
            withData.Errors.Select(e => e.Message).Order(StringComparer.Ordinal));
        Assert.All(withData.Errors, e => Assert.Equal(ValidationErrorType.MissingVariable, e.Type));
    }

    [Fact]
    public void Containers_ListsTextBoxFootnoteAndSection_AreProcessed()
    {
        OdtDocumentVerifier output = Process("lo-containers.odt", ContainersData(), out _);

        Assert.Equal(
            new[]
            {
                "Project Templify features:",
                "ODT (new)",
                "DOCX",
                "Steps:",
                "Install",
                "Run",
                "See the box.",
                "Contact: Bob",
                "☎ +49 1",
                "☎ +49 2",
                "Terms apply.",
                "Offer (valid until 2026-12-31)",
                "Appendix for Templify",
                "End.",
            },
            output.GetParagraphTexts());

        // Items of the loop list are cloned; the removed conditional items leave no empty bullets behind.
        List<XElement> lists = output.Body.Elements(OdtDocumentVerifier.Text + "list").ToList();
        Assert.Equal(2, lists.Count);
        Assert.Equal(2, lists[0].Elements(OdtDocumentVerifier.Text + "list-item").Count());
        Assert.Equal(2, lists[1].Elements(OdtDocumentVerifier.Text + "list-item").Count());
        Assert.Single(output.Body.Descendants(OdtDocumentVerifier.Draw + "frame"));
        Assert.Equal("ftn1", (string?)output.Body.Descendants(OdtDocumentVerifier.Text + "note").Single().Attribute(OdtDocumentVerifier.Text + "id"));
    }

    [Fact]
    public void Containers_FalseConditions_RemoveTheirContent()
    {
        Dictionary<string, object> data = ContainersData();
        data["NeedsConfig"] = true;
        data["ShowAppendix"] = false;

        OdtDocumentVerifier output = Process("lo-containers.odt", data, out _);

        List<string> texts = output.GetParagraphTexts();
        Assert.Equal(new[] { "Install", "Configure app.json", "Run" }, texts.Skip(4).Take(3));
        Assert.DoesNotContain(texts, t => t.StartsWith("Appendix", StringComparison.Ordinal));

        // The section's content was removed entirely; it keeps an empty paragraph so it stays editable.
        XElement section = output.Body.Elements(OdtDocumentVerifier.Text + "section").Single();
        Assert.Equal(string.Empty, OdtDocumentVerifier.RenderText(Assert.Single(section.Elements())));
    }

    [Fact]
    public void LongDocument_WithSoftPageBreaks_IsProcessed()
    {
        byte[] template = LoadFixture("lo-long-document.odt");
        int templateBreaks = new OdtDocumentVerifier(template).Body.Descendants(OdtDocumentVerifier.Text + "soft-page-break").Count();

        OdtDocumentVerifier output = Process("lo-long-document.odt", LongDocumentData(), out ProcessingResult result);

        List<string> texts = output.GetParagraphTexts();
        Assert.Equal("Report Q3", texts[0]);
        Assert.Equal(
            "Filler paragraph 26 with enough text to take a full line on an A4 page, so that the document runs over several pages.",
            texts[26]);
        Assert.Equal("Chapter 1: Alpha", texts[70]);
        Assert.Equal("Alpha line 16", texts[86]);
        Assert.Equal("Chapter 2: Beta", texts[100]);
        Assert.Equal("Beta line 16", texts[116]);
        Assert.Equal(new[] { "k1", "v1", "k2", "v2", "End of Q3" }, texts.TakeLast(5));
        Assert.DoesNotContain(texts, t => t.Contains("{{", StringComparison.Ordinal));
        Assert.Equal(2 + (2 * 31) + 4, result.ReplacementCount);

        // A soft page break inside the loop is copied with each item (LibreOffice recomputes them on load).
        Assert.Equal(3, templateBreaks);
        Assert.Equal(4, output.Body.Descendants(OdtDocumentVerifier.Text + "soft-page-break").Count());
    }

    [Fact]
    public void WordDocumentConvertedByLibreOffice_IsProcessed()
    {
        OdtDocumentVerifier output = Process("lo-from-word.odt", FromWordData(), out _);

        Assert.Equal(new[] { "Hello Ada Lovelace", "Member since 2019", "Order 1: 10.50", "Order 2: 3.00" }, output.GetParagraphTexts());

        // LibreOffice's bold style from Word (T1) has no complex-script weight, so markdown bold gets its own style,
        // nested in T1.
        XElement bold = output.Body.Descendants(OdtDocumentVerifier.Text + "span").Single(s => s.Value == "Love" && !s.HasElements);
        Assert.Equal("T2", (string?)bold.Attribute(OdtDocumentVerifier.Text + "style-name"));
        Assert.Equal("T1", (string?)bold.Parent!.Attribute(OdtDocumentVerifier.Text + "style-name"));
    }

    [Theory]
    [InlineData("lo-invoice.odt")]
    [InlineData("lo-containers.odt")]
    [InlineData("lo-long-document.odt")]
    [InlineData("lo-from-word.odt")]
    public void Fixtures_AreValidTemplates(string fixture)
    {
        ValidationResult result = new OdtTemplateProcessor().ValidateTemplate(new MemoryStream(LoadFixture(fixture)));

        Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(e => e.Message)));
        Assert.Empty(result.Warnings);
        Assert.NotEmpty(result.AllPlaceholders);
    }

    [Fact]
    public void Fixtures_AreLibreOfficePackages()
    {
        // Guards the fixtures themselves: they must stay LibreOffice-written (settings, thumbnail, rsid styles).
        OdtDocumentVerifier invoice = new OdtDocumentVerifier(LoadFixture("lo-invoice.odt"));

        Assert.Contains("settings.xml", invoice.EntryNames);
        Assert.Contains("Thumbnails/thumbnail.png", invoice.EntryNames);
        Assert.Contains("LibreOffice", invoice.GetEntryString("meta.xml"), StringComparison.Ordinal);
        Assert.Contains("officeooo:rsid", invoice.GetEntryString("content.xml"), StringComparison.Ordinal);
    }
}
