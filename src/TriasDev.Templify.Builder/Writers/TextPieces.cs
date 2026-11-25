// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace TriasDev.Templify.Builder.Writers;

/// <summary>The kinds of piece literal text splits into.</summary>
internal enum TextPieceKind
{
    Text,
    Tab,
    LineBreak,
}

/// <summary>
/// Splits literal text into runs of characters, tabs and line breaks; each writer writes them as the format's
/// own elements. <c>\r\n</c> and a lone <c>\r</c> count as one line break.
/// </summary>
internal static class TextPieces
{
    public static IEnumerable<(TextPieceKind Kind, string Text)> Split(string text)
    {
        int start = 0;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c is not ('\t' or '\n' or '\r'))
            {
                continue;
            }

            if (i > start)
            {
                yield return (TextPieceKind.Text, text.Substring(start, i - start));
            }

            if (c == '\t')
            {
                yield return (TextPieceKind.Tab, string.Empty);
            }
            else
            {
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                {
                    i++;
                }

                yield return (TextPieceKind.LineBreak, string.Empty);
            }

            start = i + 1;
        }

        if (start < text.Length)
        {
            yield return (TextPieceKind.Text, text.Substring(start));
        }
    }
}
