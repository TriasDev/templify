// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using TriasDev.Templify.Core;
using TriasDev.Templify.Tests.Helpers;
using TriasDev.Templify.Tests.Integration;

namespace TriasDev.Templify.Tests.Odt;

/// <summary>
/// The template lint (#255) for OpenDocument templates; see <see cref="TemplateLintTests"/> (body shapes are also
/// compared verbatim with Word in <see cref="OdtValidationParityTests"/>).
/// </summary>
public sealed class OdtTemplateLintTests
{
    [Fact]
    public void R1_SplitPlaceholder() =>
        Assert.Equal(
            ["UnterminatedTag: Unterminated tag '{{Na': a placeholder or marker must open and close in the same paragraph. @ body, paragraph 1: 'Hello {{Na'"],
            Describe(Build("Hello {{Na", "me}} world")));

    [Fact]
    public void R3_IfAndForeachInOneParagraph() =>
        Assert.Equal(2, Describe(Build("{{#if Flag}}{{#foreach Items}}", "{{Name}}", "{{/foreach}}{{/if}}"))
            .Count(e => e.StartsWith("MultipleBlockMarkers: ", StringComparison.Ordinal)));

    [Fact]
    public void R9_OrphanEndIf() =>
        Assert.Equal(
            ["UnmatchedConditionalEnd: Conditional marker '{{/if}}' has no matching '{{#if}}'. @ body, paragraph 2: '{{/if}}'"],
            Describe(Build("x", "{{/if}}")));

    [Fact]
    public void R9_InnerLoopEnd_NotOrphan() =>
        Assert.Empty(Describe(Build("{{#foreach A}}", "{{#foreach B}}", "x", "{{/foreach}}", "{{/foreach}}")));

    [Fact]
    public void R7_UnmatchedInTableCell_IsLocatedAtTheRow() =>
        Assert.Equal(
            ["UnmatchedConditionalStart: Table row conditional start marker '{{#if A}}' has no matching '{{/if}}'. @ body, table 1, row 1: '{{#if A}}'"],
            Describe(new OdtDocumentBuilder().AddTable(["{{#if A}}"], ["x"]).ToBytes()));

    [Fact]
    public void R7_ListItemMarkerWithEndOutside_IsLocated()
    {
        byte[] template = new OdtDocumentBuilder()
            .AddXml("<text:list><text:list-item><text:p>{{#if A}}</text:p></text:list-item><text:list-item><text:p>x</text:p></text:list-item></text:list>")
            .ToBytes();

        Assert.Contains(Describe(template), e => e.StartsWith("UnmatchedConditionalStart: ", StringComparison.Ordinal) && e.Contains("@ body, list 1, item 1: '{{#if A}}'", StringComparison.Ordinal));
    }

    [Fact]
    public void SameRowLoop_ExactlyOneError() =>
        Assert.Equal(ValidationErrorType.UnmatchedLoopStart,
            Assert.Single(Validate(new OdtDocumentBuilder().AddTable(["{{#foreach Items}}", "{{Name}}", "{{/foreach}}"], ["x", "x", "x"]).ToBytes()).Errors).Type);

    [Fact]
    public void SplitCellConditional_ExactlyOneError() =>
        Assert.StartsWith("Invalid table row conditional",
            Assert.Single(Validate(new OdtDocumentBuilder().AddTable(["{{#if A}}", "{{/if}}"], ["x", "x"]).ToBytes()).Errors).Message,
            StringComparison.Ordinal);

    [Fact]
    public void UnmatchedInBodyAndHeader_TwoErrors()
    {
        List<string> errors = Describe(new OdtDocumentBuilder().AddParagraph("{{#if A}}").AddParagraph("x")
            .AddHeaderParagraph("{{#if A}}").AddHeaderParagraph("y").ToBytes());

        Assert.Equal(2, errors.Count);
        Assert.Contains(errors, e => e.EndsWith("@ body, paragraph 1: '{{#if A}}'", StringComparison.Ordinal));
        Assert.Contains(errors, e => e.EndsWith("@ header (Standard), paragraph 1: '{{#if A}}'", StringComparison.Ordinal));
    }

    [Fact]
    public void Header_Location() =>
        Assert.StartsWith(
            "header (Standard), paragraph 1",
            Assert.Single(Validate(new OdtDocumentBuilder().AddParagraph("b").AddHeaderParagraph("{{#if A}}").AddHeaderParagraph("x").ToBytes()).Errors).Location,
            StringComparison.Ordinal);

    [Fact]
    public void InlineExpression_Location() =>
        Assert.Equal(
            "body, paragraph 1: '{{(A and)}}'",
            Assert.Single(Validate(Build("{{(A and)}}")).Errors).Location);

    [Fact]
    public void Lossless_WhitespaceAndInvisibleCharactersAroundMarkers() =>
        Assert.Empty(Describe(Build(" {{#if Flag}}​", "x", " {{/if}} ")));

    [Fact]
    public void Lossless_NestedInlineConditional() =>
        Assert.Empty(Describe(Build("{{#if A}}{{#if B}}x{{/if}}{{/if}}")));

    [Fact]
    public void Lossless_ExampleTemplates()
    {
        string directory = Path.Combine(TemplateLintTests.FindRepositoryRoot(), "examples", "templates");
        foreach (string path in Directory.GetFiles(directory, "*.odt"))
        {
            ValidationResult result = Validate(File.ReadAllBytes(path));
            Assert.True(result.IsValid, $"{Path.GetFileName(path)}: {string.Join("; ", result.Errors.Select(e => e.Message))}");
        }
    }

    private static List<string> Describe(byte[] template) =>
        Validate(template).Errors.Select(e => $"{e.Type}: {e.Message} @ {e.Location}").Order(StringComparer.Ordinal).ToList();

    private static ValidationResult Validate(byte[] template) =>
        new OdtTemplateProcessor(OdtTestHelper.InvariantOptions()).ValidateTemplate(new MemoryStream(template));

    private static byte[] Build(params string[] paragraphs)
    {
        OdtDocumentBuilder builder = new OdtDocumentBuilder();
        foreach (string paragraph in paragraphs)
        {
            builder.AddParagraph(paragraph);
        }

        return builder.ToBytes();
    }
}
