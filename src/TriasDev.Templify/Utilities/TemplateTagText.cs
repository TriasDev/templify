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
    /// R1: the text from the first tag start that has no <c>}}</c> after it, e.g. <c>{{Na</c> in <c>Hello {{Na</c>;
    /// <c>null</c> when every tag start is closed. Stray <c>}}</c> and braces not starting a tag are not reported.
    /// </summary>
    public static string? FindUnterminatedTag(string text)
    {
        foreach (Match start in TagStartRegex().Matches(text))
        {
            if (text.IndexOf("}}", start.Index + 2, StringComparison.Ordinal) < 0)
            {
                return text.Substring(start.Index).TrimEnd();
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
