// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Xml.Linq;
using DocumentFormat.OpenXml.CustomProperties;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.VariantTypes;
using TriasDev.Templify.Core;

namespace TriasDev.Templify.Builder.Tests;

public sealed class IdentityTests
{
    private static readonly XNamespace _meta = "urn:oasis:names:tc:opendocument:xmlns:meta:1.0";
    private static readonly XNamespace _office = "urn:oasis:names:tc:opendocument:xmlns:office:1.0";

    public static TheoryData<TemplateFormat> Formats => new TheoryData<TemplateFormat>(BuilderHarness.Formats);

    internal static readonly TemplateIdentity InvoiceIdentity = new TemplateIdentity("invoice", 1);

    /// <summary>Version 1 of an invoice template. <paramref name="withOldFields"/> adds fields that version 2 dropped.</summary>
    internal static TestDefinition Invoice(int version, bool withOldFields = false) => new TestDefinition(
        template =>
        {
            TemplatePath order = template.Data["order"];
            template.Content(c =>
            {
                c.Paragraph(x =>
                {
                    x.Text("Invoice ");
                    x.Value(order["number"]);
                });
                c.If(order["isPaid"], b => b.Paragraph("Paid"));
                c.ForEach(order["lines"], "line", (line, b) => b.Paragraph(x =>
                {
                    x.Value(line["description"]);
                    x.Text(": ");
                    x.Value(line["amount"]);
                }));
                if (version >= 2)
                {
                    c.Paragraph(order["dueDate"]);
                }

                if (withOldFields)
                {
                    c.Paragraph(order["fax"]);
                    c.If(order["archived"], b => b.Paragraph("archived"));
                    c.ForEach(order["lines"], "line", (line, b) => b.Paragraph(line["discount"]));
                }
            });
        },
        InvoiceIdentity with { Version = version });

    [Theory]
    [MemberData(nameof(Formats))]
    public void Stamp_IsWrittenAndReadBack(TemplateFormat format)
    {
        GeneratedTemplate template = TemplateBuilder.Build(Invoice(2), BuilderHarness.Options(format, "de-DE"));

        TemplateInspection inspection = TemplateInspector.Inspect(template.OpenRead());

        Assert.Equal(format, inspection.Format);
        Assert.Equal(template.Stamp, inspection.Stamp);
        Assert.Equal("invoice", template.Stamp.TemplateId);
        Assert.Equal(2, template.Stamp.Version);
        Assert.Equal(template.Contract.Hash, template.Stamp.ContractHash);
        Assert.Equal("de-DE", template.Stamp.Language);
        Assert.Matches(@"^\d+\.\d+\.\d+", template.Stamp.BuilderVersion);
        Assert.DoesNotContain("+", template.Stamp.BuilderVersion, StringComparison.Ordinal);
        Assert.Matches(@"^\d+\.\d+\.\d+", template.Stamp.MinimumEngineVersion);
    }

    [Theory]
    [MemberData(nameof(Formats))]
    public void Stamp_SurvivesFilling_AndCanBeRemoved(TemplateFormat format)
    {
        string json = """{ "order": { "number": "7", "isPaid": true, "lines": [ { "description": "Pen", "amount": 2 } ], "dueDate": "soon" } }""";
        (GeneratedTemplate template, byte[] filled) = BuilderHarness.BuildAndFill(Invoice(2), format, json);
        Assert.Equal(template.Stamp, TemplateInspector.Inspect(new MemoryStream(filled)).Stamp);

        byte[] withOther = AddOtherProperty(filled, format);
        using MemoryStream document = new MemoryStream();
        document.Write(withOther);

        Assert.True(TemplateStampRemover.Remove(document));

        byte[] cleaned = document.ToArray();
        Assert.Null(TemplateInspector.Inspect(new MemoryStream(cleaned)).Stamp);
        Assert.Equal("kept", ReadOtherProperty(cleaned, format));
        BuilderHarness.AssertPackage(format, cleaned);
        Assert.Equal(BuilderHarness.ParagraphTexts(format, filled), BuilderHarness.ParagraphTexts(format, cleaned));
    }

    [Theory]
    [MemberData(nameof(Formats))]
    public void Remover_WithoutStamp_ChangesNothing(TemplateFormat format)
    {
        GeneratedTemplate template = BuilderHarness.Build(Invoice(1), format);
        using MemoryStream document = new MemoryStream();
        template.WriteTo(document);
        Assert.True(TemplateStampRemover.Remove(document));
        byte[] once = document.ToArray();

        Assert.False(TemplateStampRemover.Remove(document));
        Assert.Equal(once, document.ToArray());
    }

    [Fact]
    public void Remover_NeedsAWritableSeekableStream()
    {
        GeneratedTemplate template = BuilderHarness.Build(Invoice(1), TemplateFormat.Docx);

        Assert.Throws<ArgumentException>(() => TemplateStampRemover.Remove(template.OpenRead()));
    }

    internal static string RemoveParagraphsContaining(string xml, TemplateFormat format, params string[] texts)
    {
        XDocument document = XDocument.Parse(xml);
        XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        XNamespace text = "urn:oasis:names:tc:opendocument:xmlns:text:1.0";
        IEnumerable<XElement> paragraphs = format == TemplateFormat.Docx ? document.Descendants(w + "p") : document.Descendants(text + "p");
        paragraphs.Where(p => texts.Contains(p.Value)).ToList().ForEach(p => p.Remove());
        return document.ToString(SaveOptions.DisableFormatting);
    }

    private static byte[] AddOtherProperty(byte[] package, TemplateFormat format)
    {
        if (format == TemplateFormat.Odt)
        {
            return BuilderHarness.EditEntry(package, "meta.xml", xml =>
            {
                XDocument meta = XDocument.Parse(xml);
                meta.Root!.Element(_office + "meta")!.Add(new XElement(_meta + "user-defined", new XAttribute(_meta + "name", "Department"), "kept"));
                return meta.ToString(SaveOptions.DisableFormatting);
            });
        }

        using MemoryStream stream = new MemoryStream();
        stream.Write(package);
        using (WordprocessingDocument document = WordprocessingDocument.Open(stream, true))
        {
            Properties properties = document.CustomFilePropertiesPart!.Properties!;
            properties.Append(new CustomDocumentProperty(new VTLPWSTR("kept"))
            {
                FormatId = "{D5CDD505-2E9C-101B-9397-08002B2CF9AE}",
                PropertyId = 99,
                Name = "Department",
            });
            properties.Save();
        }

        return stream.ToArray();
    }

    private static string? ReadOtherProperty(byte[] package, TemplateFormat format)
    {
        if (format == TemplateFormat.Odt)
        {
            return BuilderHarness.ReadXml(package, "meta.xml").Descendants(_meta + "user-defined")
                .SingleOrDefault(e => (string?)e.Attribute(_meta + "name") == "Department")?.Value;
        }

        using MemoryStream stream = new MemoryStream(package);
        using WordprocessingDocument document = WordprocessingDocument.Open(stream, false);
        return document.CustomFilePropertiesPart?.Properties?.Elements<CustomDocumentProperty>()
            .SingleOrDefault(p => p.Name?.Value == "Department")?.InnerText;
    }
}
