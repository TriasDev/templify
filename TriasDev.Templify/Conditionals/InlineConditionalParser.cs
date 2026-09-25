// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Text.RegularExpressions;

namespace TriasDev.Templify.Conditionals;

/// <summary>
/// A branch of an inline conditional: its condition (null for <c>{{#else}}</c>) and the
/// [<see cref="ContentStart"/>, <see cref="ContentEnd"/>) range of its content in the parsed text.
/// </summary>
internal sealed record InlineConditionalBranch(string? Condition, int ContentStart, int ContentEnd);

/// <summary>
/// An inline conditional (<c>{{#if}}</c> … <c>{{/if}}</c> within one text) occupying
/// [<see cref="StartIndex"/>, <see cref="EndIndex"/>) of the parsed text.
/// </summary>
internal sealed record InlineConditional(int StartIndex, int EndIndex, IReadOnlyList<InlineConditionalBranch> Branches);

/// <summary>
/// Finds the top-level inline conditionals in a text, honoring nesting and elseif/else branches.
/// Nested conditionals are part of a branch's content range and can be found by parsing that range.
/// </summary>
/// <remarks>
/// The parser works on plain text only, so it is independent of the document format.
/// </remarks>
internal static class InlineConditionalParser
{
    /// <summary>
    /// Finds all top-level inline conditionals in <paramref name="text"/>, left to right.
    /// Unmatched <c>{{#if}}</c> markers are skipped (left as text).
    /// </summary>
    /// <exception cref="InvalidOperationException">An <c>{{#elseif}}</c> follows an <c>{{#else}}</c>.</exception>
    public static List<InlineConditional> Parse(string text)
    {
        List<InlineConditional> result = new List<InlineConditional>();

        int searchStart = 0;
        while (searchStart < text.Length)
        {
            Match ifMatch = ConditionalPatterns.IfStart.Match(text, searchStart);
            if (!ifMatch.Success)
            {
                break;
            }

            // Branch markers at our level (depth 1), starting with the {{#if}} itself.
            List<(int Index, int Length, string? Condition)> branchMarkers = new List<(int, int, string?)>
            {
                (ifMatch.Index, ifMatch.Length, ifMatch.Groups[1].Value.Trim()),
            };

            int depth = 1;
            int pos = ifMatch.Index + ifMatch.Length;
            int endMatchIndex = -1;
            int endMatchLength = 0;
            bool hasElseAtOurLevel = false;

            while (pos < text.Length && depth > 0)
            {
                Match nextIfMatch = ConditionalPatterns.IfStart.Match(text, pos);
                Match nextEndMatch = ConditionalPatterns.IfEnd.Match(text, pos);
                Match nextElseIfMatch = ConditionalPatterns.ElseIf.Match(text, pos);
                Match nextElseMatch = ConditionalPatterns.Else.Match(text, pos);

                int nextIfPos = nextIfMatch.Success ? nextIfMatch.Index : int.MaxValue;
                int nextEndPos = nextEndMatch.Success ? nextEndMatch.Index : int.MaxValue;
                int nextElseIfPos = nextElseIfMatch.Success ? nextElseIfMatch.Index : int.MaxValue;
                int nextElsePos = nextElseMatch.Success ? nextElseMatch.Index : int.MaxValue;

                int minPos = Math.Min(Math.Min(nextIfPos, nextEndPos), Math.Min(nextElseIfPos, nextElsePos));

                if (minPos == int.MaxValue)
                {
                    break;
                }

                if (minPos == nextEndPos)
                {
                    depth--;
                    if (depth == 0)
                    {
                        endMatchIndex = nextEndMatch.Index;
                        endMatchLength = nextEndMatch.Length;
                        break;
                    }

                    pos = nextEndMatch.Index + nextEndMatch.Length;
                }
                else if (minPos == nextIfPos)
                {
                    depth++;
                    pos = nextIfMatch.Index + nextIfMatch.Length;
                }
                else if (minPos == nextElseIfPos && depth == 1)
                {
                    if (hasElseAtOurLevel)
                    {
                        throw new InvalidOperationException(
                            "Invalid conditional structure: '{{#elseif}}' cannot appear after '{{#else}}'. " +
                            "The '{{#else}}' branch must be the last branch before '{{/if}}'.");
                    }

                    branchMarkers.Add((nextElseIfMatch.Index, nextElseIfMatch.Length, nextElseIfMatch.Groups[1].Value.Trim()));
                    pos = nextElseIfMatch.Index + nextElseIfMatch.Length;
                }
                else if (minPos == nextElsePos && depth == 1)
                {
                    hasElseAtOurLevel = true;
                    branchMarkers.Add((nextElseMatch.Index, nextElseMatch.Length, null));
                    pos = nextElseMatch.Index + nextElseMatch.Length;
                }
                else
                {
                    // Marker at a deeper level: skip it.
                    pos = minPos + 1;
                }
            }

            if (endMatchIndex < 0)
            {
                // Unmatched conditional: leave it as text.
                searchStart = ifMatch.Index + ifMatch.Length;
                continue;
            }

            List<InlineConditionalBranch> branches = new List<InlineConditionalBranch>(branchMarkers.Count);
            for (int m = 0; m < branchMarkers.Count; m++)
            {
                (int index, int length, string? condition) = branchMarkers[m];
                int contentEnd = m + 1 < branchMarkers.Count ? branchMarkers[m + 1].Index : endMatchIndex;
                branches.Add(new InlineConditionalBranch(condition, index + length, contentEnd));
            }

            int endIndex = endMatchIndex + endMatchLength;
            result.Add(new InlineConditional(ifMatch.Index, endIndex, branches));
            searchStart = endIndex;
        }

        return result;
    }
}
