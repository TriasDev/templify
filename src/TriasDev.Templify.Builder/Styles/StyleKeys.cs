// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Text.RegularExpressions;

namespace TriasDev.Templify.Builder;

/// <summary>Text alignment of a paragraph, relative to the writing direction.</summary>
public enum TextAlignment
{
    /// <summary>Aligned to the start of the line (left in left-to-right text).</summary>
    Start = 0,

    /// <summary>Centered.</summary>
    Center = 1,

    /// <summary>Aligned to the end of the line (right in left-to-right text).</summary>
    End = 2,

    /// <summary>Justified.</summary>
    Justify = 3,
}

/// <summary>
/// Names a paragraph style. Define it once as a static field, use it in the <see cref="StyleSheet"/> and on paragraphs.
/// </summary>
public sealed record ParagraphStyleKey
{
    /// <summary>Initializes a paragraph style key.</summary>
    /// <param name="id">The style's id in the document: letters, digits, <c>_</c> and <c>-</c>, starting with a letter or <c>_</c>.</param>
    /// <param name="displayName">
    /// The name Word and LibreOffice show in their style lists. A name of one of Word's built-in styles (for example
    /// <c>Body Text</c>) is translated to LibreOffice's own style when LibreOffice opens the DOCX (<c>Text body</c>).
    /// </param>
    /// <exception cref="ArgumentException">The id or the display name is invalid, or the id is reserved by the builder.</exception>
    public ParagraphStyleKey(string id, string displayName)
    {
        Id = StyleKeyRules.ValidateId(id, nameof(id));
        DisplayName = StyleKeyRules.ValidateDisplayName(displayName, nameof(displayName));
    }

    /// <summary>Gets the style's id in the document.</summary>
    public string Id { get; }

    /// <summary>Gets the name Word and LibreOffice show.</summary>
    public string DisplayName { get; }
}

/// <summary>
/// Names a character style. Define it once as a static field, use it in the <see cref="StyleSheet"/> and on text.
/// </summary>
public sealed record CharacterStyleKey
{
    /// <summary>Initializes a character style key.</summary>
    /// <param name="id">The style's id in the document: letters, digits, <c>_</c> and <c>-</c>, starting with a letter or <c>_</c>.</param>
    /// <param name="displayName">The name Word and LibreOffice show in their style lists.</param>
    /// <exception cref="ArgumentException">The id or the display name is invalid, or the id is reserved by the builder.</exception>
    public CharacterStyleKey(string id, string displayName)
    {
        Id = StyleKeyRules.ValidateId(id, nameof(id));
        DisplayName = StyleKeyRules.ValidateDisplayName(displayName, nameof(displayName));
    }

    /// <summary>Gets the style's id in the document.</summary>
    public string Id { get; }

    /// <summary>Gets the name Word and LibreOffice show.</summary>
    public string DisplayName { get; }
}

/// <summary>The rules style keys follow in both formats.</summary>
internal static partial class StyleKeyRules
{
    /// <summary>
    /// Ids the builder writes itself: the default paragraph style of each format and Word's default character style.
    /// </summary>
    /// <summary>The prefix of the styles the builder owns (for example its page-break style).</summary>
    public const string ReservedPrefix = "Templify";

    public static readonly IReadOnlySet<string> ReservedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Normal", "Standard", "DefaultParagraphFont",
    };

    public static string ValidateId(string id, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(id, parameterName);
        if (!IdPattern().IsMatch(id))
        {
            throw new ArgumentException(
                $"'{id}' is not a valid style id: use letters, digits, '_' and '-', starting with a letter or '_'.",
                parameterName);
        }

        if (ReservedIds.Contains(id) || id.StartsWith(ReservedPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"The style id '{id}' is reserved by the builder (as are ids starting with '{ReservedPrefix}'); choose another id.", parameterName);
        }

        return id;
    }

    public static string ValidateDisplayName(string displayName, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(displayName, parameterName);
        if (string.IsNullOrWhiteSpace(displayName) || displayName.Any(char.IsControl))
        {
            throw new ArgumentException("A style's display name must be non-empty and contain no control characters.", parameterName);
        }

        return displayName;
    }

    // ODF style names are NCNames, WordprocessingML style ids are free text: this is the subset both accept.
    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex IdPattern();
}
