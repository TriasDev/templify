using BenchmarkDotNet.Attributes;
using TriasDev.Templify.Core;

namespace TriasDev.Templify.Benchmarks;

/// <summary>
/// Benchmarks for complex real-world scenarios.
/// Combines placeholders, loops, and conditionals.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 10)]
public class ComplexScenarioBenchmarks
{
    private MemoryStream _templateSmall = null!;
    private MemoryStream _templateMedium = null!;
    private MemoryStream _templateLarge = null!;

    private Dictionary<string, object> _dataSmall = null!;
    private Dictionary<string, object> _dataMedium = null!;
    private Dictionary<string, object> _dataLarge = null!;

    [GlobalSetup]
    public void Setup()
    {
        // Small: 5 sections
        _templateSmall = BenchmarkDocumentBuilder.CreateComplexDocument(5);
        _dataSmall = CreateComplexData(5, 5);

        // Medium: 10 sections
        _templateMedium = BenchmarkDocumentBuilder.CreateComplexDocument(10);
        _dataMedium = CreateComplexData(10, 10);

        // Large: 20 sections
        _templateLarge = BenchmarkDocumentBuilder.CreateComplexDocument(20);
        _dataLarge = CreateComplexData(20, 15);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _templateSmall?.Dispose();
        _templateMedium?.Dispose();
        _templateLarge?.Dispose();
    }

    [Benchmark(Baseline = true)]
    public void ComplexScenario_Small_5Sections()
    {
        ProcessTemplate(_templateSmall, _dataSmall);
    }

    [Benchmark]
    public void ComplexScenario_Medium_10Sections()
    {
        ProcessTemplate(_templateMedium, _dataMedium);
    }

    [Benchmark]
    public void ComplexScenario_Large_20Sections()
    {
        ProcessTemplate(_templateLarge, _dataLarge);
    }

    private static void ProcessTemplate(MemoryStream template, Dictionary<string, object> data)
    {
        template.Position = 0;
        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        using MemoryStream output = new MemoryStream();
        processor.ProcessTemplate(template, output, data);
    }

    private static Dictionary<string, object> CreateComplexData(int sectionCount, int itemsPerSection)
    {
        Dictionary<string, object> data = new Dictionary<string, object>();

        for (int i = 0; i < sectionCount; i++)
        {
            data[$"SectionNum{i}"] = i + 1;
            data[$"SectionName{i}"] = $"Section {i + 1}";
            data[$"ShowDetails{i}"] = i % 2 == 0; // Alternate true/false

            // Create items for each section
            data[$"Items{i}"] = Enumerable.Range(1, itemsPerSection).Select(j => new Dictionary<string, object>
            {
                ["Name"] = $"Item {j}",
                ["Value"] = j * 10,
                ["IsActive"] = j % 3 == 0 // Every third item is active
            }).ToList();
        }

        return data;
    }
}
