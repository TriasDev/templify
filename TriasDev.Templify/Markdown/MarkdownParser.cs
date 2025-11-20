// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Text;
using System.Text.RegularExpressions;

namespace TriasDev.Templify.Markdown;

/// <summary>
/// Parses markdown syntax in text and converts it to formatted segments.
/// </summary>
internal static class MarkdownParser
{
    // Regex pattern to match markdown formatting:
    // - ~~text~~ for strikethrough
    // - ***text*** for bold+italic
    // - **text** or __text__ for bold
    // - *text* or _text_ for italic
    // Character class restrictions ([^~], [^*], [^_]) prevent catastrophic backtracking
    private static readonly Regex _markdownPattern = new(
        @"(~~(?<strike>[^~]+?)~~)" +                           // ~~strikethrough~~
        @"|((?<!\*)\*\*\*(?<bolditalic>[^*]+?)\*\*\*(?!\*))" + // ***bold+italic*** (not part of ****)
        @"|(?<!\*)\*\*(?<bold>[^*]+?)\*\*(?!\*)" +             // **bold** (not part of ***)
        @"|__(?<bold2>[^_]+?)__" +                             // __bold__
        @"|(?<![*_])\*(?<italic>[^*]+?)\*(?![*_])" +           // *italic* (not part of ** or _)
        @"|(?<![*_])_(?<italic2>[^_]+?)_(?![*_])",             // _italic_ (not part of __ or *)
        RegexOptions.Compiled);

    /// <summary>
    /// Parses text containing markdown syntax into a list of formatted segments.
    /// </summary>
    /// <param name="text">The text to parse.</param>
    /// <returns>A list of segments with formatting information.</returns>
    public static List<MarkdownSegment> Parse(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return new List<MarkdownSegment>();
        }

        List<MarkdownSegment> segments = new();
        int lastIndex = 0;

        foreach (Match match in _markdownPattern.Matches(text))
        {
            // Add any plain text before this match (filter empty segments)
            if (match.Index > lastIndex)
            {
                string plainText = text.Substring(lastIndex, match.Index - lastIndex);
                if (!string.IsNullOrEmpty(plainText))
                {
                    segments.Add(new MarkdownSegment(plainText));
                }
            }

            // Determine the formatting based on which group matched
            string content;
            bool isBold = false;
            bool isItalic = false;
            bool isStrikethrough = false;

            if (match.Groups["strike"].Success)
            {
                content = match.Groups["strike"].Value;
                isStrikethrough = true;
            }
            else if (match.Groups["bolditalic"].Success)
            {
                content = match.Groups["bolditalic"].Value;
                isBold = true;
                isItalic = true;
            }
            else if (match.Groups["bold"].Success)
            {
                content = match.Groups["bold"].Value;
                isBold = true;
            }
            else if (match.Groups["bold2"].Success)
            {
                content = match.Groups["bold2"].Value;
                isBold = true;
            }
            else if (match.Groups["italic"].Success)
            {
                content = match.Groups["italic"].Value;
                isItalic = true;
            }
            else if (match.Groups["italic2"].Success)
            {
                content = match.Groups["italic2"].Value;
                isItalic = true;
            }
            else
            {
                // Shouldn't happen, but treat as plain text
                content = match.Value;
            }

            // Only add non-empty segments
            if (!string.IsNullOrEmpty(content))
            {
                segments.Add(new MarkdownSegment(content, isBold, isItalic, isStrikethrough));
            }
            lastIndex = match.Index + match.Length;
        }

        // Add any remaining plain text after the last match (filter empty segments)
        if (lastIndex < text.Length)
        {
            string remainingText = text.Substring(lastIndex);
            if (!string.IsNullOrEmpty(remainingText))
            {
                segments.Add(new MarkdownSegment(remainingText));
            }
        }

        // If no markdown was found, return the entire text as a single segment
        if (segments.Count == 0)
        {
            segments.Add(new MarkdownSegment(text));
        }

        return segments;
    }

    /// <summary>
    /// Checks if the text contains any markdown formatting syntax.
    /// </summary>
    /// <param name="text">The text to check.</param>
    /// <returns>True if markdown syntax is detected, false otherwise.</returns>
    public static bool ContainsMarkdown(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        return _markdownPattern.IsMatch(text);
    }
}
