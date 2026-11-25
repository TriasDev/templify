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

    private static readonly TemplateIdentity _invoice = new TemplateIdentity("invoice", 1);

    /// <summary>Version 1 of an invoice template. <paramref name="withOldFields"/> adds fields that version 2 dropped.</summary>
    private static TestDefinition Invoice(int version, bool withOldFields = false) => new TestDefinition(
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
        _invoice with { Version = version });

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

    [Theory]
    [MemberData(nameof(Formats))]
    public void CheckUpload_UnchangedTemplate_IsUsableAndCurrent(TemplateFormat format)
    {
        GeneratedTemplate template = BuilderHarness.Build(Invoice(2), format);

        UploadCheckResult result = TemplateInspector.CheckUpload(template.OpenRead(), template.Contract, _invoice);

        Assert.True(result.IsUsable);
        Assert.Equal(TemplateOrigin.CurrentContract, result.Origin);
        Assert.Equal(format, result.Format);
        Assert.Empty(result.UnknownPaths);
    }

    [Theory]
    [MemberData(nameof(Formats))]
    public void CheckUpload_CopyOfAnOlderVersion_IsUsableButDifferent(TemplateFormat format)
    {
        GeneratedTemplate old = BuilderHarness.Build(Invoice(1), format);
        DataContract current = BuilderHarness.Build(Invoice(2), format).Contract;

        UploadCheckResult result = TemplateInspector.CheckUpload(old.OpenRead(), current, _invoice);

        Assert.True(result.IsUsable);
        Assert.Equal(TemplateOrigin.DifferentContract, result.Origin);
        Assert.Equal(1, result.Stamp!.Version);
    }

    [Theory]
    [MemberData(nameof(Formats))]
    public void CheckUpload_PathsTheCurrentDataNoLongerHas_AreListed(TemplateFormat format)
    {
        GeneratedTemplate old = BuilderHarness.Build(Invoice(1, withOldFields: true), format);
        DataContract current = BuilderHarness.Build(Invoice(2), format).Contract;

        UploadCheckResult result = TemplateInspector.CheckUpload(old.OpenRead(), current, _invoice);

        Assert.False(result.IsUsable);
        Assert.True(result.Syntax.IsValid);
        // order.archived comes from a MissingConditionVariable warning, read from its message until #275.
        Assert.Equal(new[] { "line.discount", "order.archived", "order.fax" }, result.UnknownPaths);
    }

    [Theory]
    [MemberData(nameof(Formats))]
    public void CheckUpload_HandEdits_AreJudgedByTheirPaths(TemplateFormat format)
    {
        GeneratedTemplate template = BuilderHarness.Build(Invoice(2), format);
        byte[] bytes = template.Content.ToArray();
        string part = BuilderHarness.MainPart(format);

        // A section deleted (the paid note and its markers), a loop variable renamed.
        byte[] cutDown = BuilderHarness.EditEntry(bytes, part, xml => RemoveParagraphsContaining(xml, format, "{{#if order.isPaid}}", "Paid", "{{/if}}"));
        byte[] renamed = BuilderHarness.EditEntry(bytes, part, xml => xml.Replace("line.", "l.", StringComparison.Ordinal).Replace("foreach line in", "foreach l in", StringComparison.Ordinal));
        // The end marker deleted: broken syntax.
        byte[] broken = BuilderHarness.EditEntry(bytes, part, xml => RemoveParagraphsContaining(xml, format, "{{/foreach}}"));

        Assert.True(TemplateInspector.CheckUpload(new MemoryStream(cutDown), template.Contract).IsUsable);
        Assert.True(TemplateInspector.CheckUpload(new MemoryStream(renamed), template.Contract).IsUsable);
        UploadCheckResult brokenResult = TemplateInspector.CheckUpload(new MemoryStream(broken), template.Contract);
        Assert.False(brokenResult.IsUsable);
        Assert.False(brokenResult.Syntax.IsValid);
    }

    [Theory]
    [MemberData(nameof(Formats))]
    public void CheckUpload_StampRemoved_IsUnknownButStillChecked(TemplateFormat format)
    {
        GeneratedTemplate template = BuilderHarness.Build(Invoice(2), format);
        using MemoryStream stripped = new MemoryStream();
        template.WriteTo(stripped);
        TemplateStampRemover.Remove(stripped);

        UploadCheckResult result = TemplateInspector.CheckUpload(stripped, template.Contract, _invoice);

        Assert.Null(result.Stamp);
        Assert.Equal(TemplateOrigin.Unknown, result.Origin);
        Assert.True(result.IsUsable);
    }

    [Fact]
    public void CheckUpload_OtherTemplate_IsReported()
    {
        GeneratedTemplate template = BuilderHarness.Build(Invoice(2), TemplateFormat.Odt);

        UploadCheckResult result = TemplateInspector.CheckUpload(template.OpenRead(), template.Contract, new TemplateIdentity("letter", 1));

        Assert.Equal(TemplateOrigin.OtherTemplate, result.Origin);
    }

    [Fact]
    public void CheckUpload_NotATemplate_IsNotUsable()
    {
        UploadCheckResult result = TemplateInspector.CheckUpload(new MemoryStream(new byte[] { 1, 2, 3, 4 }), BuilderHarness.Build(Invoice(2), TemplateFormat.Docx).Contract);

        Assert.Equal(TemplateFormat.Unknown, result.Format);
        Assert.False(result.IsUsable);
        Assert.Null(TemplateInspector.Inspect(new MemoryStream(new byte[] { 1, 2, 3, 4 })).Stamp);
    }

    private static string RemoveParagraphsContaining(string xml, TemplateFormat format, params string[] texts)
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
