// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using TriasDev.Templify.Core;

namespace TriasDev.Templify.Benchmarks;

/// <summary>
/// Benchmarks for loop processing.
/// Tests performance with varying collection sizes and nesting levels.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 10)]
public class LoopBenchmarks
{
    private MemoryStream _templateSmallLoop = null!;
    private MemoryStream _templateMediumLoop = null!;
    private MemoryStream _templateLargeLoop = null!;
    private MemoryStream _templateNestedLoop = null!;

    private Dictionary<string, object> _dataSmallLoop = null!;
    private Dictionary<string, object> _dataMediumLoop = null!;
    private Dictionary<string, object> _dataLargeLoop = null!;
    private Dictionary<string, object> _dataNestedLoop = null!;

    [GlobalSetup]
    public void Setup()
    {
        // Small loop: 1 loop with 10 items
        _templateSmallLoop = BenchmarkDocumentBuilder.CreateDocumentWithLoops(1, 10);
        _dataSmallLoop = new Dictionary<string, object>
        {
            ["Collection0"] = Enumerable.Range(1, 10).Select(i => $"Item{i}").ToList()
        };

        // Medium loop: 5 loops with 20 items each
        _templateMediumLoop = BenchmarkDocumentBuilder.CreateDocumentWithLoops(5, 20);
        _dataMediumLoop = new Dictionary<string, object>();
        for (int i = 0; i < 5; i++)
        {
            _dataMediumLoop[$"Collection{i}"] = Enumerable.Range(1, 20).Select(j => $"Item{j}").ToList();
        }

        // Large loop: 1 loop with 100 items
        _templateLargeLoop = BenchmarkDocumentBuilder.CreateDocumentWithLoops(1, 100);
        _dataLargeLoop = new Dictionary<string, object>
        {
            ["Collection0"] = Enumerable.Range(1, 100).Select(i => $"Item{i}").ToList()
        };

        // Nested loops: 10 outer items, 5 inner items each
        _templateNestedLoop = BenchmarkDocumentBuilder.CreateDocumentWithNestedLoops(10, 5);
        _dataNestedLoop = new Dictionary<string, object>
        {
            ["OuterCollection"] = Enumerable.Range(1, 10).Select(i => new Dictionary<string, object>
            {
                ["Name"] = $"Outer{i}",
                ["InnerCollection"] = Enumerable.Range(1, 5).Select(j => new Dictionary<string, object>
                {
                    ["Value"] = $"Inner{j}"
                }).ToList()
            }).ToList()
        };
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _templateSmallLoop?.Dispose();
        _templateMediumLoop?.Dispose();
        _templateLargeLoop?.Dispose();
        _templateNestedLoop?.Dispose();
    }

    [Benchmark(Baseline = true)]
    public void Loop_Small_10Items()
    {
        ProcessTemplate(_templateSmallLoop, _dataSmallLoop);
    }

    [Benchmark]
    public void Loop_Medium_5Loops_20ItemsEach()
    {
        ProcessTemplate(_templateMediumLoop, _dataMediumLoop);
    }

    [Benchmark]
    public void Loop_Large_100Items()
    {
        ProcessTemplate(_templateLargeLoop, _dataLargeLoop);
    }

    [Benchmark]
    public void Loop_Nested_10x5()
    {
        ProcessTemplate(_templateNestedLoop, _dataNestedLoop);
    }

    private static void ProcessTemplate(MemoryStream template, Dictionary<string, object> data)
    {
        template.Position = 0;
        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        using MemoryStream output = new MemoryStream();
        processor.ProcessTemplate(template, output, data);
    }
}
