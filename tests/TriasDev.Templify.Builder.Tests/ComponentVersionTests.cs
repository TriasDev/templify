// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using DocumentFormat.OpenXml.CustomProperties;
using DocumentFormat.OpenXml.Packaging;
using TriasDev.Templify.Builder.Json;
using TriasDev.Templify.Core;

namespace TriasDev.Templify.Builder.Tests;

/// <summary>Component versions in the template stamp (#294).</summary>
public sealed class ComponentVersionTests
{
    [ComponentVersion(2)]
    public sealed record Terms : ITemplateComponent
    {
        public void Compose(BlockComposer content) => content.Paragraph("Terms");
    }

    public sealed record Card(string Title) : ITemplateComponent
    {
        public void Compose(BlockComposer content) => content.Paragraph(Title);
    }

    [ComponentVersion(3)]
    public sealed record Box<T>(string Title) : ITemplateComponent
    {
        public void Compose(BlockComposer content) => content.Paragraph(Title);
    }

    public static TheoryData<TemplateFormat> Formats => new TheoryData<TemplateFormat>(BuilderHarness.Formats);

    private static TestDefinition WithComponents() => new TestDefinition(t => t.Content(c => c
        .Component(new Terms())
        .Component(new Card("a"))
        .Component(new Card("b"))
        .Component(new Box<int>("box"))
        .Section("intro", s => s.Paragraph("Intro"))));

    [Theory]
    [MemberData(nameof(Formats))]
    public void Stamp_RecordsComponentsAndVersions(TemplateFormat format)
    {
        GeneratedTemplate template = BuilderHarness.Build(WithComponents(), format);

        Assert.Equal(
            [new KeyValuePair<string, int>("Box", 3), new KeyValuePair<string, int>("Terms", 2)],
            template.Stamp.Components.OrderBy(c => c.Key, StringComparer.Ordinal));
        TemplateStamp? read = TemplateInspector.Inspect(template.OpenRead()).Stamp;
        Assert.Equal(template.Stamp, read);
    }

    [Fact]
    public void Stamp_UnversionedComponentsAndSections_AreNotRecorded()
    {
        GeneratedTemplate template = BuilderHarness.Build(new TestDefinition(t => t.Content(c => c.Component(new Card("a")).Section("intro", s => s.Paragraph("Intro")))), TemplateFormat.Docx);

        Assert.Empty(template.Stamp.Components);
        Assert.DoesNotContain("Templify.Components", PropertyNames(template.Content.ToArray()));
    }

    [Fact]
    public void Stamp_WithoutComponentProperty_StillReads()
    {
        GeneratedTemplate template = BuilderHarness.Build(WithComponents(), TemplateFormat.Docx);
        byte[] without = RemoveProperty(template.Content.ToArray(), "Templify.Components");

        TemplateStamp? read = TemplateInspector.Inspect(new MemoryStream(without)).Stamp;

        Assert.NotNull(read);
        Assert.Empty(read.Components);
        Assert.Equal(template.Stamp.TemplateId, read.TemplateId);
    }

    [Fact]
    public void Stamp_ComponentProperty_IsSortedPairs()
    {
        GeneratedTemplate template = BuilderHarness.Build(WithComponents(), TemplateFormat.Docx);

        Assert.Equal("Box=3;Terms=2", PropertyValue(template.Content.ToArray(), "Templify.Components"));
    }

    [Theory]
    [MemberData(nameof(Formats))]
    public void Json_RegisteredVersionedComponent_KeepsTheStamp(TemplateFormat format)
    {
        TestDefinition definition = new TestDefinition(t => t.Content(c => c.Component(new Terms()).Component(new Card("a"))));
        ComponentCatalog components = new ComponentCatalog().Register<Terms>("terms");
        TemplateBuildOptions options = BuilderHarness.Options(format, "de-DE");

        string json = TemplateJson.Export(definition, options, components).Json;
        GeneratedTemplate fromJson = TemplateBuilder.Build(TemplateJson.Import(json, new TemplateJsonImportOptions { Components = components }), options);

        Assert.Equal(TemplateBuilder.Build(definition, options).Content.ToArray(), fromJson.Content.ToArray());
        Assert.Equal(2, fromJson.Stamp.Components["Terms"]);
    }

    [Fact]
    public void Json_UnregisteredVersionedComponent_IsNotExportable()
    {
        TestDefinition definition = new TestDefinition(t => t.Content(c => c.Component(new Terms())));

        TemplateBuildException error = Assert.Throws<TemplateBuildException>(() => TemplateJson.Export(definition, BuilderHarness.Options(TemplateFormat.Docx, "de-DE")));

        BuildDiagnostic problem = Assert.Single(error.Errors);
        Assert.Equal("TB9204", problem.Code);
        Assert.Contains("Terms", problem.Message, StringComparison.Ordinal);
        Assert.Contains("register", problem.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ComponentVersion_MustBePositive()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ComponentVersionAttribute(0));
    }

    private static List<string> PropertyNames(byte[] docx)
    {
        using MemoryStream stream = new MemoryStream(docx);
        using WordprocessingDocument document = WordprocessingDocument.Open(stream, false);
        return document.CustomFilePropertiesPart!.Properties!.Elements<CustomDocumentProperty>().Select(p => p.Name!.Value!).ToList();
    }

    private static string? PropertyValue(byte[] docx, string name)
    {
        using MemoryStream stream = new MemoryStream(docx);
        using WordprocessingDocument document = WordprocessingDocument.Open(stream, false);
        return document.CustomFilePropertiesPart!.Properties!.Elements<CustomDocumentProperty>().FirstOrDefault(p => p.Name!.Value == name)?.InnerText;
    }

    private static byte[] RemoveProperty(byte[] docx, string name)
    {
        using MemoryStream stream = new MemoryStream();
        stream.Write(docx);
        using (WordprocessingDocument document = WordprocessingDocument.Open(stream, true))
        {
            Properties properties = document.CustomFilePropertiesPart!.Properties!;
            properties.Elements<CustomDocumentProperty>().Single(p => p.Name!.Value == name).Remove();
            properties.Save();
        }

        return stream.ToArray();
    }
}
