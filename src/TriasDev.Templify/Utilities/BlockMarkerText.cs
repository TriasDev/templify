// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Text;
using System.Text.RegularExpressions;
using TriasDev.Templify.Conditionals;
using TriasDev.Templify.Core;
using TriasDev.Templify.Loops;

namespace TriasDev.Templify.Utilities;

/// <summary>
/// Finds the text that is lost when a block marker's paragraph (or table row, list item, …) is removed, and words
/// the <see cref="ProcessingWarningType.BlockMarkerWithText"/> / <see cref="ValidationErrorType.BlockMarkerWithText"/>
/// message (#253). Format-agnostic: callers pass the marker element's own text and a name for the element.
/// </summary>
internal static partial class BlockMarkerText
{
    private const int MaxQuotedLength = 100;

    // The patterns of the loop and conditional detectors, so that exactly the text the detectors read as markers
    // is treated as marker text.
    private static readonly Regex[] _markerPatterns =
    {
        LoopDetector.ForeachStart,
        LoopDetector.ForeachEnd,
        ConditionalPatterns.IfStart,
        ConditionalPatterns.ElseIf,
        ConditionalPatterns.Else,
        ConditionalPatterns.IfEnd,
    };

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceRegex();

    // Invisible format characters (zero-width space, bidi marks, soft hyphen, BOM) are not lost text a reader sees.
    [GeneratedRegex(@"\p{Cf}", RegexOptions.CultureInvariant)]
    private static partial Regex FormatCharacterRegex();

    /// <summary>
    /// Returns the message for a marker element whose text other than its block markers is lost when the element is
    /// removed, or <c>null</c> when the element holds only markers and whitespace.
    /// </summary>
    /// <param name="elementText">The marker element's own text.</param>
    /// <param name="unit">What is removed, e.g. "paragraph" or "table row".</param>
    public static string? GetLostTextMessage(string elementText, string unit)
    {
        (List<string> markers, string remainder) = Split(elementText);
        if (markers.Count == 0 || remainder.Length == 0)
        {
            return null;
        }

        string quotedMarkers = string.Join(", ", markers.Distinct(StringComparer.Ordinal).Select(m => $"'{m}'"));
        string noun = markers.Distinct(StringComparer.Ordinal).Count() == 1 ? "marker" : "markers";
        return $"Text next to the block {noun} {quotedMarkers} is removed together with its {unit}: '{Quote(remainder)}'. " +
               $"Put block markers in {unit}s of their own.";
    }

    /// <summary>
    /// Returns the message for a loop whose start and end markers are in the same element (an inline loop, which is
    /// not supported: the element is removed), or <c>null</c> when nothing but markers and whitespace is lost.
    /// </summary>
    public static string? GetInlineLoopMessage(string elementText, string unit)
    {
        (List<string> markers, string remainder) = Split(elementText);
        string? startMarker = markers.FirstOrDefault(m => LoopDetector.ForeachStart.IsMatch(m));
        if (startMarker == null || remainder.Length == 0)
        {
            return null;
        }

        return $"The loop '{startMarker}' opens and closes in the same {unit}, which is not supported: " +
               $"the {unit} is removed with its text '{Quote(Normalize(elementText))}'. " +
               $"Put '{{{{#foreach}}}}' and '{{{{/foreach}}}}' in {unit}s of their own.";
    }

    /// <summary>Adds a warning for each message, once per message (blocks in loops are processed per iteration).</summary>
    public static void Report(IWarningCollector warningCollector, IEnumerable<string> messages)
    {
        foreach (string message in messages)
        {
            if (!warningCollector.GetWarnings().Any(w => w.Type == ProcessingWarningType.BlockMarkerWithText && w.Message == message))
            {
                warningCollector.AddWarning(ProcessingWarning.BlockMarkerWithText(message));
            }
        }
    }

    /// <summary>The element text for a validation error's location: whitespace collapsed, shortened.</summary>
    public static string GetLocation(string elementText) => Quote(Normalize(elementText));

    private static (List<string> Markers, string Remainder) Split(string text)
    {
        List<Match> matches = _markerPatterns
            .SelectMany(p => p.Matches(text))
            .OrderBy(m => m.Index)
            .ToList();

        List<string> markers = new List<string>();
        StringBuilder remainder = new StringBuilder();
        int position = 0;
        foreach (Match match in matches)
        {
            if (match.Index < position)
            {
                continue; // overlaps a marker already taken
            }

            remainder.Append(text, position, match.Index - position).Append(' ');
            markers.Add(match.Value);
            position = match.Index + match.Length;
        }

        remainder.Append(text, position, text.Length - position);
        return (markers, Normalize(remainder.ToString()));
    }

    private static string Normalize(string text) =>
        WhitespaceRegex().Replace(FormatCharacterRegex().Replace(text, string.Empty), " ").Trim();

    private static string Quote(string text) =>
        text.Length <= MaxQuotedLength ? text : text.Substring(0, MaxQuotedLength) + "…";
}
