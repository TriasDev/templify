// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;
using TriasDev.Templify.Core;
using TriasDev.Templify.Tests.Helpers;

namespace TriasDev.Templify.Tests.Odt;

/// <summary>
/// Opens processed documents with LibreOffice headless and checks what LibreOffice reads.
/// Local only: skipped when LibreOffice is not installed (and on CI unless TEMPLIFY_SOFFICE is set).
/// </summary>
[Trait("Category", "LibreOffice")]
public sealed class OdtLibreOfficeRoundTripTests
{
    [Fact]
    public void Placeholders_RoundTripThroughLibreOffice()
    {
        LibreOfficeRunner.RequireExecutable();

        OdtDocumentBuilder template = new OdtDocumentBuilder()
            .AddHeading("Offer {{Number}}")
            .AddXml("<text:p>Dear <text:span text:style-name=\"T1\">{{Cust</text:span>omer.Name}},</text:p>")
            .AddParagraph("Total: {{Total:number:N2}} | {{Date:date:yyyy-MM-dd}} | {{Flag:yesno}}")
            .AddXml("<text:p>[{{Spaced}}]<text:tab/>{{Multi}}</text:p>")
            .AddTable(new[] { "Item", "{{Item}}" })
            .AddParagraph("Escaped: {{Special}}");

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Number"] = 42,
            ["Customer"] = new Dictionary<string, object> { ["Name"] = "Anna Müller" },
            ["Total"] = 1234.5m,
            ["Date"] = new DateTime(2026, 9, 26),
            ["Flag"] = true,
            ["Spaced"] = " a  b ",
            ["Multi"] = "Line 1\nLine 2",
            ["Item"] = "Widget",
            ["Special"] = "<Tom & \"Jerry\">",
        };

        byte[] output = ProcessToBytes(template, data);
        string[] lines = LibreOfficeRunner.ConvertToTextLines(output);

        Assert.Equal("Offer 42", lines[0]);
        Assert.Equal("Dear Anna Müller,", lines[1]);
        Assert.Equal("Total: 1,234.50 | 2026-09-26 | Yes", lines[2]);
        Assert.Equal("[ a  b ]\tLine 1", lines[3]);
        Assert.Equal("Line 2", lines[4]);
        Assert.Contains("Widget", string.Join("\n", lines), StringComparison.Ordinal);
        Assert.Equal("Escaped: <Tom & \"Jerry\">", lines[^1]);
    }

    [Fact]
    public void Template_Ott_OpensAsOdtAndConvertsToPdf()
    {
        LibreOfficeRunner.RequireExecutable();

        OdtDocumentBuilder template = new OdtDocumentBuilder()
            .AsTemplate()
            .AddHeaderParagraph("Header {{Company}}")
            .AddFooterParagraph("Footer {{Company}}")
            .AddParagraph("Body {{Company}}");

        byte[] output = ProcessToBytes(template, new Dictionary<string, object> { ["Company"] = "ACME" });

        string[] lines = LibreOfficeRunner.ConvertToTextLines(output);
        Assert.Contains("Body ACME", lines);

        byte[] pdf = LibreOfficeRunner.Convert(output, "odt", "pdf", "pdf");
        Assert.StartsWith("%PDF", Encoding.ASCII.GetString(pdf, 0, 4), StringComparison.Ordinal);
    }

    [Fact]
    public void LibreOfficeGeneratedDocument_IsProcessed()
    {
        LibreOfficeRunner.RequireExecutable();

        // Let LibreOffice write a real document (settings, thumbnail, manifest.rdf, LibreOffice namespaces).
        byte[] source = Encoding.UTF8.GetBytes("Hello {{Name}}  and  {{Other}}\n\tTabbed {{Name}}\n");
        byte[] template = LibreOfficeRunner.Convert(source, "txt", "odt", "odt");

        OdtTemplateProcessor processor = new OdtTemplateProcessor(new PlaceholderReplacementOptions { Culture = CultureInfo.InvariantCulture });
        ProcessingResult result = processor.ProcessTemplate(
            template,
            new Dictionary<string, object> { ["Name"] = "World", ["Other"] = "x" },
            out byte[] output);

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Equal(3, result.ReplacementCount);
        new OdtDocumentVerifier(output).AssertValidOdtPackage();

        string[] lines = LibreOfficeRunner.ConvertToTextLines(output);
        Assert.Equal("Hello World  and  x", lines[0]);
        Assert.Equal("\tTabbed World", lines[1]);
    }

    [Fact]
    public void Conditionals_RoundTripThroughLibreOffice()
    {
        LibreOfficeRunner.RequireExecutable();

        OdtDocumentBuilder template = new OdtDocumentBuilder()
            .AddParagraph("{{#if Premium}}")
            .AddParagraph("Premium customer")
            .AddParagraph("{{#else}}")
            .AddParagraph("Standard customer")
            .AddParagraph("{{/if}}")
            .AddXml("<text:p>Dear {{#if IsMale}}Mr.{{#else}}<text:span text:style-name=\"T1\">Ms.</text:span>{{/if}} {{Name}}</text:p>")
            .AddTable(new[] { "{{#if ShowRow}}" }, new[] { "Hidden row" }, new[] { "{{/if}}" }, new[] { "Kept row" })
            .AddXml(
                "<text:list><text:list-item><text:p>First</text:p></text:list-item>" +
                "<text:list-item><text:p>{{#if ShowItem}}</text:p></text:list-item>" +
                "<text:list-item><text:p>Hidden item</text:p></text:list-item>" +
                "<text:list-item><text:p>{{/if}}</text:p></text:list-item>" +
                "<text:list-item><text:p>Last</text:p></text:list-item></text:list>")
            .AddParagraph("End");

        byte[] output = ProcessToBytes(template, new Dictionary<string, object>
        {
            ["Premium"] = false,
            ["IsMale"] = false,
            ["Name"] = "Smith",
            ["ShowRow"] = false,
            ["ShowItem"] = false,
        });

        string[] lines = LibreOfficeRunner.ConvertToTextLines(output);
        string all = string.Join("\n", lines);
        Assert.Equal("Standard customer", lines[0]);
        Assert.Equal("Dear Ms. Smith", lines[1]);
        Assert.Contains("Kept row", all, StringComparison.Ordinal);
        Assert.DoesNotContain("Hidden", all, StringComparison.Ordinal);
        Assert.DoesNotContain("{{", all, StringComparison.Ordinal);
        Assert.Contains("First", all, StringComparison.Ordinal);
        Assert.Contains("Last", all, StringComparison.Ordinal);
        Assert.Equal("End", lines[^1]);
    }

    [Fact]
    public void Loops_RoundTripThroughLibreOffice()
    {
        LibreOfficeRunner.RequireExecutable();

        OdtDocumentBuilder template = new OdtDocumentBuilder()
            .AddParagraph("{{#foreach Items}}")
            .AddXml("<text:p>{{@number}}. <text:span text:style-name=\"T1\">{{Name}}</text:span></text:p>")
            .AddParagraph("{{/foreach}}")
            .AddTable(new[] { "Name" }, new[] { "{{#foreach Items}}" }, new[] { "Row {{Name}}" }, new[] { "{{/foreach}}" })
            .AddXml(
                "<text:list><text:list-item><text:p>{{#foreach Items}}</text:p></text:list-item>" +
                "<text:list-item><text:p>Bullet {{Name}}</text:p></text:list-item>" +
                "<text:list-item><text:p>{{/foreach}}</text:p></text:list-item></text:list>")
            .AddParagraph("End");

        byte[] output = ProcessToBytes(template, new Dictionary<string, object>
        {
            ["Items"] = new List<Dictionary<string, object>>
            {
                new() { ["Name"] = "Alpha" },
                new() { ["Name"] = "Beta" },
            },
        });

        string[] lines = LibreOfficeRunner.ConvertToTextLines(output);
        string all = string.Join("\n", lines);
        Assert.Equal("1. Alpha", lines[0]);
        Assert.Equal("2. Beta", lines[1]);
        Assert.Contains("Row Alpha", all, StringComparison.Ordinal);
        Assert.Contains("Row Beta", all, StringComparison.Ordinal);
        Assert.Contains("Bullet Alpha", all, StringComparison.Ordinal);
        Assert.Contains("Bullet Beta", all, StringComparison.Ordinal);
        Assert.DoesNotContain("{{", all, StringComparison.Ordinal);
        Assert.Equal("End", lines[^1]);
    }

    private static byte[] ProcessToBytes(OdtDocumentBuilder template, Dictionary<string, object> data)
    {
        OdtTemplateProcessor processor = new OdtTemplateProcessor(new PlaceholderReplacementOptions { Culture = CultureInfo.InvariantCulture });
        ProcessingResult result = processor.ProcessTemplate(template.ToBytes(), data, out byte[] output);
        Assert.True(result.IsSuccess, result.ErrorMessage);
        new OdtDocumentVerifier(output).AssertValidOdtPackage();
        return output;
    }
}
