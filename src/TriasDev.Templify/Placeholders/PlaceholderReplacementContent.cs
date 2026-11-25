// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using TriasDev.Templify.Core;
using TriasDev.Templify.Replacements;
using TriasDev.Templify.Utilities;

namespace TriasDev.Templify.Placeholders;

/// <summary>
/// Builds the replacement of a resolved placeholder value for the document engines (Word and OpenDocument): format
/// conversion, text replacements, XML sanitizing, then newline and markdown pieces. A <c>:join</c> list is built per
/// item, with the separator as literal text between the items, so markdown markers never pair across items or inside
/// the separator.
/// </summary>
internal static class PlaceholderReplacementContent
{
    /// <summary>Builds the replacement for a resolved placeholder <paramref name="value"/>.</summary>
    /// <param name="value">The resolved value.</param>
    /// <param name="format">The placeholder's format specifier, or <c>null</c>.</param>
    /// <param name="options">Culture, formatters, text replacements, newline and markdown options.</param>
    public static ReplacementContent Create(object? value, string? format, PlaceholderReplacementOptions options)
    {
        // The :raw specifier only disables markdown; the value itself uses default conversion.
        bool isRaw = string.Equals(format, "raw", StringComparison.OrdinalIgnoreCase);
        bool parseMarkdown = options.EnableMarkdown && !isRaw;

        if (JoinFormat.TryParse(format, out JoinFormat? join) && JoinFormat.IsCollection(value))
        {
            // Items that become empty only after cleaning are skipped too, so there is no "a, , b".
            List<ReplacementContent> items = join.GetItemTexts(value!, options.Culture)
                .Select(text => Clean(text, options))
                .Where(text => text.Length > 0)
                .Select(text => ReplacementContent.FromValue(text, options.EnableNewlineSupport, parseMarkdown))
                .ToList();
            return ReplacementContent.Join(items, join.Separator);
        }

        string converted = ValueConverter.ConvertToString(value, options.Culture, isRaw ? null : format, options.BooleanFormatterRegistry);
        return ReplacementContent.FromValue(Clean(converted, options), options.EnableNewlineSupport, parseMarkdown);
    }

    /// <summary>Text replacements (e.g. HTML entities), then removal of characters invalid in XML 1.0.</summary>
    private static string Clean(string text, PlaceholderReplacementOptions options) =>
        XmlCharacterSanitizer.Sanitize(TextReplacements.Apply(text, options.TextReplacements))!;
}
