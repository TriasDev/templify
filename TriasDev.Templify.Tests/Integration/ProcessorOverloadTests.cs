// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using TriasDev.Templify.Core;
using TriasDev.Templify.Tests.Helpers;

namespace TriasDev.Templify.Tests.Integration;

/// <summary>
/// The read-only data, byte array and file overloads (#156) produce the same output as the original
/// stream + <see cref="Dictionary{TKey, TValue}"/> overloads.
/// </summary>
public sealed class ProcessorOverloadTests : IDisposable
{
    private const string Json = """{ "Name": "Alice", "Count": 3, "Items": [ { "Title": "A" }, { "Title": "B" } ] }""";

    private readonly string _tempDirectory;

    public ProcessorOverloadTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "templify-overloads-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
    }

    public void Dispose()
    {
        Directory.Delete(_tempDirectory, recursive: true);
    }

    [Fact]
    public void ProcessTemplate_StreamWithReadOnlyData_MatchesDictionaryOverload()
    {
        (ProcessingResult expected, List<string> expectedTexts) = ProcessWithDictionary(CreateData());

        MemoryStream output = new MemoryStream();
        IReadOnlyDictionary<string, object?> data = new ReadOnlyDictionary<string, object?>(CreateNullableData());
        ProcessingResult result = CreateProcessor().ProcessTemplate(CreateTemplate(), output, data);

        AssertSameResult(expected, result);
        Assert.Equal(expectedTexts, ReadTexts(output));
    }

    [Fact]
    public void ProcessTemplate_StreamWithReadOnlyData_UsesTheDictionaryComparer()
    {
        Dictionary<string, object?> caseInsensitive = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["name"] = "Alice"
        };

        MemoryStream output = new MemoryStream();
        ProcessingResult result = CreateProcessor().ProcessTemplate(
            CreateTemplate("Hello {{Name}}!"),
            output,
            new ReadOnlyDictionary<string, object?>(caseInsensitive));

        Assert.True(result.IsSuccess);
        Assert.Equal(new[] { "Hello Alice!" }, ReadTexts(output));
    }

    [Fact]
    public void ProcessTemplate_StreamWithReadOnlyData_NullValueBehavesLikeDictionaryOverload()
    {
        PlaceholderReplacementOptions options = new PlaceholderReplacementOptions
        {
            Culture = CultureInfo.InvariantCulture,
            MissingVariableBehavior = MissingVariableBehavior.ReplaceWithEmpty
        };
        DocumentTemplateProcessor processor = new DocumentTemplateProcessor(options);

        MemoryStream expectedOutput = new MemoryStream();
        ProcessingResult expected = processor.ProcessTemplate(
            CreateTemplate("[{{Name}}]"), expectedOutput, new Dictionary<string, object> { ["Name"] = null! });

        MemoryStream output = new MemoryStream();
        IReadOnlyDictionary<string, object?> data = new Dictionary<string, object?> { ["Name"] = null };
        ProcessingResult result = processor.ProcessTemplate(CreateTemplate("[{{Name}}]"), output, data);

        AssertSameResult(expected, result);
        Assert.Equal(ReadTexts(expectedOutput), ReadTexts(output));
    }

    [Fact]
    public void ProcessTemplate_Bytes_AllDataShapesMatchStreamOverload()
    {
        (ProcessingResult expected, List<string> expectedTexts) = ProcessWithDictionary(CreateData());
        byte[] template = CreateTemplate().ToArray();
        byte[] templateCopy = template.ToArray();
        DocumentTemplateProcessor processor = CreateProcessor();

        ProcessingResult fromDictionary = processor.ProcessTemplate(template, CreateData(), out byte[] outputFromDictionary);
        IReadOnlyDictionary<string, object?> readOnlyData = CreateNullableData();
        ProcessingResult fromReadOnly = processor.ProcessTemplate(template, readOnlyData, out byte[] outputFromReadOnly);
        ProcessingResult fromJson = processor.ProcessTemplate(template, Json, out byte[] outputFromJson);

        AssertSameResult(expected, fromDictionary);
        AssertSameResult(expected, fromReadOnly);
        AssertSameResult(expected, fromJson);
        Assert.Equal(expectedTexts, ReadTexts(outputFromDictionary));
        Assert.Equal(expectedTexts, ReadTexts(outputFromReadOnly));
        Assert.Equal(expectedTexts, ReadTexts(outputFromJson));
        Assert.Equal(templateCopy, template);
    }

    [Fact]
    public void ProcessTemplate_Bytes_FailedResultReturnsEmptyOutput()
    {
        byte[] template = CreateTemplate("{{#if Name}}unclosed").ToArray();

        ProcessingResult result = CreateProcessor().ProcessTemplate(template, CreateData(), out byte[] output);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.ErrorMessage);
        Assert.Empty(output);
    }

    [Fact]
    public void ProcessTemplate_Bytes_MissingVariableWithThrowException_Throws()
    {
        DocumentTemplateProcessor processor = new DocumentTemplateProcessor(new PlaceholderReplacementOptions
        {
            MissingVariableBehavior = MissingVariableBehavior.ThrowException
        });
        byte[] template = CreateTemplate("Hello {{Unknown}}").ToArray();
        IReadOnlyDictionary<string, object?> data = new Dictionary<string, object?>();

        Assert.Throws<InvalidOperationException>(() => processor.ProcessTemplate(template, data, out _));
    }

    [Fact]
    public void ProcessTemplate_Bytes_NullArguments_Throw()
    {
        DocumentTemplateProcessor processor = CreateProcessor();
        byte[] template = CreateTemplate().ToArray();

        Assert.Throws<ArgumentNullException>(() => processor.ProcessTemplate(null!, CreateData(), out _));
        Assert.Throws<ArgumentNullException>(() => processor.ProcessTemplate(template, (Dictionary<string, object>)null!, out _));
        Assert.Throws<ArgumentNullException>(() => processor.ProcessTemplate(template, (IReadOnlyDictionary<string, object?>)null!, out _));
        Assert.Throws<ArgumentNullException>(() => processor.ProcessTemplate(template, (string)null!, out _));
    }

    [Fact]
    public void ProcessTemplate_Bytes_InvalidJson_ThrowsJsonException()
    {
        byte[] template = CreateTemplate().ToArray();

        Assert.ThrowsAny<JsonException>(() => CreateProcessor().ProcessTemplate(template, "[1, 2]", out _));
    }

    [Fact]
    public void ProcessTemplateFile_AllDataShapesMatchStreamOverload()
    {
        (ProcessingResult expected, List<string> expectedTexts) = ProcessWithDictionary(CreateData());
        string templatePath = WriteTemplateFile(CreateTemplate());
        DocumentTemplateProcessor processor = CreateProcessor();

        string fromDictionaryPath = Path.Combine(_tempDirectory, "dictionary.docx");
        string fromReadOnlyPath = Path.Combine(_tempDirectory, "readonly.docx");
        string fromJsonPath = Path.Combine(_tempDirectory, "json.docx");
        IReadOnlyDictionary<string, object?> readOnlyData = CreateNullableData();

        AssertSameResult(expected, processor.ProcessTemplateFile(templatePath, fromDictionaryPath, CreateData()));
        AssertSameResult(expected, processor.ProcessTemplateFile(templatePath, fromReadOnlyPath, readOnlyData));
        AssertSameResult(expected, processor.ProcessTemplateFile(templatePath, fromJsonPath, Json));

        Assert.Equal(expectedTexts, ReadTexts(File.ReadAllBytes(fromDictionaryPath)));
        Assert.Equal(expectedTexts, ReadTexts(File.ReadAllBytes(fromReadOnlyPath)));
        Assert.Equal(expectedTexts, ReadTexts(File.ReadAllBytes(fromJsonPath)));
    }

    [Fact]
    public void ProcessTemplateFile_OutputSameAsTemplate_OverwritesTemplate()
    {
        (_, List<string> expectedTexts) = ProcessWithDictionary(CreateData());
        string path = WriteTemplateFile(CreateTemplate());

        ProcessingResult result = CreateProcessor().ProcessTemplateFile(path, path, CreateData());

        Assert.True(result.IsSuccess);
        Assert.Equal(expectedTexts, ReadTexts(File.ReadAllBytes(path)));
    }

    [Fact]
    public void ProcessTemplateFile_FailedResult_DoesNotWriteOutputFile()
    {
        string templatePath = WriteTemplateFile(CreateTemplate("{{#if Name}}unclosed"));
        string outputPath = Path.Combine(_tempDirectory, "output.docx");

        ProcessingResult result = CreateProcessor().ProcessTemplateFile(templatePath, outputPath, CreateData());

        Assert.False(result.IsSuccess);
        Assert.False(File.Exists(outputPath));
    }

    [Fact]
    public void ProcessTemplateFile_MissingTemplate_ThrowsFileNotFoundException()
    {
        string templatePath = Path.Combine(_tempDirectory, "missing.docx");
        string outputPath = Path.Combine(_tempDirectory, "output.docx");

        Assert.Throws<FileNotFoundException>(() => CreateProcessor().ProcessTemplateFile(templatePath, outputPath, CreateData()));
        Assert.False(File.Exists(outputPath));
    }

    [Fact]
    public void ProcessTemplateFile_InvalidArguments_Throw()
    {
        DocumentTemplateProcessor processor = CreateProcessor();
        string templatePath = WriteTemplateFile(CreateTemplate());
        string outputPath = Path.Combine(_tempDirectory, "output.docx");

        Assert.Throws<ArgumentNullException>(() => processor.ProcessTemplateFile(null!, outputPath, CreateData()));
        Assert.Throws<ArgumentNullException>(() => processor.ProcessTemplateFile(templatePath, null!, CreateData()));
        Assert.Throws<ArgumentException>(() => processor.ProcessTemplateFile(" ", outputPath, CreateData()));
        Assert.Throws<ArgumentException>(() => processor.ProcessTemplateFile(templatePath, "", CreateData()));
        Assert.Throws<ArgumentNullException>(() => processor.ProcessTemplateFile(templatePath, outputPath, (Dictionary<string, object>)null!));
        Assert.Throws<ArgumentNullException>(() => processor.ProcessTemplateFile(templatePath, outputPath, (IReadOnlyDictionary<string, object?>)null!));
        Assert.Throws<ArgumentNullException>(() => processor.ProcessTemplateFile(templatePath, outputPath, (string)null!));
        Assert.False(File.Exists(outputPath));
    }

    [Fact]
    public void ProcessTemplate_StreamWithReadOnlyData_NullArguments_Throw()
    {
        DocumentTemplateProcessor processor = CreateProcessor();
        IReadOnlyDictionary<string, object?> data = CreateNullableData();

        Assert.Throws<ArgumentNullException>(() => processor.ProcessTemplate(null!, new MemoryStream(), data));
        Assert.Throws<ArgumentNullException>(() => processor.ProcessTemplate(CreateTemplate(), null!, data));
        Assert.Throws<ArgumentNullException>(() => processor.ProcessTemplate(CreateTemplate(), new MemoryStream(), (IReadOnlyDictionary<string, object?>)null!));
    }

    [Fact]
    public void ValidateTemplate_WithReadOnlyData_MatchesDictionaryOverload()
    {
        DocumentTemplateProcessor processor = CreateProcessor();
        Dictionary<string, object> partial = new Dictionary<string, object> { ["Name"] = "Alice" };

        ValidationResult expected = processor.ValidateTemplate(CreateTemplate(), partial);
        IReadOnlyDictionary<string, object?> readOnly = new ReadOnlyDictionary<string, object?>(
            new Dictionary<string, object?> { ["Name"] = "Alice" });
        ValidationResult result = processor.ValidateTemplate(CreateTemplate(), readOnly);

        Assert.Equal(expected.IsValid, result.IsValid);
        Assert.Equal(expected.AllPlaceholders, result.AllPlaceholders);
        Assert.Equal(expected.MissingVariables, result.MissingVariables);
        Assert.Contains("Count", result.MissingVariables);
        Assert.Throws<ArgumentNullException>(() => processor.ValidateTemplate(CreateTemplate(), (IReadOnlyDictionary<string, object?>)null!));
    }

    [Fact]
    public void TextTemplateProcessor_WithReadOnlyData_MatchesDictionaryOverload()
    {
        const string template = "Hi {{Name}} ({{Count}}): {{#foreach Items}}{{Title}};{{/foreach}}{{Missing}}";
        TextTemplateProcessor processor = new TextTemplateProcessor(new PlaceholderReplacementOptions { Culture = CultureInfo.InvariantCulture });

        TextProcessingResult expected = processor.ProcessTemplate(template, CreateData());
        IReadOnlyDictionary<string, object?> readOnly = new ReadOnlyDictionary<string, object?>(CreateNullableData());
        TextProcessingResult result = processor.ProcessTemplate(template, readOnly);

        Assert.True(result.IsSuccess);
        Assert.Equal(expected.ProcessedText, result.ProcessedText);
        Assert.Equal(expected.ReplacementCount, result.ReplacementCount);
        Assert.Equal(expected.MissingVariables, result.MissingVariables);
        Assert.Throws<ArgumentNullException>(() => processor.ProcessTemplate(template, (IReadOnlyDictionary<string, object?>)null!));
        Assert.Throws<ArgumentNullException>(() => processor.ProcessTemplate(null!, readOnly));
    }

    private static DocumentTemplateProcessor CreateProcessor()
    {
        return new DocumentTemplateProcessor(new PlaceholderReplacementOptions { Culture = CultureInfo.InvariantCulture });
    }

    private static MemoryStream CreateTemplate()
    {
        return CreateTemplate("Hello {{Name}}, count {{Count}}.", "{{#foreach Items}}", "- {{Title}}", "{{/foreach}}", "Missing: {{Missing}}");
    }

    private static MemoryStream CreateTemplate(params string[] paragraphs)
    {
        DocumentBuilder builder = new DocumentBuilder();
        foreach (string paragraph in paragraphs)
        {
            builder.AddParagraph(paragraph);
        }

        return builder.ToStream();
    }

    private static Dictionary<string, object> CreateData()
    {
        return new Dictionary<string, object>
        {
            ["Name"] = "Alice",
            ["Count"] = 3,
            ["Items"] = new List<Dictionary<string, object>>
            {
                new Dictionary<string, object> { ["Title"] = "A" },
                new Dictionary<string, object> { ["Title"] = "B" }
            }
        };
    }

    private static Dictionary<string, object?> CreateNullableData()
    {
        return CreateData().ToDictionary(pair => pair.Key, pair => (object?)pair.Value);
    }

    private static (ProcessingResult Result, List<string> Texts) ProcessWithDictionary(Dictionary<string, object> data)
    {
        MemoryStream output = new MemoryStream();
        ProcessingResult result = CreateProcessor().ProcessTemplate(CreateTemplate(), output, data);
        Assert.True(result.IsSuccess, result.ErrorMessage);
        return (result, ReadTexts(output));
    }

    private static void AssertSameResult(ProcessingResult expected, ProcessingResult actual)
    {
        Assert.True(actual.IsSuccess, actual.ErrorMessage);
        Assert.Equal(expected.ReplacementCount, actual.ReplacementCount);
        Assert.Equal(expected.MissingVariables, actual.MissingVariables);
        Assert.Equal(expected.Warnings.Count, actual.Warnings.Count);
    }

    private static List<string> ReadTexts(MemoryStream output)
    {
        using DocumentVerifier verifier = new DocumentVerifier(output);
        return verifier.GetAllParagraphTexts();
    }

    private static List<string> ReadTexts(byte[] output)
    {
        return ReadTexts(new MemoryStream(output));
    }

    private string WriteTemplateFile(MemoryStream template)
    {
        string path = Path.Combine(_tempDirectory, Guid.NewGuid().ToString("N") + ".docx");
        File.WriteAllBytes(path, template.ToArray());
        return path;
    }
}
