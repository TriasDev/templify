// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;
using TriasDev.Templify.Core;
using TriasDev.Templify.Tests.Helpers;

namespace TriasDev.Templify.Tests.Integration;

/// <summary>
/// The template lint (#255) for Word templates: rules R1 (unterminated tag), R3 (multiple block markers), R7 (detector
/// errors in every container), R9 (orphan end and branch markers), and locations for structural errors.
/// </summary>
public sealed class TemplateLintTests
{
    [Fact]
    public void R1_SplitPlaceholder()
    {
        ValidationError error = AssertSingleError(Build("Hello {{Na", "me}} world"));

        Assert.Equal(ValidationErrorType.UnterminatedTag, error.Type);
        Assert.Equal("Unterminated tag '{{Na': a placeholder or marker must open and close in the same paragraph.", error.Message);
        Assert.Equal("body, paragraph 1: 'Hello {{Na'", error.Location);
    }

    [Fact]
    public void R1_SplitBlockMarker_AlsoReportsTheOrphanEnd()
    {
        List<string> errors = Describe(Build("{{#if", "Flag}}", "body", "{{/if}}"));

        Assert.Equal(
            [
                "UnmatchedConditionalEnd: Conditional marker '{{/if}}' has no matching '{{#if}}'. @ body, paragraph 4: '{{/if}}'",
                "UnterminatedTag: Unterminated tag '{{#if': a placeholder or marker must open and close in the same paragraph. @ body, paragraph 1: '{{#if'",
            ],
            errors);
    }

    [Fact]
    public void R1_LiteralBraces_NotReported() =>
        AssertValid(Build("{\"a\":{\"b\":1}}", "}}", "{{ not a tag"));

    [Fact]
    public void R3_IfAndForeachInOneParagraph()
    {
        List<string> errors = Describe(Build("{{#if Flag}}{{#foreach Items}}", "{{Name}}", "{{/foreach}}{{/if}}"));

        Assert.Equal(
            [
                "MultipleBlockMarkers: More than one block marker in one paragraph: '{{#if Flag}}', '{{#foreach Items}}'. Put each block marker in a paragraph of its own. @ body, paragraph 1: '{{#if Flag}}{{#foreach Items}}'",
                "MultipleBlockMarkers: More than one block marker in one paragraph: '{{/foreach}}', '{{/if}}'. Put each block marker in a paragraph of its own. @ body, paragraph 3: '{{/foreach}}{{/if}}'",
            ],
            errors);
    }

    [Fact]
    public void R3_NestedInlineConditional_NotReported() =>
        AssertValid(Build("{{#if A}}{{#if B}}x{{/if}}{{/if}}"));

    [Fact]
    public void R7_ContentControlIfWithEndOutside()
    {
        DocumentBuilder builder = new DocumentBuilder()
            .AddElement(new SdtBlock(new SdtProperties(), new SdtContentBlock(P("{{#if Flag}}"), P("x"))))
            .AddParagraph("{{/if}}");
        byte[] template = builder.ToStream().ToArray();

        Assert.Contains(
            "UnmatchedConditionalStart: Conditional start marker '{{#if Flag}}' has no matching '{{/if}}'. @ body, content control 1, paragraph 1: '{{#if Flag}}'",
            Describe(template));
        Assert.False(Process(template).IsSuccess);
    }

    [Fact]
    public void R7_NestedEndMarkersInLoop()
    {
        byte[] template = Build("{{#foreach Items}}", "{{#if Flag}}", "{{Name}}", "{{/if}}{{/foreach}}");

        Assert.Contains(
            "UnmatchedConditionalStart: Conditional start marker '{{#if Flag}}' has no matching '{{/if}}'. @ body, paragraph 2: '{{#if Flag}}'",
            Describe(template));
        Assert.False(Process(template).IsSuccess);
    }

    [Fact]
    public void R7_UnmatchedInTableCell()
    {
        byte[] template = new DocumentBuilder().AddTableWithCellParagraphs(1, 1, (_, _) => ["{{#if A}}", "x"]).ToStream().ToArray();

        // An {{#if}} left open in a cell continues at row level: processing fails with the row-level error, located at the row.
        Assert.Equal(
            ["UnmatchedConditionalStart: Table row conditional start marker '{{#if A}}' has no matching '{{/if}}'. @ body, table 1, row 1: '{{#if A}}x'"],
            Describe(template));
        Assert.EndsWith("Table row conditional start marker '{{#if A}}' has no matching '{{/if}}'.", Process(template).ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void R9_OrphanEndIf() =>
        Assert.Equal(
            ["UnmatchedConditionalEnd: Conditional marker '{{/if}}' has no matching '{{#if}}'. @ body, paragraph 2: '{{/if}}'"],
            Describe(Build("x", "{{/if}}")));

    [Fact]
    public void R9_OrphanElse() =>
        Assert.Equal(
            ["UnmatchedConditionalEnd: Conditional marker '{{#else}}' has no matching '{{#if}}'. @ body, paragraph 1: '{{#else}}'"],
            Describe(Build("{{#else}}", "x")));

    [Fact]
    public void R9_OrphanEndForeach() =>
        Assert.Equal(
            ["UnmatchedLoopEnd: Loop end marker '{{/foreach}}' has no matching '{{#foreach}}'. @ body, paragraph 2: '{{/foreach}}'"],
            Describe(Build("x", "{{/foreach}}")));

    [Fact]
    public void R9_InnerLoopEnd_NotOrphan() =>
        AssertValid(Build("{{#foreach A}}", "{{#foreach B}}", "x", "{{/foreach}}", "{{/foreach}}"));

    [Fact]
    public void R2_Location() =>
        Assert.Equal(
            "body, paragraph 1: 'Assets: {{#foreach Items}}'",
            AssertSingleError(Build("Assets: {{#foreach Items}}", "{{Name}}", "{{/foreach}}")).Location);

    [Fact]
    public void Header_Location()
    {
        byte[] template = new DocumentBuilder().AddParagraph("b").AddHeaderWithParagraphs(HeaderFooterValues.Default, "{{#if A}}", "x").ToStream().ToArray();

        Assert.StartsWith("header (default), paragraph 1", AssertSingleError(template).Location, StringComparison.Ordinal);
    }

    [Fact]
    public void ConditionExpression_Location() =>
        Assert.Equal(
            "body, paragraph 1: '{{#if Count >}}'",
            AssertSingleError(Build("{{#if Count >}}", "x", "{{/if}}")).Location);

    [Fact]
    public void Lossless_WhitespaceAndInvisibleCharactersAroundMarkers() =>
        AssertValid(Build(" {{#if Flag}}​", "x", " {{/if}} "));

    [Fact]
    public void Lossless_MarkersSplitAcrossRuns() =>
        AssertValid(new DocumentBuilder()
            .AddParagraphWithRuns(("{{#fo", null), ("reach Items}}", null))
            .AddParagraph("{{Name}}")
            .AddParagraphWithRuns(("{{/fore", null), ("ach}}", null))
            .ToStream().ToArray());

    [Fact]
    public void Lossless_ExampleTemplates()
    {
        string directory = Path.Combine(FindRepositoryRoot(), "examples", "templates");
        foreach (string path in Directory.GetFiles(directory, "*.docx"))
        {
            AssertValidWithoutData(File.ReadAllBytes(path), Path.GetFileName(path));
        }
    }

    [Fact]
    public void Lossless_EmbeddedWarningReportTemplate()
    {
        using Stream resource = typeof(DocumentTemplateProcessor).Assembly
            .GetManifestResourceStream("TriasDev.Templify.Resources.WarningReportTemplate.docx")!;
        using MemoryStream copy = new MemoryStream();
        resource.CopyTo(copy);

        AssertValidWithoutData(copy.ToArray(), "WarningReportTemplate.docx");
    }

    private static ValidationError AssertSingleError(byte[] template)
    {
        ValidationError? first = null;
        foreach (ValidationResult result in Validate(template))
        {
            ValidationError error = Assert.Single(result.Errors);
            first ??= error;
        }

        return first!;
    }

    private static void AssertValid(byte[] template, string? name = null)
    {
        foreach (ValidationResult result in Validate(template))
        {
            Assert.True(result.IsValid, $"{name}: {string.Join("; ", result.Errors.Select(e => $"{e.Type}: {e.Message} @ {e.Location}"))}");
        }
    }

    /// <summary>Real templates reference their own data, so they are checked without data (structural rules only).</summary>
    private static void AssertValidWithoutData(byte[] template, string name)
    {
        ValidationResult result = Validate(template).First();
        Assert.True(result.IsValid, $"{name}: {string.Join("; ", result.Errors.Select(e => $"{e.Type}: {e.Message} @ {e.Location}"))}");
    }

    /// <summary>Errors without data, as "Type: Message @ Location", sorted.</summary>
    private static List<string> Describe(byte[] template) =>
        Validate(template).First().Errors.Select(e => $"{e.Type}: {e.Message} @ {e.Location}").Order(StringComparer.Ordinal).ToList();

    private static IEnumerable<ValidationResult> Validate(byte[] template)
    {
        DocumentTemplateProcessor processor = TemplateTestHarness.CreateInvariantProcessor();
        yield return processor.ValidateTemplate(new MemoryStream(template));
        yield return processor.ValidateTemplate(new MemoryStream(template), new Dictionary<string, object>
        {
            ["Flag"] = true,
            ["A"] = true,
            ["B"] = true,
            ["Count"] = 1,
            ["Items"] = new List<Dictionary<string, object>> { new() { ["Name"] = "n" } },
        });
    }

    private static ProcessingResult Process(byte[] template) =>
        TemplateTestHarness.CreateInvariantProcessor().ProcessTemplate(
            new MemoryStream(template),
            new MemoryStream(),
            new Dictionary<string, object> { ["Flag"] = true, ["Items"] = new List<string> { "a" } });

    private static byte[] Build(params string[] paragraphs)
    {
        DocumentBuilder builder = new DocumentBuilder();
        foreach (string paragraph in paragraphs)
        {
            builder.AddParagraph(paragraph);
        }

        return builder.ToStream().ToArray();
    }

    private static Paragraph P(string text) => new Paragraph(new Run(new Text(text) { Space = SpaceProcessingModeValues.Preserve }));

    internal static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "templify.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
