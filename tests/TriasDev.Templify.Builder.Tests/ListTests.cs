// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using TriasDev.Templify.Core;
using TriasDev.Templify.Tests.Helpers;
using TriasDev.Templify.Tests.Odt;

namespace TriasDev.Templify.Builder.Tests;

public sealed partial class ListTests
{
    private static readonly XNamespace _w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private static readonly XNamespace _text = "urn:oasis:names:tc:opendocument:xmlns:text:1.0";
    private static readonly XNamespace _style = "urn:oasis:names:tc:opendocument:xmlns:style:1.0";
    private static readonly XNamespace _fo = "urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0";
    private static readonly XNamespace _office = "urn:oasis:names:tc:opendocument:xmlns:office:1.0";

    public static TheoryData<TemplateFormat> Formats => new TheoryData<TemplateFormat>(BuilderHarness.Formats);

    private static readonly ListStyleKey _bullets = new ListStyleKey("Bullets");
    private static readonly ParagraphStyleKey _item = new ParagraphStyleKey("ListItem", "List Item");

    private static TestDefinition Measures() => new TestDefinition(t =>
    {
        t.Styles(StyleSheet.Create(s =>
        {
            s.Paragraph(_item, p => p.SpaceAfter(2.Pt()));
            s.List(_bullets, l => l
                .Level(0, "•", 6.Mm(), 4.Mm())
                .Level(1, "–", 12.Mm(), 4.Mm())
                .ItemStyle(_item));
        }));
        t.Content(c =>
        {
            c.Paragraph("Measures");
            c.List(_bullets, l =>
            {
                l.Item("General");
                l.ForEach(t.Data["measures"], "measure", (measure, ml) =>
                {
                    ml.Item(measure["title"]);
                    ml.ForEach(measure["requirements"], "req", (req, rl) => rl.Item(1, x =>
                    {
                        x.Value(req.Self);
                        x.Text(" (");
                        x.Value(measure["title"]);
                        x.Text(")");
                    }));
                    ml.If(measure["optional"], o => o.Item(1, "optional")).Else(o => o.Item(1, "mandatory"));
                });
            });
            c.Paragraph("after");
        });
    });

    private const string Json = """
    {
      "measures": [
        { "title": "Encryption", "requirements": [ "at rest", "in transit" ], "optional": false },
        { "title": "Training", "requirements": [ "yearly" ], "optional": true }
      ]
    }
    """;

    private static readonly string[] _expected =
    {
        "Measures", "General", "Encryption", "at rest (Encryption)", "in transit (Encryption)", "mandatory",
        "Training", "yearly (Training)", "optional", "after",
    };

    [Theory]
    [MemberData(nameof(Formats))]
    public void List_WithLoopsAndConditions_IsFilled(TemplateFormat format)
    {
        (_, byte[] filled) = BuilderHarness.BuildAndFill(Measures(), format, Json);

        Assert.Equal(_expected, BuilderHarness.ParagraphTexts(format, filled));
    }

    [Fact]
    public void Docx_UsesNumberingWithBulletLevels()
    {
        (GeneratedTemplate template, byte[] filled) = BuilderHarness.BuildAndFill(Measures(), TemplateFormat.Docx, Json);
        byte[] package = template.Content.ToArray();

        XElement numbering = BuilderHarness.ReadXml(package, "word/numbering.xml").Root!;
        List<XElement> levels = numbering.Descendants(_w + "lvl").ToList();
        Assert.Equal(new[] { "•", "–" }, levels.Select(l => l.Element(_w + "lvlText")!.Attribute(_w + "val")!.Value));
        Assert.Equal(new[] { "340", "680" }, levels.Select(l => l.Descendants(_w + "ind").Single().Attribute(_w + "left")!.Value));
        Assert.Equal("1", numbering.Element(_w + "num")!.Attribute(_w + "numId")!.Value);

        List<XElement> paragraphs = BuilderHarness.ReadXml(filled, "word/document.xml").Descendants(_w + "p").ToList();
        XElement general = paragraphs.Single(p => p.Value == "General");
        Assert.Equal("ListItem", general.Element(_w + "pPr")!.Element(_w + "pStyle")!.Attribute(_w + "val")!.Value);
        Assert.Equal("0", general.Descendants(_w + "ilvl").Single().Attribute(_w + "val")!.Value);
        Assert.Equal("1", paragraphs.Single(p => p.Value == "yearly (Training)").Descendants(_w + "ilvl").Single().Attribute(_w + "val")!.Value);
        Assert.Null(paragraphs.Single(p => p.Value == "after").Element(_w + "pPr"));
    }

    [Fact]
    public void Odt_UsesAListStyleAndNestedLists()
    {
        (GeneratedTemplate template, byte[] filled) = BuilderHarness.BuildAndFill(Measures(), TemplateFormat.Odt, Json);

        XElement listStyle = BuilderHarness.ReadXml(template.Content.ToArray(), "styles.xml").Descendants(_text + "list-style").Single();
        Assert.Equal("Bullets", listStyle.Attribute(_style + "name")!.Value);
        List<XElement> levels = listStyle.Elements(_text + "list-level-style-bullet").ToList();
        Assert.Equal(new[] { "1", "2" }, levels.Select(l => l.Attribute(_text + "level")!.Value));
        Assert.Equal("-11.339pt", levels[0].Descendants(_style + "list-level-label-alignment").Single().Attribute(_fo + "text-indent")!.Value);

        XElement body = BuilderHarness.ReadXml(filled, "content.xml").Descendants(_office + "text").First();
        XElement nested = body.Descendants(_text + "p").Single(p => OdtDocumentVerifier.RenderText(p) == "yearly (Training)");
        Assert.Equal(2, nested.Ancestors(_text + "list").Count());
        Assert.Equal("Bullets", nested.Ancestors(_text + "list").Last().Attribute(_text + "style-name")!.Value);
        Assert.Equal("ListItem", nested.Attribute(_text + "style-name")!.Value);
    }

    [Theory]
    [MemberData(nameof(Formats))]
    public void Contract_IncludesListLoops(TemplateFormat format)
    {
        Assert.Equal(
            new[] { "measures (Collection)", "measures[].optional (Condition)", "measures[].requirements (Collection)", "measures[].requirements[] (Value)", "measures[].title (Value)" },
            BuilderHarness.Build(Measures(), format).Contract.Entries.Select(e => $"{e.Path} ({e.Usage})"));
    }

    [Fact]
    public void Errors_AreReported()
    {
        TestDefinition undefined = new TestDefinition(t => t.Content(c => c.List(_bullets, l => l.Item("x"))));
        TestDefinition level = new TestDefinition(t =>
        {
            t.Styles(StyleSheet.Create(s => s.List(_bullets, l => l.Level(0, "•", 6.Mm(), 4.Mm()))));
            t.Content(c => c.List(_bullets, l => l.Item(2, "too deep")));
        });

        Assert.Equal("TB2010", Assert.Single(Assert.Throws<TemplateBuildException>(() => BuilderHarness.Build(undefined, TemplateFormat.Docx)).Errors).Code);
        Assert.Equal("TB8001", Assert.Single(Assert.Throws<TemplateBuildException>(() => BuilderHarness.Build(level, TemplateFormat.Odt)).Errors).Code);
        Assert.Throws<ArgumentOutOfRangeException>(() => StyleSheet.Create(s => s.List(_bullets, l => l.Level(0, "•", 2.Mm(), 4.Mm()))));
        Assert.Throws<ArgumentOutOfRangeException>(() => StyleSheet.Create(s => s.List(_bullets, l => l.Level(9, "•", 6.Mm(), 4.Mm()))));
        Assert.Throws<ArgumentException>(() => StyleSheet.Create(s => s.List(_bullets, l => l.Level(0, string.Empty, 6.Mm(), 4.Mm()))));
    }

    [Theory]
    [MemberData(nameof(Formats))]
    [Trait("Category", "LibreOffice")]
    public void LibreOffice_PlacesBulletsAndText(TemplateFormat format)
    {
        LibreOfficeRunner.RequireExecutable();
        (GeneratedTemplate template, byte[] filled) = BuilderHarness.BuildAndFill(Measures(), format, Json);
        string extension = template.FileExtension.TrimStart('.');

        // LibreOffice's text export draws its own bullets per level (•, ◦, …); the PDF shows the list style's.
        string text = Encoding.UTF8.GetString(LibreOfficeRunner.Convert(filled, extension, "txt:Text (encoded):UTF8", "txt"));
        Assert.Contains("• General", text, StringComparison.Ordinal);

        // Level 0: bullet at 6 − 4 = 2 mm, text at 6 mm; level 1: bullet "–" at 8 mm, text at 12 mm (from 70.866 pt).
        Dictionary<string, double> starts = WordStarts(LibreOfficeRunner.Convert(filled, extension, "pdf", "pdf"));
        Assert.InRange(starts["General"], 70.866 + 17.008 - 1, 70.866 + 17.008 + 1);
        Assert.InRange(starts["at"], 70.866 + 34.016 - 1, 70.866 + 34.016 + 1);
        Assert.InRange(starts["•"], 70.866 + 5.669 - 1, 70.866 + 5.669 + 1);
        Assert.InRange(starts["–"], 70.866 + 22.677 - 1, 70.866 + 22.677 + 1);
    }

    private static Dictionary<string, double> WordStarts(byte[] pdf)
    {
        string? executable = new[] { "/opt/homebrew/bin/pdftotext", "/usr/local/bin/pdftotext", "/usr/bin/pdftotext" }.FirstOrDefault(File.Exists);
        Assert.SkipWhen(executable == null, "pdftotext (poppler) is not installed.");
        string file = Path.Combine(Path.GetTempPath(), "templify-list-" + Guid.NewGuid().ToString("N") + ".pdf");
        File.WriteAllBytes(file, pdf);
        try
        {
            ProcessStartInfo start = new ProcessStartInfo(executable!) { RedirectStandardOutput = true, UseShellExecute = false };
            foreach (string argument in new[] { "-bbox", file, "-" })
            {
                start.ArgumentList.Add(argument);
            }

            using Process process = Process.Start(start)!;
            string html = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            Dictionary<string, double> starts = new Dictionary<string, double>(StringComparer.Ordinal);
            foreach (Match match in WordBox().Matches(html))
            {
                starts.TryAdd(match.Groups[2].Value, double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture));
            }

            return starts;
        }
        finally
        {
            File.Delete(file);
        }
    }

    [GeneratedRegex("xMin=\"([0-9.]+)\"[^>]*>([^<]+)</word>")]
    private static partial Regex WordBox();
}
