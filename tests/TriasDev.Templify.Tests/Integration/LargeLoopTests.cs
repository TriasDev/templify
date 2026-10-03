// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using TriasDev.Templify.Tests.Helpers;

namespace TriasDev.Templify.Tests.Integration;

/// <summary>
/// Guards against accidental super-linear behavior in loop expansion.
/// </summary>
/// <remarks>
/// 20,000 iterations take roughly 0.3-0.5 s on a developer machine. The limit is deliberately generous so that
/// slow or busy CI runners do not cause flaky failures, while an O(n²) regression (tens of seconds or more)
/// still fails. Exclude with <c>--filter "Category!=Performance"</c>.
/// </remarks>
public sealed class LargeLoopTests
{
    private const int ItemCount = 20_000;
    private static readonly TimeSpan _limit = TimeSpan.FromSeconds(20);

    [Fact]
    [Trait("Category", "Performance")]
    public void ProcessTemplate_LoopWith20000Items_CompletesWithinGenerousLimit()
    {
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("Start");
        builder.AddParagraph("{{#foreach Items}}");
        builder.AddParagraph("{{@number}}: {{Name}} {{#if IsEven}}even{{/if}}");
        builder.AddParagraph("{{/foreach}}");
        builder.AddParagraph("End");

        List<Dictionary<string, object>> items = Enumerable.Range(0, ItemCount)
            .Select(i => new Dictionary<string, object> { ["Name"] = "Item" + i, ["IsEven"] = i % 2 == 0 })
            .ToList();
        Dictionary<string, object> data = new Dictionary<string, object> { ["Items"] = items };

        Stopwatch stopwatch = Stopwatch.StartNew();
        using TemplateTestRun run = TemplateTestHarness.Process(builder, data);
        stopwatch.Stop();

        Assert.True(run.Result.IsSuccess, run.Result.ErrorMessage);
        Assert.True(
            stopwatch.Elapsed < _limit,
            $"Processing {ItemCount} loop items took {stopwatch.Elapsed.TotalSeconds:F1} s (limit {_limit.TotalSeconds} s).");

        List<string> paragraphs = run.Verifier.GetAllParagraphTexts();
        Assert.Equal(ItemCount + 2, paragraphs.Count);
        Assert.Equal("1: Item0 even", paragraphs[1]);
        Assert.Equal($"{ItemCount}: Item{ItemCount - 1} ", paragraphs[ItemCount]);
        Assert.Equal("End", paragraphs[^1]);
    }
}
