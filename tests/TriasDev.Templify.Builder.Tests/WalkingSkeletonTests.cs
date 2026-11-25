// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.IO.Compression;
using System.Text;
using TriasDev.Templify.Core;
using TriasDev.Templify.Tests.Helpers;
using TriasDev.Templify.Tests.Odt;

namespace TriasDev.Templify.Builder.Tests;

public sealed class WalkingSkeletonTests
{
    private const string GreetingJson = """{ "customer": { "name": "Alice" } }""";

    public static TheoryData<TemplateFormat> Formats => new TheoryData<TemplateFormat>(BuilderHarness.Formats);

    private static TestDefinition Greeting() => new TestDefinition(template =>
    {
        TemplatePath customer = template.Data["customer"];
        template.Content(c =>
        {
            c.Paragraph("Greeting");
            c.Paragraph(x =>
            {
                x.Text("Hello ");
                x.Value(customer["name"]);
                x.Text("!");
            });
            c.Paragraph(customer["name"]);
        });
    });

    [Theory]
    [MemberData(nameof(Formats))]
    public void Build_ParagraphWithPlaceholder_IsFilledByTemplify(TemplateFormat format)
    {
        (GeneratedTemplate template, byte[] filled) = BuilderHarness.BuildAndFill(Greeting(), format, GreetingJson);

        Assert.Equal(new[] { "Greeting", "Hello {{customer.name}}!", "{{customer.name}}" }, BuilderHarness.ParagraphTexts(format, template.Content.ToArray()));
        Assert.Equal(new[] { "Greeting", "Hello Alice!", "Alice" }, BuilderHarness.ParagraphTexts(format, filled));
    }

    [Theory]
    [MemberData(nameof(Formats))]
    public void Build_DescribesTheFormat(TemplateFormat format)
    {
        GeneratedTemplate template = BuilderHarness.Build(Greeting(), format);

        Assert.Equal(format, template.Format);
        Assert.Equal(format == TemplateFormat.Docx ? ".docx" : ".odt", template.FileExtension);
        Assert.Equal(
            format == TemplateFormat.Docx
                ? "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
                : "application/vnd.oasis.opendocument.text",
            template.MediaType);
        Assert.Equal(64, template.ContentHash.Length);
        Assert.Equal(template.ContentHash, template.ContentHash.ToLowerInvariant());

        using (Stream stream = template.OpenRead())
        {
            Assert.False(stream.CanWrite);
            Assert.True(stream.CanSeek);
        }

        using MemoryStream copy = new MemoryStream();
        template.WriteTo(copy);
        Assert.Equal(template.Content.ToArray(), copy.ToArray());
    }

    [Theory]
    [MemberData(nameof(Formats))]
    public void Build_IsDeterministic_AcrossBuildsAndCultures(TemplateFormat format)
    {
        byte[] reference = BuilderHarness.Build(Greeting(), format).Content.ToArray();

        foreach (string culture in new[] { "tr-TR", "ar-SA", "de-DE" })
        {
            CultureInfo previous = CultureInfo.CurrentCulture;
            CultureInfo previousUi = CultureInfo.CurrentUICulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
                Assert.Equal(reference, BuilderHarness.Build(Greeting(), format).Content.ToArray());
            }
            finally
            {
                CultureInfo.CurrentCulture = previous;
                CultureInfo.CurrentUICulture = previousUi;
            }
        }
    }

    [Theory]
    [MemberData(nameof(Formats))]
    public void Build_EntriesHaveAFixedTimestamp(TemplateFormat format)
    {
        using MemoryStream stream = new MemoryStream(BuilderHarness.Build(Greeting(), format).Content.ToArray());
        using ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Read);

        Assert.All(archive.Entries, e => Assert.Equal(new DateTime(2000, 1, 1), e.LastWriteTime.DateTime));
    }

    [Fact]
    public void Build_Odt_StoresTheMimetypeFirstAndUncompressed()
    {
        using MemoryStream stream = new MemoryStream(BuilderHarness.Build(Greeting(), TemplateFormat.Odt).Content.ToArray());
        using ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Read);

        ZipArchiveEntry first = archive.Entries[0];
        Assert.Equal("mimetype", first.FullName);
        Assert.Equal(first.Length, first.CompressedLength);
    }

    [Theory]
    [InlineData(TemplateFormat.Docx, "de-DE")]
    [InlineData(TemplateFormat.Odt, "de-DE")]
    [InlineData(TemplateFormat.Odt, "en")]
    public void Build_WritesTheDocumentLanguage(TemplateFormat format, string language)
    {
        GeneratedTemplate template = TemplateBuilder.Build(Greeting(), BuilderHarness.Options(format, language));

        string part = format == TemplateFormat.Docx ? "docProps/core.xml" : "meta.xml";
        using MemoryStream stream = new MemoryStream(template.Content.ToArray());
        using ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Read);
        using StreamReader reader = new StreamReader(archive.GetEntry(part)!.Open(), Encoding.UTF8);
        Assert.Contains($"<dc:language>{language}</dc:language>", reader.ReadToEnd(), StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Formats))]
    public void Build_LiteralWhitespace_RendersExactly(TemplateFormat format)
    {
        TestDefinition definition = new TestDefinition(template => template.Content(c =>
        {
            c.Paragraph("  two leading,  two inner, one trailing ");
            c.Paragraph("tab\there");
            c.Paragraph("line\nbreak and\r\nwindows");
            c.Paragraph(x =>
            {
                x.Text("a ");
                x.Text(" b");
            });
            c.Paragraph(string.Empty);
        }));

        (GeneratedTemplate template, _) = BuilderHarness.BuildAndFill(definition, format, "{}");

        Assert.Equal(
            new[] { "  two leading,  two inner, one trailing ", "tab\there", "line\nbreak and\nwindows", "a  b", string.Empty },
            BuilderHarness.ParagraphTexts(format, template.Content.ToArray()));
    }

    [Theory]
    [MemberData(nameof(Formats))]
    [Trait("Category", "LibreOffice")]
    public void LibreOffice_OpensBuiltAndFilledDocuments(TemplateFormat format)
    {
        LibreOfficeRunner.RequireExecutable();
        (GeneratedTemplate template, byte[] filled) = BuilderHarness.BuildAndFill(Greeting(), format, GreetingJson);
        string extension = template.FileExtension.TrimStart('.');

        Assert.Equal(new[] { "Greeting", "Hello {{customer.name}}!", "{{customer.name}}" }, ToText(template.Content.ToArray(), extension));
        Assert.Equal(new[] { "Greeting", "Hello Alice!", "Alice" }, ToText(filled, extension));
        Assert.NotEmpty(LibreOfficeRunner.Convert(filled, extension, "pdf", "pdf"));
    }

    private static string[] ToText(byte[] document, string extension)
    {
        string text = Encoding.UTF8.GetString(LibreOfficeRunner.Convert(document, extension, "txt:Text (encoded):UTF8", "txt"));
        return text.TrimStart('﻿').Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n').Split('\n');
    }
}
