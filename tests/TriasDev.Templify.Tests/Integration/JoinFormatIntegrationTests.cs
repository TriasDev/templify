// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using DocumentFormat.OpenXml.Wordprocessing;
using TriasDev.Templify.Core;
using TriasDev.Templify.Tests.Helpers;

namespace TriasDev.Templify.Tests.Integration;

/// <summary>
/// The <c>:join</c> format specifier in Word templates: a collection rendered as one text.
/// </summary>
public sealed class JoinFormatIntegrationTests
{
    [Fact]
    public void Join_InParagraph()
    {
        using TemplateTestRun run = Process(["Rating for IT asset: {{Assets:join:Name}}"], AssetsData());

        Assert.Equal(["Rating for IT asset: A, B"], run.Verifier.GetAllParagraphTexts());
    }

    [Fact]
    public void Join_InsideBlockLoop()
    {
        Dictionary<string, object> data = new()
        {
            ["Items"] = new List<Dictionary<string, object>>
            {
                new() { ["Title"] = "T1", ["Assets"] = Assets("A", "B") },
                new() { ["Title"] = "T2", ["Assets"] = Assets() },
            },
        };

        using TemplateTestRun run = Process(["{{#foreach x in Items}}", "{{x.Title}}: {{x.Assets:join:Name}}", "{{/foreach}}"], data);

        Assert.Equal(["T1: A, B", "T2: "], run.Verifier.GetAllParagraphTexts());
    }

    [Fact]
    public void Join_EmptySeparator()
    {
        using TemplateTestRun run = Process(["{{Codes:join:\"\"}}"], new() { ["Codes"] = new List<string> { "A", "B", "C" } });

        Assert.Equal(["ABC"], run.Verifier.GetAllParagraphTexts());
    }

    [Fact]
    public void Join_JsonData()
    {
        DocumentBuilder builder = new DocumentBuilder().AddParagraph("{{Assets:join:Name}}");

        using TemplateTestRun run = TemplateTestHarness.ProcessJson(builder, "{\"Assets\":[{\"Name\":\"A\"},{\"Name\":\"B\"}]}");

        Assert.Equal(["A, B"], run.Verifier.GetAllParagraphTexts());
    }

    [Fact]
    public void Join_MarkdownInItems()
    {
        using TemplateTestRun run = Process(["{{Names:join}}"], new() { ["Names"] = new List<string> { "**A**", "B" } });

        Assert.Equal(["A, B"], run.Verifier.GetAllParagraphTexts());
        List<Run> runs = run.Verifier.GetRuns(0);
        Assert.Equal("A", runs[0].InnerText);
        Assert.NotNull(runs[0].RunProperties?.Bold);
        Assert.All(runs.Skip(1), r => Assert.Null(r.RunProperties?.Bold));
    }

    [Fact]
    public void Join_KeepsPlaceholderRunFormatting()
    {
        DocumentBuilder builder = new DocumentBuilder().AddParagraph("{{Assets:join:Name}}", new RunProperties(new Bold()));

        using TemplateTestRun run = TemplateTestHarness.Process(builder, AssetsData());

        Assert.Equal(["A, B"], run.Verifier.GetAllParagraphTexts());
        Assert.All(run.Verifier.GetRuns(0), r => Assert.NotNull(r.RunProperties?.Bold));
    }

    [Fact]
    public void Join_PropertyMissingOnAllItems_WarnsOnce()
    {
        using TemplateTestRun run = Process(["{{Assets:join:Nmae}}"], AssetsData());

        Assert.Equal([""], run.Verifier.GetAllParagraphTexts());
        ProcessingWarning warning = Assert.Single(run.Result.Warnings);
        Assert.Equal(ProcessingWarningType.MissingVariable, warning.Type);
        Assert.Equal("Assets.Nmae", warning.VariableName);
        Assert.Contains("Assets.Nmae", run.Result.MissingVariables);
    }

    [Fact]
    public void Join_MissingCollection_UnchangedBehavior()
    {
        using TemplateTestRun run = Process(["{{Nope:join}}"], new());

        Assert.Equal(["{{Nope:join}}"], run.Verifier.GetAllParagraphTexts());
        Assert.Equal("Nope", Assert.Single(run.Result.Warnings).VariableName);
    }

    internal static List<Dictionary<string, object>> Assets(params string[] names) =>
        names.Select(n => new Dictionary<string, object> { ["Name"] = n }).ToList();

    internal static Dictionary<string, object> AssetsData() => new() { ["Assets"] = Assets("A", "B") };

    private static TemplateTestRun Process(string[] paragraphs, Dictionary<string, object> data)
    {
        DocumentBuilder builder = new DocumentBuilder();
        foreach (string paragraph in paragraphs)
        {
            builder.AddParagraph(paragraph);
        }

        return TemplateTestHarness.Process(builder, data);
    }
}
