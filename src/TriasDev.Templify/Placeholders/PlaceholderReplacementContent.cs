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
    public static ReplacementContent Create(object? value, string? format, PlaceholderReplacementOptions options)
    {
        // The :raw specifier only disables markdown; the value itself uses default conversion.
        bool isRaw = string.Equals(format, "raw", StringComparison.OrdinalIgnoreCase);
        bool parseMarkdown = options.EnableMarkdown && !isRaw;

        if (!isRaw && JoinFormat.TryParse(format, out JoinFormat? join) && JoinFormat.IsCollection(value))
        {
            List<ReplacementContent> items = join.GetItemTexts(value!, options.Culture)
                .Select(text => ReplacementContent.FromValue(Clean(text, options), options.EnableNewlineSupport, parseMarkdown))
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
