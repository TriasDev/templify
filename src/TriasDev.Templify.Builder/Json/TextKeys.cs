// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Text;
using System.Text.RegularExpressions;

namespace TriasDev.Templify.Builder.Json;

/// <summary>
/// Text keys in JSON definitions. The export composes with key markers (<see cref="Marker"/>), so a translated text
/// can be found wherever the definition's code put it; the JSON writes it as <c>[[key]]</c>, and the import puts the
/// build language's translation back.
/// </summary>
internal static partial class TextKeys
{
    /// <summary>Starts a key marker (a private-use character, never in real text).</summary>
    public const char MarkerStart = '';

    /// <summary>Ends a key marker.</summary>
    public const char MarkerEnd = '';

    private const string Open = "[[";
    private const string Close = "]]";

    /// <summary>The marker of a key, returned by the texts while exporting.</summary>
    public static string Marker(string key) => MarkerStart + key + MarkerEnd;

    /// <summary>Whether a key can be written as <c>[[key]]</c>.</summary>
    public static bool IsKey(string key) => KeyPattern().IsMatch(key);

    /// <summary>Whether a text holds a key marker.</summary>
    public static bool HasMarkers(string text) => text.Contains(MarkerStart, StringComparison.Ordinal);

    /// <summary>The keys of the markers in a text, in order.</summary>
    public static IEnumerable<string> MarkedKeys(string text)
    {
        int position = 0;
        while ((position = text.IndexOf(MarkerStart, position)) >= 0)
        {
            int end = text.IndexOf(MarkerEnd, position + 1);
            if (end < 0)
            {
                yield break;
            }

            yield return text[(position + 1)..end];
            position = end + 1;
        }
    }

    /// <summary>Replaces every marker by <c>[[key]]</c>.</summary>
    public static string MarkersToKeys(string text) =>
        HasMarkers(text) ? MarkerText().Replace(text, m => Open + m.Groups[1].Value + Close) : text;

    /// <summary>
    /// Replaces every <c>[[key]]</c> by <paramref name="lookup"/>'s text. A lone <c>]]</c> is text; a <c>[[</c> that
    /// does not start a key is an error.
    /// </summary>
    /// <exception cref="FormatException">A <c>[[</c> does not start a valid key.</exception>
    public static string Resolve(string text, Func<string, string> lookup)
    {
        int start = text.IndexOf(Open, StringComparison.Ordinal);
        if (start < 0)
        {
            return text;
        }

        StringBuilder result = new StringBuilder();
        int position = 0;
        while (start >= 0)
        {
            int end = text.IndexOf(Close, start + Open.Length, StringComparison.Ordinal);
            string key = end < 0 ? string.Empty : text[(start + Open.Length)..end];
            if (end < 0 || !IsKey(key))
            {
                string shown = end < 0 ? text[start..] : text[start..(end + Close.Length)];
                throw new FormatException($"'{shown}' is not a text key: write [[key]] with letters, digits, '_', '.' or '-'.");
            }

            result.Append(text, position, start - position).Append(lookup(key));
            position = end + Close.Length;
            start = text.IndexOf(Open, position, StringComparison.Ordinal);
        }

        return result.Append(text, position, text.Length - position).ToString();
    }

    [GeneratedRegex("^[A-Za-z0-9_.-]+$")]
    private static partial Regex KeyPattern();

    [GeneratedRegex("([^]*)")]
    private static partial Regex MarkerText();
}
