// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Text.RegularExpressions;
using System.Xml.Linq;
using TriasDev.Templify.Conditionals;
using TriasDev.Templify.Core;

namespace TriasDev.Templify.OpenDocument;

/// <summary>
/// Detects conditional blocks (<c>{{#if}}</c>/<c>{{#elseif}}</c>/<c>{{#else}}</c>/<c>{{/if}}</c>) in sequences of
/// OpenDocument elements. The counterpart of <c>ConditionalDetector</c>, with the same rules and error messages.
/// </summary>
internal static partial class OdtConditionalDetector
{
    private const string ElseIfAfterElseMessage =
        "Invalid conditional structure: '{{#elseif}}' cannot appear after '{{#else}}'. " +
        "The '{{#else}}' branch must be the last branch before '{{/if}}'.";

    /// <summary>
    /// Matches any conditional marker, in document order. Alternatives are ordered so that
    /// {{#elseif ...}} is never mistaken for {{#else}} or {{#if ...}}.
    /// </summary>
    private static readonly Regex _anyMarkerPattern = AnyMarkerRegex();

    [GeneratedRegex(
        @"\{\{(?:#elseif\s+(?<elseif>.+?)|(?<else>#else)|#if\s+(?<if>.+?)|(?<end>/if))\}\}",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AnyMarkerRegex();

    private enum RowMarkerKind
    {
        If,
        ElseIf,
        Else,
        End,
    }

    private readonly record struct RowMarker(RowMarkerKind Kind, string? Condition);

    /// <summary>
    /// Detects conditional blocks in a sequence of sibling elements, including nested blocks in all branches.
    /// A block whose markers are all in one paragraph is an inline conditional (start marker = end marker).
    /// </summary>
    /// <exception cref="TemplateSyntaxException">A marker is unmatched or an elseif follows the else.</exception>
    public static IReadOnlyList<OdtConditionalBlock> DetectConditionals(IReadOnlyList<XElement> elements, int nestingLevel = 0)
    {
        List<OdtConditionalBlock> conditionals = new List<OdtConditionalBlock>();
        int i = 0;

        while (i < elements.Count)
        {
            string? text = OdtMarkerText.GetMarkerText(elements[i]);
            Match ifMatch = text != null ? ConditionalPatterns.IfStart.Match(text) : Match.Empty;
            if (!ifMatch.Success)
            {
                i++;
                continue;
            }

            string conditionExpression = ifMatch.Groups[1].Value.Trim();
            (List<(int Index, string? Condition)> branchMarkers, int endIndex) = FindConditionalMarkers(elements, i, conditionExpression);

            if (endIndex == -1)
            {
                throw new TemplateSyntaxException(
                    ValidationErrorType.UnmatchedConditionalStart,
                    $"Conditional start marker '{{{{#if {conditionExpression}}}}}' has no matching '{{{{/if}}}}'.");
            }

            // A container (row, cell, list) that holds a complete conditional is not a marker: the conditional
            // is detected when its content is processed.
            if (endIndex == i && !OdfNames.IsParagraph(elements[i]))
            {
                i++;
                continue;
            }

            List<OdtConditionalBranch> branches = CreateBranches(elements, branchMarkers, endIndex);
            conditionals.Add(new OdtConditionalBlock(branches, elements[endIndex], isTableRowConditional: false, nestingLevel));

            foreach (OdtConditionalBranch branch in branches.Where(b => b.ContentElements.Count > 0))
            {
                conditionals.AddRange(DetectConditionals(branch.ContentElements, nestingLevel + 1));
            }

            i = endIndex + 1;
        }

        return conditionals;
    }

    /// <summary>
    /// Finds the branch markers (the {{#if}} itself, {{#elseif}}s and the {{#else}}) at the depth of the
    /// {{#if}} at <paramref name="startIndex"/>, and the index of the matching {{/if}} (-1 if none).
    /// </summary>
    private static (List<(int Index, string? Condition)> BranchMarkers, int EndIndex) FindConditionalMarkers(
        IReadOnlyList<XElement> elements,
        int startIndex,
        string condition)
    {
        List<(int Index, string? Condition)> branchMarkers = new List<(int, string?)> { (startIndex, condition) };
        bool hasElse = false;

        // A conditional fully contained in its start element is inline.
        string startText = OdtMarkerText.GetMarkerText(elements[startIndex])!;
        int depth = ConditionalPatterns.IfStart.Matches(startText).Count - ConditionalPatterns.IfEnd.Matches(startText).Count;
        if (depth == 0)
        {
            return (branchMarkers, startIndex);
        }

        for (int i = startIndex + 1; i < elements.Count; i++)
        {
            string? text = OdtMarkerText.GetMarkerText(elements[i]);
            if (text == null)
            {
                continue;
            }

            int ends = ConditionalPatterns.IfEnd.Matches(text).Count;
            depth += ConditionalPatterns.IfStart.Matches(text).Count;
            depth -= ends;

            if (depth == 0)
            {
                return (branchMarkers, i);
            }

            // Only markers at our level count (depth 1 before this element's end markers).
            if (depth + ends != 1)
            {
                continue;
            }

            Match elseIfMatch = ConditionalPatterns.ElseIf.Match(text);
            if (elseIfMatch.Success)
            {
                if (hasElse)
                {
                    throw new TemplateSyntaxException(ValidationErrorType.InvalidConditionalExpression, ElseIfAfterElseMessage);
                }

                branchMarkers.Add((i, elseIfMatch.Groups[1].Value.Trim()));
            }

            if (ConditionalPatterns.Else.IsMatch(text) && !hasElse)
            {
                hasElse = true;
                branchMarkers.Add((i, null));
            }
        }

        return (branchMarkers, -1);
    }

    private static List<OdtConditionalBranch> CreateBranches(
        IReadOnlyList<XElement> elements,
        List<(int Index, string? Condition)> branchMarkers,
        int endIndex)
    {
        List<OdtConditionalBranch> branches = new List<OdtConditionalBranch>();
        for (int m = 0; m < branchMarkers.Count; m++)
        {
            int markerIndex = branchMarkers[m].Index;
            int contentEnd = m + 1 < branchMarkers.Count ? branchMarkers[m + 1].Index : endIndex;

            List<XElement> content = new List<XElement>();
            for (int j = markerIndex + 1; j < contentEnd; j++)
            {
                content.Add(elements[j]);
            }

            branches.Add(new OdtConditionalBranch(branchMarkers[m].Condition, content, elements[markerIndex]));
        }

        return branches;
    }

    /// <summary>
    /// Detects table-row conditionals in a sequence of sibling rows: conditionals whose {{#if}}, {{#elseif}},
    /// {{#else}} and {{/if}} markers are in separate rows. Marker rows are removed, and the rows between them are
    /// kept or removed as a whole. Conditionals confined to a single cell are left for cell-level processing.
    /// </summary>
    public static IReadOnlyList<OdtConditionalBlock> DetectTableRowConditionals(IReadOnlyList<XElement> rows) =>
        DetectUnitConditionals(rows, _tableRows, nestingLevel: 0);

    /// <summary>
    /// Detects list-item conditionals in the items of a list: conditionals whose markers are in separate list
    /// items, like table-row conditionals. Marker items are removed; the items between them are kept or removed
    /// as a whole. Conditionals confined to a single item are left for item-level processing.
    /// </summary>
    public static IReadOnlyList<OdtConditionalBlock> DetectListItemConditionals(IReadOnlyList<XElement> items) =>
        DetectUnitConditionals(items, _listItems, nestingLevel: 0);

    /// <summary>How a kind of sibling unit (table row, list item) exposes its marker text.</summary>
    private sealed record UnitKind(string Name, string Plural, Func<XElement, IEnumerable<string>> GetTexts);

    private static readonly UnitKind _tableRows = new UnitKind(
        "table row",
        "table rows",
        row => OdtMarkerText.GetRowCells(row).Select(OdtMarkerText.GetOwnText));

    private static readonly UnitKind _listItems = new UnitKind(
        "list item",
        "list items",
        item => new[] { OdtMarkerText.GetOwnText(item) });

    private static IReadOnlyList<OdtConditionalBlock> DetectUnitConditionals(IReadOnlyList<XElement> rows, UnitKind kind, int nestingLevel)
    {
        List<OdtConditionalBlock> conditionals = new List<OdtConditionalBlock>();
        RowMarker?[] markers = rows.Select(row => GetUnitLevelMarker(row, kind)).ToArray();
        int i = 0;

        while (i < rows.Count)
        {
            if (markers[i] is not { Kind: RowMarkerKind.If } ifMarker)
            {
                i++;
                continue;
            }

            string conditionExpression = ifMarker.Condition!;
            List<(int Index, string? Condition)> branchMarkers = new List<(int, string?)> { (i, conditionExpression) };
            bool hasElse = false;
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
                        if (hasElse)
                        {
                            throw new TemplateSyntaxException(ValidationErrorType.InvalidConditionalExpression, ElseIfAfterElseMessage);
                        }

                        branchMarkers.Add((j, marker.Condition));
                        break;
                    case RowMarkerKind.Else when depth == 1 && !hasElse:
                        hasElse = true;
                        branchMarkers.Add((j, null));
                        break;
                }
            }

            if (endIndex == -1)
            {
                throw new TemplateSyntaxException(
                    ValidationErrorType.UnmatchedConditionalStart,
                    $"{Capitalize(kind.Name)} conditional start marker '{{{{#if {conditionExpression}}}}}' has no matching '{{{{/if}}}}'.");
            }

            List<OdtConditionalBranch> branches = CreateBranches(rows, branchMarkers, endIndex);
            conditionals.Add(new OdtConditionalBlock(branches, rows[endIndex], isTableRowConditional: true, nestingLevel));

            foreach (OdtConditionalBranch branch in branches.Where(b => b.ContentElements.Count > 0))
            {
                conditionals.AddRange(DetectUnitConditionals(branch.ContentElements, kind, nestingLevel + 1));
            }

            i = endIndex + 1;
        }

        return conditionals;
    }

    private static string Capitalize(string text) => char.ToUpperInvariant(text[0]) + text.Substring(1);

    /// <summary>
    /// Gets the unit-level conditional marker of a table row or list item, if any. Markers that open and
    /// close within the same cell (or item) are cell-level and ignored.
    /// </summary>
    /// <exception cref="TemplateSyntaxException">The unit contains more than one unit-level marker.</exception>
    private static RowMarker? GetUnitLevelMarker(XElement row, UnitKind kind)
    {
        List<RowMarker> rowMarkers = new List<RowMarker>();

        foreach (string text in kind.GetTexts(row))
        {
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

            foreach (string condition in openIfs.Reverse())
            {
                rowMarkers.Add(new RowMarker(RowMarkerKind.If, condition));
            }
        }

        if (rowMarkers.Count > 1)
        {
            throw new TemplateSyntaxException(
                ValidationErrorType.InvalidConditionalExpression,
                $"Invalid {kind.Name} conditional: each '{{{{#if}}}}', '{{{{#elseif}}}}', '{{{{#else}}}}' and '{{{{/if}}}}' " +
                $"that spans {kind.Plural} must be placed in its own {(kind == _tableRows ? "row" : "list item")}.");
        }

        return rowMarkers.Count == 1 ? rowMarkers[0] : null;
    }
}
