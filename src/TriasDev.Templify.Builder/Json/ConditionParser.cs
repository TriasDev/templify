// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;

namespace TriasDev.Templify.Builder.Json;

/// <summary>
/// Reads a condition in Templify's syntax, as the builder writes it, back into a <see cref="Condition"/>. The
/// conditions are built through <see cref="TemplatePath"/>'s methods, so the same rules apply as in C#.
/// </summary>
/// <remarks>
/// Grammar (loosest first): <c>or</c>, <c>and</c>, <c>not</c>, then a path with an optional comparison
/// (<c>= != &gt; &gt;= &lt; &lt;=</c>, <c>in (…)</c>, <c>contains</c>, <c>startswith</c>, <c>endswith</c>) or postfix
/// (<c>exists</c>, <c>is empty</c>, <c>is not empty</c>), or a parenthesised condition. Literals are double-quoted
/// strings (<c>\"</c> and <c>\\</c> escapes) or invariant numbers; a bare word is never a literal.
/// </remarks>
internal static class ConditionParser
{
    private static readonly HashSet<string> _keywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "and", "or", "not", "in", "is", "empty", "exists", "contains", "startswith", "endswith",
    };

    /// <summary>Parses a condition.</summary>
    /// <param name="text">The condition text.</param>
    /// <param name="resolvePath">Turns a path as written (keyword escapes removed) into a path with its loop scope.</param>
    /// <returns>The condition.</returns>
    /// <exception cref="FormatException">The text is not a condition; the message names the 0-based column.</exception>
    public static Condition Parse(string text, Func<string, TemplatePath> resolvePath)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(resolvePath);
        Parser parser = new Parser(Tokenize(text), text.Length, resolvePath);
        Condition condition = parser.Or();
        parser.ExpectEnd();
        return condition;
    }

    private enum Kind
    {
        Word,
        String,
        Number,
        Operator,
        Open,
        Close,
        Comma,
    }

    private sealed record Token(Kind Kind, string Text, int Column, string? Value = null);

    private static List<Token> Tokenize(string text)
    {
        List<Token> tokens = new List<Token>();
        int i = 0;
        while (i < text.Length)
        {
            char c = text[i];
            if (char.IsWhiteSpace(c))
            {
                i++;
                continue;
            }

            int start = i;
            switch (c)
            {
                case '(':
                    tokens.Add(new Token(Kind.Open, "(", i++));
                    continue;
                case ')':
                    tokens.Add(new Token(Kind.Close, ")", i++));
                    continue;
                case ',':
                    tokens.Add(new Token(Kind.Comma, ",", i++));
                    continue;
                case '"':
                    tokens.Add(ReadString(text, ref i));
                    continue;
                case '=':
                    // "==" is "=", as in Templify.
                    tokens.Add(new Token(Kind.Operator, "=", i));
                    i += i + 1 < text.Length && text[i + 1] == '=' ? 2 : 1;
                    continue;
                case '!' or '>' or '<':
                    i++;
                    if (i < text.Length && text[i] == '=')
                    {
                        i++;
                    }
                    else if (c == '!')
                    {
                        throw Error("'!' must be followed by '='", start);
                    }

                    tokens.Add(new Token(Kind.Operator, text[start..i], start));
                    continue;
            }

            while (i < text.Length && !char.IsWhiteSpace(text[i]) && text[i] is not ('(' or ')' or ',' or '"' or '=' or '!' or '>' or '<'))
            {
                i++;
            }

            if (i == start)
            {
                throw Error($"unexpected '{c}'", start);
            }

            string word = text[start..i];
            bool number = decimal.TryParse(word, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out _);
            tokens.Add(new Token(number ? Kind.Number : Kind.Word, word, start));
        }

        return tokens;
    }

    private static Token ReadString(string text, ref int i)
    {
        int start = i++;
        StringBuilder value = new StringBuilder();
        while (i < text.Length)
        {
            char c = text[i++];
            if (c == '"')
            {
                return new Token(Kind.String, text[start..i], start, value.ToString());
            }

            if (c == '\\' && i < text.Length && text[i] is '"' or '\\')
            {
                value.Append(text[i++]);
                continue;
            }

            value.Append(c);
        }

        throw Error("the string is not closed", start);
    }

    private static FormatException Error(string message, int column) =>
        new FormatException($"Condition: {message} at column {column.ToString(CultureInfo.InvariantCulture)}.");

    private sealed class Parser(List<Token> tokens, int length, Func<string, TemplatePath> resolvePath)
    {
        private int _position;

        private Token? Current => _position < tokens.Count ? tokens[_position] : null;

        private int Column => Current?.Column ?? length;

        public void ExpectEnd()
        {
            if (Current is { } token)
            {
                throw Error($"unexpected '{token.Text}'", token.Column);
            }
        }

        public Condition Or()
        {
            Condition left = And();
            while (IsKeyword("or"))
            {
                _position++;
                left = left | And();
            }

            return left;
        }

        private Condition And()
        {
            Condition left = Unary();
            while (IsKeyword("and"))
            {
                _position++;
                left = left & Unary();
            }

            return left;
        }

        private Condition Unary()
        {
            // Iterative, so a long chain of 'not' cannot exhaust the stack.
            int nots = 0;
            while (IsKeyword("not"))
            {
                _position++;
                nots++;
            }

            Condition condition = Primary();
            for (int i = 0; i < nots; i++)
            {
                condition = !condition;
            }

            return condition;
        }

        private Condition Primary()
        {
            Token token = Current ?? throw Error("a condition is missing", length);
            if (token.Kind == Kind.Open)
            {
                _position++;
                Condition inner = Or();
                Expect(Kind.Close, "')'");
                return inner;
            }

            if (token.Kind != Kind.Word || (_keywords.Contains(token.Text) && !token.Text.StartsWith('[')))
            {
                throw Error($"a path is expected, not '{token.Text}'", token.Column);
            }

            _position++;
            TemplatePath path = Resolve(token);
            return Rest(path);
        }

        private Condition Rest(TemplatePath path)
        {
            Token? next = Current;
            if (next is { Kind: Kind.Operator })
            {
                _position++;
                return Comparison(path, next);
            }

            if (next is not { Kind: Kind.Word })
            {
                return path;
            }

            switch (next.Text.ToLowerInvariant())
            {
                case "in":
                    _position++;
                    return In(path);
                case "contains":
                    _position++;
                    return path.Contains(StringLiteral(next.Text));
                case "startswith":
                    _position++;
                    return path.StartsWith(StringLiteral(next.Text));
                case "endswith":
                    _position++;
                    return path.EndsWith(StringLiteral(next.Text));
                case "exists":
                    _position++;
                    return path.Exists;
                case "is":
                    _position++;
                    if (IsKeyword("not"))
                    {
                        _position++;
                        ExpectKeyword("empty");
                        return path.IsNotEmpty;
                    }

                    ExpectKeyword("empty");
                    return path.IsEmpty;
                default:
                    return path;
            }
        }

        private Condition Comparison(TemplatePath path, Token op)
        {
            Token literal = Literal();
            if (literal.Kind == Kind.String)
            {
                return op.Text switch
                {
                    "=" => path.Eq(literal.Value!),
                    "!=" => path.NotEq(literal.Value!),
                    _ => throw Error($"'{op.Text}' compares with a number, not a string", literal.Column),
                };
            }

            double number = double.Parse(literal.Text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);
            return op.Text switch
            {
                "=" => path.Eq(number),
                "!=" => path.NotEq(number),
                ">" => path.Gt(number),
                ">=" => path.Ge(number),
                "<" => path.Lt(number),
                _ => path.Le(number),
            };
        }

        private Condition In(TemplatePath path)
        {
            Expect(Kind.Open, "'('");
            List<string> values = new List<string> { StringLiteral("in") };
            while (Current is { Kind: Kind.Comma })
            {
                _position++;
                values.Add(StringLiteral("in"));
            }

            Expect(Kind.Close, "')'");
            return path.In(values.ToArray());
        }

        private string StringLiteral(string op)
        {
            Token literal = Literal();
            return literal.Kind == Kind.String
                ? literal.Value!
                : throw Error($"'{op}' takes a quoted string, not {literal.Text}", literal.Column);
        }

        private Token Literal()
        {
            Token token = Current ?? throw Error("a value is missing", length);
            if (token.Kind == Kind.Word)
            {
                throw Error($"'{token.Text}' is not a value: quote the literal (\"{token.Text}\")", token.Column);
            }

            if (token.Kind is not (Kind.String or Kind.Number))
            {
                throw Error($"a value is expected, not '{token.Text}'", token.Column);
            }

            _position++;
            return token;
        }

        private TemplatePath Resolve(Token token)
        {
            string text = token.Text;
            if (text.StartsWith('[') && text.EndsWith(']') && text.IndexOf('[', 1) < 0)
            {
                text = text[1..^1];
            }

            try
            {
                return resolvePath(text);
            }
            catch (ArgumentException exception)
            {
                throw Error($"'{token.Text}' is not a path ({exception.Message})", token.Column);
            }
        }

        private bool IsKeyword(string keyword) =>
            Current is { Kind: Kind.Word } token && string.Equals(token.Text, keyword, StringComparison.OrdinalIgnoreCase);

        private void ExpectKeyword(string keyword)
        {
            if (!IsKeyword(keyword))
            {
                throw Error($"'{keyword}' is expected", Column);
            }

            _position++;
        }

        private void Expect(Kind kind, string what)
        {
            if (Current?.Kind != kind)
            {
                throw Error($"{what} is expected", Column);
            }

            _position++;
        }
    }
}
