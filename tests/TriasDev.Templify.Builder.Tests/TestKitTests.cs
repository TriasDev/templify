// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.IO.Compression;
using TriasDev.Templify.Builder.Testing;
using TriasDev.Templify.Core;

namespace TriasDev.Templify.Builder.Tests;

/// <summary>The consumer test kit (TriasDev.Templify.Builder.Testing, #286).</summary>
public sealed class TestKitTests
{
    private static readonly ParagraphStyleKey _heading = new ParagraphStyleKey("Heading1", "Heading 1");

    private static readonly CultureInfo _de = CultureInfo.GetCultureInfo("de-DE");
    private static readonly CultureInfo _en = CultureInfo.GetCultureInfo("en-US");

    private const string Sample = """
        { "order": { "number": "A-17", "isPaid": true, "total": 1234.5,
          "lines": [ { "description": "Chair", "amount": 2 }, { "description": "Desk", "amount": 1 } ] } }
        """;

    /// <summary>A definition with texts, a conditional, a loop and a table: what a consumer verifies.</summary>
    private sealed class OrderTemplate : ITemplateDefinition
    {
        public TemplateIdentity Identity { get; } = new TemplateIdentity("order", 2);

        public void Compose(TemplateComposer template)
        {
            TemplatePath order = template.Data["order"];
            template.Styles(StyleSheet.Create(s => s.Paragraph(_heading, p => p.Size(16.Pt()).Bold().OutlineLevel(1))));
            template.Page(p => p.Header(h => h.Paragraph(x =>
            {
                x.Text(template.Texts["order.title"] + " ");
                x.Value(order["number"]);
            })));
            template.Content(c =>
            {
                c.Paragraph(_heading, x =>
                {
                    x.Text(template.Texts["order.title"] + " ");
                    x.Value(order["number"]);
                });
                c.If(order["isPaid"], b => b.Paragraph(template.Texts["order.paid"]));
                c.Table(t =>
                {
                    t.Columns(cols =>
                    {
                        cols.Relative(3);
                        cols.Relative(1);
                    });
                    t.ForEachRow(order["lines"], "line", (line, rows) => rows.Row(r =>
                    {
                        r.Cell(line["description"]);
                        r.Cell(line["amount"]);
                    }));
                });
                c.Paragraph(x =>
                {
                    x.Text(template.Texts["order.total"] + "\t");
                    x.Value(order["total"], ValueFormat.Number("N2"));
                });
            });
        }
    }

    private static readonly ITextCatalog _catalog = TextCatalog.FromDictionary(new Dictionary<string, IReadOnlyDictionary<string, string>>
    {
        ["order.title"] = new Dictionary<string, string> { ["de"] = "Auftrag", ["en"] = "Order" },
        ["order.paid"] = new Dictionary<string, string> { ["de"] = "Bezahlt.", ["en"] = "Paid." },
        ["order.total"] = new Dictionary<string, string> { ["de"] = "Summe:", ["en"] = "Total:" },
    });

    private static ITextCatalog Texts(CultureInfo language) => _catalog;

    private static TemplateVerificationOptions Options => new TemplateVerificationOptions
    {
        Languages = [_de, _en],
        Texts = Texts,
        SampleData = [Sample],
    };

    [Fact]
    public void VerifyAll_PassesForAGoodDefinition()
    {
        TemplateVerificationReport report = TemplateVerifier.VerifyAll(() => new OrderTemplate(), Options);

        Assert.True(report.IsSuccess);
        Assert.Equal(4, report.Templates.Count);
        Assert.Equal(
            new[] { "de-DE/Docx", "de-DE/Odt", "en-US/Docx", "en-US/Odt" },
            report.Templates.Select(t => t.Language.Name + "/" + t.Format));
        Assert.Single(report.Templates.Select(t => t.Template.Contract.Hash).Distinct());
        Assert.StartsWith("Template verification passed (4 templates)", report.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Verify_DefaultOptions_BuildBothFormatsInEnglish()
    {
        TemplateVerificationReport report = TemplateVerifier.Verify(() => new TestDefinition(t => t.Content(c => c.Paragraph("Hello"))));

        Assert.True(report.IsSuccess, report.ToString());
        Assert.Equal(new[] { TemplateFormat.Docx, TemplateFormat.Odt }, report.Templates.Select(t => t.Format));
        Assert.All(report.Templates, t => Assert.Equal("en-US", t.Language.Name));
    }

    [Fact]
    public void Verify_ReportsBuildErrorsPerTarget()
    {
        TemplateVerificationReport report = TemplateVerifier.Verify(
            () => new TestDefinition(t => t.Content(c => c.Paragraph(new ParagraphStyleKey("Undefined", "Undefined"), "x"))));

        Assert.False(report.IsSuccess);
        Assert.Empty(report.Templates);
        Assert.Equal(2, report.Failures.Count);
        Assert.All(report.Failures, f =>
        {
            Assert.Equal(VerificationCheck.Build, f.Check);
            Assert.StartsWith("TB", f.Message, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void VerifyAll_ThrowsWithEveryFailure()
    {
        TemplateVerificationException exception = Assert.Throws<TemplateVerificationException>(() => TemplateVerifier.VerifyAll(
            () => new OrderTemplate(),
            new TemplateVerificationOptions { Texts = Texts, SampleData = ["""{ "order": { "number": "A-17" } }"""] }));

        // The sample lacks order.lines and order.total: the fill fails in both formats.
        Assert.Equal(2, exception.Report.Failures.Count);
        Assert.All(exception.Report.Failures, f => Assert.Equal(VerificationCheck.Fill, f.Check));
        Assert.Contains("[en-US/docx] Fill: sample data #1:", exception.Message, StringComparison.Ordinal);
        Assert.Contains("[en-US/odt] Fill: sample data #1:", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Verify_FindsNonDeterministicDefinitions()
    {
        // A definition must not read the clock, random values or the current culture.
        TemplateVerificationReport report = TemplateVerifier.Verify(
            () => new TestDefinition(t => t.Content(c => c.Paragraph(Guid.NewGuid().ToString()))),
            new TemplateVerificationOptions { Formats = [TemplateFormat.Odt] });

        TemplateVerificationFailure[] failures = report.Failures.ToArray();
        Assert.Equal(2, failures.Length);
        Assert.All(failures, f => Assert.Equal(VerificationCheck.Determinism, f.Check));
        Assert.Contains("tr-TR", failures[0].Message, StringComparison.Ordinal);
        Assert.Contains("ar-SA", failures[1].Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Verify_FindsContractsThatDifferAcrossLanguages()
    {
        // A definition whose data depends on something else than the texts: here a call counter, standing in for
        // a definition that branches on the language outside the catalog.
        int builds = 0;
        TemplateVerificationReport report = TemplateVerifier.Verify(
            () =>
            {
                bool first = builds++ == 0;
                return new TestDefinition(t => t.Content(c => c.Paragraph(first ? t.Data["a"] : t.Data["b"])));
            },
            new TemplateVerificationOptions { Formats = [TemplateFormat.Docx], Languages = [_de, _en], DeterminismCultures = [] });

        TemplateVerificationFailure failure = Assert.Single(report.Failures);
        Assert.Equal(VerificationCheck.ContractParity, failure.Check);
        Assert.Null(failure.Language);
        Assert.Equal("en-US/docx reads different data than de-DE/docx: -a (Value), +b (Value)", failure.Message);
    }

    [Fact]
    public void Verify_BuildWarnings_FailOnlyWhenAsked()
    {
        Func<ITemplateDefinition> toc = () => new TestDefinition(t => t.Content(c => c.TableOfContents()));

        Assert.True(TemplateVerifier.Verify(toc).IsSuccess);
        TemplateVerificationReport strict = TemplateVerifier.Verify(toc, new TemplateVerificationOptions { FailOnBuildWarnings = true });
        Assert.All(strict.Failures, f => Assert.Equal(VerificationCheck.BuildWarning, f.Check));
        Assert.Contains(strict.Failures, f => f.Message.StartsWith("TB9001", StringComparison.Ordinal));
    }

    [Fact]
    public void Verify_RejectsEmptyTargets()
    {
        Assert.Throws<ArgumentException>(() => TemplateVerifier.Verify(() => new OrderTemplate(), new TemplateVerificationOptions { Languages = [] }));
        Assert.Throws<ArgumentNullException>(() => TemplateVerifier.Verify(null!));
    }

    [Theory]
    [InlineData(TemplateFormat.Docx)]
    [InlineData(TemplateFormat.Odt)]
    public void CheckTemplate_FindsPlaceholdersOutsideTheContract(TemplateFormat format)
    {
        // A hand-edited template that reads a path its contract does not declare.
        GeneratedTemplate template = BuilderHarness.Build(new TestDefinition(t => t.Content(c => c.Paragraph(t.Data["name"]))), format);
        byte[] edited = BuilderHarness.EditEntry(
            template.Content.ToArray(),
            BuilderHarness.MainPart(format),
            xml => xml.Replace("{{name}}", "{{name}} {{extra}}", StringComparison.Ordinal));
        GeneratedTemplate tampered = Rewrap(template, edited);

        IReadOnlyList<string> problems = TemplateVerifier.CheckTemplate(tampered);

        Assert.Contains(problems, p => p.StartsWith("Contract: 'extra' is read but not in the data contract.", StringComparison.Ordinal));
    }

    [Fact]
    public void CheckFill_ReportsMissingVariablesAndAcceptsCompleteData()
    {
        GeneratedTemplate template = BuilderHarness.Build(new TestDefinition(t => t.Content(c => c.Paragraph(t.Data["name"]))), TemplateFormat.Docx);

        Assert.Empty(TemplateVerifier.CheckFill(template, """{ "name": "Ada" }"""));
        Assert.Contains("name", Assert.Single(TemplateVerifier.CheckFill(template, "{}")), StringComparison.Ordinal);
    }

    [Fact]
    public void CheckPackage_FindsACompressedMimetype()
    {
        GeneratedTemplate template = BuilderHarness.Build(new TestDefinition(t => t.Content(c => c.Paragraph("x"))), TemplateFormat.Odt);
        byte[] broken = Repack(template.Content.ToArray(), compressMimetype: true);

        IReadOnlyList<string> problems = TemplateVerifier.CheckPackage(TemplateFormat.Odt, broken);

        Assert.Contains("The mimetype entry is compressed; it must be stored.", problems);
    }

    [Fact]
    public void CheckPackage_FindsAManifestEntryWithoutAFile()
    {
        GeneratedTemplate template = BuilderHarness.Build(new TestDefinition(t => t.Content(c => c.Paragraph("x"))), TemplateFormat.Odt);
        byte[] broken = BuilderHarness.EditEntry(
            template.Content.ToArray(),
            "META-INF/manifest.xml",
            xml => xml.Replace("</manifest:manifest>", "<manifest:file-entry manifest:full-path=\"Pictures/missing.png\" manifest:media-type=\"image/png\"/></manifest:manifest>", StringComparison.Ordinal));

        Assert.Contains("The manifest lists 'Pictures/missing.png', which is not in the package.", TemplateVerifier.CheckPackage(TemplateFormat.Odt, broken));
    }

    [Fact]
    public void CheckPackage_FindsDocxSchemaErrors()
    {
        GeneratedTemplate template = BuilderHarness.Build(new TestDefinition(t => t.Content(c => c.Paragraph("x"))), TemplateFormat.Docx);
        byte[] broken = BuilderHarness.EditEntry(
            template.Content.ToArray(),
            "word/document.xml",
            xml => xml.Replace("<w:body>", "<w:body><w:p><w:r><w:bogus/></w:r></w:p>", StringComparison.Ordinal));

        Assert.NotEmpty(TemplateVerifier.CheckPackage(TemplateFormat.Docx, broken));
        Assert.Empty(TemplateVerifier.CheckPackage(TemplateFormat.Docx, template.Content));
    }

    [Fact]
    public void CheckPackage_NotAPackage_IsAProblemNotAnException()
    {
        Assert.Single(TemplateVerifier.CheckPackage(TemplateFormat.Odt, new byte[] { 1, 2, 3 }));
        Assert.Single(TemplateVerifier.CheckPackage(TemplateFormat.Docx, new byte[] { 1, 2, 3 }));
    }

    [Theory]
    [InlineData(TemplateFormat.Docx)]
    [InlineData(TemplateFormat.Odt)]
    public void Snapshot_IsTheSameForBothFormats(TemplateFormat format)
    {
        GeneratedTemplate template = TemplateBuilder.Build(
            new OrderTemplate(),
            new TemplateBuildOptions { Format = format, Language = _en, Texts = Texts(_en) });

        string snapshot = TemplateSnapshot.Create(template);

        Assert.Equal(
            """
            == body
            Heading1 | Order {{order.number}}
            TemplifyMarker | {{#if order.isPaid}}
            Normal | Paid.
            TemplifyMarker | {{/if}}
            table
              row
                cell
                  TemplifyMarker | {{#foreach line in order.lines}}
              row
                cell
                  Normal | {{line.description}}
                cell
                  Normal | {{line.amount}}
              row
                cell
                  TemplifyMarker | {{/foreach}}
            Normal | Total:→{{order.total:number:N2}}
            == header
            Normal | Order {{order.number}}
            """.Replace("\r\n", "\n", StringComparison.Ordinal),
            snapshot);
    }

    [Theory]
    [InlineData(TemplateFormat.Docx)]
    [InlineData(TemplateFormat.Odt)]
    public void Snapshot_ShowsListsFieldsImagesAndBreaks(TemplateFormat format)
    {
        ListStyleKey bullets = new ListStyleKey("Bullets");
        TestDefinition definition = new TestDefinition(t =>
        {
            t.Styles(StyleSheet.Create(s => s.List(bullets, l => l.Level(0, "•", 6.Mm(), 4.Mm()).Level(1, "–", 12.Mm(), 4.Mm()))));
            t.Page(p => p.Footer(f => f.Paragraph(x =>
            {
                x.PageNumber();
                x.Text(" / ");
                x.PageCount();
            })));
            t.Content(c =>
            {
                c.List(bullets, l =>
                {
                    l.Item("one");
                    l.Item("two");
                    l.Item(1, "nested");
                });
                c.Paragraph(x =>
                {
                    x.Text("a");
                    x.LineBreak();
                    x.Text("b");
                    x.Tab();
                    x.Image(ImageSource.Png(ImageTests.Png), "logo");
                });
            });
        });

        string snapshot = TemplateSnapshot.Create(BuilderHarness.Build(definition, format));

        Assert.Equal(
            """
            == body
            Normal [list 1] | one
            Normal [list 1] | two
            Normal [list 2] | nested
            Normal | a↵b→[image]
            == footer
            Normal | {PAGE} / {NUMPAGES}
            """.Replace("\r\n", "\n", StringComparison.Ordinal),
            snapshot);
    }

    [Theory]
    [InlineData(TemplateFormat.Docx)]
    [InlineData(TemplateFormat.Odt)]
    public void Snapshot_OfAFilledDocument(TemplateFormat format)
    {
        (GeneratedTemplate template, byte[] filled) = BuilderHarness.BuildAndFill(
            new TestDefinition(t => t.Content(c => c.If(t.Data["show"], b => b.Paragraph(t.Data["name"])))),
            format,
            """{ "show": true, "name": "Ada" }""");

        Assert.Equal("== body\nNormal | Ada", TemplateSnapshot.Create(format, filled));
        Assert.Contains("{{#if show}}", TemplateSnapshot.Create(template), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(TemplateFormat.Docx)]
    [InlineData(TemplateFormat.Odt)]
    [Trait("Category", "LibreOffice")]
    public void LibreOfficeConverter_ConvertsTemplatesAndFilledDocuments(TemplateFormat format)
    {
        Assert.SkipUnless(LibreOfficeConverter.IsAvailable, "LibreOffice (soffice) is not installed; set TEMPLIFY_SOFFICE to run round-trip tests.");
        (GeneratedTemplate template, byte[] filled) = BuilderHarness.BuildAndFill(
            new TestDefinition(t => t.Content(c => c.Paragraph(t.Data["name"]))),
            format,
            """{ "name": "Ada Lovelace" }""");

        Assert.Equal("Ada Lovelace\n", LibreOfficeConverter.ToText(filled, template.FileExtension));
        byte[] pdf = LibreOfficeConverter.ToPdf(template);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdf, 0, 4));
    }

    private static GeneratedTemplate Rewrap(GeneratedTemplate template, byte[] content) =>
        new GeneratedTemplate(template.Format, content, template.Contract, template.Stamp, template.Warnings);

    private static byte[] Repack(byte[] package, bool compressMimetype)
    {
        using MemoryStream output = new MemoryStream();
        using (MemoryStream input = new MemoryStream(package))
        using (ZipArchive source = new ZipArchive(input, ZipArchiveMode.Read))
        using (ZipArchive target = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (ZipArchiveEntry entry in source.Entries)
            {
                CompressionLevel level = entry.FullName == "mimetype" && !compressMimetype ? CompressionLevel.NoCompression : CompressionLevel.Optimal;
                using Stream from = entry.Open();
                using Stream to = target.CreateEntry(entry.FullName, level).Open();
                from.CopyTo(to);
            }
        }

        return output.ToArray();
    }
}
