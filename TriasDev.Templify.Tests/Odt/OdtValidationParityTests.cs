// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using TriasDev.Templify.Core;
using TriasDev.Templify.Tests.Helpers;

namespace TriasDev.Templify.Tests.Odt;

/// <summary>
/// The OpenDocument validator has "the same rules and messages" as the Word validator: the same paragraphs,
/// validated with the same data, give the same result in both formats.
/// </summary>
public sealed class OdtValidationParityTests
{
    public static TheoryData<string, string[]> Templates => new TheoryData<string, string[]>
    {
        { "placeholders", new[] { "{{Name}} {{Missing}}", "{{Customer.Name}} {{Customer.Nope}} {{List[0]}}" } },
        { "unmatched if", new[] { "{{#if A}}", "x" } },
        { "unmatched foreach", new[] { "{{#foreach Items}}", "x" } },
        { "orphan end markers", new[] { "x", "{{/if}}", "{{/foreach}}" } },
        { "invalid condition", new[] { "{{#if Count >}}", "x", "{{/if}}" } },
        { "elseif after else", new[] { "{{#if A}}", "a", "{{#else}}", "b", "{{#elseif B}}", "c", "{{/if}}" } },
        { "invalid iteration variable", new[] { "{{#foreach in in Items}}", "x", "{{/foreach}}" } },
        { "reserved word as variable", new[] { "{{#if contains}}", "x", "{{/if}}" } },
        { "condition variables", new[] { "{{#if Status = \"A\" and Count > 2 or Missing}}", "x", "{{#elseif Other}}", "y", "{{/if}}" } },
        { "named loop scopes", new[] { "{{#foreach item in Items}}", "{{item.Title}} {{item.Nope}} {{Name}} {{Title}} {{@index}} {{@number}}", "{{/foreach}}" } },
        {
            "nested loops over item properties",
            new[]
            {
                "{{#foreach category in Categories}}", "{{category.Name}}",
                "{{#foreach product in category.Products}}", "{{category.Name}}: {{product.Name}} {{product.Nope}}", "{{/foreach}}",
                "{{#foreach Tags}}", "{{.}} {{this}}", "{{/foreach}}",
                "{{#foreach Absent}}", "{{Inner}}", "{{/foreach}}",
                "{{/foreach}}",
            }
        },
        { "implicit nested loop", new[] { "{{#foreach Categories}}", "{{#foreach Products}}", "{{Name}} {{Price}}", "{{/foreach}}", "{{/foreach}}" } },
        { "empty and missing collections", new[] { "{{#foreach Empty}}", "{{X}}", "{{/foreach}}", "{{#foreach Missing}}", "{{Y}}", "{{/foreach}}" } },
        { "null and scalar collections", new[] { "{{#foreach NullList}}", "{{X}}", "{{/foreach}}", "{{#foreach Name}}", "{{Y}}", "{{/foreach}}" } },
        { "json data", new[] { "{{#foreach Json.Rows}}", "{{Key}} {{Nope}}", "{{/foreach}}", "{{Json.Title}} {{Json.Nope}}" } },
        { "poco items with nulls", new[] { "{{#foreach Pocos}}", "{{Label}} {{Nope}}", "{{/foreach}}" } },
        { "conditional in loop", new[] { "{{#foreach item in Items}}", "{{#if item.Active}}", "{{item.Title}}", "{{/if}}", "{{/foreach}}" } },
        { "inline conditional", new[] { "{{Name}}{{#if Flag}} (flag {{Title}}){{/if}}" } },
        { "nested invalid iteration variable", new[] { "{{#foreach Items}}", "{{#foreach in in Tags}}", "x", "{{/foreach}}", "{{/foreach}}" } },
        { "unmatched loop with body", new[] { "{{Name}} {{Missing}}", "{{#foreach Items}}", "{{Title}} {{Nope}}" } },
        { "nested unmatched conditional in loop", new[] { "{{#foreach Items}}", "{{#if Active}}", "{{Title}}", "{{/foreach}}" } },
    };

    [Theory]
    [MemberData(nameof(Templates))]
    public void OdtValidation_MatchesWordValidation(string name, string[] paragraphs)
    {
        Assert.NotEmpty(name);
        OdtDocumentBuilder odt = new OdtDocumentBuilder();
        DocumentBuilder docx = new DocumentBuilder();
        foreach (string paragraph in paragraphs)
        {
            odt.AddParagraph(paragraph);
            docx.AddParagraph(paragraph);
        }

        byte[] docxBytes = docx.ToStream().ToArray();
        byte[] odtBytes = odt.ToBytes();
        Dictionary<string, object> data = CreateData();

        foreach (Dictionary<string, object>? validationData in new[] { null, data })
        {
            ValidationResult word = Validate(new DocumentTemplateProcessor(), new MemoryStream(docxBytes), validationData);
            ValidationResult openDocument = Validate(new OdtTemplateProcessor(), new MemoryStream(odtBytes), validationData);

            Assert.Equal(Describe(word.Errors), Describe(openDocument.Errors));
            Assert.Equal(word.Warnings.Select(w => $"{w.Type}: {w.Message}"), openDocument.Warnings.Select(w => $"{w.Type}: {w.Message}"));
            Assert.Equal(word.AllPlaceholders, openDocument.AllPlaceholders);
            Assert.Equal(word.MissingVariables, openDocument.MissingVariables);
            Assert.Equal(word.IsValid, openDocument.IsValid);
        }
    }

    private static ValidationResult Validate(DocumentTemplateProcessor processor, Stream stream, Dictionary<string, object>? data) =>
        data == null ? processor.ValidateTemplate(stream) : processor.ValidateTemplate(stream, data);

    private static ValidationResult Validate(OdtTemplateProcessor processor, Stream stream, Dictionary<string, object>? data) =>
        data == null ? processor.ValidateTemplate(stream) : processor.ValidateTemplate(stream, data);

    private static List<string> Describe(IEnumerable<ValidationError> errors) =>
        errors.Select(e => $"{e.Type}: {e.Message}").Order(StringComparer.Ordinal).ToList();

    private static Dictionary<string, object> CreateData() => new Dictionary<string, object>
    {
        ["Name"] = "n",
        ["Title"] = "t",
        ["Status"] = "A",
        ["Count"] = 3,
        ["A"] = true,
        ["B"] = false,
        ["Flag"] = true,
        ["contains"] = true,
        ["Customer"] = new Dictionary<string, object> { ["Name"] = "c" },
        ["List"] = new List<string> { "a" },
        ["Items"] = new List<Dictionary<string, object>>
        {
            new() { ["Title"] = "i1", ["Active"] = true },
            new() { ["Title"] = "i2" },
        },
        ["Categories"] = new List<Dictionary<string, object>>
        {
            new()
            {
                ["Name"] = "c1",
                ["Products"] = new List<Dictionary<string, object>> { new() { ["Name"] = "p1", ["Price"] = 1 } },
                ["Tags"] = new List<string> { "x" },
            },
        },
        ["Empty"] = new List<object>(),
        ["NullList"] = null!,
        ["Json"] = System.Text.Json.JsonDocument.Parse("{\"Title\":\"j\",\"Rows\":[{\"Key\":\"k\"},{\"Key\":\"l\"}]}").RootElement,
        ["Pocos"] = new List<Poco?> { new Poco("a"), null },
    };

    public sealed record Poco(string Label);
}
