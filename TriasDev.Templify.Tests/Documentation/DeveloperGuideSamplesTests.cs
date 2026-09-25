// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text.Json;
using TriasDev.Templify.Conditionals;
using TriasDev.Templify.Core;
using TriasDev.Templify.Formatting;
using TriasDev.Templify.Tests.Helpers;
using TriasDev.Templify.Utilities;

namespace TriasDev.Templify.Tests.Documentation;

/// <summary>
/// Mirrors the samples of docs/for-developers/quick-start.md, condition-evaluation.md and
/// processing-warnings.md, so that the documented API calls keep compiling and the documented results hold.
/// </summary>
/// <remarks>Keep these tests in sync with the documentation when either changes.</remarks>
public sealed class DeveloperGuideSamplesTests
{
    private static readonly CultureInfo _invariant = CultureInfo.InvariantCulture;

    private static MemoryStream CreateTemplate(params string[] paragraphs)
    {
        DocumentBuilder builder = new DocumentBuilder();
        foreach (string paragraph in paragraphs)
        {
            builder.AddParagraph(paragraph);
        }

        return builder.ToStream();
    }

    private static List<string> ReadParagraphs(byte[] document)
    {
        using DocumentVerifier verifier = new DocumentVerifier(new MemoryStream(document));
        return verifier.GetAllParagraphTexts();
    }

    [Fact]
    public void QuickStart_FirstTemplate_ReplacesPlaceholders()
    {
        var data = new Dictionary<string, object>
        {
            ["Name"] = "John Doe",
            ["InvoiceNumber"] = "INV-2025-001",
            ["Date"] = "2025-01-15",
            ["Amount"] = 1250.50m
        };

        var processor = new DocumentTemplateProcessor(new PlaceholderReplacementOptions { Culture = _invariant });
        using MemoryStream templateStream = CreateTemplate(
            "Hello {{Name}}!",
            "This is your invoice #{{InvoiceNumber}} dated {{Date}}.",
            "Total Amount: {{Amount}} EUR");
        using MemoryStream outputStream = new MemoryStream();

        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        Assert.True(result.IsSuccess);
        Assert.Null(result.ErrorMessage);
        Assert.Equal(4, result.ReplacementCount);
        Assert.Equal(
            new[] { "Hello John Doe!", "This is your invoice #INV-2025-001 dated 2025-01-15.", "Total Amount: 1250.50 EUR" },
            ReadParagraphs(outputStream.ToArray()));
    }

    [Fact]
    public void QuickStart_PlaceholderWithSpaces_IsNotReplaced()
    {
        var processor = new DocumentTemplateProcessor();
        byte[] template = CreateTemplate("{{ Name }}").ToArray();

        ProcessingResult result = processor.ProcessTemplate(
            template, new Dictionary<string, object> { ["Name"] = "John" }, out byte[] output);

        Assert.True(result.IsSuccess);
        Assert.Equal(new[] { "{{ Name }}" }, ReadParagraphs(output));
    }

    [Fact]
    public void QuickStart_OtherInputAndOutputShapes_Work()
    {
        var processor = new DocumentTemplateProcessor(new PlaceholderReplacementOptions { Culture = _invariant });
        var data = new Dictionary<string, object> { ["CustomerName"] = "Jane" };
        byte[] template = CreateTemplate("Hi {{CustomerName}}").ToArray();

        // Bytes to bytes
        ProcessingResult bytesResult = processor.ProcessTemplate(template, data, out byte[] output);
        Assert.True(bytesResult.IsSuccess);
        Assert.Equal(new[] { "Hi Jane" }, ReadParagraphs(output));

        // Read-only data with a null value
        IReadOnlyDictionary<string, object?> readOnlyData = new Dictionary<string, object?> { ["CustomerName"] = null };
        using MemoryStream templateStream = new MemoryStream(template);
        using MemoryStream outputStream = new MemoryStream();
        ProcessingResult readOnlyResult = processor.ProcessTemplate(templateStream, outputStream, readOnlyData);
        Assert.True(readOnlyResult.IsSuccess);
        Assert.Empty(readOnlyResult.MissingVariables);
        Assert.Equal(new[] { "Hi " }, ReadParagraphs(outputStream.ToArray()));

        // File to file; the output file is written only on success
        string directory = Path.Combine(Path.GetTempPath(), "templify-docs-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string templatePath = Path.Combine(directory, "template.docx");
            string outputPath = Path.Combine(directory, "output.docx");
            File.WriteAllBytes(templatePath, template);

            ProcessingResult fileResult = processor.ProcessTemplateFile(templatePath, outputPath, data);

            Assert.True(fileResult.IsSuccess);
            Assert.Equal(new[] { "Hi Jane" }, ReadParagraphs(File.ReadAllBytes(outputPath)));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void QuickStart_WriteOnlyOutputStream_IsAFailedResult()
    {
        var processor = new DocumentTemplateProcessor();
        using MemoryStream templateStream = CreateTemplate("x");
        using WriteOnlyStream outputStream = new WriteOnlyStream();

        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, new Dictionary<string, object>());

        Assert.False(result.IsSuccess);
        Assert.Contains("readable, writable and seekable", result.ErrorMessage);
    }

    [Fact]
    public void QuickStart_Json_OverloadAndParser_ResolveNestedData()
    {
        string json = """{ "Customer": { "Name": "Bob" }, "Items": [ { "Name": "A" }, { "Name": "B" } ], "Flag": false }""";
        var processor = new DocumentTemplateProcessor(new PlaceholderReplacementOptions { Culture = _invariant });
        byte[] template = CreateTemplate(
            "{{Customer.Name}}",
            "{{#foreach Items}}",
            "{{Name}}",
            "{{/foreach}}",
            "{{#if Flag}}",
            "flag",
            "{{/if}}").ToArray();

        ProcessingResult jsonResult = processor.ProcessTemplate(template, json, out byte[] jsonOutput);
        Assert.True(jsonResult.IsSuccess);
        Assert.Equal(new[] { "Bob", "A", "B" }, ReadParagraphs(jsonOutput));

        Dictionary<string, object> data = JsonDataParser.ParseJsonToDataDictionary(json);
        ProcessingResult dictionaryResult = processor.ProcessTemplate(template, data, out byte[] dictionaryOutput);
        Assert.True(dictionaryResult.IsSuccess);
        Assert.Equal(new[] { "Bob", "A", "B" }, ReadParagraphs(dictionaryOutput));
    }

    [Fact]
    public void QuickStart_JsonSerializerDictionary_HasDocumentedPitfalls()
    {
        Dictionary<string, object> data = JsonSerializer.Deserialize<Dictionary<string, object>>(
            """{ "Customer": { "Name": "Bob" }, "Items": [ { "Name": "A" } ], "Flag": false }""")!;
        var processor = new DocumentTemplateProcessor(new PlaceholderReplacementOptions { Culture = _invariant });

        // Nested paths through JsonElement values resolve ...
        ProcessingResult nested = processor.ProcessTemplate(
            CreateTemplate("{{Customer.Name}}", "{{#if Flag}}", "flag", "{{/if}}").ToArray(), data, out byte[] output);
        Assert.True(nested.IsSuccess);

        // ... but a JSON false is truthy ...
        Assert.Equal(new[] { "Bob", "flag" }, ReadParagraphs(output));

        // ... and a top-level JSON array is not a collection for {{#foreach}}.
        ProcessingResult loop = processor.ProcessTemplate(
            CreateTemplate("{{#foreach Items}}", "{{Name}}", "{{/foreach}}").ToArray(), data, out _);
        Assert.False(loop.IsSuccess);
        Assert.Contains("is not a collection", loop.ErrorMessage);
    }

    [Fact]
    public void QuickStart_InvalidJson_Throws()
    {
        var processor = new DocumentTemplateProcessor();

        Assert.ThrowsAny<JsonException>(() => processor.ProcessTemplate(CreateTemplate("x").ToArray(), "not json", out _));
    }

    [Fact]
    public void QuickStart_ErrorHandling_SyntaxErrorIsFailureAndMalformedConditionIsWarning()
    {
        var processor = new DocumentTemplateProcessor();

        ProcessingResult unmatched = processor.ProcessTemplate(
            CreateTemplate("{{#if IsActive}}", "text").ToArray(), new Dictionary<string, object>(), out _);
        Assert.False(unmatched.IsSuccess);
        Assert.StartsWith("Processing failed:", unmatched.ErrorMessage);

        ProcessingResult malformed = processor.ProcessTemplate(
            CreateTemplate("{{#if Count > 2 && IsActive}}", "text", "{{/if}}").ToArray(),
            new Dictionary<string, object> { ["Count"] = 3, ["IsActive"] = true },
            out byte[] output);
        Assert.True(malformed.IsSuccess);
        Assert.DoesNotContain("text", ReadParagraphs(output));
        ProcessingWarning warning = Assert.Single(malformed.Warnings);
        Assert.Equal(ProcessingWarningType.ExpressionFailed, warning.Type);
        Assert.Equal("conditional", warning.Context);
    }

    [Fact]
    public void QuickStart_ThrowException_ThrowsInvalidOperationException()
    {
        var processor = new DocumentTemplateProcessor(new PlaceholderReplacementOptions
        {
            MissingVariableBehavior = MissingVariableBehavior.ThrowException
        });

        Assert.Throws<InvalidOperationException>(() =>
            processor.ProcessTemplate(CreateTemplate("{{Missing}}").ToArray(), new Dictionary<string, object>(), out _));
    }

    [Fact]
    public void QuickStart_ValidateTemplate_ReportsPlaceholdersAndMissingVariables()
    {
        var processor = new DocumentTemplateProcessor();
        using MemoryStream template = CreateTemplate("{{Name}} {{Missing}}", "{{#foreach Items}}", "{{/foreach}}");

        ValidationResult validation = processor.ValidateTemplate(
            template, new Dictionary<string, object> { ["Name"] = "a", ["Items"] = new List<object>() });

        // Missing variables are errors of type MissingVariable, so the template is not valid for this data
        Assert.False(validation.IsValid);
        ValidationError error = Assert.Single(validation.Errors);
        Assert.Equal(ValidationErrorType.MissingVariable, error.Type);
        Assert.Contains("Name", validation.AllPlaceholders);
        Assert.Equal(new[] { "Missing" }, validation.MissingVariables);
        Assert.Contains(validation.Warnings, w => w.Type == ValidationWarningType.EmptyLoopCollection);
    }

    [Fact]
    public void QuickStart_SharedBooleanFormatterRegistry_IsUsed()
    {
        var registry = new BooleanFormatterRegistry(CultureInfo.GetCultureInfo("de-DE"));
        registry.Register("status", new BooleanFormatter("Aktiv", "Inaktiv"));
        var options = new PlaceholderReplacementOptions
        {
            Culture = CultureInfo.GetCultureInfo("de-DE"),
            BooleanFormatterRegistry = registry
        };

        ProcessingResult result = new DocumentTemplateProcessor(options).ProcessTemplate(
            CreateTemplate("{{On:status}} {{On:yesno}}").ToArray(), new Dictionary<string, object> { ["On"] = true }, out byte[] output);

        Assert.True(result.IsSuccess);
        Assert.Equal(new[] { "Aktiv Ja" }, ReadParagraphs(output));
    }

    [Fact]
    public void ProcessingWarnings_FailedInlineExpression_AddsExpressionFailedAndMissingVariable()
    {
        var processor = new DocumentTemplateProcessor();

        ProcessingResult result = processor.ProcessTemplate(
            CreateTemplate("{{(Status === \"Active\")}}").ToArray(),
            new Dictionary<string, object> { ["Status"] = "Active" },
            out byte[] output);

        Assert.True(result.IsSuccess);
        Assert.Equal(
            new[] { ProcessingWarningType.ExpressionFailed, ProcessingWarningType.MissingVariable },
            result.Warnings.Select(w => w.Type));
        Assert.Equal("expression", result.Warnings[0].Context);
        Assert.Equal(new[] { "(Status === \"Active\")" }, result.MissingVariables);
        Assert.Equal(new[] { "{{(Status === \"Active\")}}" }, ReadParagraphs(output));
        Assert.Equal(
            "MissingVariable [placeholder]: Variable 'X' was not found in the data.",
            ProcessingWarning.MissingVariable("X").ToString());
    }

    [Fact]
    public void ConditionEvaluation_DocumentedSemantics_Hold()
    {
        var evaluator = new ConditionEvaluator();
        var data = new Dictionary<string, object>
        {
            ["IsActive"] = true,
            ["Count"] = 5,
            ["Status"] = "Active",
            ["Zero"] = "0",
            ["Roles"] = new List<object> { "Admin" },
            ["Role"] = "Admin",
            ["Exists"] = "yes"
        };

        Assert.True(evaluator.Evaluate("IsActive and Count > 3", data));
        Assert.False(evaluator.Evaluate("not Status = \"Active\"", data)); // not (Status = "Active")
        Assert.True(evaluator.Evaluate("Status = Active", data));             // bareword fallback
        Assert.True(evaluator.Evaluate("Missing = \"Missing\"", data));       // ... and its flip side
        Assert.False(evaluator.Evaluate("Zero", data));                       // "0" is falsy
        Assert.True(evaluator.Evaluate("Count = \"5\"", data));               // number vs. string: invariant string form
        Assert.False(evaluator.Evaluate("not Role in Roles", data));          // not (Role in Roles)
        Assert.True(evaluator.Evaluate("Exists", data));                      // keyword in operand position is a variable
        Assert.True(evaluator.Evaluate("[Exists] = \"yes\"", data));          // bracket escape
        Assert.False(evaluator.Evaluate("Status = 'Active'", data));          // single quotes are no string delimiter here
        Assert.False(evaluator.Evaluate("Count > 2 && IsActive", data));      // malformed: false, no exception

        ConditionValidationResult validation = evaluator.Validate("Count > 2 && IsActive");
        Assert.False(validation.IsValid);
        ConditionValidationIssue issue = Assert.Single(validation.Issues);
        Assert.Equal(ConditionValidationIssueType.UnknownOperator, issue.Type);
    }

    [Fact]
    public void ConditionEvaluation_InlineDialect_IsStricter()
    {
        var processor = new DocumentTemplateProcessor(new PlaceholderReplacementOptions { Culture = _invariant });

        ProcessingResult result = processor.ProcessTemplate(
            CreateTemplate("{{(N = \"5\")}}", "{{(S)}}", "{{(One)}}", "{{(Status = 'Active')}}").ToArray(),
            new Dictionary<string, object> { ["N"] = 5, ["S"] = "true", ["One"] = 1, ["Status"] = "Active" },
            out byte[] output);

        Assert.True(result.IsSuccess);
        Assert.Equal(new[] { "False", "False", "False", "True" }, ReadParagraphs(output));
    }

    [Fact]
    public void TextTemplates_EmailSample_ProducesDocumentedOutput()
    {
        var processor = new TextTemplateProcessor(new PlaceholderReplacementOptions { Culture = _invariant });
        string emailTemplate = "Dear {{CustomerName}},\n\nThank you for your order #{{OrderId}}.\n\n"
            + "{{#if IsVip}}As a VIP customer, you'll receive free shipping!{{#else}}Your order will arrive in 3-5 business days.{{/if}}\n\n"
            + "Order Details:\n{{#foreach Items}}- {{Name}}: ${{Price}}\n{{/foreach}}\nTotal: ${{Total}}\n\n"
            + "Best regards,\nThe {{CompanyName}} Team";
        var data = new Dictionary<string, object>
        {
            ["CustomerName"] = "Alice Smith",
            ["OrderId"] = 12345,
            ["IsVip"] = true,
            ["CompanyName"] = "TriasDev",
            ["Items"] = new[]
            {
                new { Name = "Premium Widget", Price = 29.99 },
                new { Name = "Deluxe Gadget", Price = 49.99 }
            },
            ["Total"] = 79.98
        };

        TextProcessingResult result = processor.ProcessTemplate(emailTemplate, data);

        Assert.True(result.IsSuccess);
        Assert.Equal(
            "Dear Alice Smith,\n\nThank you for your order #12345.\n\nAs a VIP customer, you'll receive free shipping!\n\n"
            + "Order Details:\n- Premium Widget: $29.99\n- Deluxe Gadget: $49.99\n\nTotal: $79.98\n\n"
            + "Best regards,\nThe TriasDev Team",
            result.ProcessedText);
    }

    private sealed class WriteOnlyStream : MemoryStream
    {
        public override bool CanRead => false;
    }
}
