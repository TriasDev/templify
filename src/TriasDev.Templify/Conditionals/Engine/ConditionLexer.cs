// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;

namespace TriasDev.Templify.Conditionals.Engine;

/// <summary>Converts a condition-expression string into a flat token list.</summary>
internal sealed class ConditionLexer
{
    /// <summary>Operator keywords (<c>and</c>, <c>in</c>, <c>is</c>, <c>empty</c>, …), taken from the operator registry.</summary>
    private static readonly FrozenSet<string> _wordOperators = ConditionOperatorRegistry.Shared.WordOperators;

    /// <summary>
    /// Keywords introduced in 1.7.0. Before 1.7.0 they were ordinary identifiers, so when one of them appears
    /// where an operand is expected (e.g. <c>{{#if Exists}}</c>), the parser treats it as a variable name.
    /// </summary>
    internal static readonly FrozenSet<string> OperandFallbackKeywords =
        new[] { "in", "contains", "startswith", "endswith", "exists", "is", "empty" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    private readonly bool _allowSingleQuotedStrings;

    /// <summary>
    /// Creates a new <see cref="ConditionLexer"/>.
    /// </summary>
    /// <param name="allowSingleQuotedStrings">
    /// When <see langword="true"/>, single quotes (<c>'</c>) delimit string literals in addition to
    /// double quotes, mirroring the legacy inline <c>{{(...)}}</c> expression parser. Defaults to
    /// <see langword="false"/>, which preserves the original behavior used by <c>{{#if}}</c>/text/standalone
    /// condition evaluation, where a single quote is not a string delimiter.
    /// </param>
    public ConditionLexer(bool allowSingleQuotedStrings = false)
    {
        _allowSingleQuotedStrings = allowSingleQuotedStrings;
    }

    public IReadOnlyList<ConditionToken> Tokenize(string expression)
    {
        string text = NormalizeQuotes(expression ?? string.Empty);
        List<ConditionToken> tokens = new();
        int i = 0;

        while (i < text.Length)
        {
            char c = text[i];

            if (char.IsWhiteSpace(c))
            { i++; continue; }

            if (c == '(')
            { tokens.Add(new ConditionToken(ConditionTokenType.LParen, "(")); i++; continue; }
            if (c == ')')
            { tokens.Add(new ConditionToken(ConditionTokenType.RParen, ")")); i++; continue; }
            if (c == ',')
            { tokens.Add(new ConditionToken(ConditionTokenType.Comma, ",")); i++; continue; }

            if (c == '"' || (_allowSingleQuotedStrings && c == '\''))
            {
                int stringStart = i;
                if (!TryScanString(text, ref i, out string value))
                {
                    throw new ConditionParseException(
                        $"Unterminated string literal starting at position {stringStart}.",
                        isUnterminatedString: true);
                }
                tokens.Add(new ConditionToken(ConditionTokenType.String, value));
                continue;
            }

            string? symbol = MatchSymbolOperator(text, i);
            if (symbol != null)
            {
                tokens.Add(new ConditionToken(ConditionTokenType.Operator, symbol));
                i += symbol.Length;
                continue;
            }

            if (IsWordChar(c) || (c == '-' && i + 1 < text.Length && char.IsDigit(text[i + 1])))
            {
                int start = i;
                if (c == '-')
                { i++; }
                while (i < text.Length && IsWordChar(text[i]))
                { i++; }
                string word = text.Substring(start, i - start);
                tokens.Add(ClassifyWord(word));
                continue;
            }

            // Unknown character: emit as an Operator token so validation/parse can reject it.
            tokens.Add(new ConditionToken(ConditionTokenType.Operator, c.ToString()));
            i++;
        }

        tokens.Add(new ConditionToken(ConditionTokenType.End, string.Empty));
        return tokens;
    }

    /// <summary>
    /// Scans a string literal starting at the opening quote at <paramref name="i"/>. Inside the literal,
    /// <c>\\</c> is an escaped backslash and a backslash followed by the delimiting quote is an escaped quote;
    /// any other backslash is kept literally (so Windows paths like <c>"C:\Temp"</c> keep working).
    /// </summary>
    /// <returns><see langword="false"/> when the closing quote is missing.</returns>
    private static bool TryScanString(string text, ref int i, out string value)
    {
        char quote = text[i];
        i++;
        StringBuilder sb = new();
        while (i < text.Length)
        {
            char ch = text[i];
            if (ch == quote)
            {
                i++; // closing quote
                value = sb.ToString();
                return true;
            }

            if (ch == '\\' && i + 1 < text.Length && (text[i + 1] == quote || text[i + 1] == '\\'))
            {
                sb.Append(text[i + 1]);
                i += 2;
                continue;
            }

            sb.Append(ch);
            i++;
        }

        value = sb.ToString();
        return false;
    }

    private static ConditionToken ClassifyWord(string word)
    {
        // Bracketed identifier: "[Empty]" (optionally followed by a path, e.g. "[Empty].Count") is the
        // escape for a variable whose name collides with a keyword ("in", "is", "empty", "not", "true", ...).
        if (TryUnescapeBracketedIdentifier(word, out string? escaped))
        {
            return new ConditionToken(ConditionTokenType.Identifier, escaped);
        }

        if (_wordOperators.Contains(word))
        {
            return new ConditionToken(ConditionTokenType.Operator, word.ToLowerInvariant(), rawText: word);
        }

        if (word.Equals("true", StringComparison.OrdinalIgnoreCase))
        {
            return new ConditionToken(ConditionTokenType.Boolean, word, true);
        }

        if (word.Equals("false", StringComparison.OrdinalIgnoreCase))
        {
            return new ConditionToken(ConditionTokenType.Boolean, word, false);
        }

        if (word.Equals("null", StringComparison.OrdinalIgnoreCase))
        {
            return new ConditionToken(ConditionTokenType.Null, word, null);
        }

        if (TryParseNumber(word, out object? number))
        {
            return new ConditionToken(ConditionTokenType.Number, word, number);
        }

        return new ConditionToken(ConditionTokenType.Identifier, word);
    }

    private static bool TryUnescapeBracketedIdentifier(string word, [NotNullWhen(true)] out string? identifier)
    {
        identifier = null;
        if (word.Length < 3 || word[0] != '[')
        {
            return false;
        }

        int close = word.IndexOf(']');
        if (close < 2)
        {
            return false;
        }

        for (int k = 1; k < close; k++)
        {
            if (!char.IsLetterOrDigit(word[k]) && word[k] != '_')
            {
                return false;
            }
        }

        // A bare "[0]" stays as-is (it is not a name).
        if (char.IsDigit(word[1]))
        {
            return false;
        }

        identifier = word.Substring(1, close - 1) + word.Substring(close + 1);
        return true;
    }

    private static bool TryParseNumber(string word, out object? value)
    {
        // Words without any digit ("NaN", "Infinity", "-Infinity") are identifiers, not numbers.
        if (!word.Any(c => c is >= '0' and <= '9'))
        {
            value = null;
            return false;
        }

        if (!word.Contains('.') && int.TryParse(word, NumberStyles.Integer, CultureInfo.InvariantCulture, out int iv))
        {
            value = iv;
            return true;
        }

        if (double.TryParse(word, NumberStyles.Float, CultureInfo.InvariantCulture, out double dv))
        {
            value = dv;
            return true;
        }

        value = null;
        return false;
    }

    private static string? MatchSymbolOperator(string text, int i)
    {
        string[] twoChar = { ">=", "<=", "==", "!=" };
        if (i + 1 < text.Length)
        {
            string pair = text.Substring(i, 2);
            foreach (string op in twoChar)
            {
                if (pair == op)
                { return op; }
            }
        }

        char c = text[i];
        if (c == '=' || c == '>' || c == '<')
        { return c.ToString(); }
        return null;
    }

    private static bool IsWordChar(char c)
        => char.IsLetterOrDigit(c) || c == '_' || c == '.' || c == '[' || c == ']' || c == '@';

    /// <summary>
    /// Normalizes typographic (curly) quotes to ASCII quotes. Word auto-formats ASCII quotes
    /// to typographic quotes, which would otherwise break string literals.
    /// </summary>
    internal static string NormalizeQuotes(string expression)
    {
        return expression
            .Replace('\u201C', '"')  // Left Double Quotation Mark
            .Replace('\u201D', '"')  // Right Double Quotation Mark
            .Replace('\u201E', '"')  // Double Low-9 Quotation Mark (German)
            .Replace('\u201F', '"')  // Double High-Reversed-9 Quotation Mark
            .Replace('\u2018', '\'') // Left Single Quotation Mark
            .Replace('\u2019', '\'') // Right Single Quotation Mark
            .Replace('\u201A', '\'') // Single Low-9 Quotation Mark
            .Replace('\u201B', '\''); // Single High-Reversed-9 Quotation Mark
    }
}
