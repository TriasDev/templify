using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using System.Text.RegularExpressions;

namespace TriasDev.Templify.Loops;

/// <summary>
/// Detects and parses loop blocks in Word documents.
/// Supports {{#foreach CollectionName}}...{{/foreach}} syntax.
/// </summary>
internal static class LoopDetector
{
    private static readonly Regex ForeachStartPattern = new Regex(
        @"\{\{#foreach\s+([\w.]+)\}\}",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex ForeachEndPattern = new Regex(
        @"\{\{/foreach\}\}",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex EmptyStartPattern = new Regex(
        @"\{\{#empty\}\}",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex EmptyEndPattern = new Regex(
        @"\{\{/empty\}\}",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Detects all loop blocks in the document body.
    /// </summary>
    public static IReadOnlyList<LoopBlock> DetectLoops(WordprocessingDocument document)
    {
        if (document.MainDocumentPart?.Document?.Body == null)
        {
            return Array.Empty<LoopBlock>();
        }

        Body body = document.MainDocumentPart.Document.Body;
        List<LoopBlock> loops = new List<LoopBlock>();

        // First, detect paragraph-level loops
        loops.AddRange(DetectLoopsInElements(body.Elements<OpenXmlElement>().ToList()));

        // Second, detect table row loops within tables
        foreach (Table table in body.Elements<Table>())
        {
            loops.AddRange(DetectTableRowLoops(table));
        }

        return loops;
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
                Match foreachMatch = ForeachStartPattern.Match(text);
                if (foreachMatch.Success)
                {
                    string collectionName = foreachMatch.Groups[1].Value;

                    // Find the matching end marker
                    int endIndex = FindMatchingEnd(elements, i);
                    if (endIndex == -1)
                    {
                        throw new InvalidOperationException(
                            $"Loop start marker '{{{{#foreach {collectionName}}}}}' has no matching '{{{{/foreach}}}}'.");
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
                        contentElements,
                        element,
                        elements[endIndex],
                        isTableRowLoop: false,
                        emptyBlock: null);

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
            MatchCollection startMatches = ForeachStartPattern.Matches(startText);
            MatchCollection endMatches = ForeachEndPattern.Matches(startText);

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
            MatchCollection startMatches = ForeachStartPattern.Matches(text);
            depth += startMatches.Count;

            // Count all {{/foreach}} occurrences in this element
            MatchCollection endMatches = ForeachEndPattern.Matches(text);
            depth -= endMatches.Count;

            if (depth == 0)
            {
                return i; // Found matching end
            }
        }

        return -1; // No matching end found
    }

    /// <summary>
    /// Gets the text content of an element (paragraph, table cell, or structured document tag).
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

        // Handle SdtElement (Structured Document Tags) which can contain content controls
        // These can have loop markers and need to be checked
        if (element is DocumentFormat.OpenXml.Wordprocessing.SdtElement sdt)
        {
            return sdt.InnerText;
        }

        // For tables, don't return text - they're handled separately
        if (element is Table)
        {
            return null;
        }

        return null;
    }

    /// <summary>
    /// Checks if an element contains a loop marker.
    /// </summary>
    public static bool ContainsLoopMarker(OpenXmlElement element)
    {
        string? text = GetElementText(element);
        if (text == null)
        {
            return false;
        }

        return ForeachStartPattern.IsMatch(text) || ForeachEndPattern.IsMatch(text);
    }

    /// <summary>
    /// Detects table row loops within a table.
    /// Table row loops have {{#foreach}} and {{/foreach}} markers in separate rows.
    /// </summary>
    /// <remarks>
    /// Made internal for use by DocumentWalker in Phase 2 visitor pattern refactoring.
    /// </remarks>
    internal static IReadOnlyList<LoopBlock> DetectTableRowLoops(Table table)
    {
        List<LoopBlock> loops = new List<LoopBlock>();
        List<TableRow> rows = table.Elements<TableRow>().ToList();
        int i = 0;

        while (i < rows.Count)
        {
            TableRow row = rows[i];
            string? text = row.InnerText;

            if (text != null)
            {
                Match foreachMatch = ForeachStartPattern.Match(text);
                if (foreachMatch.Success)
                {
                    string collectionName = foreachMatch.Groups[1].Value;

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
                        throw new InvalidOperationException(
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
                        contentRows,
                        rows[i],      // Start marker row
                        rows[endIndex], // End marker row
                        isTableRowLoop: true,
                        emptyBlock: null);

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
    private static int FindMatchingEndInRows(List<TableRow> rows, int startIndex)
    {
        int depth = 1;

        for (int i = startIndex + 1; i < rows.Count; i++)
        {
            string? text = rows[i].InnerText;
            if (text == null)
            {
                continue;
            }

            if (ForeachStartPattern.IsMatch(text))
            {
                depth++;
            }
            else if (ForeachEndPattern.IsMatch(text))
            {
                depth--;
                if (depth == 0)
                {
                    return i;
                }
            }
        }

        return -1; // No matching end found
    }

    /// <summary>
    /// Checks if a specific loop is contained entirely within a single table cell.
    /// This happens when both {{#foreach CollectionName}} and {{/foreach}} markers are in the same cell.
    /// </summary>
    /// <param name="row">The table row to check.</param>
    /// <param name="collectionName">The specific collection name to check for.</param>
    /// <returns>True if the loop for this collection is fully contained in a single cell.</returns>
    private static bool IsLoopContainedInSingleCell(TableRow row, string collectionName)
    {
        // Create a regex pattern for this specific collection
        Regex specificStartPattern = new Regex(
            $@"\{{\{{#foreach\s+{Regex.Escape(collectionName)}\}}\}}",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // Check each cell in the row
        foreach (TableCell cell in row.Elements<TableCell>())
        {
            string? cellText = cell.InnerText;
            if (cellText == null)
            {
                continue;
            }

            // Check if this cell contains the specific foreach start marker
            if (specificStartPattern.IsMatch(cellText))
            {
                // Check if the matching end marker is also in this cell
                // For simplicity, we check if there's at least one {{/foreach}} in the cell
                // and that the number of starts <= number of ends (meaning this specific loop is closed)

                MatchCollection startMatches = ForeachStartPattern.Matches(cellText);
                MatchCollection endMatches = ForeachEndPattern.Matches(cellText);

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
