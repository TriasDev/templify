// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Text.RegularExpressions;
using TriasDev.Templify.Conditionals;
using TriasDev.Templify.Loops;

namespace TriasDev.Templify.Utilities;

/// <summary>
/// Format-agnostic text rules of the template lint (#255) over an element's own text: unterminated tags (R1), block
/// markers outside inline conditionals (R3), end and branch markers (R9). Markers are matched with the detectors'
/// own patterns.
/// </summary>
internal static partial class TemplateTagText
{
    private static readonly Regex[] _blockMarkerPatterns =
    {
        LoopDetector.ForeachStart,
        LoopDetector.ForeachEnd,
        ConditionalPatterns.IfStart,
        ConditionalPatterns.ElseIf,
        ConditionalPatterns.Else,
        ConditionalPatterns.IfEnd,
    };

    private static readonly Regex[] _endAndBranchPatterns =
    {
        ConditionalPatterns.IfEnd,
        ConditionalPatterns.Else,
        ConditionalPatterns.ElseIf,
        LoopDetector.ForeachEnd,
    };

    /// <summary>A tag start: <c>{{</c> followed by a name character, <c>@</c>, <c>.</c>, <c>#</c>, <c>/</c> or <c>(</c>.</summary>
    [GeneratedRegex(@"\{\{[\w@.#/(]", RegexOptions.CultureInvariant)]
    private static partial Regex TagStartRegex();

    /// <summary>
    /// R1: the text of the first tag start that is not closed before the next tag start (or the end), e.g. <c>{{Na</c>
    /// in <c>Hello {{Na</c> and <c>{{B and</c> in <c>{{B and {{A}}</c>; <c>null</c> when every tag start is closed.
    /// Stray <c>}}</c> and braces not starting a tag are not reported.
    /// </summary>
    public static string? FindUnterminatedTag(string text)
    {
        MatchCollection starts = TagStartRegex().Matches(text);
        for (int i = 0; i < starts.Count; i++)
        {
            int start = starts[i].Index;
            int nextStart = i + 1 < starts.Count ? starts[i + 1].Index : text.Length;
            int close = text.IndexOf("}}", start + 2, StringComparison.Ordinal);
            if (close < 0 || close > nextStart)
            {
                return text.Substring(start, nextStart - start).TrimEnd();
            }
        }

        return null;
    }

    /// <summary>R3: the block markers of the text that are not part of a complete inline conditional.</summary>
    public static IReadOnlyList<string> GetBlockMarkersOutsideInlineConditionals(string text)
    {
        List<(int Start, int End)> inline = new List<(int, int)>();
        try
        {
            inline.AddRange(InlineConditionalParser.Parse(text).Select(c => (c.StartIndex, c.EndIndex)));
        }
        catch (Core.TemplateSyntaxException)
        {
            // A malformed inline conditional is reported elsewhere; count all markers.
        }

        return Matches(text, _blockMarkerPatterns)
            .Where(m => !inline.Any(r => m.Index >= r.Start && m.Index < r.End))
            .Select(m => m.Value)
            .ToList();
    }

    /// <summary>R9: the <c>{{/if}}</c>, <c>{{#else}}</c>, <c>{{#elseif …}}</c> and <c>{{/foreach}}</c> markers of the text.</summary>
    public static IReadOnlyList<string> GetEndAndBranchMarkers(string text) =>
        Matches(text, _endAndBranchPatterns).Select(m => m.Value).ToList();

    private static IEnumerable<Match> Matches(string text, IEnumerable<Regex> patterns) =>
        patterns.SelectMany(p => p.Matches(text)).OrderBy(m => m.Index);
}
