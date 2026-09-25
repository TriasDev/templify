// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using TriasDev.Templify.Converter.Converters;
using static TriasDev.Templify.Converter.Tests.TestDocuments;

namespace TriasDev.Templify.Converter.Tests;

public class DocumentCleanerTests
{
    /// <summary>Six SDTs, some nested, one in the header.</summary>
    private static void CreateNested(string path)
    {
        Create(
            path,
            body: new OpenXmlElement[]
            {
                BlockControl("outer", BlockControl("inner", Para(TextRun("A "), InlineVariable("x", "X")))),
                Para(TextRun("B "), InlineControl("run", new Hyperlink(TextRun("link")) { Anchor = "top" })),
                Table(RowControl("row", Row(Cell(TextRun("C1")), Cell(TextRun("C2"))))),
            },
            header: new OpenXmlElement[] { Para(TextRun("Head "), InlineVariable("h", "H")) });
    }

    [Fact]
    public void CleanDocument_InPlace_KeepsValidFileAndCountsEachControlOnce()
    {
        using TempDirectory dir = new();
        string path = dir.File("doc.docx");
        CreateNested(path);

        int removed = DocumentCleaner.CleanDocument(path);

        Assert.Equal(6, removed);
        Assert.Equal(0, CountSdts(path));
        Assert.Empty(SchemaErrors(path));
        List<string> paragraphs = BodyParagraphs(path);
        Assert.Contains("A X", paragraphs);
        Assert.Contains("B link", paragraphs);
        Assert.Contains("C1", paragraphs);
        Assert.Equal("Head H", HeaderText(path));
        Assert.Empty(Directory.GetFiles(dir.Path, "*.tmp"));

        using WordprocessingDocument doc = WordprocessingDocument.Open(path, false);
        Assert.Single(doc.MainDocumentPart!.Document!.Body!.Descendants<Hyperlink>());
    }

    [Fact]
    public void CleanDocument_SamePathWrittenDifferently_IsTreatedAsInPlace()
    {
        using TempDirectory dir = new();
        string path = dir.File("doc.docx");
        CreateNested(path);
        string alias = Path.Combine(dir.Path, "sub", "..", "doc.docx");
        Directory.CreateDirectory(Path.Combine(dir.Path, "sub"));

        Assert.True(SafeFileWriter.IsSamePath(path, alias));

        DocumentCleaner.CleanDocument(path, alias);

        Assert.Equal(0, CountSdts(path));
        Assert.Empty(SchemaErrors(path));
        Assert.Single(Directory.GetFiles(dir.Path, "*.docx"));
    }

    [Fact]
    public void CleanDocument_WithOutput_LeavesInputUntouched()
    {
        using TempDirectory dir = new();
        string input = dir.File("in.docx");
        string output = dir.File("out.docx");
        CreateNested(input);
        byte[] before = File.ReadAllBytes(input);

        DocumentCleaner.CleanDocument(input, output);

        Assert.Equal(before, File.ReadAllBytes(input));
        Assert.Equal(0, CountSdts(output));
    }

    [Fact]
    public void CleanDocument_WhenProcessingFails_InputIsPreserved()
    {
        using TempDirectory dir = new();
        string path = dir.File("broken.docx");
        byte[] garbage = { 1, 2, 3, 4, 5 };
        File.WriteAllBytes(path, garbage);

        Assert.ThrowsAny<Exception>(() => DocumentCleaner.CleanDocument(path));

        Assert.Equal(garbage, File.ReadAllBytes(path));
        Assert.Single(Directory.GetFiles(dir.Path));
    }

    [Fact]
    public void CleanDocument_OutputIsReadableByTemplify()
    {
        using TempDirectory dir = new();
        string path = dir.File("doc.docx");
        CreateNested(path);
        DocumentCleaner.CleanDocument(path);

        using FileStream stream = File.OpenRead(path);
        using MemoryStream output = new();
        Core.ProcessingResult result = new Core.DocumentTemplateProcessor().ProcessTemplate(stream, output, new Dictionary<string, object>());

        Assert.True(result.IsSuccess, result.ErrorMessage);
    }
}
