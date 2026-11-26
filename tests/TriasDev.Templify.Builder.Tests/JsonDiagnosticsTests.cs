// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using TriasDev.Templify.Builder.Json;
using TriasDev.Templify.Core;

namespace TriasDev.Templify.Builder.Tests;

/// <summary>Import diagnostics with JSON pointers (#274).</summary>
public sealed class JsonDiagnosticsTests
{
    private static string Definition(string content, string styles = "{}") => $$"""
        {
          "templifyDefinition": 1,
          "identity": { "id": "diag", "version": 1 },
          "language": "en-US",
          "styles": {{styles}},
          "content": {{content}}
        }
        """;

    private static IReadOnlyList<BuildDiagnostic> ImportErrors(string json, TemplateJsonImportOptions? options = null) =>
        Assert.Throws<TemplateBuildException>(() => TemplateJson.Import(json, options)).Errors;

    private static IReadOnlyList<BuildDiagnostic> BuildErrors(string json) =>
        Assert.Throws<TemplateBuildException>(() => TemplateBuilder.Build(TemplateJson.Import(json), BuilderHarness.Options(TemplateFormat.Docx))).Errors;

    [Fact]
    public void Import_ReportsEveryStructuralProblemAtOnce()
    {
        string json = Definition("""
            [
              { "paragraph": "ok" },
              { "paragrahp": "typo" },
              { "paragraph": { "style": 5, "content": "x" } },
              { "paragraph": { "content": "x", "colour": "red" } },
              { "paragraph": { "content": [ { "image": "sha256:missing", "alt": "a", "width": "10pt", "height": "10pt" } ] } }
            ]
            """);

        IReadOnlyList<BuildDiagnostic> errors = ImportErrors(json);

        Assert.All(errors, e => Assert.Equal("TB9200", e.Code));
        Assert.Equal(
            new[] { "/content/1", "/content/2/paragraph/style", "/content/3/paragraph/colour", "/content/4/paragraph/content/0/image" },
            errors.Select(e => e.Location));
    }

    [Fact]
    public void Build_ComposerArgumentErrors_BecomeTB9201_AndTheRestIsStillChecked()
    {
        string json = Definition(
            """
            [
              { "list": { "style": "Bullets", "items": [ { "item": { "level": 12, "content": "deep" } } ] } },
              { "rule": { "line": "0pt #000000" } },
              { "paragraph": { "style": "Missing", "content": "x" } }
            ]
            """,
            """{ "list": { "Bullets": { "levels": [ { "level": 0, "bullet": "•", "indent": "6mm", "hanging": "4mm" } ] } } }""");

        IReadOnlyList<BuildDiagnostic> errors = BuildErrors(json);

        Assert.Contains(errors, e => e.Code == "TB9201" && e.Location == "/content/0/list/items/0");
        Assert.Contains(errors, e => e.Code == "TB9201" && e.Location == "/content/1");
        Assert.Contains(errors, e => e.Code == "TB2010" && e.Location == "/content/2");
    }

    [Fact]
    public void Build_ScopeErrorsCarryTheirLocation()
    {
        string json = Definition("""
            [
              { "paragraph": { "content": [ "{{@index}}" ] } }
            ]
            """);

        BuildDiagnostic error = Assert.Single(BuildErrors(json));

        Assert.Equal("TB9201", error.Code);
        Assert.Equal("/content/0/paragraph/content/0", error.Location);
        Assert.Contains("@index", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Import_RawXml_RefusedByDefault()
    {
        string json = Definition("""
            [ { "raw": { "docx": "<p xmlns=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"/>", "odt": "<p xmlns=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\"/>", "uses": [] } } ]
            """);

        BuildDiagnostic error = Assert.Single(ImportErrors(json));

        Assert.Equal("TB9202", error.Code);
        Assert.Equal("/content/0/raw", error.Location);
        BuilderHarness.Build(TemplateJson.Import(json, new TemplateJsonImportOptions { AllowRawXml = true }), TemplateFormat.Docx);
    }

    [Fact]
    public void Import_InvalidJsonText()
    {
        BuildDiagnostic error = Assert.Single(ImportErrors("{"));

        Assert.Equal("TB9200", error.Code);
        Assert.Equal(string.Empty, error.Location);
        Assert.Contains("LineNumber", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Import_UnknownVersion()
    {
        BuildDiagnostic error = Assert.Single(ImportErrors(Definition("[]").Replace("\"templifyDefinition\": 1", "\"templifyDefinition\": 2", StringComparison.Ordinal)));

        Assert.Equal("TB9200", error.Code);
        Assert.Equal("/templifyDefinition", error.Location);
    }

    [Fact]
    public void Import_UnknownTopLevelKeyAndBadCondition()
    {
        string json = Definition("""
            [ { "if": "status = paid", "then": [ { "paragraph": "x" } ] } ]
            """).Replace("\"language\"", "\"extra\": 1, \"language\"", StringComparison.Ordinal);

        IReadOnlyList<BuildDiagnostic> errors = ImportErrors(json);

        Assert.Contains(errors, e => e.Location == "/extra");
        BuildDiagnostic condition = Assert.Single(errors, e => e.Location == "/content/0/if");
        Assert.Contains("quote", condition.Message, StringComparison.Ordinal);
    }
}
