// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;

namespace TriasDev.Templify.Replacements;

/// <summary>
/// Provides text replacement functionality and built-in replacement presets.
/// </summary>
/// <remarks>
/// <para>
/// ADR: Lookup Table vs HTML Parser
/// </para>
/// <para>
/// Context: Need to transform HTML-like strings in template values to Word-compatible output.
/// </para>
/// <para>
/// Decision: Lookup Table approach using simple string-to-string replacement dictionary.
/// </para>
/// <para>
/// Rationale:
/// <list type="bullet">
/// <item><description>Simplicity: No complex HTML parsing required, just string.Replace() operations</description></item>
/// <item><description>Flexibility: Users can define custom mappings beyond HTML</description></item>
/// <item><description>Predictable: No edge cases from malformed HTML</description></item>
/// <item><description>Extensible: Easy to add new replacements without code changes</description></item>
/// <item><description>Sufficient: Paired tags like &lt;b&gt;text&lt;/b&gt; can use existing markdown support</description></item>
/// </list>
/// </para>
/// <para>
/// Trade-offs:
/// <list type="bullet">
/// <item><description>Cannot support opening/closing tag pairs - users should use markdown instead</description></item>
/// <item><description>Each variation needs explicit entry (e.g., &lt;br&gt;, &lt;br/&gt;, &lt;br /&gt; are separate)</description></item>
/// </list>
/// </para>
/// </remarks>
public static class TextReplacements
{
    /// <summary>
    /// Built-in preset for common HTML entities and line break tags.
    /// Converts HTML tags and entities to their Word-compatible equivalents.
    /// </summary>
    /// <remarks>
    /// Includes:
    /// <list type="bullet">
    /// <item><description>&lt;br&gt;, &lt;br/&gt;, &lt;br /&gt; → newline (\n)</description></item>
    /// <item><description>&amp;nbsp; → non-breaking space (U+00A0)</description></item>
    /// <item><description>&amp;lt; → &lt;</description></item>
    /// <item><description>&amp;gt; → &gt;</description></item>
    /// <item><description>&amp;amp; → &amp;</description></item>
    /// <item><description>&amp;quot; → "</description></item>
    /// <item><description>&amp;apos; → '</description></item>
    /// <item><description>&amp;mdash; → em dash (—)</description></item>
    /// <item><description>&amp;ndash; → en dash (–)</description></item>
    /// </list>
    /// </remarks>
    public static IReadOnlyDictionary<string, string> HtmlEntities { get; } = new ReadOnlyDictionary<string, string>(
        new Dictionary<string, string>
        {
            // Line break variations (lowercase and uppercase)
            ["<br>"] = "\n",
            ["<br/>"] = "\n",
            ["<br />"] = "\n",
            ["<BR>"] = "\n",
            ["<BR/>"] = "\n",
            ["<BR />"] = "\n",

            // Common HTML entities (lowercase only - per HTML spec, entities are case-sensitive)
            ["&nbsp;"] = "\u00A0",  // Non-breaking space
            ["&lt;"] = "<",
            ["&gt;"] = ">",
            ["&amp;"] = "&",
            ["&quot;"] = "\"",
            ["&apos;"] = "'",
            ["&mdash;"] = "\u2014", // Em dash (—)
            ["&ndash;"] = "\u2013", // En dash (–)
        });

    /// <summary>
    /// Applies text replacements to the input string.
    /// </summary>
    /// <param name="input">The input string to transform. If null, returns null.</param>
    /// <param name="replacements">Dictionary of text replacements to apply. If null or empty, returns input unchanged.</param>
    /// <returns>The transformed string with all replacements applied, or null if input was null.</returns>
    /// <remarks>
    /// Replacements are applied in dictionary enumeration order.
    /// For predictable behavior with overlapping patterns, consider using an ordered dictionary
    /// or applying replacements in a specific sequence.
    /// </remarks>
    public static string? Apply(string? input, IReadOnlyDictionary<string, string>? replacements)
    {
        if (string.IsNullOrEmpty(input) || replacements == null || replacements.Count == 0)
        {
            return input;
        }

        string result = input;
        foreach (KeyValuePair<string, string> replacement in replacements)
        {
            result = result.Replace(replacement.Key, replacement.Value);
        }

        return result;
    }
}
