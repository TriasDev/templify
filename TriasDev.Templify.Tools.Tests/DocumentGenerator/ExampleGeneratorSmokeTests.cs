// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using DocumentFormat.OpenXml.Packaging;
using TriasDev.Templify.Core;
using TriasDev.Templify.DocumentGenerator;
using TriasDev.Templify.Tools.Tests.Gui;

namespace TriasDev.Templify.Tools.Tests.DocumentGenerator;

/// <summary>
/// Smoke tests over every registered documentation example generator (Stirling-PDF is not involved).
/// </summary>
public sealed class ExampleGeneratorSmokeTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    public static TheoryData<string> GeneratorNames()
    {
        TheoryData<string> data = new();
        foreach (IExampleGenerator generator in ExampleGenerators.All)
        {
            data.Add(generator.Name);
        }

        return data;
    }

    private static IExampleGenerator GetGenerator(string name) =>
        ExampleGenerators.All.Single(g => g.Name == name);

    private static string ReadDocumentText(Stream stream)
    {
        using WordprocessingDocument document = WordprocessingDocument.Open(stream, false);
        return document.MainDocumentPart!.Document!.Body!.InnerText;
    }

    [Theory]
    [MemberData(nameof(GeneratorNames))]
    public void Generator_TemplateProcessesWithoutWarningsOrLeftoverPlaceholders(string name)
    {
        IExampleGenerator generator = GetGenerator(name);
        string templatePath = generator.GenerateTemplate(_temp.Path);

        DocumentTemplateProcessor processor = new();
        using FileStream templateStream = File.OpenRead(templatePath);
        using MemoryStream output = new();
        ProcessingResult result = processor.ProcessTemplate(templateStream, output, generator.GetSampleData());

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Empty(result.Warnings);
        Assert.Empty(result.MissingVariables);

        output.Position = 0;
        string text = ReadDocumentText(output);
        Assert.DoesNotContain("{{", text);
        Assert.DoesNotContain("}}", text);
        Assert.False(string.IsNullOrWhiteSpace(text));
    }

    [Theory]
    [MemberData(nameof(GeneratorNames))]
    public void Generator_ProcessTemplate_WritesOutputFile(string name)
    {
        IExampleGenerator generator = GetGenerator(name);
        string templatePath = generator.GenerateTemplate(_temp.Path);

        string outputPath = generator.ProcessTemplate(templatePath, _temp.Path);

        Assert.True(File.Exists(outputPath));
        using FileStream stream = File.OpenRead(outputPath);
        Assert.DoesNotContain("{{", ReadDocumentText(stream));
    }

    [Fact]
    public void Generators_HaveUniqueNames()
    {
        List<string> names = ExampleGenerators.All.Select(g => g.Name).ToList();

        Assert.Equal(names.Count, names.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void Generators_SampleDataIsDeterministic()
    {
        foreach (IExampleGenerator generator in ExampleGenerators.All)
        {
            Dictionary<string, object> first = generator.GetSampleData();
            Dictionary<string, object> second = generator.GetSampleData();

            foreach ((string key, object value) in first)
            {
                if (value is string text)
                {
                    Assert.Equal(text, second[key]);
                }
            }
        }
    }

    [Fact]
    public void AdvancedConditionals_EvaluatesOperatorsAsIntended()
    {
        IExampleGenerator generator = GetGenerator("advanced-conditionals");
        string templatePath = generator.GenerateTemplate(_temp.Path);

        DocumentTemplateProcessor processor = new();
        using FileStream templateStream = File.OpenRead(templatePath);
        using MemoryStream output = new();
        processor.ProcessTemplate(templateStream, output, generator.GetSampleData());
        output.Position = 0;
        string text = ReadDocumentText(output);

        Assert.Contains("Silver member - 640 points", text);          // elseif
        Assert.DoesNotContain("Gold member", text);
        Assert.Contains("You can publish articles.", text);            // in (literal list)
        Assert.Contains("Local support is available in Germany.", text); // in (collection)
        Assert.Contains("requires urgent attention", text);            // contains
        Assert.Contains("stored in the EU region", text);              // startswith
        Assert.Contains("A nickname field is present", text);          // exists
        Assert.Contains("You have not set a nickname yet.", text);     // is empty
        Assert.Contains("Your interests: Hiking, Photography", text);  // is not empty
        Assert.Contains("You qualify for the partner lounge.", text);  // grouping
    }

    [Fact]
    public void RepositoryPaths_FindsRootFromTestOutputDirectory()
    {
        string? root = RepositoryPaths.FindRepositoryRoot(AppContext.BaseDirectory);

        Assert.NotNull(root);
        Assert.True(File.Exists(Path.Combine(root, "templify.sln")));
        Assert.Equal(Path.Combine(root, "docs", "images", "examples"), RepositoryPaths.ImagesDirectory(root));
    }
}
