// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace TriasDev.Templify.Utilities;

/// <summary>
/// Sanitizes strings by removing characters that are invalid in XML 1.0.
/// XML 1.0 allows: #x9 | #xA | #xD | [#x20-#xD7FF] | [#xE000-#xFFFD] | [#x10000-#x10FFFF].
/// </summary>
internal static class XmlCharacterSanitizer
{
    /// <summary>
    /// Removes characters that are invalid in XML 1.0 from the input string.
    /// Returns the same string instance if no invalid characters are found.
    /// </summary>
    /// <param name="value">The string to sanitize.</param>
    /// <returns>The sanitized string, or null if the input was null.</returns>
    public static string? Sanitize(string? value)
    {
        if (value is null)
        {
            return null;
        }

        // Fast path: check if any invalid characters exist before allocating
        int firstInvalidIndex = -1;
        for (int i = 0; i < value.Length; i++)
        {
            if (!IsValidXmlCharacter(value[i]))
            {
                firstInvalidIndex = i;
                break;
            }
        }

        if (firstInvalidIndex < 0)
        {
            return value;
        }

        // Build result, skipping invalid characters
        Span<char> buffer = value.Length <= 256
            ? stackalloc char[value.Length]
            : new char[value.Length];

        // Copy the valid prefix
        value.AsSpan(0, firstInvalidIndex).CopyTo(buffer);
        int writeIndex = firstInvalidIndex;

        // Filter the rest
        for (int i = firstInvalidIndex; i < value.Length; i++)
        {
            if (IsValidXmlCharacter(value[i]))
            {
                buffer[writeIndex++] = value[i];
            }
        }

        return new string(buffer[..writeIndex]);
    }

    private static bool IsValidXmlCharacter(char c)
    {
        // XML 1.0 valid characters: #x9 | #xA | #xD | [#x20-#xD7FF] | [#xE000-#xFFFD]
        // Surrogate chars (0xD800-0xDFFF) are allowed through because they form valid pairs
        // for supplementary plane characters (#x10000-#x10FFFF) like emoji.
        return c == '\x09' || c == '\x0A' || c == '\x0D'
            || (c >= '\x20' && c <= '\xD7FF')
            || (c >= '\xD800' && c <= '\xDFFF')  // Surrogates: allow for valid pairs (emoji, etc.)
            || (c >= '\xE000' && c <= '\xFFFD');
    }
}
