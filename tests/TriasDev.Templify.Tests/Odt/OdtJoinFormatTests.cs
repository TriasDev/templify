// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using TriasDev.Templify.Core;
using TriasDev.Templify.Tests.Helpers;
using TriasDev.Templify.Tests.Integration;

namespace TriasDev.Templify.Tests.Odt;

/// <summary>The <c>:join</c> format specifier in OpenDocument templates (see <see cref="JoinFormatIntegrationTests"/>).</summary>
public sealed class OdtJoinFormatTests
{
    [Fact]
    public void Join_InParagraph()
    {
        (_, OdtDocumentVerifier output) = OdtTestHelper.Process(
            new OdtDocumentBuilder().AddParagraph("Rating for IT asset: {{Assets:join:Name}}"),
            JoinFormatIntegrationTests.AssetsData());

        Assert.Equal(["Rating for IT asset: A, B"], output.GetParagraphTexts());
    }

    [Fact]
    public void Join_InsideBlockLoop()
    {
        Dictionary<string, object> data = new()
        {
            ["Items"] = new List<Dictionary<string, object>>
            {
                new() { ["Title"] = "T1", ["Assets"] = JoinFormatIntegrationTests.Assets("A", "B") },
                new() { ["Title"] = "T2", ["Assets"] = JoinFormatIntegrationTests.Assets() },
            },
        };
        OdtDocumentBuilder template = new OdtDocumentBuilder()
            .AddParagraph("{{#foreach x in Items}}")
            .AddParagraph("{{x.Title}}: {{x.Assets:join:Name}}")
            .AddParagraph("{{/foreach}}");

        (_, OdtDocumentVerifier output) = OdtTestHelper.Process(template, data);

        Assert.Equal(["T1: A, B", "T2: "], output.GetParagraphTexts());
    }

    [Theory]
    [InlineData("{{Codes:join}}", "file_a, file_b")]
    [InlineData("{{Codes:join:\" * \"}}", "file_a * file_b")]
    public void Join_MarkdownPerItem_SeparatorLiteral(string template, string expected)
    {
        (_, OdtDocumentVerifier output) = OdtTestHelper.Process(
            new OdtDocumentBuilder().AddParagraph(template),
            new Dictionary<string, object> { ["Codes"] = new List<string> { "file_a", "file_b" } });

        Assert.Equal([expected], output.GetParagraphTexts());
    }

    [Fact]
    public void Join_MarkdownInItems_IsApplied()
    {
        (_, OdtDocumentVerifier output) = OdtTestHelper.Process(
            new OdtDocumentBuilder().AddParagraph("{{Names:join}}"),
            new Dictionary<string, object> { ["Names"] = new List<string> { "**A**", "B" } });

        Assert.Equal(["A, B"], output.GetParagraphTexts());
        Assert.Contains("font-weight", output.GetEntryString("content.xml"), StringComparison.Ordinal);
    }

    [Fact]
    public void Join_ItemEmptyAfterCleaning_IsSkipped()
    {
        (_, OdtDocumentVerifier output) = OdtTestHelper.Process(
            new OdtDocumentBuilder().AddParagraph("{{T:join}}"),
            new Dictionary<string, object> { ["T"] = new List<string> { "a", "\u0002", "b" } });

        Assert.Equal(["a, b"], output.GetParagraphTexts());
    }

    [Fact]
    public void Join_PropertyMissingOnAllItems_WarnsOnce()
    {
        (ProcessingResult result, OdtDocumentVerifier output) = OdtTestHelper.Process(
            new OdtDocumentBuilder().AddParagraph("{{Assets:join:Nmae}}"),
            JoinFormatIntegrationTests.AssetsData());

        Assert.Equal([""], output.GetParagraphTexts());
        ProcessingWarning warning = Assert.Single(result.Warnings);
        Assert.Equal(ProcessingWarningType.MissingVariable, warning.Type);
        Assert.Equal("Assets.Nmae", warning.VariableName);
        Assert.Contains("Assets.Nmae", result.MissingVariables);
    }
}
