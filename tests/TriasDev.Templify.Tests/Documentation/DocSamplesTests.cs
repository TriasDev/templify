// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text.Json;
using TriasDev.Templify.Conditionals;
using TriasDev.Templify.Core;
using TriasDev.Templify.Tests.Helpers;
using TriasDev.Templify.Utilities;

namespace TriasDev.Templify.Tests.Documentation;

/// <summary>
/// Mirrors the key code samples and documented behavior of README.md, TriasDev.Templify/README.md and
/// TriasDev.Templify/Examples.md, so that the samples keep compiling against the public API and the documented
/// output stays true. When one of these tests fails, update the documentation together with the code.
/// </summary>
public sealed class DocSamplesTests : IDisposable
{
    private static readonly CultureInfo _enUs = CultureInfo.GetCultureInfo("en-US");

    private readonly string _tempDirectory;

    public DocSamplesTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "templify-docsamples-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
    }

    public void Dispose()
    {
        Directory.Delete(_tempDirectory, recursive: true);
    }

    // README.md: "Your First Document"
    [Fact]
    public void Readme_QuickStart_ProcessesTemplateFromFiles()
    {
        string templatePath = WriteTemplate("template.docx", "Hello {{Name}}!", "Your order #{{OrderId}} has been confirmed.");
        string outputPath = Path.Combine(_tempDirectory, "output.docx");

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Name"] = "John Doe",
            ["OrderId"] = "12345"
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        ProcessingResult result;
        using (FileStream templateStream = File.OpenRead(templatePath))
        using (FileStream outputStream = File.Create(outputPath))
        {
            result = processor.ProcessTemplate(templateStream, outputStream, data);
        }

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Equal(2, result.ReplacementCount);
        Assert.Equal(
            new[] { "Hello John Doe!", "Your order #12345 has been confirmed." },
            ReadParagraphs(File.ReadAllBytes(outputPath)));
    }

    // Examples.md "Handle Streams Properly": a write-only output stream is a failed result, not an exception.
    [Fact]
    public void Examples_WriteOnlyOutputStream_IsReportedAsFailure()
    {
        string templatePath = WriteTemplate("template.docx", "Hello {{Name}}!");
        string outputPath = Path.Combine(_tempDirectory, "output.docx");

        ProcessingResult result;
        using (FileStream templateStream = File.OpenRead(templatePath))
        using (FileStream outputStream = File.OpenWrite(outputPath))
        {
            result = new DocumentTemplateProcessor().ProcessTemplate(
                templateStream, outputStream, new Dictionary<string, object> { ["Name"] = "x" });
        }

        Assert.False(result.IsSuccess);
        Assert.Contains("Invalid output stream", result.ErrorMessage);
    }

    // TriasDev.Templify/README.md "Basic Usage": other entry points (file, byte array, JSON)
    [Fact]
    public void LibraryReadme_OtherEntryPoints_ProduceTheSameDocument()
    {
        string templatePath = WriteTemplate("template.docx", "Company: {{CompanyName}}", "Approved: {{IsApproved:yesno}}");
        string outputPath = Path.Combine(_tempDirectory, "output.docx");
        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["CompanyName"] = "TriasDev GmbH & Co. KG",
            ["IsApproved"] = true
        };
        DocumentTemplateProcessor processor = new DocumentTemplateProcessor(new PlaceholderReplacementOptions { Culture = _enUs });
        string[] expected = { "Company: TriasDev GmbH & Co. KG", "Approved: Yes" };

        ProcessingResult fileResult = processor.ProcessTemplateFile(templatePath, outputPath, data);
        Assert.True(fileResult.IsSuccess, fileResult.ErrorMessage);
        Assert.Equal(expected, ReadParagraphs(File.ReadAllBytes(outputPath)));

        byte[] template = File.ReadAllBytes(templatePath);
        ProcessingResult bytesResult = processor.ProcessTemplate(template, data, out byte[] document);
        Assert.True(bytesResult.IsSuccess, bytesResult.ErrorMessage);
        Assert.Equal(expected, ReadParagraphs(document));

        string json = """{ "CompanyName": "TriasDev GmbH & Co. KG", "IsApproved": true }""";
        string jsonOutputPath = Path.Combine(_tempDirectory, "output-json.docx");
        ProcessingResult jsonResult = processor.ProcessTemplateFile(templatePath, jsonOutputPath, json);
        Assert.True(jsonResult.IsSuccess, jsonResult.ErrorMessage);
        Assert.Equal(expected, ReadParagraphs(File.ReadAllBytes(jsonOutputPath)));
    }

    // README.md "Format Specifiers" (en-US outputs) and TriasDev.Templify/README.md format specifier table
    [Fact]
    public void Readme_FormatSpecifiers_ProduceDocumentedOutput()
    {
        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Name"] = "Alice Johnson",
            ["Code"] = "ABC-123",
            ["Amount"] = 1234.567m,
            ["Value"] = 1234.567m,
            ["Percentage"] = 0.1234m,
            ["OrderDate"] = new DateTime(2024, 1, 15),
            ["IsActive"] = true,
            ["FileName"] = "my_report_final.docx",
            ["NumberAsText"] = "1234.5"
        };

        using TemplateTestRun run = TemplateTestHarness.Process(
            Template(
                "{{Name:uppercase}}",
                "{{Code:lowercase}}",
                "{{Amount:currency}}",
                "{{Value:number:N2}}",
                "{{Percentage:number:P2}}",
                "{{OrderDate:date:yyyy-MM-dd}}",
                "{{OrderDate:date:MMMM d, yyyy}}",
                "{{IsActive:checkbox}}",
                "{{IsActive:yesno}}",
                "{{IsActive:check}}",
                "{{FileName:raw}}",
                "{{NumberAsText:currency}}"),
            data,
            new PlaceholderReplacementOptions { Culture = _enUs });

        Assert.Equal(
            new[]
            {
                "ALICE JOHNSON", "abc-123", "$1,234.57", "1,234.57", "12.34%", "2024-01-15", "January 15, 2024",
                "☑", "Yes", "✓", "my_report_final.docx", "1234.5"
            },
            run.Verifier.GetAllParagraphTexts());
    }

    // TriasDev.Templify/README.md "Localization": word formats follow the culture
    [Fact]
    public void LibraryReadme_BooleanLocalization_FollowsCulture()
    {
        Dictionary<string, object> data = new Dictionary<string, object> { ["IsActive"] = true };

        using TemplateTestRun german = TemplateTestHarness.Process(
            Template("{{IsActive:yesno}}"), data, new PlaceholderReplacementOptions { Culture = CultureInfo.GetCultureInfo("de-DE") });
        using TemplateTestRun japanese = TemplateTestHarness.Process(
            Template("{{IsActive:yesno}}", "{{IsActive:truefalse}}"), data, new PlaceholderReplacementOptions { Culture = CultureInfo.GetCultureInfo("ja-JP") });

        Assert.Equal("Ja", german.Verifier.GetParagraphText(0));
        Assert.Equal(new[] { "はい", "True" }, japanese.Verifier.GetAllParagraphTexts());
    }

    // README.md "Markdown Formatting": EnableMarkdown = false and :raw keep values literal
    [Fact]
    public void Readme_MarkdownOptOut_KeepsValuesLiteral()
    {
        Dictionary<string, object> data = new Dictionary<string, object> { ["FileName"] = "my_report_final.docx" };

        using TemplateTestRun markdown = TemplateTestHarness.Process(Template("{{FileName}}", "{{FileName:raw}}"), data);
        using TemplateTestRun disabled = TemplateTestHarness.Process(
            Template("{{FileName}}"), data, new PlaceholderReplacementOptions { EnableMarkdown = false, Culture = CultureInfo.InvariantCulture });

        Assert.Equal(new[] { "myreportfinal.docx", "my_report_final.docx" }, markdown.Verifier.GetAllParagraphTexts());
        Assert.Equal("my_report_final.docx", disabled.Verifier.GetParagraphText(0));
    }

    // README.md "Standalone Condition Evaluation"
    [Fact]
    public void Readme_StandaloneConditionEvaluation_Works()
    {
        ConditionEvaluator evaluator = new ConditionEvaluator();
        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["IsActive"] = true,
            ["Count"] = 5,
            ["Status"] = "Active"
        };

        Assert.True(evaluator.Evaluate("IsActive and Count > 0", data));

        IConditionContext context = evaluator.CreateConditionContext(data);
        Assert.True(context.Evaluate("IsActive"));
        Assert.True(context.Evaluate("IsActive = true"));
        Assert.True(context.Evaluate("Count > 3"));
        Assert.True(context.Evaluate("Status in (\"Active\", \"Pending\")"));
    }

    // README.md / TriasDev.Templify/README.md: operators, precedence, bareword fallback, reserved words
    [Theory]
    [InlineData("not Status = \"Active\"", false)] // not (Status = "Active")
    [InlineData("not Status = \"Deleted\"", true)]
    [InlineData("Status = Active", true)] // unquoted word that is not a variable is compared as text
    [InlineData("Missing = \"Missing\"", true)] // pitfall: a missing variable compares as its own name
    [InlineData("status = \"Active\"", false)] // dictionary keys are case-sensitive
    [InlineData("Status = \"active\"", false)] // text comparison is case-sensitive
    [InlineData("Count = 5.0", true)] // numbers compare numerically across types
    [InlineData("Status in (\"Active\", \"Pending\")", true)]
    [InlineData("\"admin\" in Roles", true)]
    [InlineData("Tags contains \"urgent\"", true)] // membership on a collection
    [InlineData("Email endswith \"@example.com\"", true)]
    [InlineData("Discount exists", true)] // present with a null value
    [InlineData("Notes is empty", true)] // missing
    [InlineData("Tags is not empty", true)]
    [InlineData("(Count > 3 or IsDeleted) and not IsDeleted", true)]
    [InlineData("Count > 3 && Status", false)] // not an operator: parse error, evaluates to false
    [InlineData("Zero", false)] // "0" string is falsy
    [InlineData("FalseText", false)] // "false" string is falsy
    [InlineData("Empty", true)] // 1.7.0 keyword read as a variable where a value is expected
    [InlineData("[Not] = \"x\"", true)] // bracket escape for keyword names
    [InlineData("[Empty].Count > 0", false)] // bracket escape followed by a path ("E!".Count does not exist)
    public void Readme_ConditionSemantics_MatchDocumentation(string expression, bool expected)
    {
        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Status"] = "Active",
            ["Count"] = 5,
            ["IsDeleted"] = false,
            ["Roles"] = new[] { "admin", "editor" },
            ["Tags"] = new List<string> { "urgent", "billing" },
            ["Email"] = "alice@example.com",
            ["Discount"] = null!,
            ["Zero"] = "0",
            ["FalseText"] = "false",
            ["Empty"] = "E!",
            ["Not"] = "x"
        };

        Assert.Equal(expected, new ConditionEvaluator().Evaluate(expression, data));
    }

    // TriasDev.Templify/README.md "Differences from {{#if}}": inline expressions are stricter
    [Fact]
    public void LibraryReadme_InlineExpressions_UseStricterTruthinessAndAllowSingleQuotes()
    {
        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Name"] = "Alice",
            ["Age"] = 25,
            ["Status"] = "Active"
        };

        using TemplateTestRun run = TemplateTestHarness.Process(
            Template("{{(Name)}}", "{{(Age >= 18):yesno}}", "{{(Status = 'Active')}}", "{{(Age > 18 && Name)}}"),
            data,
            new PlaceholderReplacementOptions { Culture = _enUs });

        Assert.Equal(new[] { "False", "Yes", "True", "{{(Age > 18 && Name)}}" }, run.Verifier.GetAllParagraphTexts());
        Assert.Contains(run.Result.Warnings, w => w.Type == ProcessingWarningType.ExpressionFailed);
        Assert.Contains(run.Result.Warnings, w => w.Type == ProcessingWarningType.MissingVariable);
    }

    // TriasDev.Templify/README.md "Conditional Blocks": malformed conditions and syntax errors
    [Fact]
    public void LibraryReadme_ErrorModel_MatchesDocumentation()
    {
        Dictionary<string, object> data = new Dictionary<string, object> { ["A"] = true, ["B"] = true };

        using TemplateTestRun malformed = TemplateTestHarness.Process(Template("{{#if A && B}}", "shown?", "{{/if}}", "end"), data);
        Assert.True(malformed.Result.IsSuccess);
        Assert.Equal(new[] { "end" }, malformed.Verifier.GetAllParagraphTexts());
        Assert.Contains(malformed.Result.Warnings, w => w.Type == ProcessingWarningType.ExpressionFailed && w.Context == "conditional");

        using TemplateTestRun unmatched = TemplateTestHarness.Process(Template("{{#if A}}", "x"), data);
        Assert.False(unmatched.Result.IsSuccess);
        Assert.StartsWith("Processing failed:", unmatched.Result.ErrorMessage);

        using TemplateTestRun notACollection = TemplateTestHarness.Process(Template("{{#foreach A}}", "x", "{{/foreach}}"), data);
        Assert.False(notACollection.Result.IsSuccess);

        DocumentTemplateProcessor throwing = new DocumentTemplateProcessor(
            new PlaceholderReplacementOptions { MissingVariableBehavior = MissingVariableBehavior.ThrowException });
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => throwing.ProcessTemplate(Template("Email: {{ContactEmail}}").ToStream(), new MemoryStream(), data));
        Assert.Equal("Missing variable or invalid expression: ContactEmail", exception.Message);
    }

    // TriasDev.Templify/README.md "Nested Data Structures": no spaces inside braces, bracket keys, typo-free dictionary syntax
    [Fact]
    public void LibraryReadme_PlaceholderSyntax_MatchesDocumentation()
    {
        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Name"] = "Alice",
            ["Settings"] = new Dictionary<string, string> { ["Theme"] = "Dark", ["My Key"] = "x" },
            ["Items"] = new List<string> { "License", "Support" }
        };

        using TemplateTestRun run = TemplateTestHarness.Process(
            Template("{{Settings[Theme]}}", "{{Settings.Theme}}", "{{Items[1]}}", "{{ Name }}", "{{Settings[My Key]}}"),
            data);

        Assert.Equal(
            new[] { "Dark", "Dark", "Support", "{{ Name }}", "{{Settings[My Key]}}" },
            run.Verifier.GetAllParagraphTexts());
    }

    // Examples.md "Loop Metadata" and TriasDev.Templify/README.md "Loop Metadata Variables"
    [Fact]
    public void Examples_LoopMetadata_MatchesDocumentedOutput()
    {
        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Tasks"] = new List<Dictionary<string, object>>
            {
                new Dictionary<string, object> { ["Title"] = "Design" },
                new Dictionary<string, object> { ["Title"] = "Deploy" }
            }
        };

        using TemplateTestRun run = TemplateTestHarness.Process(
            Template(
                "Total: {{Tasks.Count}} tasks",
                "{{#foreach Tasks}}",
                "Task {{@number}} of {{@count}} (index {{@index}}): {{Title}}{{#if @last}} (last){{/if}}",
                "{{/foreach}}",
                "{{@count}}"),
            data);

        Assert.Equal(
            new[] { "Total: 2 tasks", "Task 1 of 2 (index 0): Design", "Task 2 of 2 (index 1): Deploy (last)", "{{@count}}" },
            run.Verifier.GetAllParagraphTexts());
    }

    // TriasDev.Templify/README.md "Loop Markers and Paragraphs" / Examples.md "Loop in Header"
    [Fact]
    public void LibraryReadme_LoopMarkersSharingOneParagraph_AreNotSupported()
    {
        Dictionary<string, object> data = new Dictionary<string, object> { ["Tags"] = new List<string> { "a", "b" } };

        using TemplateTestRun run = TemplateTestHarness.Process(Template("before", "{{#foreach Tags}}{{.}}, {{/foreach}}", "after"), data);

        Assert.Equal(new[] { "before", "after" }, run.Verifier.GetAllParagraphTexts());
    }

    // TriasDev.Templify/README.md "Empty, Missing and Null Collections"
    [Fact]
    public void LibraryReadme_NullItemsAndCollections_MatchDocumentation()
    {
        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Name"] = "ROOT",
            ["Items"] = new List<object?> { null, new Dictionary<string, object?> { ["Name"] = "X" } }!,
            ["Missing"] = null!
        };

        using TemplateTestRun run = TemplateTestHarness.Process(
            Template(
                "{{#foreach Items}}", "[{{Name}}]", "{{/foreach}}",
                "{{#foreach item in Items}}", "[{{item.Name}}]", "{{/foreach}}",
                "{{#foreach Missing}}", "never", "{{/foreach}}"),
            data);

        Assert.Equal(new[] { "[ROOT]", "[X]", "[]", "[X]" }, run.Verifier.GetAllParagraphTexts());
        Assert.Contains(run.Result.Warnings, w => w.Type == ProcessingWarningType.NullLoopCollection);
    }

    // Examples.md "Table Loop Example": markers in rows of their own; the marker rows are removed
    [Fact]
    public void Examples_TableRowLoop_RepeatsRowsBetweenMarkerRows()
    {
        string[][] rows =
        {
            new[] { "Product", "Quantity" },
            new[] { "{{#foreach LineItems}}", "" },
            new[] { "{{Product}}", "{{Quantity}}" },
            new[] { "{{/foreach}}", "" },
            new[] { "Total", "{{GrandTotal}}" }
        };
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddTable(rows.Length, 2, (row, column) => rows[row][column]);

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["LineItems"] = new List<Dictionary<string, object>>
            {
                new Dictionary<string, object> { ["Product"] = "A", ["Quantity"] = 5 },
                new Dictionary<string, object> { ["Product"] = "B", ["Quantity"] = 3 }
            },
            ["GrandTotal"] = 8
        };

        using TemplateTestRun run = TemplateTestHarness.Process(builder, data);

        List<List<string>> cells = run.Verifier.GetTableCellTexts(0);
        Assert.Equal(
            new[] { "Product|Quantity", "A|5", "B|3", "Total|8" },
            cells.Select(row => string.Join("|", row)).ToArray());
    }

    // Examples.md "Validation and Warnings"
    [Fact]
    public void Examples_ValidationAndWarnings_Work()
    {
        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        Dictionary<string, object> data = new Dictionary<string, object> { ["Name"] = "Alice", ["Tags"] = new List<string>() };

        ValidationResult validation = processor.ValidateTemplate(
            Template("{{Name}} {{Email}}", "{{#foreach Tags}}", "{{.}}", "{{/foreach}}").ToStream(), data);

        // With data, a missing variable is a validation error (ValidationErrorType.MissingVariable)
        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors, e => e.Type == ValidationErrorType.MissingVariable);
        Assert.Contains("Email", validation.MissingVariables);
        Assert.Contains("Name", validation.AllPlaceholders);
        Assert.Contains(validation.Warnings, w => w.Type == ValidationWarningType.EmptyLoopCollection);

        ValidationResult invalid = processor.ValidateTemplate(Template("{{#if Name}}", "x").ToStream());
        Assert.False(invalid.IsValid);
        Assert.NotEmpty(invalid.Errors);

        MemoryStream output = new MemoryStream();
        ProcessingResult result = processor.ProcessTemplate(Template("{{Name}} {{Email}}").ToStream(), output, data);
        Assert.True(result.HasWarnings);
        ProcessingWarning warning = Assert.Single(result.Warnings);
        Assert.Equal("MissingVariable [placeholder]: Variable 'Email' was not found in the data.", warning.ToString());
        Assert.NotEmpty(result.GetWarningReportBytes());
    }

    // Examples.md "JSON Data" and TriasDev.Templify/README.md "JsonDataParser"
    [Fact]
    public void Examples_JsonData_ParserWorksAndJsonElementPitfallsAreAsDocumented()
    {
        string json = """{ "Customer": { "Name": "Bob" }, "Items": [ { "N": "a" }, { "N": "b" } ], "Flag": false }""";
        DocumentBuilder Loop() => Template("{{Customer.Name}}", "{{#foreach Items}}", "{{N}}", "{{/foreach}}", "{{#if Flag}}", "flag", "{{/if}}");

        Dictionary<string, object> parsed = JsonDataParser.ParseJsonToDataDictionary(json);
        parsed["GeneratedAt"] = new DateTime(2025, 1, 1);
        using TemplateTestRun viaParser = TemplateTestHarness.Process(Loop(), parsed);
        Assert.Equal(new[] { "Bob", "a", "b" }, viaParser.Verifier.GetAllParagraphTexts());

        using TemplateTestRun viaOverload = TemplateTestHarness.ProcessJson(Loop(), json);
        Assert.Equal(new[] { "Bob", "a", "b" }, viaOverload.Verifier.GetAllParagraphTexts());

        // JsonSerializer.Deserialize<Dictionary<string, object>> yields JsonElement values: nested paths work,
        // a top-level JSON array cannot be used as a loop collection, and a JsonElement false is truthy.
        Dictionary<string, object> elements = JsonSerializer.Deserialize<Dictionary<string, object>>(json)!;
        using TemplateTestRun nested = TemplateTestHarness.Process(Template("{{Customer.Name}}", "{{Items[1].N}}"), elements);
        Assert.Equal(new[] { "Bob", "b" }, nested.Verifier.GetAllParagraphTexts());

        using TemplateTestRun loop = TemplateTestHarness.Process(Template("{{#foreach Items}}", "{{N}}", "{{/foreach}}"), elements);
        Assert.False(loop.Result.IsSuccess);

        Assert.True(new ConditionEvaluator().Evaluate("Flag", elements));

        Assert.Throws<JsonException>(() => new DocumentTemplateProcessor().ProcessTemplate(Array.Empty<byte>(), "not json", out _));
    }

    // TriasDev.Templify/README.md "TextTemplateProcessor"
    [Fact]
    public void LibraryReadme_TextTemplateProcessor_Works()
    {
        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Name"] = "Alice",
            ["Tags"] = new List<string> { "a", "b" },
            ["Message"] = "**bold**"
        };

        TextTemplateProcessor textProcessor = new TextTemplateProcessor(new PlaceholderReplacementOptions { Culture = _enUs });
        TextProcessingResult text = textProcessor.ProcessTemplate(
            "Hello {{Name}}! {{#foreach t in Tags}}[{{t}}]{{/foreach}} {{#if Tags is not empty}}{{(Name = 'Alice'):yesno}}{{/if}} {{Message}}",
            data);

        Assert.True(text.IsSuccess, text.ErrorMessage);
        Assert.Equal("Hello Alice! [a][b] Yes **bold**", text.ProcessedText);
        Assert.False(text.HasWarnings);
    }

    private static DocumentBuilder Template(params string[] paragraphs)
    {
        DocumentBuilder builder = new DocumentBuilder();
        foreach (string paragraph in paragraphs)
        {
            builder.AddParagraph(paragraph);
        }

        return builder;
    }

    private string WriteTemplate(string fileName, params string[] paragraphs)
    {
        string path = Path.Combine(_tempDirectory, fileName);
        using MemoryStream stream = Template(paragraphs).ToStream();
        File.WriteAllBytes(path, stream.ToArray());
        return path;
    }

    private static List<string> ReadParagraphs(byte[] document)
    {
        using MemoryStream stream = new MemoryStream(document);
        using DocumentVerifier verifier = new DocumentVerifier(stream);
        return verifier.GetAllParagraphTexts();
    }
}
