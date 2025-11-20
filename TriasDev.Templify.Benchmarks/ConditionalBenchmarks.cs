// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using TriasDev.Templify.Core;

namespace TriasDev.Templify.Benchmarks;

/// <summary>
/// Benchmarks for conditional processing.
/// Tests performance with varying numbers of conditionals.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 10)]
public class ConditionalBenchmarks
{
    private MemoryStream _template10 = null!;
    private MemoryStream _template50 = null!;
    private MemoryStream _template100 = null!;

    private Dictionary<string, object> _data10True = null!;
    private Dictionary<string, object> _data50True = null!;
    private Dictionary<string, object> _data100True = null!;

    private Dictionary<string, object> _data10False = null!;
    private Dictionary<string, object> _data50False = null!;
    private Dictionary<string, object> _data100False = null!;

    [GlobalSetup]
    public void Setup()
    {
        // Create templates
        _template10 = BenchmarkDocumentBuilder.CreateDocumentWithConditionals(10);
        _template50 = BenchmarkDocumentBuilder.CreateDocumentWithConditionals(50);
        _template100 = BenchmarkDocumentBuilder.CreateDocumentWithConditionals(100);

        // Create data with all flags true
        _data10True = CreateData(10, true);
        _data50True = CreateData(50, true);
        _data100True = CreateData(100, true);

        // Create data with all flags false
        _data10False = CreateData(10, false);
        _data50False = CreateData(50, false);
        _data100False = CreateData(100, false);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _template10?.Dispose();
        _template50?.Dispose();
        _template100?.Dispose();
    }

    [Benchmark(Baseline = true)]
    public void Conditional_10_AllTrue()
    {
        ProcessTemplate(_template10, _data10True);
    }

    [Benchmark]
    public void Conditional_10_AllFalse()
    {
        ProcessTemplate(_template10, _data10False);
    }

    [Benchmark]
    public void Conditional_50_AllTrue()
    {
        ProcessTemplate(_template50, _data50True);
    }

    [Benchmark]
    public void Conditional_50_AllFalse()
    {
        ProcessTemplate(_template50, _data50False);
    }

    [Benchmark]
    public void Conditional_100_AllTrue()
    {
        ProcessTemplate(_template100, _data100True);
    }

    [Benchmark]
    public void Conditional_100_AllFalse()
    {
        ProcessTemplate(_template100, _data100False);
    }

    private static void ProcessTemplate(MemoryStream template, Dictionary<string, object> data)
    {
        template.Position = 0;
        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        using MemoryStream output = new MemoryStream();
        processor.ProcessTemplate(template, output, data);
    }

    private static Dictionary<string, object> CreateData(int count, bool flagValue)
    {
        Dictionary<string, object> data = new Dictionary<string, object>();
        for (int i = 0; i < count; i++)
        {
            data[$"Flag{i}"] = flagValue;
        }
        return data;
    }
}
