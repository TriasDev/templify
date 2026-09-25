// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Text.RegularExpressions;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace TriasDev.Templify.Conditionals;

/// <summary>
/// Detects and parses conditional blocks in Word documents.
/// Supports {{#if condition}}...{{#elseif condition}}...{{#else}}...{{/if}} syntax.
/// </summary>
internal static class ConditionalDetector
{

    /// <summary>
    /// Holds information about markers found in a conditional block.
    /// </summary>
    private readonly struct ConditionalMarkers
    {
        public List<(int Index, string Condition)> ElseIfMarkers { get; init; }
        public int ElseIndex { get; init; }
        public int EndIndex { get; init; }
    }

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
    /// Handles nested conditionals properly by recursively detecting conditionals in all branches.
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
                Match ifMatch = ConditionalPatterns.IfStart.Match(text);
                if (ifMatch.Success)
                {
                    string conditionExpression = ifMatch.Groups[1].Value.Trim();

                    // Find the matching elseif, else (optional) and end markers
                    ConditionalMarkers markers = FindConditionalMarkers(elements, i);

                    if (markers.EndIndex == -1)
                    {
                        throw new InvalidOperationException(
                            $"Conditional start marker '{{{{#if {conditionExpression}}}}}' has no matching '{{{{/if}}}}'.");
                    }

                    // Build branches
                    List<ConditionalBranch> branches = new List<ConditionalBranch>();

                    // Determine branch boundaries
                    List<(int MarkerIndex, string? Condition)> allMarkers = new List<(int, string?)>();
                    allMarkers.Add((i, conditionExpression)); // if marker

                    foreach (var elseIfMarker in markers.ElseIfMarkers)
                    {
                        allMarkers.Add((elseIfMarker.Index, elseIfMarker.Condition));
                    }

                    if (markers.ElseIndex != -1)
                    {
                        allMarkers.Add((markers.ElseIndex, null)); // else marker (no condition)
                    }

                    // Create branches from marker boundaries
                    for (int m = 0; m < allMarkers.Count; m++)
                    {
                        int markerIndex = allMarkers[m].MarkerIndex;
                        string? condition = allMarkers[m].Condition;

                        // Content extends from marker+1 to next marker or end
                        int contentStart = markerIndex + 1;
                        int contentEnd = (m + 1 < allMarkers.Count)
                            ? allMarkers[m + 1].MarkerIndex
                            : markers.EndIndex;

                        List<OpenXmlElement> contentElements = new List<OpenXmlElement>();
                        for (int j = contentStart; j < contentEnd; j++)
                        {
                            contentElements.Add(elements[j]);
                        }

                        branches.Add(new ConditionalBranch(
                            condition,
                            contentElements,
                            elements[markerIndex]));
                    }

                    // Create conditional block with branches
                    ConditionalBlock conditionalBlock = new ConditionalBlock(
                        branches,
                        elements[markers.EndIndex],
                        isTableRowConditional: false,
                        nestingLevel: nestingLevel);

                    conditionals.Add(conditionalBlock);

                    // Recursively detect nested conditionals in all branches
                    foreach (ConditionalBranch branch in branches)
                    {
                        if (branch.ContentElements.Count > 0)
                        {
                            IReadOnlyList<ConditionalBlock> nestedConditionals = DetectConditionalsInElements(
                                branch.ContentElements.ToList(),
                                nestingLevel + 1);
                            conditionals.AddRange(nestedConditionals);
                        }
                    }

                    // Skip past this conditional
                    i = markers.EndIndex + 1;
                    continue;
                }
            }

            i++;
        }

        return conditionals;
    }

    /// <summary>
    /// Finds all conditional markers (elseif, else, end) for a {{#if}} at the given index.
    /// Properly handles nested conditionals by tracking depth.
    /// Validates that {{#else}} appears after all {{#elseif}} markers.
    /// </summary>
    private static ConditionalMarkers FindConditionalMarkers(List<OpenXmlElement> elements, int startIndex)
    {
        int depth = 1;
        List<(int Index, string Condition)> elseIfMarkers = new List<(int, string)>();
        int elseIndex = -1;
        int endIndex = -1;

        // First, check if the SAME element contains the closing tag (for same-line conditionals)
        string? startText = GetElementText(elements[startIndex]);
        if (startText != null)
        {
            // Count all {{#if and {{/if}} occurrences in the same element
            MatchCollection ifMatches = ConditionalPatterns.IfStart.Matches(startText);
            MatchCollection endMatches = ConditionalPatterns.IfEnd.Matches(startText);

            // The depth after this element is: initial (1) + additional ifs - all ends
            depth = depth + (ifMatches.Count - 1) - endMatches.Count;

            if (depth == 0)
            {
                // The conditional is fully contained within the same element
                return new ConditionalMarkers
                {
                    ElseIfMarkers = elseIfMarkers,
                    ElseIndex = elseIndex,
                    EndIndex = startIndex
                };
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
            MatchCollection ifMatches = ConditionalPatterns.IfStart.Matches(text);
            depth += ifMatches.Count;

            // Count all {{/if}} occurrences in this element
            MatchCollection endMatches = ConditionalPatterns.IfEnd.Matches(text);
            depth -= endMatches.Count;

            if (depth == 0)
            {
                endIndex = i;
                break;
            }

            // Only capture markers at our depth level (depth == 1 before any decrement)
            int effectiveDepth = depth + endMatches.Count;

            // Check for elseif at our depth level
            Match elseIfMatch = ConditionalPatterns.ElseIf.Match(text);
            if (elseIfMatch.Success && effectiveDepth == 1)
            {
                // Validate: elseif cannot appear after else
                if (elseIndex != -1)
                {
                    throw new InvalidOperationException(
                        "Invalid conditional structure: '{{#elseif}}' cannot appear after '{{#else}}'. " +
                        "The '{{#else}}' branch must be the last branch before '{{/if}}'.");
                }

                elseIfMarkers.Add((i, elseIfMatch.Groups[1].Value.Trim()));
            }

            // Check for else at our depth level
            if (ConditionalPatterns.Else.IsMatch(text) && effectiveDepth == 1 && elseIndex == -1)
            {
                elseIndex = i;
            }
        }

        return new ConditionalMarkers
        {
            ElseIfMarkers = elseIfMarkers,
            ElseIndex = elseIndex,
            EndIndex = endIndex
        };
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

        return ConditionalPatterns.IfStart.IsMatch(text) ||
               ConditionalPatterns.ElseIf.IsMatch(text) ||
               ConditionalPatterns.Else.IsMatch(text) ||
               ConditionalPatterns.IfEnd.IsMatch(text);
    }

    /// <summary>
    /// Kind of a conditional marker found at table-row level.
    /// </summary>
    private enum RowMarkerKind
    {
        If,
        ElseIf,
        Else,
        End
    }

    /// <summary>
    /// A conditional marker that belongs to a table row (as opposed to one confined to a single cell).
    /// </summary>
    private readonly record struct RowMarker(RowMarkerKind Kind, string? Condition);

    /// <summary>
    /// Matches any conditional marker, in document order. Alternatives are ordered so that
    /// {{#elseif ...}} is never mistaken for {{#else}} or {{#if ...}}.
    /// </summary>
    private static readonly Regex _anyMarkerPattern = new Regex(
        @"\{\{(?:#elseif\s+(?<elseif>.+?)|(?<else>#else)|#if\s+(?<if>.+?)|(?<end>/if))\}\}",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>
    /// Detects table row conditionals within a table.
    /// </summary>
    internal static IReadOnlyList<ConditionalBlock> DetectTableRowConditionals(Table table)
    {
        return DetectTableRowConditionals(table.Elements<TableRow>().ToList());
    }

    /// <summary>
    /// Detects table row conditionals in a sequence of table rows.
    /// Table row conditionals have their {{#if}}, {{#elseif}}, {{#else}} and {{/if}} markers in separate rows;
    /// marker rows are removed, and the rows between them are kept or removed as a whole.
    /// Conditionals confined to a single cell (inline or multi-paragraph) are not row conditionals:
    /// they are left for cell-level processing.
    /// </summary>
    /// <param name="rows">The rows to scan (e.g. all rows of a table, or the cloned rows of a loop iteration).</param>
    /// <param name="nestingLevel">Current nesting level (0 for top-level, 1+ for nested).</param>
    internal static IReadOnlyList<ConditionalBlock> DetectTableRowConditionals(
        IReadOnlyList<TableRow> rows,
        int nestingLevel = 0)
    {
        List<ConditionalBlock> conditionals = new List<ConditionalBlock>();
        RowMarker?[] markers = rows.Select(GetRowLevelMarker).ToArray();
        int i = 0;

        while (i < rows.Count)
        {
            if (markers[i] is not { Kind: RowMarkerKind.If } ifMarker)
            {
                i++;
                continue;
            }

            string conditionExpression = ifMarker.Condition!;

            // Collect the branch markers at our depth and the matching end marker.
            List<(int MarkerIndex, string? Condition)> branchMarkers = new List<(int, string?)>
            {
                (i, conditionExpression)
            };
            int elseIndex = -1;
            int endIndex = -1;
            int depth = 1;

            for (int j = i + 1; j < rows.Count && endIndex == -1; j++)
            {
                if (markers[j] is not RowMarker marker)
                {
                    continue;
                }

                switch (marker.Kind)
                {
                    case RowMarkerKind.If:
                        depth++;
                        break;
                    case RowMarkerKind.End:
                        depth--;
                        if (depth == 0)
                        {
                            endIndex = j;
                        }

                        break;
                    case RowMarkerKind.ElseIf when depth == 1:
                        if (elseIndex != -1)
                        {
                            throw new InvalidOperationException(
                                "Invalid conditional structure: '{{#elseif}}' cannot appear after '{{#else}}'. " +
                                "The '{{#else}}' branch must be the last branch before '{{/if}}'.");
                        }

                        branchMarkers.Add((j, marker.Condition));
                        break;
                    case RowMarkerKind.Else when depth == 1 && elseIndex == -1:
                        elseIndex = j;
                        branchMarkers.Add((j, null));
                        break;
                }
            }

            if (endIndex == -1)
            {
                throw new InvalidOperationException(
                    $"Table row conditional start marker '{{{{#if {conditionExpression}}}}}' has no matching '{{{{/if}}}}'.");
            }

            List<ConditionalBranch> branches = new List<ConditionalBranch>();
            for (int m = 0; m < branchMarkers.Count; m++)
            {
                int markerIndex = branchMarkers[m].MarkerIndex;
                int contentEnd = m + 1 < branchMarkers.Count ? branchMarkers[m + 1].MarkerIndex : endIndex;

                List<OpenXmlElement> contentRows = new List<OpenXmlElement>();
                for (int j = markerIndex + 1; j < contentEnd; j++)
                {
                    contentRows.Add(rows[j]);
                }

                branches.Add(new ConditionalBranch(branchMarkers[m].Condition, contentRows, rows[markerIndex]));
            }

            conditionals.Add(new ConditionalBlock(
                branches,
                rows[endIndex],
                isTableRowConditional: true,
                nestingLevel: nestingLevel));

            // Nested table row conditionals inside the branches
            foreach (ConditionalBranch branch in branches)
            {
                if (branch.ContentElements.Count > 0)
                {
                    conditionals.AddRange(DetectTableRowConditionals(
                        branch.ContentElements.Cast<TableRow>().ToList(),
                        nestingLevel + 1));
                }
            }

            i = endIndex + 1;
        }

        return conditionals;
    }

    /// <summary>
    /// Gets the row-level conditional marker of a table row, if any.
    /// </summary>
    /// <remarks>
    /// Markers that open and close within the same cell (inline or multi-paragraph cell conditionals,
    /// including their {{#elseif}}/{{#else}}) are cell-level and ignored here. A marker is row-level when it
    /// is not enclosed by an {{#if}} of the same cell: an {{#if}} left open at the end of its cell, or an
    /// {{#elseif}}/{{#else}}/{{/if}} that does not belong to an {{#if}} of the same cell.
    /// </remarks>
    /// <exception cref="InvalidOperationException">The row contains more than one row-level marker.</exception>
    private static RowMarker? GetRowLevelMarker(TableRow row)
    {
        List<RowMarker> rowMarkers = new List<RowMarker>();

        foreach (TableCell cell in row.Elements<TableCell>())
        {
            string text = cell.InnerText;
            if (text.Length == 0)
            {
                continue;
            }

            Stack<string> openIfs = new Stack<string>();

            foreach (Match match in _anyMarkerPattern.Matches(text))
            {
                if (match.Groups["if"].Success)
                {
                    openIfs.Push(match.Groups["if"].Value.Trim());
                }
                else if (match.Groups["end"].Success)
                {
                    if (openIfs.Count > 0)
                    {
                        openIfs.Pop();
                    }
                    else
                    {
                        rowMarkers.Add(new RowMarker(RowMarkerKind.End, null));
                    }
                }
                else if (openIfs.Count == 0)
                {
                    rowMarkers.Add(match.Groups["elseif"].Success
                        ? new RowMarker(RowMarkerKind.ElseIf, match.Groups["elseif"].Value.Trim())
                        : new RowMarker(RowMarkerKind.Else, null));
                }
            }

            // {{#if}} markers still open at the end of the cell continue at row level (outermost first).
            foreach (string condition in openIfs.Reverse())
            {
                rowMarkers.Add(new RowMarker(RowMarkerKind.If, condition));
            }
        }

        if (rowMarkers.Count > 1)
        {
            throw new InvalidOperationException(
                "Invalid table row conditional: each '{{#if}}', '{{#elseif}}', '{{#else}}' and '{{/if}}' " +
                "that spans table rows must be placed in its own row.");
        }

        return rowMarkers.Count == 1 ? rowMarkers[0] : null;
    }
}
