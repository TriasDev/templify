// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;

namespace TriasDev.Templify.Conditionals.Engine;

/// <summary>Converts a condition-expression string into a flat token list.</summary>
internal sealed class ConditionLexer
{
    private static readonly HashSet<string> _wordOperators = new(StringComparer.OrdinalIgnoreCase)
    {
        "and", "or", "not", "in", "contains", "startswith", "endswith", "exists", "is", "empty"
    };

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

            if (c == '"')
            {
                i++;
                StringBuilder sb = new();
                while (i < text.Length && text[i] != '"')
                {
                    if (text[i] == '\\' && i + 1 < text.Length && text[i + 1] == '"')
                    { sb.Append('"'); i += 2; continue; }
                    sb.Append(text[i]);
                    i++;
                }
                i++; // closing quote (if present)
                tokens.Add(new ConditionToken(ConditionTokenType.String, sb.ToString()));
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

    private static ConditionToken ClassifyWord(string word)
    {
        if (_wordOperators.Contains(word))
        {
            return new ConditionToken(ConditionTokenType.Operator, word.ToLowerInvariant());
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

    private static bool TryParseNumber(string word, out object? value)
    {
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

    private static string NormalizeQuotes(string expression)
    {
        return expression
            .Replace('“', '"').Replace('”', '"').Replace('„', '"').Replace('‟', '"')
            .Replace('‘', '\'').Replace('’', '\'').Replace('‚', '\'').Replace('‛', '\'');
    }
}
