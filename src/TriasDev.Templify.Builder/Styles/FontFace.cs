// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace TriasDev.Templify.Builder;

/// <summary>
/// A font family, referenced by name. The font is not embedded: it has to be installed where the document is
/// opened or converted.
/// </summary>
/// <remarks>
/// Word for Mac ignores an embedded bold face. A design that needs a weight on every platform names the weight's
/// family, for example <c>new FontFace("Geist SemiBold")</c>, instead of using <c>Bold()</c> on a regular family.
/// </remarks>
public sealed record FontFace
{
    /// <summary>Initializes a font face.</summary>
    /// <param name="family">The family name, for example <c>Source Sans 3</c>.</param>
    /// <exception cref="ArgumentException">The name is empty or contains quotes or control characters.</exception>
    public FontFace(string family)
    {
        ArgumentNullException.ThrowIfNull(family);
        if (string.IsNullOrWhiteSpace(family) || family.Trim() != family || family.Any(c => char.IsControl(c) || c is '"' or '\''))
        {
            throw new ArgumentException(
                $"'{family}' is not a font family name: it must be non-empty, without leading or trailing spaces, quotes or control characters.",
                nameof(family));
        }

        Family = family;
    }

    /// <summary>Gets the family name.</summary>
    public string Family { get; }
}
