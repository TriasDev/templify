using BenchmarkDotNet.Attributes;
using TriasDev.Templify.Core;

namespace TriasDev.Templify.Benchmarks;

/// <summary>
/// Benchmarks for simple placeholder replacement.
/// Tests performance with varying numbers of placeholders.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 10)]
public class PlaceholderBenchmarks
{
    private MemoryStream _template10 = null!;
    private MemoryStream _template50 = null!;
    private MemoryStream _template100 = null!;
    private MemoryStream _template500 = null!;

    private Dictionary<string, object> _data10 = null!;
    private Dictionary<string, object> _data50 = null!;
    private Dictionary<string, object> _data100 = null!;
    private Dictionary<string, object> _data500 = null!;

    [GlobalSetup]
    public void Setup()
    {
        // Create templates with varying placeholder counts
        _template10 = BenchmarkDocumentBuilder.CreateDocumentWithPlaceholders(10);
        _template50 = BenchmarkDocumentBuilder.CreateDocumentWithPlaceholders(50);
        _template100 = BenchmarkDocumentBuilder.CreateDocumentWithPlaceholders(100);
        _template500 = BenchmarkDocumentBuilder.CreateDocumentWithPlaceholders(500);

        // Create corresponding data
        _data10 = CreateData(10);
        _data50 = CreateData(50);
        _data100 = CreateData(100);
        _data500 = CreateData(500);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _template10?.Dispose();
        _template50?.Dispose();
        _template100?.Dispose();
        _template500?.Dispose();
    }

    [Benchmark(Baseline = true)]
    public void PlaceholderReplacement_10_Placeholders()
    {
        ProcessTemplate(_template10, _data10);
    }

    [Benchmark]
    public void PlaceholderReplacement_50_Placeholders()
    {
        ProcessTemplate(_template50, _data50);
    }

    [Benchmark]
    public void PlaceholderReplacement_100_Placeholders()
    {
        ProcessTemplate(_template100, _data100);
    }

    [Benchmark]
    public void PlaceholderReplacement_500_Placeholders()
    {
        ProcessTemplate(_template500, _data500);
    }

    private static void ProcessTemplate(MemoryStream template, Dictionary<string, object> data)
    {
        template.Position = 0;
        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        using MemoryStream output = new MemoryStream();
        processor.ProcessTemplate(template, output, data);
    }

    private static Dictionary<string, object> CreateData(int count)
    {
        Dictionary<string, object> data = new Dictionary<string, object>();
        for (int i = 0; i < count; i++)
        {
            data[$"Var{i}"] = $"Value{i}";
        }
        return data;
    }
}
