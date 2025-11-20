// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using TriasDev.Templify.Converter.Converters;

namespace TriasDev.Templify.Tests;

/// <summary>
/// Tests for the ConditionalConverter class.
/// </summary>
public class ConditionalConverterTests
{
    [Fact]
    public void Convert_SimpleNegation_GeneratesCorrectSyntax()
    {
        // Arrange: Create a document with a content control tagged "conditionalRemove_field_not"
        using MemoryStream stream = new MemoryStream();
        using (WordprocessingDocument doc = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            MainDocumentPart mainPart = doc.AddMainDocumentPart();
            mainPart.Document = new Document(new Body());
            Body body = mainPart.Document.Body;

            // Create content control: conditionalRemove_field_not
            SdtBlock sdt = new SdtBlock(
                new SdtProperties(
                    new Tag { Val = "conditionalRemove_field_not" }
                ),
                new SdtContentBlock(
                    new Paragraph(new Run(new Text("Content")))
                )
            );
            body.AppendChild(sdt);
            doc.Save();
        }

        // Act: Convert the document
        stream.Position = 0;
        using (WordprocessingDocument doc = WordprocessingDocument.Open(stream, true))
        {
            Body body = doc.MainDocumentPart!.Document.Body!;
            SdtBlock sdt = body.Elements<SdtBlock>().First();

            ConditionalConverter converter = new ConditionalConverter();
            bool result = converter.Convert(sdt, "conditionalRemove_field_not");

            Assert.True(result);
        }

        // Assert: Check the generated content
        stream.Position = 0;
        using (WordprocessingDocument doc = WordprocessingDocument.Open(stream, false))
        {
            Body body = doc.MainDocumentPart!.Document.Body!;
            string bodyText = body.InnerText;

            // Should generate: {{#if not field}}Content{{/if}}
            Assert.Contains("{{#if not field}}", bodyText);
            Assert.Contains("{{/if}}", bodyText);
            Assert.DoesNotContain("(", bodyText); // No parentheses
            Assert.DoesNotContain(")", bodyText);
        }
    }

    [Fact]
    public void Convert_EqualityComparison_GeneratesCorrectSyntax()
    {
        // Arrange: Create a document with "conditionalRemove_key_eq_other"
        using MemoryStream stream = new MemoryStream();
        using (WordprocessingDocument doc = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            MainDocumentPart mainPart = doc.AddMainDocumentPart();
            mainPart.Document = new Document(new Body());
            Body body = mainPart.Document.Body;

            SdtBlock sdt = new SdtBlock(
                new SdtProperties(
                    new Tag { Val = "conditionalRemove_key_eq_other" }
                ),
                new SdtContentBlock(
                    new Paragraph(new Run(new Text("Content")))
                )
            );
            body.AppendChild(sdt);
            doc.Save();
        }

        // Act: Convert
        stream.Position = 0;
        using (WordprocessingDocument doc = WordprocessingDocument.Open(stream, true))
        {
            Body body = doc.MainDocumentPart!.Document.Body!;
            SdtBlock sdt = body.Elements<SdtBlock>().First();

            ConditionalConverter converter = new ConditionalConverter();
            converter.Convert(sdt, "conditionalRemove_key_eq_other");
        }

        // Assert
        stream.Position = 0;
        using (WordprocessingDocument doc = WordprocessingDocument.Open(stream, false))
        {
            Body body = doc.MainDocumentPart!.Document.Body!;
            string bodyText = body.InnerText;

            // Should generate: {{#if key = "other"}}Content{{/if}}
            Assert.Contains("{{#if key = \"other\"}}", bodyText);
            Assert.Contains("{{/if}}", bodyText);
        }
    }

    [Fact]
    public void Convert_InequalityComparison_GeneratesCorrectSyntax()
    {
        // Arrange: Create a document with "conditionalRemove_key_ne_other"
        using MemoryStream stream = new MemoryStream();
        using (WordprocessingDocument doc = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            MainDocumentPart mainPart = doc.AddMainDocumentPart();
            mainPart.Document = new Document(new Body());
            Body body = mainPart.Document.Body;

            SdtBlock sdt = new SdtBlock(
                new SdtProperties(
                    new Tag { Val = "conditionalRemove_key_ne_other" }
                ),
                new SdtContentBlock(
                    new Paragraph(new Run(new Text("Content")))
                )
            );
            body.AppendChild(sdt);
            doc.Save();
        }

        // Act: Convert
        stream.Position = 0;
        using (WordprocessingDocument doc = WordprocessingDocument.Open(stream, true))
        {
            Body body = doc.MainDocumentPart!.Document.Body!;
            SdtBlock sdt = body.Elements<SdtBlock>().First();

            ConditionalConverter converter = new ConditionalConverter();
            converter.Convert(sdt, "conditionalRemove_key_ne_other");
        }

        // Assert
        stream.Position = 0;
        using (WordprocessingDocument doc = WordprocessingDocument.Open(stream, false))
        {
            Body body = doc.MainDocumentPart!.Document.Body!;
            string bodyText = body.InnerText;

            // Should generate: {{#if key != "other"}}Content{{/if}}
            Assert.Contains("{{#if key != \"other\"}}", bodyText);
            Assert.Contains("{{/if}}", bodyText);
        }
    }

    [Fact]
    public void Convert_NotWithComparison_GeneratesValidSyntax()
    {
        // Arrange: Create a document with "conditionalRemove_key_eq_other_not"
        // This is a semantically questionable tag, but should at least generate valid syntax
        using MemoryStream stream = new MemoryStream();
        using (WordprocessingDocument doc = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            MainDocumentPart mainPart = doc.AddMainDocumentPart();
            mainPart.Document = new Document(new Body());
            Body body = mainPart.Document.Body;

            SdtBlock sdt = new SdtBlock(
                new SdtProperties(
                    new Tag { Val = "conditionalRemove_key_eq_other_not" }
                ),
                new SdtContentBlock(
                    new Paragraph(new Run(new Text("Content")))
                )
            );
            body.AppendChild(sdt);
            doc.Save();
        }

        // Act: Convert
        stream.Position = 0;
        using (WordprocessingDocument doc = WordprocessingDocument.Open(stream, true))
        {
            Body body = doc.MainDocumentPart!.Document.Body!;
            SdtBlock sdt = body.Elements<SdtBlock>().First();

            ConditionalConverter converter = new ConditionalConverter();
            converter.Convert(sdt, "conditionalRemove_key_eq_other_not");
        }

        // Assert
        stream.Position = 0;
        using (WordprocessingDocument doc = WordprocessingDocument.Open(stream, false))
        {
            Body body = doc.MainDocumentPart!.Document.Body!;
            string bodyText = body.InnerText;

            // Should generate: {{#if not key = "other"}}Content{{/if}}
            // No parentheses should be present
            Assert.Contains("{{#if not key = \"other\"}}", bodyText);
            Assert.Contains("{{/if}}", bodyText);
            Assert.DoesNotContain("(", bodyText);
            Assert.DoesNotContain(")", bodyText);
        }
    }

    [Fact]
    public void Convert_NumericComparison_GeneratesCorrectSyntax()
    {
        // Arrange: Create a document with "conditionalRemove_count_gt_5"
        using MemoryStream stream = new MemoryStream();
        using (WordprocessingDocument doc = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            MainDocumentPart mainPart = doc.AddMainDocumentPart();
            mainPart.Document = new Document(new Body());
            Body body = mainPart.Document.Body;

            SdtBlock sdt = new SdtBlock(
                new SdtProperties(
                    new Tag { Val = "conditionalRemove_count_gt_5" }
                ),
                new SdtContentBlock(
                    new Paragraph(new Run(new Text("Content")))
                )
            );
            body.AppendChild(sdt);
            doc.Save();
        }

        // Act: Convert
        stream.Position = 0;
        using (WordprocessingDocument doc = WordprocessingDocument.Open(stream, true))
        {
            Body body = doc.MainDocumentPart!.Document.Body!;
            SdtBlock sdt = body.Elements<SdtBlock>().First();

            ConditionalConverter converter = new ConditionalConverter();
            converter.Convert(sdt, "conditionalRemove_count_gt_5");
        }

        // Assert
        stream.Position = 0;
        using (WordprocessingDocument doc = WordprocessingDocument.Open(stream, false))
        {
            Body body = doc.MainDocumentPart!.Document.Body!;
            string bodyText = body.InnerText;

            // Should generate: {{#if count > 5}}Content{{/if}}
            // Numeric values should not be quoted
            Assert.Contains("{{#if count > 5}}", bodyText);
            Assert.Contains("{{/if}}", bodyText);
            Assert.DoesNotContain("\"5\"", bodyText);
        }
    }

    [Fact]
    public void Convert_SimpleExistenceCheck_GeneratesCorrectSyntax()
    {
        // Arrange: Create a document with "conditionalRemove_field"
        using MemoryStream stream = new MemoryStream();
        using (WordprocessingDocument doc = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            MainDocumentPart mainPart = doc.AddMainDocumentPart();
            mainPart.Document = new Document(new Body());
            Body body = mainPart.Document.Body;

            SdtBlock sdt = new SdtBlock(
                new SdtProperties(
                    new Tag { Val = "conditionalRemove_field" }
                ),
                new SdtContentBlock(
                    new Paragraph(new Run(new Text("Content")))
                )
            );
            body.AppendChild(sdt);
            doc.Save();
        }

        // Act: Convert
        stream.Position = 0;
        using (WordprocessingDocument doc = WordprocessingDocument.Open(stream, true))
        {
            Body body = doc.MainDocumentPart!.Document.Body!;
            SdtBlock sdt = body.Elements<SdtBlock>().First();

            ConditionalConverter converter = new ConditionalConverter();
            converter.Convert(sdt, "conditionalRemove_field");
        }

        // Assert
        stream.Position = 0;
        using (WordprocessingDocument doc = WordprocessingDocument.Open(stream, false))
        {
            Body body = doc.MainDocumentPart!.Document.Body!;
            string bodyText = body.InnerText;

            // Should generate: {{#if field}}Content{{/if}}
            Assert.Contains("{{#if field}}", bodyText);
            Assert.Contains("{{/if}}", bodyText);
        }
    }

    [Fact]
    public void Convert_NonConditionalTag_ReturnsFalse()
    {
        // Arrange: Create a document with a non-conditional tag
        using MemoryStream stream = new MemoryStream();
        using (WordprocessingDocument doc = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            MainDocumentPart mainPart = doc.AddMainDocumentPart();
            mainPart.Document = new Document(new Body());
            Body body = mainPart.Document.Body;

            SdtBlock sdt = new SdtBlock(
                new SdtProperties(
                    new Tag { Val = "someOtherTag" }
                ),
                new SdtContentBlock(
                    new Paragraph(new Run(new Text("Content")))
                )
            );
            body.AppendChild(sdt);
            doc.Save();
        }

        // Act: Convert
        stream.Position = 0;
        using (WordprocessingDocument doc = WordprocessingDocument.Open(stream, true))
        {
            Body body = doc.MainDocumentPart!.Document.Body!;
            SdtBlock sdt = body.Elements<SdtBlock>().First();

            ConditionalConverter converter = new ConditionalConverter();
            bool result = converter.Convert(sdt, "someOtherTag");

            // Assert: Should return false for non-conditional tags
            Assert.False(result);
        }
    }
}
