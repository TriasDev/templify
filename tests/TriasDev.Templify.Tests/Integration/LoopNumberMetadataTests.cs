// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using TriasDev.Templify.Core;
using TriasDev.Templify.Tests.Helpers;

namespace TriasDev.Templify.Tests.Integration;

/// <summary>
/// Tests for the 1-based <c>{{@number}}</c> loop metadata (#174).
/// </summary>
public class LoopNumberMetadataTests
{
    private static Dictionary<string, object> Items(params string[] names) => new()
    {
        ["Items"] = names.Select(n => (object)new Dictionary<string, object> { ["Name"] = n }).ToList(),
    };

    private static (ProcessingResult Result, List<string> Paragraphs) Process(Dictionary<string, object> data, params string[] paragraphs)
    {
        DocumentBuilder builder = new DocumentBuilder();
        foreach (string paragraph in paragraphs)
        {
            builder.AddParagraph(paragraph);
        }

        MemoryStream outputStream = new MemoryStream();
        ProcessingResult result = new DocumentTemplateProcessor().ProcessTemplate(builder.ToStream(), outputStream, data);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        return (result, verifier.GetAllParagraphTexts());
    }

    [Fact]
    public void ImplicitLoop_Number_IsOneBased()
    {
        (ProcessingResult result, List<string> paragraphs) = Process(
            Items("A", "B", "C"), "{{#foreach Items}}", "{{@number}}. {{Name}} ({{@index}})", "{{/foreach}}");

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Equal(new[] { "1. A (0)", "2. B (1)", "3. C (2)" }, paragraphs);
    }

    [Fact]
    public void NamedLoop_Number_IsOneBased()
    {
        (ProcessingResult result, List<string> paragraphs) = Process(
            Items("A", "B"), "{{#foreach item in Items}}", "{{@number}}: {{item.Name}}", "{{/foreach}}");

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Equal(new[] { "1: A", "2: B" }, paragraphs);
    }

    [Fact]
    public void NestedLoops_Number_ResolvesInnermostLoop()
    {
        Dictionary<string, object> data = new()
        {
            ["Groups"] = new List<object>
            {
                new Dictionary<string, object> { ["Name"] = "G1", ["Items"] = new List<object> { "a", "b" } },
                new Dictionary<string, object> { ["Name"] = "G2", ["Items"] = new List<object> { "c" } },
            },
        };

        (ProcessingResult result, List<string> paragraphs) = Process(
            data,
            "{{#foreach group in Groups}}",
            "Group {{@number}}: {{group.Name}}",
            "{{#foreach Items}}",
            "{{@number}}. {{.}}",
            "{{/foreach}}",
            "{{/foreach}}");

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Equal(new[] { "Group 1: G1", "1. a", "2. b", "Group 2: G2", "1. c" }, paragraphs);
    }

    [Fact]
    public void Number_InConditions_Evaluates()
    {
        (ProcessingResult result, List<string> paragraphs) = Process(
            Items("A", "B", "C"),
            "{{#foreach Items}}",
            "{{#if @number > 1}}",
            "later {{@number}}",
            "{{#else}}",
            "first {{@number}}",
            "{{/if}}",
            "{{/foreach}}");

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Equal(new[] { "first 1", "later 2", "later 3" }, paragraphs);
    }

    [Fact]
    public void TableRowLoop_Number_IsOneBased()
    {
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddTable(3, 2, (row, col) => row switch
        {
            0 => col == 0 ? "{{#foreach Items}}" : "",
            1 => col == 0 ? "{{@number}}" : "{{Name}}",
            _ => col == 0 ? "{{/foreach}}" : "",
        });

        MemoryStream outputStream = new MemoryStream();
        ProcessingResult result = new DocumentTemplateProcessor().ProcessTemplate(builder.ToStream(), outputStream, Items("A", "B"));

        Assert.True(result.IsSuccess, result.ErrorMessage);
        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        List<List<string>> rows = verifier.GetTableCellTexts(0);
        Assert.Equal(2, rows.Count);
        Assert.Equal(new[] { "1", "A" }, rows[0]);
        Assert.Equal(new[] { "2", "B" }, rows[1]);
    }

    [Fact]
    public void TextTemplateProcessor_Number_IsOneBased()
    {
        TextProcessingResult result = new TextTemplateProcessor().ProcessTemplate(
            "{{#foreach Items}}[{{@number}}:{{Name}}]{{/foreach}}", Items("A", "B"));

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Equal("[1:A][2:B]", result.ProcessedText);
    }

    [Fact]
    public void ValidateTemplate_Number_IsNotFlaggedAsMissing()
    {
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{#foreach Items}}");
        builder.AddParagraph("{{@number}}. {{Name}}{{#if @number > 1}}!{{/if}}");
        builder.AddParagraph("{{/foreach}}");

        ValidationResult result = new DocumentTemplateProcessor().ValidateTemplate(builder.ToStream(), Items("A"));

        Assert.True(result.IsValid);
        Assert.Empty(result.MissingVariables);
        Assert.Contains("@number", result.AllPlaceholders);
    }
}
