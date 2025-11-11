using System.Text;

namespace TriasDev.Templify.Expressions;

/// <summary>
/// Parses boolean expressions from text.
/// Supports: and, or, not, ==, !=, >, >=, <, <=, parentheses
/// Examples:
/// - (var1 and var2)
/// - (var1 or var2)
/// - (not IsActive)
/// - (Count > 0)
/// - ((var1 or var2) and var3)
/// </summary>
internal sealed class BooleanExpressionParser
{
    private string _text = string.Empty;
    private int _position;

    /// <summary>
    /// Parses a boolean expression from text.
    /// </summary>
    /// <param name="text">The expression text (without surrounding {{ }} and format specifier).</param>
    /// <returns>The parsed expression, or null if parsing fails.</returns>
    public BooleanExpression? Parse(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        _text = text.Trim();
        _position = 0;

        // Check if it starts with '(' - if not, it's a simple variable
        if (!_text.StartsWith("("))
        {
            return null; // Not an expression, just a variable
        }

        try
        {
            return ParseOrExpression();
        }
        catch
        {
            return null;
        }
    }

    private BooleanExpression? ParseOrExpression()
    {
        BooleanExpression? left = ParseAndExpression();
        if (left == null) return null;

        while (ConsumeKeyword("or"))
        {
            BooleanExpression? right = ParseAndExpression();
            if (right == null) return null;
            left = new OrExpression(left, right);
        }

        return left;
    }

    private BooleanExpression? ParseAndExpression()
    {
        BooleanExpression? left = ParseUnaryExpression();
        if (left == null) return null;

        while (ConsumeKeyword("and"))
        {
            BooleanExpression? right = ParseUnaryExpression();
            if (right == null) return null;
            left = new AndExpression(left, right);
        }

        return left;
    }

    private BooleanExpression? ParseUnaryExpression()
    {
        SkipWhitespace();

        // Check for 'not'
        if (ConsumeKeyword("not"))
        {
            BooleanExpression? operand = ParsePrimaryExpression();
            return operand == null ? null : new NotExpression(operand);
        }

        return ParsePrimaryExpression();
    }

    private BooleanExpression? ParsePrimaryExpression()
    {
        SkipWhitespace();

        // Check for parenthesized expression
        if (Consume('('))
        {
            BooleanExpression? expr = ParseOrExpression();
            if (expr == null || !Consume(')'))
            {
                return null;
            }
            return expr;
        }

        // Try to parse comparison
        string? identifier = ParseIdentifier();
        if (identifier == null) return null;

        SkipWhitespace();

        // Check for comparison operators
        if (TryParseComparisonOperator(out ComparisonOperator op))
        {
            SkipWhitespace();
            object? value = ParseValue();
            return new ComparisonExpression(identifier, op, value);
        }

        // Simple variable reference
        return new VariableExpression(identifier);
    }

    private bool TryParseComparisonOperator(out ComparisonOperator op)
    {
        op = ComparisonOperator.Equal;

        if (Consume("=="))
        {
            op = ComparisonOperator.Equal;
            return true;
        }
        if (Consume("!="))
        {
            op = ComparisonOperator.NotEqual;
            return true;
        }
        if (Consume(">="))
        {
            op = ComparisonOperator.GreaterThanOrEqual;
            return true;
        }
        if (Consume("<="))
        {
            op = ComparisonOperator.LessThanOrEqual;
            return true;
        }
        if (Consume('>'))
        {
            op = ComparisonOperator.GreaterThan;
            return true;
        }
        if (Consume('<'))
        {
            op = ComparisonOperator.LessThan;
            return true;
        }

        return false;
    }

    private object? ParseValue()
    {
        SkipWhitespace();

        // Try to parse number
        if (char.IsDigit(Peek()) || Peek() == '-')
        {
            return ParseNumber();
        }

        // Try to parse string literal
        if (Peek() == '"' || Peek() == '\'')
        {
            return ParseStringLiteral();
        }

        // Try to parse boolean literal
        if (ConsumeKeyword("true"))
        {
            return true;
        }
        if (ConsumeKeyword("false"))
        {
            return false;
        }

        // Try to parse null
        if (ConsumeKeyword("null"))
        {
            return null;
        }

        // Parse as identifier (another variable reference)
        return ParseIdentifier();
    }

    private object? ParseNumber()
    {
        StringBuilder sb = new StringBuilder();

        if (Peek() == '-')
        {
            sb.Append(Consume());
        }

        while (char.IsDigit(Peek()) || Peek() == '.')
        {
            sb.Append(Consume());
        }

        string numberStr = sb.ToString();

        if (numberStr.Contains('.'))
        {
            if (double.TryParse(numberStr, out double d))
            {
                return d;
            }
        }
        else
        {
            if (int.TryParse(numberStr, out int i))
            {
                return i;
            }
        }

        return null;
    }

    private string? ParseStringLiteral()
    {
        char quote = Consume();
        StringBuilder sb = new StringBuilder();

        while (Peek() != quote && Peek() != '\0')
        {
            char c = Consume();
            if (c == '\\' && Peek() == quote)
            {
                sb.Append(Consume());
            }
            else
            {
                sb.Append(c);
            }
        }

        if (Peek() == quote)
        {
            Consume();
            return sb.ToString();
        }

        return null;
    }

    private string? ParseIdentifier()
    {
        StringBuilder sb = new StringBuilder();

        // Identifiers can contain letters, digits, underscores, dots, and brackets
        while (char.IsLetterOrDigit(Peek()) || Peek() == '_' || Peek() == '.' || Peek() == '[' || Peek() == ']')
        {
            sb.Append(Consume());
        }

        return sb.Length > 0 ? sb.ToString() : null;
    }

    private bool ConsumeKeyword(string keyword)
    {
        SkipWhitespace();

        int savedPosition = _position;

        foreach (char c in keyword)
        {
            if (char.ToLowerInvariant(Peek()) != char.ToLowerInvariant(c))
            {
                _position = savedPosition;
                return false;
            }
            Consume();
        }

        // Make sure the keyword is not part of a larger identifier
        if (char.IsLetterOrDigit(Peek()) || Peek() == '_')
        {
            _position = savedPosition;
            return false;
        }

        return true;
    }

    private bool Consume(string text)
    {
        SkipWhitespace();

        int savedPosition = _position;

        foreach (char c in text)
        {
            if (Peek() != c)
            {
                _position = savedPosition;
                return false;
            }
            Consume();
        }

        return true;
    }

    private bool Consume(char expected)
    {
        SkipWhitespace();

        if (Peek() == expected)
        {
            Consume();
            return true;
        }

        return false;
    }

    private char Consume()
    {
        if (_position >= _text.Length)
        {
            return '\0';
        }

        return _text[_position++];
    }

    private char Peek()
    {
        if (_position >= _text.Length)
        {
            return '\0';
        }

        return _text[_position];
    }

    private void SkipWhitespace()
    {
        while (_position < _text.Length && char.IsWhiteSpace(_text[_position]))
        {
            _position++;
        }
    }
}
