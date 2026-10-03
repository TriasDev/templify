// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Text.RegularExpressions;
using System.Xml.Linq;
using TriasDev.Templify.Core;
using TriasDev.Templify.Loops;

namespace TriasDev.Templify.OpenDocument;

/// <summary>
/// A loop block (<c>{{#foreach}}</c> … <c>{{/foreach}}</c>) over sibling OpenDocument elements (paragraphs,
/// or table rows / list items for a row-level loop). The counterpart of <c>LoopBlock</c>.
/// </summary>
internal sealed record OdtLoopBlock(
    string CollectionName,
    string? IterationVariableName,
    IReadOnlyList<XElement> ContentElements,
    XElement StartMarker,
    XElement EndMarker,
    bool IsRowLoop);

/// <summary>
/// Detects loop blocks in sequences of OpenDocument elements. The counterpart of <c>LoopDetector</c>, with the
/// same syntax (<c>{{#foreach Items}}</c>, <c>{{#foreach item in Items}}</c>), validation and error messages.
/// </summary>
internal static partial class OdtLoopDetector
{
    private static readonly Regex _foreachStartPattern = ForeachStartRegex();
    private static readonly Regex _foreachEndPattern = ForeachEndRegex();

    [GeneratedRegex(
        @"\{\{#foreach\s+" + LoopDetector.IterationVariablePrefixPattern + @"([\w.]+)\}\}",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ForeachStartRegex();

    [GeneratedRegex(LoopDetector.ForeachEndPattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ForeachEndRegex();

    /// <summary>
    /// Detects the top-level loops in a sequence of sibling elements (nested loops are detected when the
    /// content of each iteration is processed).
    /// </summary>
    /// <exception cref="TemplateSyntaxException">A start marker is unmatched or the iteration variable is invalid.</exception>
    public static IReadOnlyList<OdtLoopBlock> DetectLoops(IReadOnlyList<XElement> elements)
    {
        List<OdtLoopBlock> loops = new List<OdtLoopBlock>();
        int i = 0;

        while (i < elements.Count)
        {
            string? text = OdtMarkerText.GetMarkerText(elements[i]);
            Match match = text != null ? _foreachStartPattern.Match(text) : Match.Empty;
            if (!match.Success)
            {
                i++;
                continue;
            }

            (string collectionName, string? iterationVariable) = ReadStartMarker(match);

            int endIndex = FindMatchingEnd(elements, i);
            if (endIndex == -1)
            {
                throw new TemplateSyntaxException(
                    ValidationErrorType.UnmatchedLoopStart,
                    $"Loop start marker '{{{{#foreach {collectionName}}}}}' has no matching '{{{{/foreach}}}}'.");
            }

            // A container (row, cell, list) that holds a complete loop is not a loop marker:
            // the loop is detected when the container's content is processed.
            if (endIndex == i && !OdfNames.IsParagraph(elements[i]))
            {
                i++;
                continue;
            }

            loops.Add(new OdtLoopBlock(
                collectionName,
                iterationVariable,
                Slice(elements, i + 1, endIndex),
                elements[i],
                elements[endIndex],
                IsRowLoop: false));

            i = endIndex + 1;
        }

        return loops;
    }

    /// <summary>
    /// Detects row-level loops in sibling table rows: <c>{{#foreach}}</c> and <c>{{/foreach}}</c> in their own
    /// rows. Loops confined to a single cell are left for cell-level processing.
    /// </summary>
    public static IReadOnlyList<OdtLoopBlock> DetectTableRowLoops(IReadOnlyList<XElement> rows) =>
        DetectUnitLoops(rows, row => OdtMarkerText.GetRowCells(row).Select(OdtMarkerText.GetOwnText), "Table row");

    /// <summary>
    /// Detects item-level loops in the items of a list: <c>{{#foreach}}</c> and <c>{{/foreach}}</c> in their own
    /// items, like table rows. Loops confined to a single item are left for item-level processing.
    /// </summary>
    public static IReadOnlyList<OdtLoopBlock> DetectListItemLoops(IReadOnlyList<XElement> items) =>
        DetectUnitLoops(items, item => new[] { OdtMarkerText.GetOwnText(item) }, "List item");

    private static IReadOnlyList<OdtLoopBlock> DetectUnitLoops(
        IReadOnlyList<XElement> units,
        Func<XElement, IEnumerable<string>> getTexts,
        string kind)
    {
        List<OdtLoopBlock> loops = new List<OdtLoopBlock>();
        int i = 0;

        while (i < units.Count)
        {
            string text = OdtMarkerText.GetOwnText(units[i]);
            Match match = _foreachStartPattern.Match(text);
            if (!match.Success)
            {
                i++;
                continue;
            }

            (string collectionName, string? iterationVariable) = ReadStartMarker(match);

            if (IsLoopContainedInSinglePart(getTexts(units[i]), collectionName))
            {
                i++;
                continue;
            }

            int endIndex = FindMatchingEndInUnits(units, i);
            if (endIndex == -1)
            {
                throw new TemplateSyntaxException(
                    ValidationErrorType.UnmatchedLoopStart,
                    $"{kind} loop start marker '{{{{#foreach {collectionName}}}}}' has no matching '{{{{/foreach}}}}'.");
            }

            loops.Add(new OdtLoopBlock(
                collectionName,
                iterationVariable,
                Slice(units, i + 1, endIndex),
                units[i],
                units[endIndex],
                IsRowLoop: true));

            i = endIndex + 1;
        }

        return loops;
    }

    private static (string CollectionName, string? IterationVariable) ReadStartMarker(Match match)
    {
        // Group 1: optional iteration variable ("item" in "item in Items"); group 2: collection name.
        string? iterationVariable = match.Groups[1].Success && match.Groups[1].Length > 0 ? match.Groups[1].Value : null;
        string collectionName = match.Groups[2].Value;

        if (iterationVariable != null)
        {
            LoopDetector.ValidateIterationVariableName(iterationVariable, collectionName);
        }

        return (collectionName, iterationVariable);
    }

    private static List<XElement> Slice(IReadOnlyList<XElement> elements, int start, int end)
    {
        List<XElement> result = new List<XElement>();
        for (int j = start; j < end; j++)
        {
            result.Add(elements[j]);
        }

        return result;
    }

    /// <summary>
    /// Finds the matching {{/foreach}} for the {{#foreach}} at <paramref name="startIndex"/>, tracking nesting.
    /// </summary>
    private static int FindMatchingEnd(IReadOnlyList<XElement> elements, int startIndex)
    {
        string startText = OdtMarkerText.GetMarkerText(elements[startIndex])!;
        int depth = _foreachStartPattern.Matches(startText).Count - _foreachEndPattern.Matches(startText).Count;
        if (depth == 0)
        {
            return startIndex;
        }

        for (int i = startIndex + 1; i < elements.Count; i++)
        {
            string? text = OdtMarkerText.GetMarkerText(elements[i]);
            if (text == null)
            {
                continue;
            }

            depth += _foreachStartPattern.Matches(text).Count;
            depth -= _foreachEndPattern.Matches(text).Count;

            if (depth == 0)
            {
                return i;
            }
        }

        return -1;
    }

    private static int FindMatchingEndInUnits(IReadOnlyList<XElement> units, int startIndex)
    {
        int depth = 1;

        for (int i = startIndex + 1; i < units.Count; i++)
        {
            // The net marker count: a loop confined to a single cell of this unit does not change the depth.
            string text = OdtMarkerText.GetOwnText(units[i]);
            depth += _foreachStartPattern.Matches(text).Count;
            depth -= _foreachEndPattern.Matches(text).Count;

            if (depth <= 0)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// Checks whether the loop over <paramref name="collectionName"/> is contained in one part (cell) of a unit.
    /// </summary>
    private static bool IsLoopContainedInSinglePart(IEnumerable<string> partTexts, string collectionName)
    {
        foreach (string text in partTexts)
        {
            MatchCollection starts = _foreachStartPattern.Matches(text);
            bool containsStart = starts.Any(m => string.Equals(m.Groups[2].Value, collectionName, StringComparison.OrdinalIgnoreCase));
            if (containsStart && starts.Count > 0 && _foreachEndPattern.Matches(text).Count >= starts.Count)
            {
                return true;
            }
        }

        return false;
    }
}
