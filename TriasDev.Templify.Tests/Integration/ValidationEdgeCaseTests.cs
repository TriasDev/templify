// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Text;
using TriasDev.Templify.Core;
using TriasDev.Templify.Tests.Helpers;

namespace TriasDev.Templify.Tests.Integration;

/// <summary>
/// <see cref="DocumentTemplateProcessor.ValidateTemplate(Stream, Dictionary{string, object})"/> for invalid input,
/// table row loops and named iteration variables.
/// </summary>
public sealed class ValidationEdgeCaseTests
{
    private static ValidationResult Validate(DocumentBuilder builder, Dictionary<string, object>? data = null)
    {
        DocumentTemplateProcessor processor = TemplateTestHarness.CreateInvariantProcessor();
        using MemoryStream template = builder.ToStream();
        return data == null ? processor.ValidateTemplate(template) : processor.ValidateTemplate(template, data);
    }

    private static List<Dictionary<string, object>> Items(params string[] titles) =>
        titles.Select(t => new Dictionary<string, object> { ["Title"] = t }).ToList();

    [Fact]
    public void ValidateTemplate_NonDocxStream_ReturnsValidationFailedError()
    {
        DocumentTemplateProcessor processor = TemplateTestHarness.CreateInvariantProcessor();
        using MemoryStream template = new MemoryStream(Encoding.UTF8.GetBytes("not a docx"));

        ValidationResult result = processor.ValidateTemplate(template);

        Assert.False(result.IsValid);
        Assert.StartsWith("Validation failed:", Assert.Single(result.Errors).Message);
    }

    [Fact(Skip = "Bug: #198")]
    public void ValidateTemplate_TableRowLoopWithData_ItemPropertiesAreNotReportedMissing()
    {
        Dictionary<string, object> data = new Dictionary<string, object> { ["Items"] = Items("A") };

        // Processing resolves {{Title}} from the loop item ...
        using (TemplateTestRun run = TemplateTestHarness.Process(TableRowLoopTemplate(), data))
        {
            Assert.Equal("A", run.Verifier.GetTableCellText(0, 0, 0));
        }

        // ... so validation with the same data must not report it as missing
        ValidationResult result = Validate(TableRowLoopTemplate(), data);

        Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(e => e.Message)));
        Assert.Contains("Items", result.AllPlaceholders);
        Assert.Empty(result.MissingVariables);
    }

    private static DocumentBuilder TableRowLoopTemplate()
    {
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddTable(3, 1, (row, _) => row switch
        {
            0 => "{{#foreach Items}}",
            1 => "{{Title}}",
            _ => "{{/foreach}}"
        });
        return builder;
    }

    [Fact]
    public void ValidateTemplate_TableRowLoopOverMissingCollection_ReportsMissingVariable()
    {
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddTable(3, 1, (row, _) => row switch
        {
            0 => "{{#foreach Items}}",
            1 => "{{Title}}",
            _ => "{{/foreach}}"
        });

        ValidationResult result = Validate(builder, new Dictionary<string, object> { ["Other"] = 1 });

        Assert.False(result.IsValid);
        Assert.Contains("Items", result.MissingVariables);
        Assert.Contains(result.Errors, e => e.Type == ValidationErrorType.MissingVariable && e.Message.Contains("'Items'"));
    }

    [Fact]
    public void ValidateTemplate_TableRowLoopWithoutEnd_ReportsSyntaxError()
    {
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddTable(2, 1, (row, _) => row == 0 ? "{{#foreach Items}}" : "{{Title}}");

        ValidationResult result = Validate(builder);

        Assert.False(result.IsValid);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void ValidateTemplate_NamedIterationVariable_KnownAndUnknownProperties()
    {
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{#foreach item in Items}}");
        builder.AddParagraph("{{item}} {{item.Title}} {{item.Title.Length}} {{item.Missing}}");
        builder.AddParagraph("{{/foreach}}");

        ValidationResult result = Validate(builder, new Dictionary<string, object> { ["Items"] = Items("A", "B") });

        Assert.Contains("item.Missing", result.MissingVariables);
        Assert.DoesNotContain("item", result.MissingVariables);
        Assert.DoesNotContain("item.Title", result.MissingVariables);
        Assert.DoesNotContain("item.Title.Length", result.MissingVariables);
    }

    [Fact]
    public void ValidateTemplate_NestedLoopOverItemProperty_IsNotReportedMissing()
    {
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{#foreach Orders}}");
        builder.AddParagraph("{{#foreach Lines}}");
        builder.AddParagraph("{{Product}}");
        builder.AddParagraph("{{/foreach}}");
        builder.AddParagraph("{{/foreach}}");

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Orders"] = new List<Dictionary<string, object>>
            {
                new Dictionary<string, object>
                {
                    ["Lines"] = new List<Dictionary<string, object>> { new Dictionary<string, object> { ["Product"] = "X" } }
                }
            }
        };

        ValidationResult result = Validate(builder, data);

        Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(e => e.Message)));
        Assert.Empty(result.MissingVariables);
    }
}
