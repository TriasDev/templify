// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using TriasDev.Templify.Converter.Converters;
using TriasDev.Templify.Converter.Models;

namespace TriasDev.Templify.Converter.Analyzers;

/// <summary>
/// Analyzes a Word template document to identify all content controls.
/// Uses the same tag parser and condition builder as the converter, so the suggested syntax is
/// exactly what <c>convert</c> produces.
/// </summary>
public class TemplateAnalyzer
{
    /// <summary>
    /// Analyze a Word template document (body, headers, footers, footnotes and endnotes).
    /// </summary>
    /// <param name="templatePath">Path to the template file.</param>
    /// <returns>Analysis result containing all controls and statistics.</returns>
    public AnalysisResult AnalyzeTemplate(string templatePath)
    {
        if (!File.Exists(templatePath))
        {
            throw new FileNotFoundException($"Template file not found: {templatePath}", templatePath);
        }

        using WordprocessingDocument document = WordprocessingDocument.Open(templatePath, false);
        return AnalyzeDocument(document);
    }

    /// <summary>
    /// Analyze an opened document.
    /// </summary>
    public AnalysisResult AnalyzeDocument(WordprocessingDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (document.MainDocumentPart?.Document?.Body == null)
        {
            throw new InvalidOperationException("Document has no body");
        }

        AnalysisResult result = new AnalysisResult();

        foreach (OpenXmlPartRootElement root in OpenXmlHelpers.GetContentRoots(document))
        {
            Dictionary<Paragraph, int> paragraphs = root.Descendants<Paragraph>()
                .Select((paragraph, index) => (paragraph, index))
                .ToDictionary(pair => pair.paragraph, pair => pair.index);

            foreach (SdtElement sdt in root.Descendants<SdtElement>())
            {
                if (OpenXmlHelpers.GetContentControlTag(sdt) is string tag)
                {
                    result.Controls.Add(AnalyzeControl(sdt, tag, root, paragraphs));
                }
            }
        }

        result.GenerateStatistics();
        return result;
    }

    private static ControlInfo AnalyzeControl(SdtElement sdt, string tagValue, OpenXmlElement root, Dictionary<Paragraph, int> paragraphs)
    {
        OpenXmlTemplatesTag tag = OpenXmlTemplatesTag.Parse(tagValue);

        ControlInfo info = new ControlInfo
        {
            Tag = tagValue,
            Type = tag.Type,
            VariablePath = tag.VariablePath,
            TemplifySyntax = tag.TemplifySyntax ?? "(manual conversion required)",
            HasNestedControls = sdt.Descendants<SdtElement>().Any(),
            InTable = sdt.Ancestors<Table>().Any(),
            InTableRow = sdt is SdtRow || sdt.Ancestors<TableRow>().Any(),
            Location = GetLocation(sdt, root, paragraphs),
        };

        if (tag.Condition != null)
        {
            info.Operators.AddRange(tag.Condition.Operators);
            info.ComparisonValues.AddRange(tag.Condition.ComparisonValues);
        }

        if (tag.Type == ControlType.Unknown)
        {
            info.Notes.Add("Not an OpenXMLTemplates control; kept as-is by convert");
            return info;
        }

        foreach (string error in tag.Errors)
        {
            info.RequiresManualReview = true;
            info.Notes.Add(error);
        }

        foreach (string note in tag.ReviewNotes)
        {
            info.RequiresManualReview = true;
            info.Notes.Add(note);
        }

        if (tag.Type == ControlType.Repeating && (sdt is SdtRun || sdt.Ancestors<Paragraph>().Any()))
        {
            info.RequiresManualReview = true;
            info.Notes.Add("Inline repeating control: Templify loops must span whole paragraphs or table rows");
        }

        if (tag.Type == ControlType.Repeating && sdt is SdtCell)
        {
            info.RequiresManualReview = true;
            info.Notes.Add("Repeating table cell: Templify can repeat paragraphs or table rows, not cells");
        }

        if (tag.Type == ControlType.Repeating && sdt is SdtRow)
        {
            info.Notes.Add("Repeating table row: converted to a table row loop (marker rows)");
        }

        return info;
    }

    private static string GetLocation(SdtElement sdt, OpenXmlElement root, Dictionary<Paragraph, int> paragraphs)
    {
        string part = OpenXmlHelpers.GetPartName(root);
        Paragraph? paragraph = sdt.Ancestors<Paragraph>().FirstOrDefault()
            ?? sdt.Descendants<Paragraph>().FirstOrDefault();

        string location = part;
        if (paragraph != null)
        {
            if (paragraphs.TryGetValue(paragraph, out int index))
            {
                location += $", paragraph {index + 1}";
            }
        }

        if (sdt.Ancestors<Table>().Any())
        {
            location += " (in table)";
        }

        return location;
    }
}
