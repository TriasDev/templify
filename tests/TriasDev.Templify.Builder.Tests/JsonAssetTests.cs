// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Text.Json;
using TriasDev.Templify.Builder.Json;
using TriasDev.Templify.Core;

namespace TriasDev.Templify.Builder.Tests;

/// <summary>Named assets and images with one size (#293, style review A6).</summary>
public sealed class JsonAssetTests
{
    // 40 × 20 pixels: the height is half the width.
    private static readonly byte[] _png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAACgAAAAUCAIAAABwJOjsAAAACXBIWXMAAB2HAAAdhwGP5fFlAAAAJElEQVR42mM4ISc3IIhh1OJRi0ctHrV41OJRi0ctHrV45FgMAFLhLK5O4QWwAAAAAElFTkSuQmCC");

    public static TheoryData<TemplateFormat> Formats => new TheoryData<TemplateFormat>(BuilderHarness.Formats);

    private static TestDefinition Pictures() => new TestDefinition(t => t.Content(c =>
    {
        c.Image(ImageSource.Png(_png).WithName("logo"), "Logo", ImageFit.Height(12.Pt()));
        c.Image(ImageSource.Png(_png), "Unnamed", ImageFit.Width(30.Pt()));
        c.Image(ImageSource.Png(_png), "Squeezed", ImageFit.Exact(30.Pt(), 30.Pt()));
    }));

    private static JsonElement Root(string json) => JsonDocument.Parse(json).RootElement;

    [Fact]
    public void Export_NamesAssets_AndWritesOneSizeWhenTheOtherFollows()
    {
        TemplateJsonDocument exported = TemplateJson.Export(Pictures(), BuilderHarness.Options(TemplateFormat.Docx));
        JsonElement root = Root(exported.Json);

        JsonElement assets = root.GetProperty("assets");
        Assert.Equal(new[] { "logo", "image-1" }, assets.EnumerateObject().Select(p => p.Name));
        Assert.StartsWith("sha256:", assets.GetProperty("logo").GetString(), StringComparison.Ordinal);
        Assert.Equal(new[] { "templifyDefinition", "identity", "assets" }, root.EnumerateObject().Select(p => p.Name).Take(3));

        List<JsonElement> images = root.GetProperty("content").EnumerateArray().Select(b => b.GetProperty("paragraph")[0]).ToList();
        Assert.Equal("""{"image":"logo","alt":"Logo","height":"12pt"}""", JsonSerializer.Serialize(images[0]));
        // Width 30 pt gives height 15 pt; the export writes the height when the width follows from it.
        Assert.Equal("""{"image":"image-1","alt":"Unnamed","height":"15pt"}""", JsonSerializer.Serialize(images[1]));
        Assert.Equal("""{"image":"image-1","alt":"Squeezed","width":"30pt","height":"30pt"}""", JsonSerializer.Serialize(images[2]));
    }

    [Theory]
    [MemberData(nameof(Formats))]
    public void Import_ResolvesNamesAndTheMissingSize(TemplateFormat format)
    {
        TemplateJsonDocument exported = TemplateJson.Export(Pictures(), BuilderHarness.Options(format));
        ITemplateDefinition imported = TemplateJson.Import(exported.Json, new TemplateJsonImportOptions { Assets = exported.Assets });

        Assert.Equal(BuilderHarness.Build(Pictures(), format).ContentHash, BuilderHarness.Build(imported, format).ContentHash);
        Assert.Equal(exported.Json, TemplateJson.Export(imported, BuilderHarness.Options(format)).Json);
    }

    [Theory]
    [InlineData("""{ "logo": "sha256:missing" }""", """[ { "paragraph": [ { "image": "logo", "alt": "a", "height": "10pt" } ] } ]""", "/assets/logo")]
    [InlineData("""{}""", """[ { "paragraph": [ { "image": "logo", "alt": "a", "height": "10pt" } ] } ]""", "/content/0/paragraph/0/image")]
    [InlineData("""{ "bad name": "sha256:x" }""", """[ "x" ]""", "/assets/bad name")]
    public void AssetErrors(string assets, string content, string location)
    {
        string json = $$"""
            { "templifyDefinition": 1, "identity": { "id": "a", "version": 1 }, "assets": {{assets}}, "content": {{content}} }
            """;

        IReadOnlyList<BuildDiagnostic> errors = Assert.Throws<TemplateBuildException>(() => TemplateJson.Import(json)).Errors;

        Assert.All(errors, e => Assert.Equal("TB9200", e.Code));
        Assert.Contains(errors, e => e.Location == location);
    }
}
