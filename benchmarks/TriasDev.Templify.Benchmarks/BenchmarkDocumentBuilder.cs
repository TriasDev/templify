// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace TriasDev.Templify.Benchmarks;

/// <summary>
/// Helper class for creating benchmark test documents.
/// </summary>
internal static class BenchmarkDocumentBuilder
{
    public static MemoryStream CreateDocumentWithPlaceholders(int placeholderCount)
    {
        MemoryStream stream = new MemoryStream();

        using (WordprocessingDocument document = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document, true))
        {
            MainDocumentPart mainPart = document.AddMainDocumentPart();
            mainPart.Document = new Document();
            Body body = mainPart.Document.AppendChild(new Body());

            for (int i = 0; i < placeholderCount; i++)
            {
                Paragraph para = body.AppendChild(new Paragraph());
                Run run = para.AppendChild(new Run());
                run.AppendChild(new Text($"Value: {{{{Var{i}}}}}"));
            }

            mainPart.Document.Save();
        }

        stream.Position = 0;
        return stream;
    }

    public static MemoryStream CreateDocumentWithLoops(int loopCount, int itemsPerLoop)
    {
        MemoryStream stream = new MemoryStream();

        using (WordprocessingDocument document = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document, true))
        {
            MainDocumentPart mainPart = document.AddMainDocumentPart();
            mainPart.Document = new Document();
            Body body = mainPart.Document.AppendChild(new Body());

            for (int i = 0; i < loopCount; i++)
            {
                // Loop start marker
                Paragraph startPara = body.AppendChild(new Paragraph());
                Run startRun = startPara.AppendChild(new Run());
                startRun.AppendChild(new Text($"{{{{#foreach Collection{i}}}}}"));

                // Loop content
                Paragraph contentPara = body.AppendChild(new Paragraph());
                Run contentRun = contentPara.AppendChild(new Run());
                contentRun.AppendChild(new Text("Item: {{.}} (Index: {{@index}})"));

                // Loop end marker
                Paragraph endPara = body.AppendChild(new Paragraph());
                Run endRun = endPara.AppendChild(new Run());
                endRun.AppendChild(new Text("{{/foreach}}"));
            }

            mainPart.Document.Save();
        }

        stream.Position = 0;
        return stream;
    }

    public static MemoryStream CreateDocumentWithConditionals(int conditionalCount)
    {
        MemoryStream stream = new MemoryStream();

        using (WordprocessingDocument document = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document, true))
        {
            MainDocumentPart mainPart = document.AddMainDocumentPart();
            mainPart.Document = new Document();
            Body body = mainPart.Document.AppendChild(new Body());

            for (int i = 0; i < conditionalCount; i++)
            {
                // Conditional start
                Paragraph ifPara = body.AppendChild(new Paragraph());
                Run ifRun = ifPara.AppendChild(new Run());
                ifRun.AppendChild(new Text($"{{{{#if Flag{i}}}}}"));

                // True branch
                Paragraph truePara = body.AppendChild(new Paragraph());
                Run trueRun = truePara.AppendChild(new Run());
                trueRun.AppendChild(new Text($"Flag {i} is true"));

                // Else
                Paragraph elsePara = body.AppendChild(new Paragraph());
                Run elseRun = elsePara.AppendChild(new Run());
                elseRun.AppendChild(new Text("{{#else}}"));

                // False branch
                Paragraph falsePara = body.AppendChild(new Paragraph());
                Run falseRun = falsePara.AppendChild(new Run());
                falseRun.AppendChild(new Text($"Flag {i} is false"));

                // End if
                Paragraph endIfPara = body.AppendChild(new Paragraph());
                Run endIfRun = endIfPara.AppendChild(new Run());
                endIfRun.AppendChild(new Text("{{/if}}"));
            }

            mainPart.Document.Save();
        }

        stream.Position = 0;
        return stream;
    }

    public static MemoryStream CreateDocumentWithNestedLoops(int outerItems, int innerItems)
    {
        MemoryStream stream = new MemoryStream();

        using (WordprocessingDocument document = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document, true))
        {
            MainDocumentPart mainPart = document.AddMainDocumentPart();
            mainPart.Document = new Document();
            Body body = mainPart.Document.AppendChild(new Body());

            // Outer loop start
            AddParagraph(body, "{{#foreach OuterCollection}}");
            AddParagraph(body, "Outer: {{Name}}");

            // Inner loop start
            AddParagraph(body, "  {{#foreach InnerCollection}}");
            AddParagraph(body, "    Inner: {{Value}}");
            AddParagraph(body, "  {{/foreach}}");

            // Outer loop end
            AddParagraph(body, "{{/foreach}}");

            mainPart.Document.Save();
        }

        stream.Position = 0;
        return stream;
    }

    public static MemoryStream CreateComplexDocument(int sections)
    {
        MemoryStream stream = new MemoryStream();

        using (WordprocessingDocument document = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document, true))
        {
            MainDocumentPart mainPart = document.AddMainDocumentPart();
            mainPart.Document = new Document();
            Body body = mainPart.Document.AppendChild(new Body());

            for (int i = 0; i < sections; i++)
            {
                // Header with placeholders
                AddParagraph(body, $"Section {{{{SectionNum{i}}}}}: {{{{SectionName{i}}}}}");

                // Conditional
                AddParagraph(body, $"{{{{#if ShowDetails{i}}}}}");

                // Loop with conditional inside
                AddParagraph(body, $"  {{{{#foreach Items{i}}}}}");
                AddParagraph(body, $"    {{{{#if IsActive}}}}");
                AddParagraph(body, $"      Active Item: {{{{Name}}}} - {{{{Value}}}}");
                AddParagraph(body, $"    {{{{#else}}}}");
                AddParagraph(body, $"      Inactive: {{{{Name}}}}");
                AddParagraph(body, $"    {{{{/if}}}}");
                AddParagraph(body, $"  {{{{/foreach}}}}");

                AddParagraph(body, $"{{{{/if}}}}");
            }

            mainPart.Document.Save();
        }

        stream.Position = 0;
        return stream;
    }

    private static void AddParagraph(Body body, string text)
    {
        Paragraph para = body.AppendChild(new Paragraph());
        Run run = para.AppendChild(new Run());
        run.AppendChild(new Text(text));
    }
}
