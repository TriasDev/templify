// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;
using System.Text.RegularExpressions;
using TriasDev.Templify.Core;
using TriasDev.Templify.Utilities;

namespace TriasDev.Templify.Loops;

/// <summary>
/// Detects and parses loop blocks in Word documents.
/// Supports {{#foreach CollectionName}}...{{/foreach}} syntax
/// and {{#foreach item in CollectionName}}...{{/foreach}} for named iteration variables.
/// </summary>
internal static class LoopDetector
{
    // Note: The @? in the regex allows capturing invalid variable names starting with @
    // so we can provide a helpful validation error message instead of silently not matching.
    private static readonly Regex _foreachStartPattern = new Regex(
        @"\{\{#foreach\s+" + IterationVariablePrefixPattern + @"([\w.]+)\}\}",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>
    /// Reserved variable names that cannot be used as iteration variable names.
    /// These conflict with loop metadata syntax.
    /// </summary>
    private static readonly HashSet<string> _reservedVariableNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "in" // Reserved keyword in loop syntax
    };

    /// <summary>Pattern text of the <c>{{/foreach}}</c> end marker.</summary>
    internal const string ForeachEndPattern = @"\{\{/foreach\}\}";

    /// <summary>
    /// Pattern text of the optional named iteration variable prefix of a <c>{{#foreach}}</c> marker
    /// (<c>item in </c>; group: the variable name, which may start with <c>@</c> so it can be rejected).
    /// </summary>
    internal const string IterationVariablePrefixPattern = @"(?:(@?\w+)\s+in\s+)?";

    private static readonly Regex _foreachEndPattern = new Regex(
        ForeachEndPattern,
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>
    /// Validates an iteration variable name and throws if invalid.
    /// </summary>
    /// <param name="variableName">The variable name to validate.</param>
    /// <param name="collectionName">The collection name for error messages.</param>
    /// <exception cref="InvalidOperationException">Thrown when the variable name is invalid.</exception>
    internal static void ValidateIterationVariableName(string variableName, string collectionName)
    {
        // Check for reserved names (like "in")
        if (_reservedVariableNames.Contains(variableName))
        {
            throw new TemplateSyntaxException(
                ValidationErrorType.InvalidPlaceholderSyntax,
                $"Invalid iteration variable name '{variableName}' in '{{{{#foreach {variableName} in {collectionName}}}}}'. " +
                $"'{variableName}' is a reserved keyword.");
        }

        // Check for metadata prefix (@)
        if (variableName.StartsWith("@", StringComparison.Ordinal))
        {
            throw new TemplateSyntaxException(
                ValidationErrorType.InvalidPlaceholderSyntax,
                $"Invalid iteration variable name '{variableName}' in '{{{{#foreach {variableName} in {collectionName}}}}}'. " +
                $"Iteration variable names cannot start with '@' as this is reserved for loop metadata.");
        }
    }

    /// <summary>
    /// Detects loop blocks in a collection of elements.
    /// Handles nested loops properly.
    /// </summary>
    /// <remarks>
    /// Made internal for use by DocumentWalker in Phase 2 visitor pattern refactoring.
    /// </remarks>
    internal static IReadOnlyList<LoopBlock> DetectLoopsInElements(List<OpenXmlElement> elements)
    {
        List<LoopBlock> loops = new List<LoopBlock>();
        int i = 0;

        while (i < elements.Count)
        {
            OpenXmlElement element = elements[i];
            string? text = GetElementText(element);

            if (text != null)
            {
                Match foreachMatch = _foreachStartPattern.Match(text);
                if (foreachMatch.Success)
                {
                    // Group 1: optional iteration variable (e.g., "item" from "item in Items")
                    // Group 2: collection name (e.g., "Items")
                    string? iterationVariableName = foreachMatch.Groups[1].Success && foreachMatch.Groups[1].Length > 0
                        ? foreachMatch.Groups[1].Value
                        : null;
                    string collectionName = foreachMatch.Groups[2].Value;

                    // Validate iteration variable name if provided
                    if (iterationVariableName != null)
                    {
                        ValidateIterationVariableName(iterationVariableName, collectionName);
                    }

                    // Find the matching end marker
                    int endIndex = FindMatchingEnd(elements, i);
                    if (endIndex == -1)
                    {
                        throw new TemplateSyntaxException(
                            ValidationErrorType.UnmatchedLoopStart,
                            $"Loop start marker '{{{{#foreach {collectionName}}}}}' has no matching '{{{{/foreach}}}}'.");
                    }

                    // A content control that contains a complete loop is not a loop marker:
                    // the loop is detected when the walker descends into the control's content.
                    if (endIndex == i && element is SdtBlock)
                    {
                        i++;
                        continue;
                    }

                    // Get content elements (between start and end markers)
                    List<OpenXmlElement> contentElements = new List<OpenXmlElement>();
                    for (int j = i + 1; j < endIndex; j++)
                    {
                        contentElements.Add(elements[j]);
                    }

                    // Create loop block
                    LoopBlock loopBlock = new LoopBlock(
                        collectionName,
                        iterationVariableName,
                        contentElements,
                        element,
                        elements[endIndex],
                        isTableRowLoop: false);

                    loops.Add(loopBlock);

                    // Skip past this loop
                    i = endIndex + 1;
                    continue;
                }
            }

            i++;
        }

        return loops;
    }

    /// <summary>
    /// Finds the matching {{/foreach}} for a {{#foreach}} at the given index.
    /// Properly handles nested loops by tracking depth.
    /// </summary>
    private static int FindMatchingEnd(List<OpenXmlElement> elements, int startIndex)
    {
        int depth = 1;

        // First, check if the SAME element contains the closing tag (for same-line loops)
        string? startText = GetElementText(elements[startIndex]);
        if (startText != null)
        {
            // Count all {{#foreach and {{/foreach}} occurrences in the same element
            MatchCollection startMatches = _foreachStartPattern.Matches(startText);
            MatchCollection endMatches = _foreachEndPattern.Matches(startText);

            // The depth after this element is: initial (1) + additional starts - all ends
            depth = depth + (startMatches.Count - 1) - endMatches.Count;

            if (depth == 0)
            {
                // The loop is fully contained within the same element
                return startIndex;
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

            // Count all {{#foreach occurrences in this element
            MatchCollection startMatches = _foreachStartPattern.Matches(text);
            depth += startMatches.Count;

            // Count all {{/foreach}} occurrences in this element
            MatchCollection endMatches = _foreachEndPattern.Matches(text);
            depth -= endMatches.Count;

            if (depth == 0)
            {
                return i; // Found matching end
            }
        }

        return -1; // No matching end found
    }

    /// <summary>
    /// Gets the marker text of an element: paragraphs, table rows and cells as for conditionals,
    /// plus content controls (a block content control can hold loop markers).
    /// </summary>
    private static string? GetElementText(OpenXmlElement element) =>
        element is SdtElement
            ? TemplateElementText.GetOwnText(element)
            : TemplateElementText.GetMarkerText(element);

    /// <summary>
    /// Detects table row loops within a table.
    /// Table row loops have {{#foreach}} and {{/foreach}} markers in separate rows.
    /// </summary>
    /// <remarks>
    /// Made internal for use by DocumentWalker in Phase 2 visitor pattern refactoring.
    /// </remarks>
    internal static IReadOnlyList<LoopBlock> DetectTableRowLoops(Table table)
    {
        return DetectTableRowLoops(table.Elements<TableRow>().ToList());
    }

    /// <summary>
    /// Detects table row loops in a sequence of table rows (e.g. all rows of a table,
    /// or the cloned rows of a loop iteration).
    /// Loops confined to a single cell are skipped; they are processed when the cell content is walked.
    /// </summary>
    internal static IReadOnlyList<LoopBlock> DetectTableRowLoops(IReadOnlyList<TableRow> rows)
    {
        List<LoopBlock> loops = new List<LoopBlock>();
        int i = 0;

        while (i < rows.Count)
        {
            TableRow row = rows[i];
            string? text = TemplateElementText.GetOwnText(row);

            if (text != null)
            {
                Match foreachMatch = _foreachStartPattern.Match(text);
                if (foreachMatch.Success)
                {
                    // Group 1: optional iteration variable (e.g., "item" from "item in Items")
                    // Group 2: collection name (e.g., "Items")
                    string? iterationVariableName = foreachMatch.Groups[1].Success && foreachMatch.Groups[1].Length > 0
                        ? foreachMatch.Groups[1].Value
                        : null;
                    string collectionName = foreachMatch.Groups[2].Value;

                    // Validate iteration variable name if provided
                    if (iterationVariableName != null)
                    {
                        ValidateIterationVariableName(iterationVariableName, collectionName);
                    }

                    // Check if this specific loop is contained in a single cell
                    // If so, skip it - it will be processed when the cell content is walked
                    if (IsLoopContainedInSingleCell(row, collectionName))
                    {
                        i++;
                        continue;
                    }

                    // Find the matching end marker row
                    int endIndex = FindMatchingEndInRows(rows, i);
                    if (endIndex == -1)
                    {
                        throw new TemplateSyntaxException(
                            ValidationErrorType.UnmatchedLoopStart,
                            $"Table row loop start marker '{{{{#foreach {collectionName}}}}}' has no matching '{{{{/foreach}}}}'.");
                    }

                    // Get content rows (between start and end markers)
                    List<OpenXmlElement> contentRows = new List<OpenXmlElement>();
                    for (int j = i + 1; j < endIndex; j++)
                    {
                        contentRows.Add(rows[j]);
                    }

                    // Create loop block for table row loop
                    LoopBlock loopBlock = new LoopBlock(
                        collectionName,
                        iterationVariableName,
                        contentRows,
                        rows[i],      // Start marker row
                        rows[endIndex], // End marker row
                        isTableRowLoop: true);

                    loops.Add(loopBlock);

                    // Skip past this loop
                    i = endIndex + 1;
                    continue;
                }
            }

            i++;
        }

        return loops;
    }

    /// <summary>
    /// Finds the matching {{/foreach}} row for a {{#foreach}} at the given row index.
    /// Properly handles nested loops by tracking depth.
    /// </summary>
    private static int FindMatchingEndInRows(IReadOnlyList<TableRow> rows, int startIndex)
    {
        int depth = 1;

        for (int i = startIndex + 1; i < rows.Count; i++)
        {
            string? text = TemplateElementText.GetOwnText(rows[i]);
            if (text == null)
            {
                continue;
            }

            // Use the net marker count so that a loop confined to a single cell of this row
            // (start and end in the same row) does not change the row-level depth.
            depth += _foreachStartPattern.Matches(text).Count;
            depth -= _foreachEndPattern.Matches(text).Count;

            if (depth <= 0)
            {
                return i;
            }
        }

        return -1; // No matching end found
    }

    /// <summary>
    /// Checks if a specific loop is contained entirely within a single table cell.
    /// This happens when both {{#foreach CollectionName}} (or {{#foreach item in CollectionName}})
    /// and {{/foreach}} markers are in the same cell.
    /// </summary>
    /// <param name="row">The table row to check.</param>
    /// <param name="collectionName">The specific collection name to check for.</param>
    /// <returns>True if the loop for this collection is fully contained in a single cell.</returns>
    private static bool IsLoopContainedInSingleCell(TableRow row, string collectionName)
    {
        // Check each cell in the row, including cells wrapped in cell-level content controls
        // (w:sdt around w:tc); cells of nested tables belong to their own rows.
        foreach (TableCell cell in TemplateElementText.GetRowCells(row))
        {
            string? cellText = TemplateElementText.GetOwnText(cell);
            if (cellText == null)
            {
                continue;
            }

            MatchCollection startMatches = _foreachStartPattern.Matches(cellText);

            // Check if this cell contains a foreach start marker for this specific collection
            // (implicit or named iteration variable syntax; group 2 is the collection name)
            bool containsStartForCollection = startMatches.Any(m =>
                string.Equals(m.Groups[2].Value, collectionName, StringComparison.OrdinalIgnoreCase));

            if (containsStartForCollection)
            {
                // Check if the matching end marker is also in this cell
                // For simplicity, we check if there's at least one {{/foreach}} in the cell
                // and that the number of starts <= number of ends (meaning this specific loop is closed)

                MatchCollection endMatches = _foreachEndPattern.Matches(cellText);

                // If this cell has at least as many end markers as start markers,
                // then at least one complete loop exists in this cell
                if (endMatches.Count >= startMatches.Count && startMatches.Count > 0)
                {
                    return true;
                }
            }
        }

        return false;
    }
}
