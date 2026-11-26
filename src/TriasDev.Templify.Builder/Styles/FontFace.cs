// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace TriasDev.Templify.Builder;

/// <summary>
/// A font family. By default it is referenced by name and has to be installed where the document is opened or
/// converted; <see cref="Embedded(byte[])"/> puts the font file into the document.
/// </summary>
/// <remarks>
/// Word for Mac ignores an embedded bold face. A design that needs a weight on every platform names the weight's
/// family, for example <c>new FontFace("Geist SemiBold")</c>, instead of using <c>Bold()</c> on a regular family; with
/// embedding, embed each weight's file as its own family.
/// </remarks>
public sealed record FontFace
{
    /// <summary>Initializes a font face.</summary>
    /// <param name="family">The family name, for example <c>Source Sans 3</c>.</param>
    /// <exception cref="ArgumentException">The name is empty or contains quotes or control characters.</exception>
    public FontFace(string family)
        : this(family, embedding: null)
    {
    }

    private FontFace(string family, FontFile? embedding)
    {
        ArgumentNullException.ThrowIfNull(family);
        if (string.IsNullOrWhiteSpace(family) || family.Trim() != family || family.Any(c => char.IsControl(c) || c is '"' or '\''))
        {
            throw new ArgumentException(
                $"'{family}' is not a font family name: it must be non-empty, without leading or trailing spaces, quotes or control characters.",
                nameof(family));
        }

        Family = family;
        Embedding = embedding;
    }

    /// <summary>Gets the theme token the font came from (<see cref="Token"/>); null for a plain font. Equality ignores it.</summary>
    public string? TokenName { get; private init; }

    /// <summary>A font of the theme: <paramref name="face"/> under a name the JSON form refers to as <c>"$name"</c>.</summary>
    /// <param name="name">The token's name: a letter, then letters, digits, <c>_</c>, <c>.</c> or <c>-</c>.</param>
    /// <param name="face">The font.</param>
    /// <returns>The font with its token.</returns>
    public static FontFace Token(string name, FontFace face)
    {
        ArgumentNullException.ThrowIfNull(face);
        return face with { TokenName = Json.ThemeTokens.Validate(name, nameof(name)) };
    }

    /// <summary>Gets the family name.</summary>
    public string Family { get; }

    /// <summary>Gets a value indicating whether the font file is embedded in the document.</summary>
    public bool IsEmbedded => Embedding != null;

    /// <summary>The embedded font file, or null.</summary>
    internal FontFile? Embedding { get; }

    /// <summary>
    /// A font embedded in the document, so a server-side PDF conversion and a recipient without the font installed
    /// see it. The family name is the one the file declares (its name table, name id 1).
    /// </summary>
    /// <param name="fontFile">A TrueType font file (<c>.ttf</c>), one weight and style. Pass a subset to keep documents small; the builder does not subset.</param>
    /// <returns>The font face.</returns>
    /// <exception cref="ArgumentException">
    /// The bytes are not a TrueType font (OpenType CFF, collections and WOFF are not supported), or the font's
    /// embedding permission (<c>fsType</c>) does not allow editable embedding.
    /// </exception>
    public static FontFace Embedded(byte[] fontFile)
    {
        FontFile file = FontFile.Read(fontFile, nameof(fontFile));
        return new FontFace(file.Family, file);
    }

    /// <summary>
    /// A font embedded in the document under a family name the file declares (name id 1 or 16), for files that name
    /// their family in more than one way.
    /// </summary>
    /// <param name="family">The family name; one of the file's family names.</param>
    /// <param name="fontFile">A TrueType font file (<c>.ttf</c>).</param>
    /// <returns>The font face.</returns>
    /// <exception cref="ArgumentException">See <see cref="Embedded(byte[])"/>; also when the file does not declare <paramref name="family"/>.</exception>
    public static FontFace Embedded(string family, byte[] fontFile)
    {
        ArgumentNullException.ThrowIfNull(family);
        FontFile file = FontFile.Read(fontFile, nameof(fontFile));
        if (!file.Families.Contains(family, StringComparer.Ordinal))
        {
            throw new ArgumentException(
                $"The font file declares the family {string.Join(" / ", file.Families.Select(f => "'" + f + "'"))}, not '{family}'.",
                nameof(family));
        }

        return new FontFace(family, file);
    }

    /// <summary>Two font faces are equal when they name the same family and embed the same file (or none).</summary>
    /// <param name="other">The other font face.</param>
    /// <returns>Whether they are equal.</returns>
    public bool Equals(FontFace? other) =>
        other is not null && Family == other.Family && Embedding?.Hash == other.Embedding?.Hash;

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Family, Embedding?.Hash);
}
