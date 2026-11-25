// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Diagnostics;
using System.IO.Compression;
using System.Xml.Linq;
using TriasDev.Templify.Builder.Testing;
using TriasDev.Templify.Core;

namespace TriasDev.Templify.Builder.Tests;

/// <summary>Font embedding (#288).</summary>
public sealed class FontEmbeddingTests
{
    private const string Family = "Templify Embed Test";

    private static readonly XNamespace _w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private static readonly XNamespace _r = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace _style = "urn:oasis:names:tc:opendocument:xmlns:style:1.0";
    private static readonly XNamespace _svg = "urn:oasis:names:tc:opendocument:xmlns:svg-compatible:1.0";
    private static readonly XNamespace _xlink = "http://www.w3.org/1999/xlink";
    private static readonly XNamespace _manifest = "urn:oasis:names:tc:opendocument:xmlns:manifest:1.0";

    private static readonly ParagraphStyleKey _body = new ParagraphStyleKey("BodyCopy", "Body Copy");
    private static readonly ParagraphStyleKey _heading = new ParagraphStyleKey("SectionHeading", "Section Heading");

    public static TheoryData<TemplateFormat> Formats => new TheoryData<TemplateFormat>(BuilderHarness.Formats);

    private static byte[] FontBytes()
    {
        using Stream stream = typeof(FontEmbeddingTests).Assembly.GetManifestResourceStream("TemplifyEmbedTest-Regular.ttf")!;
        using MemoryStream copy = new MemoryStream();
        stream.CopyTo(copy);
        return copy.ToArray();
    }

    /// <summary>The font with a different <c>fsType</c> (OS/2 offset 8); checksums are not checked by the reader.</summary>
    private static byte[] WithFsType(ushort fsType)
    {
        byte[] font = FontBytes();
        int tables = BinaryPrimitives.ReadUInt16BigEndian(font.AsSpan(4));
        for (int i = 0; i < tables; i++)
        {
            int record = 12 + (i * 16);
            if (System.Text.Encoding.ASCII.GetString(font, record, 4) == "OS/2")
            {
                int offset = (int)BinaryPrimitives.ReadUInt32BigEndian(font.AsSpan(record + 8));
                BinaryPrimitives.WriteUInt16BigEndian(font.AsSpan(offset + 8), fsType);
                return font;
            }
        }

        throw new InvalidOperationException("The test font has no OS/2 table.");
    }

    private static TestDefinition Document(FontFace font, FontFace? headingFont = null) => new TestDefinition(t =>
    {
        t.Styles(StyleSheet.Create(s =>
        {
            s.Defaults(d => d.Font(font).Size(12.Pt()));
            s.Paragraph(_body, p => p.SpaceAfter(6.Pt()));
            s.Paragraph(_heading, p => p.BasedOn(_body).Font(headingFont ?? font).Size(16.Pt()));
        }));
        t.Content(c =>
        {
            c.Paragraph(_heading, "Embedded font");
            c.Paragraph(_body, x =>
            {
                x.Text("Hello ");
                x.Value(t.Data["name"]);
            });
        });
    });

    [Fact]
    public void Embedded_ReadsTheFamilyFromTheFile()
    {
        FontFace font = FontFace.Embedded(FontBytes());

        Assert.Equal(Family, font.Family);
        Assert.True(font.IsEmbedded);
        Assert.False(new FontFace(Family).IsEmbedded);
    }

    [Fact]
    public void Embedded_WithAFamily_MustBeOneTheFileDeclares()
    {
        Assert.Equal(Family, FontFace.Embedded(Family, FontBytes()).Family);

        ArgumentException exception = Assert.Throws<ArgumentException>(() => FontFace.Embedded("Other Family", FontBytes()));
        Assert.Contains("'Templify Embed Test'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Equality_IsFamilyAndFile()
    {
        FontFace embedded = FontFace.Embedded(FontBytes());

        Assert.Equal(embedded, FontFace.Embedded(FontBytes()));
        Assert.Equal(embedded.GetHashCode(), FontFace.Embedded(FontBytes()).GetHashCode());
        Assert.NotEqual(embedded, new FontFace(Family));
        Assert.NotEqual(embedded, FontFace.Embedded(WithFsType(0x0008)));
        Assert.Equal(new FontFace(Family), new FontFace(Family));
    }

    [Fact]
    public void Embedded_CopiesTheBytes()
    {
        byte[] bytes = FontBytes();
        FontFace font = FontFace.Embedded(bytes);
        bytes[100] ^= 0xFF;

        Assert.Equal(FontFace.Embedded(FontBytes()), font);
    }

    [Theory]
    [InlineData((ushort)0x0000)]
    [InlineData((ushort)0x0008)]
    [InlineData((ushort)0x000C)]
    [InlineData((ushort)0x0108)]
    public void Embedded_AllowsInstallableAndEditableFonts(ushort fsType)
    {
        Assert.True(FontFace.Embedded(WithFsType(fsType)).IsEmbedded);
    }

    [Theory]
    [InlineData((ushort)0x0002, "restricted")]
    [InlineData((ushort)0x0004, "preview and print only")]
    [InlineData((ushort)0x0208, "bitmaps only")]
    public void Embedded_RefusesFontsWhoseLicenceForbidsIt(ushort fsType, string reason)
    {
        ArgumentException exception = Assert.Throws<ArgumentException>(() => FontFace.Embedded(WithFsType(fsType)));

        Assert.Contains(reason, exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("OTTO", "PostScript (CFF)")]
    [InlineData("ttcf", "collection")]
    [InlineData("wOFF", "WOFF")]
    [InlineData("\u0089PNG", "not a TrueType font")]
    public void Embedded_RefusesOtherFileTypes(string signature, string reason)
    {
        byte[] bytes = FontBytes();
        System.Text.Encoding.Latin1.GetBytes(signature).CopyTo(bytes, 0);

        ArgumentException exception = Assert.Throws<ArgumentException>(() => FontFace.Embedded(bytes));

        Assert.Contains(reason, exception.Message, StringComparison.Ordinal);
        Assert.Throws<ArgumentException>(() => FontFace.Embedded(new byte[] { 0, 1 }));
        Assert.Throws<ArgumentNullException>(() => FontFace.Embedded(null!));
    }

    [Fact]
    public void Docx_EmbedsTheObfuscatedFont()
    {
        (GeneratedTemplate template, byte[] filled) = BuilderHarness.BuildAndFill(Document(FontFace.Embedded(FontBytes())), TemplateFormat.Docx, """{ "name": "Ada" }""");

        BuilderHarness.AssertInvariants(Document(FontFace.Embedded(FontBytes())), template);
        foreach (byte[] package in new[] { template.Content.ToArray(), filled })
        {
            XElement font = Assert.Single(BuilderHarness.ReadXml(package, "word/fontTable.xml").Root!.Elements(_w + "font"));
            Assert.Equal(Family, (string?)font.Attribute(_w + "name"));
            XElement embed = font.Element(_w + "embedRegular")!;
            string key = (string)embed.Attribute(_w + "fontKey")!;
            Assert.Matches("^\\{[0-9A-F]{8}-[0-9A-F]{4}-[0-9A-F]{4}-[0-9A-F]{4}-[0-9A-F]{12}\\}$", key);

            XElement relationship = BuilderHarness.ReadXml(package, "word/_rels/fontTable.xml.rels").Root!.Elements()
                .Single(r => (string?)r.Attribute("Id") == (string?)embed.Attribute(_r + "id"));
            Assert.Equal("http://schemas.openxmlformats.org/officeDocument/2006/relationships/font", (string?)relationship.Attribute("Type"));
            Assert.Equal(FontBytes(), Deobfuscate(Entry(package, "word/" + (string)relationship.Attribute("Target")!), key));
            Assert.NotNull(BuilderHarness.ReadXml(package, "word/settings.xml").Root!.Element(_w + "embedTrueTypeFonts"));
        }
    }

    [Fact]
    public void Odt_EmbedsTheFontFile()
    {
        (GeneratedTemplate template, byte[] filled) = BuilderHarness.BuildAndFill(Document(FontFace.Embedded(FontBytes())), TemplateFormat.Odt, """{ "name": "Ada" }""");

        foreach (byte[] package in new[] { template.Content.ToArray(), filled })
        {
            XElement face = BuilderHarness.ReadXml(package, "styles.xml").Descendants(_style + "font-face").Single(f => (string?)f.Attribute(_style + "name") == Family);
            XElement uri = face.Element(_svg + "font-face-src")!.Element(_svg + "font-face-uri")!;
            string path = (string)uri.Attribute(_xlink + "href")!;
            Assert.StartsWith("Fonts/", path, StringComparison.Ordinal);
            Assert.Equal("truetype", (string?)uri.Element(_svg + "font-face-format")!.Attribute(_svg + "string"));
            Assert.Equal(FontBytes(), Entry(package, path));

            XElement entry = BuilderHarness.ReadXml(package, "META-INF/manifest.xml").Root!.Elements(_manifest + "file-entry")
                .Single(e => (string?)e.Attribute(_manifest + "full-path") == path);
            Assert.Equal("application/x-font-ttf", (string?)entry.Attribute(_manifest + "media-type"));
        }
    }

    [Theory]
    [MemberData(nameof(Formats))]
    public void AFontUsedByManyStyles_IsEmbeddedOnce(TemplateFormat format)
    {
        GeneratedTemplate template = BuilderHarness.Build(Document(FontFace.Embedded(FontBytes()), FontFace.Embedded(FontBytes())), format);

        string folder = format == TemplateFormat.Docx ? "word/fonts/" : "Fonts/";
        Assert.Single(EntryNames(template.Content.ToArray()), n => n.StartsWith(folder, StringComparison.Ordinal));
    }

    [Theory]
    [MemberData(nameof(Formats))]
    public void WithoutEmbeddedFonts_NothingIsAdded(TemplateFormat format)
    {
        GeneratedTemplate template = BuilderHarness.Build(Document(new FontFace(Family)), format);

        List<string> names = EntryNames(template.Content.ToArray());
        Assert.DoesNotContain(names, n => n.Contains("font", StringComparison.OrdinalIgnoreCase) || n == "word/settings.xml");
    }

    [Theory]
    [MemberData(nameof(Formats))]
    public void OneFamilyWithTwoFiles_IsABuildError(TemplateFormat format)
    {
        TestDefinition definition = Document(FontFace.Embedded(FontBytes()), FontFace.Embedded(WithFsType(0x0008)));

        TemplateBuildException exception = Assert.Throws<TemplateBuildException>(() => BuilderHarness.Build(definition, format));

        BuildDiagnostic error = Assert.Single(exception.Errors);
        Assert.Equal("TB2011", error.Code);
        Assert.Contains(Family, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Verifier_PassesForEmbeddedFonts()
    {
        TemplateVerifier.VerifyAll(
            () => Document(FontFace.Embedded(FontBytes())),
            new TemplateVerificationOptions { SampleData = ["""{ "name": "Ada" }"""] });
    }

    [Theory]
    [MemberData(nameof(Formats))]
    [Trait("Category", "LibreOffice")]
    public void LibreOffice_RendersWithTheEmbeddedFont(TemplateFormat format)
    {
        Assert.SkipUnless(LibreOfficeConverter.IsAvailable, "LibreOffice (soffice) is not installed; set TEMPLIFY_SOFFICE to run round-trip tests.");
        (GeneratedTemplate template, byte[] filled) = BuilderHarness.BuildAndFill(Document(FontFace.Embedded(FontBytes())), format, """{ "name": "Ada" }""");

        // The family is installed nowhere: the PDF can only use it if LibreOffice loaded it from the document.
        Assert.Contains("TemplifyEmbedTest", PdfFonts(LibreOfficeConverter.ToPdf(filled, template.FileExtension)), StringComparison.Ordinal);
        Assert.DoesNotContain("TemplifyEmbedTest", PdfFonts(LibreOfficeConverter.ToPdf(BuilderHarness.Build(Document(new FontFace(Family)), format))), StringComparison.Ordinal);
    }

    /// <summary>The obfuscation as LibreOffice undoes it: key bytes from the GUID's hex pairs, last pair first.</summary>
    private static byte[] Deobfuscate(byte[] data, string key)
    {
        int[] positions = { 35, 33, 31, 29, 27, 25, 22, 20, 17, 15, 12, 10, 7, 5, 3, 1 };
        byte[] bytes = positions.Select(p => Convert.ToByte(key.Substring(p, 2), 16)).ToArray();
        byte[] result = (byte[])data.Clone();
        for (int i = 0; i < 16; i++)
        {
            result[i] ^= bytes[i];
            result[i + 16] ^= bytes[i];
        }

        return result;
    }

    private static byte[] Entry(byte[] package, string name)
    {
        using ZipArchive archive = new ZipArchive(new MemoryStream(package), ZipArchiveMode.Read);
        using Stream stream = archive.GetEntry(name)!.Open();
        using MemoryStream copy = new MemoryStream();
        stream.CopyTo(copy);
        return copy.ToArray();
    }

    private static List<string> EntryNames(byte[] package)
    {
        using ZipArchive archive = new ZipArchive(new MemoryStream(package), ZipArchiveMode.Read);
        return archive.Entries.Select(e => e.FullName).ToList();
    }

    private static string PdfFonts(byte[] pdf)
    {
        string? executable = new[] { "/opt/homebrew/bin/pdffonts", "/usr/local/bin/pdffonts", "/usr/bin/pdffonts" }.FirstOrDefault(File.Exists);
        Assert.SkipWhen(executable == null, "pdffonts (poppler) is not installed.");
        string file = Path.Combine(Path.GetTempPath(), "templify-fonts-" + Guid.NewGuid().ToString("N") + ".pdf");
        File.WriteAllBytes(file, pdf);
        try
        {
            ProcessStartInfo start = new ProcessStartInfo(executable!) { RedirectStandardOutput = true, UseShellExecute = false };
            start.ArgumentList.Add(file);
            using Process process = Process.Start(start)!;
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            return output;
        }
        finally
        {
            File.Delete(file);
        }
    }
}
