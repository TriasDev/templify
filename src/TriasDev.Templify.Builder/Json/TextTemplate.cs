// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Text;

namespace TriasDev.Templify.Builder.Json;

/// <summary>A piece of a JSON text string: literal text, a value placeholder or an expression placeholder.</summary>
internal abstract record TextPiece
{
    private TextPiece()
    {
    }

    /// <summary>Literal text.</summary>
    /// <param name="Text">The text.</param>
    public sealed record Literal(string Text) : TextPiece;

    /// <summary><c>{{path}}</c> or <c>{{path:format}}</c>.</summary>
    /// <param name="Path">The path text.</param>
    /// <param name="Format">The format specifier text; null without one.</param>
    public sealed record Value(string Path, string? Format) : TextPiece;

    /// <summary><c>{{(condition)}}</c> or <c>{{(condition):format}}</c>.</summary>
    /// <param name="Condition">The condition text, without the outer parentheses.</param>
    /// <param name="Format">The format specifier text; null without one.</param>
    public sealed record Expression(string Condition, string? Format) : TextPiece;
}

/// <summary>
/// Splits a JSON text string into literal text and Templify placeholders, and writes pieces back. The builder never
/// lets literal text contain <c>{{</c>, so every <c>{{</c> in a string starts a placeholder. Paths, formats and
/// conditions are returned as text; the JSON reader checks them with its own parsers.
/// </summary>
internal static class TextTemplate
{
    private const string Open = "{{";
    private const string Close = "}}";

    /// <summary>Splits <paramref name="text"/>; a string without placeholders is one literal (also when empty).</summary>
    /// <exception cref="FormatException">A placeholder is malformed; the message quotes it.</exception>
    public static IReadOnlyList<TextPiece> Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        List<TextPiece> pieces = new List<TextPiece>();
        int position = 0;
        while (position < text.Length)
        {
            int start = text.IndexOf(Open, position, StringComparison.Ordinal);
            if (start < 0)
            {
                break;
            }

            if (start > position)
            {
                pieces.Add(new TextPiece.Literal(text[position..start]));
            }

            position = start + Open.Length < text.Length && text[start + Open.Length] == '('
                ? ParseExpression(text, start, pieces)
                : ParseValue(text, start, pieces);
        }

        if (position < text.Length || pieces.Count == 0)
        {
            pieces.Add(new TextPiece.Literal(text[position..]));
        }

        return pieces;
    }

    /// <summary>Writes pieces as one string, the inverse of <see cref="Parse"/>.</summary>
    public static string Write(IEnumerable<TextPiece> pieces)
    {
        ArgumentNullException.ThrowIfNull(pieces);
        StringBuilder text = new StringBuilder();
        foreach (TextPiece piece in pieces)
        {
            text.Append(piece switch
            {
                TextPiece.Literal literal => literal.Text,
                TextPiece.Value value => Open + value.Path + (value.Format is null ? string.Empty : ":" + value.Format) + Close,
                TextPiece.Expression expression => Open + "(" + expression.Condition + ")" + (expression.Format is null ? string.Empty : ":" + expression.Format) + Close,
                _ => throw new ArgumentException("Unknown text piece.", nameof(pieces)),
            });
        }

        return text.ToString();
    }

    private static int ParseValue(string text, int start, List<TextPiece> pieces)
    {
        int end = text.IndexOf(Close, start + Open.Length, StringComparison.Ordinal);
        if (end < 0)
        {
            throw Malformed(text, start, "it has no closing '}}'");
        }

        string inner = text[(start + Open.Length)..end];
        int colon = inner.IndexOf(':', StringComparison.Ordinal);
        string path = colon < 0 ? inner : inner[..colon];
        string? format = colon < 0 ? null : inner[(colon + 1)..];
        if (path.Length == 0)
        {
            throw Malformed(text, start, "it names no data path");
        }

        if (format is { Length: 0 })
        {
            throw Malformed(text, start, "the format after ':' is empty");
        }

        pieces.Add(new TextPiece.Value(path, format));
        return end + Close.Length;
    }

    private static int ParseExpression(string text, int start, List<TextPiece> pieces)
    {
        int open = start + Open.Length;
        int closeParen = MatchingParenthesis(text, open);
        if (closeParen < 0)
        {
            throw Malformed(text, start, "its '(' has no matching ')'");
        }

        string condition = text[(open + 1)..closeParen];
        if (condition.Trim().Length == 0)
        {
            throw Malformed(text, start, "the condition is empty");
        }

        int after = closeParen + 1;
        if (text.AsSpan(after).StartsWith(Close, StringComparison.Ordinal))
        {
            pieces.Add(new TextPiece.Expression(condition, null));
            return after + Close.Length;
        }

        if (after < text.Length && text[after] == ':')
        {
            int end = text.IndexOf(Close, after, StringComparison.Ordinal);
            if (end < 0)
            {
                throw Malformed(text, start, "it has no closing '}}'");
            }

            string format = text[(after + 1)..end];
            if (format.Length == 0)
            {
                throw Malformed(text, start, "the format after ':' is empty");
            }

            pieces.Add(new TextPiece.Expression(condition, format));
            return end + Close.Length;
        }

        throw Malformed(text, start, "after the condition's ')' only ':format' and '}}' may follow");
    }

    /// <summary>The index of the ')' matching the '(' at <paramref name="open"/>, skipping quoted strings; -1 when none.</summary>
    private static int MatchingParenthesis(string text, int open)
    {
        int depth = 0;
        for (int i = open; i < text.Length; i++)
        {
            char c = text[i];
            if (c is '"' or '\'')
            {
                i = EndOfString(text, i);
                if (i < 0)
                {
                    return -1;
                }
            }
            else if (c == '(')
            {
                depth++;
            }
            else if (c == ')' && --depth == 0)
            {
                return i;
            }
            else if (c == '}' && i + 1 < text.Length && text[i + 1] == '}')
            {
                // A condition never contains '}}' (Condition rejects it): the placeholder ends here.
                return -1;
            }
        }

        return -1;
    }

    /// <summary>The index of the quote closing the string literal that starts at <paramref name="quote"/>; -1 when none.</summary>
    private static int EndOfString(string text, int quote)
    {
        char delimiter = text[quote];
        for (int i = quote + 1; i < text.Length; i++)
        {
            if (text[i] == '\\')
            {
                i++;
            }
            else if (text[i] == delimiter)
            {
                return i;
            }
        }

        return -1;
    }

    private static FormatException Malformed(string text, int start, string reason)
    {
        int end = text.IndexOf(Close, start + Open.Length, StringComparison.Ordinal);
        string placeholder = end < 0 ? text[start..] : text[start..(end + Close.Length)];
        return new FormatException($"The placeholder '{placeholder}' is malformed: {reason}.");
    }
}
