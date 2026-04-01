// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace TriasDev.Templify.Utilities;

/// <summary>
/// Sanitizes strings by removing characters that are invalid in XML 1.0.
/// XML 1.0 allows: #x9 | #xA | #xD | [#x20-#xD7FF] | [#xE000-#xFFFD] | [#x10000-#x10FFFF].
/// Valid surrogate pairs (representing #x10000-#x10FFFF) are preserved; unpaired surrogates are removed.
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
        int firstInvalidIndex = FindFirstInvalidIndex(value);

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

        // Filter the rest, handling surrogate pairs
        for (int i = firstInvalidIndex; i < value.Length; i++)
        {
            char c = value[i];

            if (char.IsHighSurrogate(c))
            {
                // Valid surrogate pair: high surrogate followed by low surrogate
                if (i + 1 < value.Length && char.IsLowSurrogate(value[i + 1]))
                {
                    buffer[writeIndex++] = c;
                    buffer[writeIndex++] = value[i + 1];
                    i++; // Skip the low surrogate
                }
                // Else: unpaired high surrogate — drop it
            }
            else if (char.IsLowSurrogate(c))
            {
                // Unpaired low surrogate — drop it
            }
            else if (IsValidXmlCharacter(c))
            {
                buffer[writeIndex++] = c;
            }
        }

        return new string(buffer[..writeIndex]);
    }

    /// <summary>
    /// Finds the index of the first invalid XML character, or -1 if all characters are valid.
    /// </summary>
    private static int FindFirstInvalidIndex(string value)
    {
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];

            if (char.IsHighSurrogate(c))
            {
                // Check for valid surrogate pair
                if (i + 1 < value.Length && char.IsLowSurrogate(value[i + 1]))
                {
                    i++; // Skip the low surrogate — valid pair
                }
                else
                {
                    return i; // Unpaired high surrogate
                }
            }
            else if (char.IsLowSurrogate(c))
            {
                return i; // Unpaired low surrogate
            }
            else if (!IsValidXmlCharacter(c))
            {
                return i;
            }
        }

        return -1;
    }

    private static bool IsValidXmlCharacter(char c)
    {
        // XML 1.0 valid characters (BMP only, surrogates handled separately):
        // #x9 | #xA | #xD | [#x20-#xD7FF] | [#xE000-#xFFFD]
        return c == '\x09' || c == '\x0A' || c == '\x0D'
            || (c >= '\x20' && c <= '\xD7FF')
            || (c >= '\xE000' && c <= '\xFFFD');
    }
}
