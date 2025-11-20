// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using TriasDev.Templify.Converter.Models;

namespace TriasDev.Templify.Converter.Analyzers;

/// <summary>
/// Analyzes a Word template document to identify all content controls.
/// </summary>
public class TemplateAnalyzer
{
    /// <summary>
    /// Analyze a Word template document.
    /// </summary>
    /// <param name="templatePath">Path to the template file.</param>
    /// <returns>Analysis result containing all controls and statistics.</returns>
    public AnalysisResult AnalyzeTemplate(string templatePath)
    {
        if (!File.Exists(templatePath))
        {
            throw new FileNotFoundException($"Template file not found: {templatePath}");
        }

        AnalysisResult result = new AnalysisResult();

        using WordprocessingDocument document = WordprocessingDocument.Open(templatePath, false);

        if (document.MainDocumentPart == null)
        {
            throw new InvalidOperationException("Document has no main document part");
        }

        // Find all Sdt elements (Structured Document Tags = Content Controls)
        List<SdtElement> contentControls = document.MainDocumentPart.Document.Body
            .Descendants<SdtElement>()
            .Where(sdt => GetControlTag(sdt) != null)
            .ToList();

        Console.WriteLine($"Found {contentControls.Count} content controls");

        // Analyze each control
        foreach (SdtElement sdt in contentControls)
        {
            ControlInfo controlInfo = AnalyzeControl(sdt);
            result.Controls.Add(controlInfo);
        }

        // Generate statistics
        result.GenerateStatistics();

        return result;
    }

    /// <summary>
    /// Analyze a single content control.
    /// </summary>
    private ControlInfo AnalyzeControl(SdtElement sdt)
    {
        string? tag = GetControlTag(sdt);
        if (tag == null)
        {
            return new ControlInfo { Type = ControlType.Unknown };
        }

        ControlInfo info = new ControlInfo
        {
            Tag = tag,
            Type = DetermineControlType(tag),
            HasNestedControls = sdt.Descendants<SdtElement>().Any(nested => nested != sdt),
            InTable = sdt.Ancestors<Table>().Any(),
            InTableRow = sdt.Ancestors<TableRow>().Any(),
            Location = GetLocation(sdt)
        };

        // Parse the tag to extract variable path and operators
        ParseTag(tag, info);

        // Generate Templify syntax
        info.TemplifySyntax = GenerateTemplifySyntax(info);

        // Determine if manual review is needed
        DetermineManualReview(info);

        return info;
    }

    /// <summary>
    /// Get the tag value from a content control.
    /// </summary>
    private string? GetControlTag(SdtElement sdt)
    {
        return sdt.SdtProperties?.GetFirstChild<Tag>()?.Val?.Value;
    }

    /// <summary>
    /// Determine the control type from the tag.
    /// </summary>
    private ControlType DetermineControlType(string tag)
    {
        if (tag.StartsWith("variable_"))
        {
            return ControlType.Variable;
        }
        else if (tag.StartsWith("conditionalRemove_"))
        {
            return ControlType.Conditional;
        }
        else if (tag.StartsWith("repeating_"))
        {
            return ControlType.Repeating;
        }

        return ControlType.Unknown;
    }

    /// <summary>
    /// Parse the tag to extract variable path, operators, and values.
    /// </summary>
    private void ParseTag(string tag, ControlInfo info)
    {
        switch (info.Type)
        {
            case ControlType.Variable:
                // Format: variable_variablePath
                info.VariablePath = tag.Substring("variable_".Length);
                break;

            case ControlType.Conditional:
                // Format: conditionalRemove_variablePath[_operator_value]
                ParseConditionalTag(tag, info);
                break;

            case ControlType.Repeating:
                // Format: repeating_collectionPath
                info.VariablePath = tag.Substring("repeating_".Length);
                break;
        }
    }

    /// <summary>
    /// Parse a conditional tag to extract path, operators, and values.
    /// </summary>
    private void ParseConditionalTag(string tag, ControlInfo info)
    {
        // Remove the "conditionalRemove_" prefix
        string remainder = tag.Substring("conditionalRemove_".Length);

        // Split by underscore
        string[] parts = remainder.Split('_');

        // First part is always the variable path
        info.VariablePath = parts[0];

        // Parse operators and values
        for (int i = 1; i < parts.Length; i++)
        {
            string part = parts[i];

            if (IsOperator(part))
            {
                info.Operators.Add(part);

                // If it's a comparison operator, the next part is the value
                if (IsComparisonOperator(part) && i + 1 < parts.Length && !IsOperator(parts[i + 1]))
                {
                    i++;
                    info.ComparisonValues.Add(parts[i]);
                }
            }
        }
    }

    /// <summary>
    /// Check if a string is an operator.
    /// </summary>
    private bool IsOperator(string value)
    {
        return value is "eq" or "ne" or "gt" or "lt" or "gte" or "lte" or "and" or "or" or "not";
    }

    /// <summary>
    /// Check if an operator is a comparison operator.
    /// </summary>
    private bool IsComparisonOperator(string value)
    {
        return value is "eq" or "ne" or "gt" or "lt" or "gte" or "lte";
    }

    /// <summary>
    /// Generate Templify syntax from control info.
    /// </summary>
    private string GenerateTemplifySyntax(ControlInfo info)
    {
        return info.Type switch
        {
            ControlType.Variable => $"{{{{{info.VariablePath}}}}}",
            ControlType.Conditional => GenerateConditionalSyntax(info),
            ControlType.Repeating => $"{{{{#foreach {info.VariablePath}}}}}...{{{{/foreach}}}}",
            _ => "???"
        };
    }

    /// <summary>
    /// Generate Templify conditional syntax.
    /// </summary>
    private string GenerateConditionalSyntax(ControlInfo info)
    {
        if (info.Operators.Count == 0)
        {
            // Simple existence check
            return $"{{{{#if {info.VariablePath}}}}}...{{{{/if}}}}";
        }

        // Build condition string
        string condition = info.VariablePath;

        for (int i = 0; i < info.Operators.Count; i++)
        {
            string op = info.Operators[i];

            switch (op)
            {
                case "eq":
                case "ne":
                case "gt":
                case "lt":
                case "gte":
                case "lte":
                    if (i < info.ComparisonValues.Count)
                    {
                        condition += $" {op} \"{info.ComparisonValues[i]}\"";
                    }
                    break;
                case "not":
                    condition = $"not ({condition})";
                    break;
                case "and":
                    condition += " and ";
                    break;
                case "or":
                    condition += " or ";
                    break;
            }
        }

        return $"{{{{#if {condition}}}}}...{{{{/if}}}}";
    }

    /// <summary>
    /// Determine if a control requires manual review.
    /// </summary>
    private void DetermineManualReview(ControlInfo info)
    {
        // Complex conditionals need review
        if (info.Type == ControlType.Conditional && info.Operators.Count > 1)
        {
            info.RequiresManualReview = true;
            info.Notes.Add("Complex conditional with multiple operators");
        }

        // Nested controls need review
        if (info.HasNestedControls)
        {
            info.RequiresManualReview = true;
            info.Notes.Add("Contains nested controls");
        }

        // Controls in table rows might need special handling
        if (info.InTableRow && info.Type == ControlType.Repeating)
        {
            info.RequiresManualReview = true;
            info.Notes.Add("Repeating control in table row - may need row-level foreach");
        }

        // Conditionals with OR/AND need review
        if (info.Operators.Contains("or") || info.Operators.Contains("and"))
        {
            info.RequiresManualReview = true;
            info.Notes.Add("Complex boolean logic");
        }
    }

    /// <summary>
    /// Get the location description for a control.
    /// </summary>
    private string GetLocation(SdtElement sdt)
    {
        // Try to find the paragraph number
        Document? doc = sdt.Ancestors<Document>().FirstOrDefault();
        if (doc == null) return "Unknown";

        List<Paragraph> allParagraphs = doc.Descendants<Paragraph>().ToList();
        Paragraph? paragraph = sdt.Ancestors<Paragraph>().FirstOrDefault();

        if (paragraph != null)
        {
            int paraIndex = allParagraphs.IndexOf(paragraph);
            if (paraIndex >= 0)
            {
                string location = $"Paragraph {paraIndex + 1}";

                // Add table information if applicable
                if (sdt.Ancestors<Table>().Any())
                {
                    location += " (in table)";
                }

                return location;
            }
        }

        return "Unknown";
    }
}
