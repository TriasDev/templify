// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;
using TriasDev.Templify.Conditionals;

namespace TriasDev.Templify.Builder;

/// <summary>
/// A condition of a conditional block: built from <see cref="TemplatePath"/> comparisons and combined with
/// <c>!</c>, <c>&amp;</c> and <c>|</c>. The builder writes it as Templify condition text, with the engine's
/// precedence, quoting and keyword escaping.
/// </summary>
/// <example>
/// <code>
/// Condition paid = order["status"].Eq("paid") | order["status"].Eq("settled");
/// Condition large = order["total"].Gt(1000) &amp; !order["customer.isVip"];
/// </code>
/// </example>
public abstract class Condition
{
    private protected Condition()
    {
    }

    /// <summary>Gets the binding strength: 1 <c>or</c>, 2 <c>and</c>, 3 <c>not</c>, 4 comparisons, 5 postfix, 6 operands.</summary>
    internal abstract int Precedence { get; }

    /// <summary>Gets the data paths the condition reads.</summary>
    internal abstract IEnumerable<TemplatePath> Paths { get; }

    /// <summary>Negates a condition: <c>not (…)</c>.</summary>
    /// <param name="operand">The condition to negate.</param>
    /// <returns>The negated condition.</returns>
    public static Condition operator !(Condition operand) => new NotCondition(operand ?? throw new ArgumentNullException(nameof(operand)));

    /// <summary>Both conditions hold: <c>… and …</c>.</summary>
    /// <param name="left">The first condition.</param>
    /// <param name="right">The second condition.</param>
    /// <returns>The combined condition.</returns>
    public static Condition operator &(Condition left, Condition right) =>
        new LogicalCondition("and", 2, left ?? throw new ArgumentNullException(nameof(left)), right ?? throw new ArgumentNullException(nameof(right)));

    /// <summary>At least one condition holds: <c>… or …</c>.</summary>
    /// <param name="left">The first condition.</param>
    /// <param name="right">The second condition.</param>
    /// <returns>The combined condition.</returns>
    public static Condition operator |(Condition left, Condition right) =>
        new LogicalCondition("or", 1, left ?? throw new ArgumentNullException(nameof(left)), right ?? throw new ArgumentNullException(nameof(right)));

    /// <summary>
    /// A condition written as Templify text, for what the typed API does not cover. It is checked with
    /// <see cref="ConditionEvaluator.Validate(string)"/> where it is written.
    /// </summary>
    /// <param name="expression">The condition text, for example <c>Count &gt; 0 and not IsArchived</c>.</param>
    /// <param name="uses">The data paths the text reads, declared for the template's data contract.</param>
    /// <returns>The condition.</returns>
    /// <exception cref="ArgumentException">The text is not a valid condition, or contains <c>{{</c> or <c>}}</c>.</exception>
    public static Condition Raw(string expression, params TemplatePath[] uses)
    {
        ArgumentNullException.ThrowIfNull(expression);
        ArgumentNullException.ThrowIfNull(uses);
        if (expression.Contains("{{", StringComparison.Ordinal) || expression.Contains("}}", StringComparison.Ordinal) || expression.Any(char.IsControl))
        {
            throw new ArgumentException("A condition must not contain '{{', '}}' or control characters.", nameof(expression));
        }

        ConditionValidationResult validation = new ConditionEvaluator().Validate(expression);
        if (!validation.IsValid)
        {
            throw new ArgumentException(
                $"'{expression}' is not a valid condition: {string.Join("; ", validation.Issues.Select(i => i.Message))}",
                nameof(expression));
        }

        return new RawCondition(expression.Trim(), uses);
    }

    /// <summary>Returns the condition as Templify writes it, for example <c>status = "paid" or total &gt; 1000</c>.</summary>
    /// <returns>The condition text.</returns>
    public override string ToString()
    {
        StringBuilder text = new StringBuilder();
        Write(text);
        return text.ToString();
    }

    /// <summary>Writes the condition text.</summary>
    internal abstract void Write(StringBuilder text);

    /// <summary>Writes an operand, in parentheses when it binds looser than <paramref name="minimum"/>.</summary>
    private protected static void WriteOperand(StringBuilder text, Condition operand, int minimum)
    {
        if (operand.Precedence < minimum)
        {
            text.Append('(');
            operand.Write(text);
            text.Append(')');
        }
        else
        {
            operand.Write(text);
        }
    }
}

/// <summary>Writes the operands of conditions: paths, strings and numbers.</summary>
internal static class ConditionText
{
    /// <summary>
    /// Words the condition lexer reads as operators or literals. A path that is exactly such a word is written in
    /// brackets (<c>[in]</c>), the engine's escape.
    /// </summary>
    private static readonly HashSet<string> _keywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "and", "or", "not", "in", "is", "empty", "exists", "contains", "startswith", "endswith", "true", "false", "null",
    };

    public static void Path(StringBuilder text, TemplatePath path)
    {
        string value = path.ToString();
        text.Append(_keywords.Contains(value) ? "[" + value + "]" : value);
    }

    public static string CheckPath(TemplatePath path, string parameterName)
    {
        string value = path.ToString();
        if (value.Length > 0 && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
        {
            throw new ArgumentException($"The path '{value}' reads as a number in a condition and cannot be used there.", parameterName);
        }

        return value;
    }

    public static string String(string value, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(value, parameterName);
        if (value.Contains("{{", StringComparison.Ordinal) || value.Contains("}}", StringComparison.Ordinal) || value.Any(char.IsControl))
        {
            throw new ArgumentException($"'{value}' cannot be compared in a condition: it must not contain '{{{{', '}}}}' or control characters.", parameterName);
        }

        // The engine turns typographic double quotes into '"' before it reads a condition.
        if (value.Any(c => c is '“' or '”' or '„' or '‟' or '″'))
        {
            throw new ArgumentException($"'{value}' contains typographic quotes, which Templify reads as '\"'.", parameterName);
        }

        return "\"" + value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
    }

    public static string Number(double value, string parameterName)
    {
        // The condition lexer reads a number as one word of digits, '.' and a leading '-': no exponent ("1E+20").
        if (double.IsNaN(value) || double.IsInfinity(value) || Math.Abs(value) > 7.9e27)
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "A condition compares with a finite number of at most 7.9e27.");
        }

        return ((decimal)value).ToString(CultureInfo.InvariantCulture);
    }
}

/// <summary>A path's truthiness: <c>{{#if path}}</c>.</summary>
internal sealed class TruthCondition(TemplatePath path) : Condition
{
    internal override int Precedence => 6;

    internal override IEnumerable<TemplatePath> Paths => [path];

    internal override void Write(StringBuilder text) => ConditionText.Path(text, path);
}

/// <summary>A binary comparison or string operator: <c>path = "x"</c>, <c>path &gt; 3</c>, <c>path contains "x"</c>.</summary>
internal sealed class ComparisonCondition(TemplatePath path, string op, string literal) : Condition
{
    internal override int Precedence => 4;

    internal override IEnumerable<TemplatePath> Paths => [path];

    internal override void Write(StringBuilder text)
    {
        ConditionText.Path(text, path);
        text.Append(' ').Append(op).Append(' ').Append(literal);
    }
}

/// <summary>Membership in a list literal: <c>path in ("a", "b")</c>.</summary>
internal sealed class InCondition(TemplatePath path, IReadOnlyList<string> literals) : Condition
{
    internal override int Precedence => 4;

    internal override IEnumerable<TemplatePath> Paths => [path];

    internal override void Write(StringBuilder text)
    {
        ConditionText.Path(text, path);
        text.Append(" in (").Append(string.Join(", ", literals)).Append(')');
    }
}

/// <summary>A postfix operator: <c>path exists</c>, <c>path is empty</c>, <c>path is not empty</c>.</summary>
internal sealed class PostfixCondition(TemplatePath path, string op) : Condition
{
    internal override int Precedence => 5;

    internal override IEnumerable<TemplatePath> Paths => [path];

    internal override void Write(StringBuilder text)
    {
        ConditionText.Path(text, path);
        text.Append(' ').Append(op);
    }
}

/// <summary><c>not …</c>; any operand other than a plain path is parenthesised, for readability.</summary>
internal sealed class NotCondition(Condition operand) : Condition
{
    internal override int Precedence => 3;

    internal override IEnumerable<TemplatePath> Paths => operand.Paths;

    internal override void Write(StringBuilder text)
    {
        text.Append("not ");
        WriteOperand(text, operand, 6);
    }
}

/// <summary><c>… and …</c> / <c>… or …</c>, left-associative.</summary>
internal sealed class LogicalCondition(string op, int precedence, Condition left, Condition right) : Condition
{
    internal override int Precedence => precedence;

    internal override IEnumerable<TemplatePath> Paths => left.Paths.Concat(right.Paths);

    internal override void Write(StringBuilder text)
    {
        WriteOperand(text, left, precedence);
        text.Append(' ').Append(op).Append(' ');
        WriteOperand(text, right, precedence + 1);
    }
}

/// <summary>Condition text written by hand; always parenthesised when it is an operand.</summary>
internal sealed class RawCondition(string expression, IReadOnlyList<TemplatePath> uses) : Condition
{
    internal override int Precedence => 0;

    internal override IEnumerable<TemplatePath> Paths => uses;

    internal override void Write(StringBuilder text) => text.Append(expression);
}
