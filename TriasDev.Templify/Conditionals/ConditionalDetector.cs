// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using System.Text.RegularExpressions;

namespace TriasDev.Templify.Conditionals;

/// <summary>
/// Detects and parses conditional blocks in Word documents.
/// Supports {{#if condition}}...{{else}}...{{/if}} syntax.
/// </summary>
internal static class ConditionalDetector
{
    private static readonly Regex IfStartPattern = new Regex(
        @"\{\{#if\s+(.+?)\}\}",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex ElsePattern = new Regex(
        @"\{\{else\}\}",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex IfEndPattern = new Regex(
        @"\{\{/if\}\}",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Detects all conditional blocks in the document body.
    /// </summary>
    public static IReadOnlyList<ConditionalBlock> DetectConditionals(WordprocessingDocument document)
    {
        if (document.MainDocumentPart?.Document?.Body == null)
        {
            return Array.Empty<ConditionalBlock>();
        }

        Body body = document.MainDocumentPart.Document.Body;
        List<ConditionalBlock> conditionals = new List<ConditionalBlock>();

        // First, detect paragraph-level conditionals
        conditionals.AddRange(DetectConditionalsInElements(body.Elements<OpenXmlElement>().ToList()));

        // Second, detect table row conditionals within tables
        foreach (Table table in body.Elements<Table>())
        {
            conditionals.AddRange(DetectTableRowConditionals(table));
        }

        return conditionals;
    }

    /// <summary>
    /// Detects conditional blocks in a collection of elements.
    /// Handles nested conditionals properly by recursively detecting conditionals in IF and ELSE branches.
    /// </summary>
    /// <param name="elements">Elements to scan for conditional blocks.</param>
    /// <param name="nestingLevel">Current nesting level (0 for top-level, 1+ for nested).</param>
    internal static IReadOnlyList<ConditionalBlock> DetectConditionalsInElements(
        List<OpenXmlElement> elements,
        int nestingLevel = 0)
    {
        List<ConditionalBlock> conditionals = new List<ConditionalBlock>();
        int i = 0;

        while (i < elements.Count)
        {
            OpenXmlElement element = elements[i];
            string? text = GetElementText(element);

            if (text != null)
            {
                Match ifMatch = IfStartPattern.Match(text);
                if (ifMatch.Success)
                {
                    string conditionExpression = ifMatch.Groups[1].Value.Trim();

                    // Find the matching else (optional) and end markers
                    (int elseIndex, int endIndex) = FindMatchingElseAndEnd(elements, i);

                    if (endIndex == -1)
                    {
                        throw new InvalidOperationException(
                            $"Conditional start marker '{{{{#if {conditionExpression}}}}}' has no matching '{{{{/if}}}}'.");
                    }

                    // Get IF content elements (between start and else/end markers)
                    List<OpenXmlElement> ifContentElements = new List<OpenXmlElement>();
                    int ifEndIndex = elseIndex != -1 ? elseIndex : endIndex;
                    for (int j = i + 1; j < ifEndIndex; j++)
                    {
                        ifContentElements.Add(elements[j]);
                    }

                    // Get ELSE content elements (between else and end markers, if else exists)
                    List<OpenXmlElement> elseContentElements = new List<OpenXmlElement>();
                    if (elseIndex != -1)
                    {
                        for (int j = elseIndex + 1; j < endIndex; j++)
                        {
                            elseContentElements.Add(elements[j]);
                        }
                    }

                    // Create conditional block with current nesting level
                    ConditionalBlock conditionalBlock = new ConditionalBlock(
                        conditionExpression,
                        ifContentElements,
                        elseContentElements,
                        element,                    // Start marker
                        elseIndex != -1 ? elements[elseIndex] : null,  // Else marker (optional)
                        elements[endIndex],         // End marker
                        isTableRowConditional: false,
                        nestingLevel: nestingLevel);

                    conditionals.Add(conditionalBlock);

                    // Recursively detect nested conditionals in IF branch
                    if (ifContentElements.Count > 0)
                    {
                        IReadOnlyList<ConditionalBlock> nestedInIf = DetectConditionalsInElements(
                            ifContentElements,
                            nestingLevel + 1);
                        conditionals.AddRange(nestedInIf);
                    }

                    // Recursively detect nested conditionals in ELSE branch
                    if (elseContentElements.Count > 0)
                    {
                        IReadOnlyList<ConditionalBlock> nestedInElse = DetectConditionalsInElements(
                            elseContentElements,
                            nestingLevel + 1);
                        conditionals.AddRange(nestedInElse);
                    }

                    // Skip past this conditional
                    i = endIndex + 1;
                    continue;
                }
            }

            i++;
        }

        return conditionals;
    }

    /// <summary>
    /// Finds the matching {{else}} (optional) and {{/if}} for a {{#if}} at the given index.
    /// Properly handles nested conditionals by tracking depth.
    /// Returns (-1, endIndex) if no else is found, or (elseIndex, endIndex) if else is found.
    /// </summary>
    private static (int elseIndex, int endIndex) FindMatchingElseAndEnd(List<OpenXmlElement> elements, int startIndex)
    {
        int depth = 1;
        int elseIndex = -1;

        // First, check if the SAME element contains the closing tag (for same-line conditionals)
        string? startText = GetElementText(elements[startIndex]);
        if (startText != null)
        {
            // Count all {{#if and {{/if}} occurrences in the same element
            MatchCollection ifMatches = IfStartPattern.Matches(startText);
            MatchCollection endMatches = IfEndPattern.Matches(startText);

            // The depth after this element is: initial (1) + additional ifs - all ends
            depth = depth + (ifMatches.Count - 1) - endMatches.Count;

            if (depth == 0)
            {
                // The conditional is fully contained within the same element
                return (elseIndex, startIndex);
            }
        }

        // If not found in the same element, search subsequent elements
        for (int i = startIndex + 1; i < elements.Count; i++)
        {
            string? text = GetElementText(elements[i]);
            if (text == null)
            {
                continue;
            }

            // Count all {{#if occurrences in this element
            MatchCollection ifMatches = IfStartPattern.Matches(text);
            depth += ifMatches.Count;

            // Count all {{/if}} occurrences in this element
            MatchCollection endMatches = IfEndPattern.Matches(text);
            depth -= endMatches.Count;

            if (depth == 0)
            {
                return (elseIndex, i); // Found matching end
            }

            // Check for else at our depth level (before we decremented)
            if (ElsePattern.IsMatch(text) && (depth + endMatches.Count) == 1 && elseIndex == -1)
            {
                // Only capture the first else at our depth level
                elseIndex = i;
            }
        }

        return (-1, -1); // No matching end found
    }

    /// <summary>
    /// Gets the text content of an element (paragraph or table cell).
    /// </summary>
    private static string? GetElementText(OpenXmlElement element)
    {
        if (element is Paragraph paragraph)
        {
            return paragraph.InnerText;
        }

        if (element is TableRow row)
        {
            return row.InnerText;
        }

        if (element is TableCell cell)
        {
            return cell.InnerText;
        }

        return null;
    }

    /// <summary>
    /// Checks if an element contains a conditional marker.
    /// </summary>
    public static bool ContainsConditionalMarker(OpenXmlElement element)
    {
        string? text = GetElementText(element);
        if (text == null)
        {
            return false;
        }

        return IfStartPattern.IsMatch(text) || ElsePattern.IsMatch(text) || IfEndPattern.IsMatch(text);
    }

    /// <summary>
    /// Detects table row conditionals within a table.
    /// Table row conditionals have {{#if}}, {{else}}, and {{/if}} markers in separate rows.
    /// </summary>
    private static IReadOnlyList<ConditionalBlock> DetectTableRowConditionals(Table table)
    {
        List<ConditionalBlock> conditionals = new List<ConditionalBlock>();
        List<TableRow> rows = table.Elements<TableRow>().ToList();
        int i = 0;

        while (i < rows.Count)
        {
            TableRow row = rows[i];
            string? text = row.InnerText;

            if (text != null)
            {
                Match ifMatch = IfStartPattern.Match(text);
                if (ifMatch.Success)
                {
                    string conditionExpression = ifMatch.Groups[1].Value.Trim();

                    // Find the matching else (optional) and end marker rows
                    (int elseIndex, int endIndex) = FindMatchingElseAndEndInRows(rows, i);

                    if (endIndex == -1)
                    {
                        throw new InvalidOperationException(
                            $"Table row conditional start marker '{{{{#if {conditionExpression}}}}}' has no matching '{{{{/if}}}}'.");
                    }

                    // Get IF content rows (between start and else/end markers)
                    List<OpenXmlElement> ifContentRows = new List<OpenXmlElement>();
                    int ifEndIndex = elseIndex != -1 ? elseIndex : endIndex;
                    for (int j = i + 1; j < ifEndIndex; j++)
                    {
                        ifContentRows.Add(rows[j]);
                    }

                    // Get ELSE content rows (between else and end markers, if else exists)
                    List<OpenXmlElement> elseContentRows = new List<OpenXmlElement>();
                    if (elseIndex != -1)
                    {
                        for (int j = elseIndex + 1; j < endIndex; j++)
                        {
                        elseContentRows.Add(rows[j]);
                        }
                    }

                    // Create conditional block for table row conditional
                    // Table row conditionals are typically top-level (nesting level 0)
                    ConditionalBlock conditionalBlock = new ConditionalBlock(
                        conditionExpression,
                        ifContentRows,
                        elseContentRows,
                        rows[i],                    // Start marker row
                        elseIndex != -1 ? rows[elseIndex] : null,  // Else marker row (optional)
                        rows[endIndex],             // End marker row
                        isTableRowConditional: true,
                        nestingLevel: 0);

                    conditionals.Add(conditionalBlock);

                    // Skip past this conditional
                    i = endIndex + 1;
                    continue;
                }
            }

            i++;
        }

        return conditionals;
    }

    /// <summary>
    /// Finds the matching {{else}} (optional) and {{/if}} row for a {{#if}} at the given row index.
    /// Properly handles nested conditionals by tracking depth.
    /// </summary>
    private static (int elseIndex, int endIndex) FindMatchingElseAndEndInRows(List<TableRow> rows, int startIndex)
    {
        int depth = 1;
        int elseIndex = -1;

        for (int i = startIndex + 1; i < rows.Count; i++)
        {
            string? text = rows[i].InnerText;
            if (text == null)
            {
                continue;
            }

            if (IfStartPattern.IsMatch(text))
            {
                depth++;
            }
            else if (IfEndPattern.IsMatch(text))
            {
                depth--;
                if (depth == 0)
                {
                    return (elseIndex, i); // Found matching end
                }
            }
            else if (ElsePattern.IsMatch(text) && depth == 1 && elseIndex == -1)
            {
                // Only capture the first else at our depth level
                elseIndex = i;
            }
        }

        return (-1, -1); // No matching end found
    }
}
