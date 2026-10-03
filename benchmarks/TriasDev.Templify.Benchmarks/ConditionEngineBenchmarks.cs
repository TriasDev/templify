// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using TriasDev.Templify.Conditionals;
using TriasDev.Templify.Core;

namespace TriasDev.Templify.Benchmarks;

/// <summary>
/// Benchmarks for the condition engine using the public API:
/// standalone evaluation (<see cref="ConditionEvaluator"/> / <see cref="IConditionContext"/>) of the
/// <c>in</c>, <c>contains</c>, <c>exists</c>, <c>is empty</c> operators and grouping, plus document
/// processing of <c>elseif</c> chains.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 10)]
public class ConditionEngineBenchmarks
{
    private const string ComparisonExpression = "Count > 10 and Status = \"Active\"";
    private const string InLiteralExpression = "Role in (\"Admin\", \"Editor\", \"Viewer\")";
    private const string InCollectionExpression = "Country in SupportedCountries";
    private const string ContainsExpression = "Description contains \"urgent\"";
    private const string ExistenceExpression = "Notes exists and Notes is empty and Tags is not empty";
    private const string GroupingExpression = "(Role in (\"Admin\", \"Editor\") or IsOwner) and not (IsSuspended or Count < 0)";

    private readonly ConditionEvaluator _evaluator = new();
    private Dictionary<string, object> _data = null!;
    private IConditionContext _context = null!;

    private MemoryStream _elseIfTemplate10 = null!;
    private MemoryStream _elseIfTemplate50 = null!;
    private Dictionary<string, object> _elseIfData10 = null!;
    private Dictionary<string, object> _elseIfData50 = null!;

    [GlobalSetup]
    public void Setup()
    {
        _data = new Dictionary<string, object>
        {
            ["Count"] = 42,
            ["Status"] = "Active",
            ["Role"] = "Editor",
            ["Country"] = "Germany",
            ["SupportedCountries"] = new List<string> { "Austria", "France", "Germany", "Italy", "Spain", "Switzerland" },
            ["Description"] = "Customer reported an urgent issue with the invoice",
            ["Notes"] = "",
            ["Tags"] = new List<string> { "billing", "priority" },
            ["IsOwner"] = false,
            ["IsSuspended"] = false,
        };
        _context = _evaluator.CreateConditionContext(_data);

        // Verify every expression takes the intended path, otherwise a parse failure could look fast.
        foreach (string expression in new[]
                 {
                     ComparisonExpression, InLiteralExpression, InCollectionExpression,
                     ContainsExpression, ExistenceExpression, GroupingExpression
                 })
        {
            BenchmarkGuard.EnsureEquals(true, _evaluator.Evaluate(expression, _data), expression);
        }

        _elseIfTemplate10 = CreateDocumentWithElseIfChains(10);
        _elseIfTemplate50 = CreateDocumentWithElseIfChains(50);
        _elseIfData10 = CreateElseIfData(10);
        _elseIfData50 = CreateElseIfData(50);

        VerifyElseIfOutput(_elseIfTemplate10, _elseIfData10, 10);
        VerifyElseIfOutput(_elseIfTemplate50, _elseIfData50, 50);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _elseIfTemplate10?.Dispose();
        _elseIfTemplate50?.Dispose();
    }

    [Benchmark(Baseline = true)]
    public bool Evaluate_Comparison() => _context.Evaluate(ComparisonExpression);

    [Benchmark]
    public bool Evaluate_InLiteralList() => _context.Evaluate(InLiteralExpression);

    [Benchmark]
    public bool Evaluate_InCollection() => _context.Evaluate(InCollectionExpression);

    [Benchmark]
    public bool Evaluate_Contains() => _context.Evaluate(ContainsExpression);

    [Benchmark]
    public bool Evaluate_ExistsAndIsEmpty() => _context.Evaluate(ExistenceExpression);

    [Benchmark]
    public bool Evaluate_Grouping() => _context.Evaluate(GroupingExpression);

    [Benchmark]
    public bool Evaluate_Grouping_NewContextPerCall() => _evaluator.Evaluate(GroupingExpression, _data);

    [Benchmark]
    public void Process_ElseIfChains_10() => ProcessTemplate(_elseIfTemplate10, _elseIfData10);

    [Benchmark]
    public void Process_ElseIfChains_50() => ProcessTemplate(_elseIfTemplate50, _elseIfData50);

    private static ProcessingResult ProcessTemplate(MemoryStream template, Dictionary<string, object> data, MemoryStream? output = null)
    {
        template.Position = 0;
        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        MemoryStream target = output ?? new MemoryStream();
        try
        {
            return BenchmarkGuard.EnsureSuccess(processor.ProcessTemplate(template, target, data));
        }
        finally
        {
            if (output == null)
            {
                target.Dispose();
            }
        }
    }

    /// <summary>
    /// Each chain: if Score{i} >= 90 / elseif >= 75 / elseif >= 50 / elseif Grade{i} in ("D", "E") / else.
    /// The data makes every chain resolve to the third branch, so each evaluation walks several conditions.
    /// </summary>
    private static MemoryStream CreateDocumentWithElseIfChains(int chainCount)
    {
        MemoryStream stream = new MemoryStream();

        using (WordprocessingDocument document = WordprocessingDocument.Create(stream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document, true))
        {
            MainDocumentPart mainPart = document.AddMainDocumentPart();
            mainPart.Document = new Document();
            Body body = mainPart.Document.AppendChild(new Body());

            for (int i = 0; i < chainCount; i++)
            {
                AddParagraph(body, $"{{{{#if Score{i} >= 90}}}}");
                AddParagraph(body, $"Chain {i}: A");
                AddParagraph(body, $"{{{{#elseif Score{i} >= 75}}}}");
                AddParagraph(body, $"Chain {i}: B");
                AddParagraph(body, $"{{{{#elseif Score{i} >= 50}}}}");
                AddParagraph(body, $"Chain {i}: C");
                AddParagraph(body, $"{{{{#elseif Grade{i} in (\"D\", \"E\")}}}}");
                AddParagraph(body, $"Chain {i}: D/E");
                AddParagraph(body, "{{#else}}");
                AddParagraph(body, $"Chain {i}: F");
                AddParagraph(body, "{{/if}}");
            }

            mainPart.Document.Save();
        }

        stream.Position = 0;
        return stream;
    }

    private static Dictionary<string, object> CreateElseIfData(int chainCount)
    {
        Dictionary<string, object> data = new Dictionary<string, object>();
        for (int i = 0; i < chainCount; i++)
        {
            data[$"Score{i}"] = 60;
            data[$"Grade{i}"] = "C";
        }

        return data;
    }

    private static void VerifyElseIfOutput(MemoryStream template, Dictionary<string, object> data, int chainCount)
    {
        using MemoryStream output = new MemoryStream();
        ProcessTemplate(template, data, output);
        output.Position = 0;

        using WordprocessingDocument document = WordprocessingDocument.Open(output, false);
        string text = document.MainDocumentPart!.Document!.Body!.InnerText;
        for (int i = 0; i < chainCount; i++)
        {
            if (!text.Contains($"Chain {i}: C", StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"elseif chain {i} did not resolve to the expected branch.");
            }
        }

        if (text.Contains("{{", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("elseif benchmark output still contains template markers.");
        }
    }

    private static void AddParagraph(Body body, string text)
    {
        Paragraph paragraph = body.AppendChild(new Paragraph());
        Run run = paragraph.AppendChild(new Run());
        run.AppendChild(new Text(text));
    }
}
