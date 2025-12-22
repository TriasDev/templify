// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Collections;
using System.Text;
using TriasDev.Templify.Core;
using TriasDev.Templify.Placeholders;

namespace TriasDev.Templify.Conditionals;

/// <summary>
/// Evaluates conditional expressions for conditional blocks.
/// Supports operators: =, !=, &gt;, &lt;, &gt;=, &lt;=, and, or, not
/// </summary>
internal sealed class ConditionalEvaluator
{
    private const string OrOperator = "or";
    private const string AndOperator = "and";
    private const string NotOperator = "not";
    private const string EqOperator = "=";
    private const string NeOperator = "!=";
    private const string GtOperator = ">";
    private const string LtOperator = "<";
    private const string GteOperator = ">=";
    private const string LteOperator = "<=";

    /// <summary>
    /// Evaluates a conditional expression.
    /// </summary>
    /// <param name="expression">The expression to evaluate (e.g., "IsActive", "Status = Active", "Count > 0 and IsEnabled")</param>
    /// <param name="context">The evaluation context for variable resolution</param>
    /// <returns>True if the condition is met, false otherwise</returns>
    public bool Evaluate(string expression, IEvaluationContext context)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return false;
        }

        // Parse the expression into tokens
        List<string> tokens = ParseExpression(expression);

        if (tokens.Count == 0)
        {
            return false;
        }

        // If it's a simple variable reference, evaluate it directly
        if (tokens.Count == 1)
        {
            return EvaluateVariable(tokens[0], context, out _);
        }

        // Process complex expression with operators
        return EvaluateTokens(tokens, context);
    }

    /// <summary>
    /// Evaluates a conditional expression (backward compatibility bridge).
    /// </summary>
    /// <param name="expression">The expression to evaluate</param>
    /// <param name="data">The data dictionary</param>
    /// <returns>True if the condition is met, false otherwise</returns>
    public bool Evaluate(string expression, Dictionary<string, object> data)
    {
        GlobalEvaluationContext context = new GlobalEvaluationContext(data);
        return Evaluate(expression, context);
    }

    /// <summary>
    /// Parses an expression into tokens, handling quoted strings.
    /// </summary>
    private List<string> ParseExpression(string expression)
    {
        expression = NormalizeQuotes(expression);

        List<string> tokens = new List<string>();
        StringBuilder currentToken = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < expression.Length; i++)
        {
            char c = expression[i];

            if (c == '"')
            {
                inQuotes = !inQuotes;
                // Don't include the quotes in the token
                continue;
            }

            if (char.IsWhiteSpace(c) && !inQuotes)
            {
                // Space outside quotes = token boundary
                if (currentToken.Length > 0)
                {
                    tokens.Add(currentToken.ToString());
                    currentToken.Clear();
                }
            }
            else
            {
                currentToken.Append(c);
            }
        }

        // Add last token
        if (currentToken.Length > 0)
        {
            tokens.Add(currentToken.ToString());
        }

        return tokens;
    }

    /// <summary>
    /// Evaluates a list of tokens with operators.
    /// </summary>
    private bool EvaluateTokens(List<string> tokens, IEvaluationContext context)
    {
        // Check if expression starts with NOT
        int startIndex = 0;
        bool negateNext = false;
        if (tokens[0].ToLower() == NotOperator)
        {
            negateNext = true;
            startIndex = 1;
        }

        // Get the initial variable value
        bool result = EvaluateVariable(tokens[startIndex], context, out object? currentValue);

        string? lastOperator = null;
        string? pendingLogicalOperator = null;

        for (int i = startIndex + 1; i < tokens.Count; i++)
        {
            string token = tokens[i];

            // Check if it's an operator
            if (IsLogicalOperator(token))
            {
                pendingLogicalOperator = token.ToLower();
                lastOperator = token.ToLower();
            }
            else if (IsComparisonOperator(token))
            {
                lastOperator = token.ToLower();
            }
            else if (token.ToLower() == NotOperator)
            {
                result = !result;
                negateNext = !negateNext;
            }
            else
            {
                // This is a value/variable to compare
                if (lastOperator != null)
                {
                    // Get the value (either from data or as literal)
                    object? nextValue = ResolveValueOrLiteral(token, context);

                    // Check if next operation is a comparison (for chained expressions like "var1 or var2 eq value")
                    bool isComparisonFollowing = false;
                    if (IsLogicalOperator(lastOperator) && i + 1 < tokens.Count)
                    {
                        isComparisonFollowing = IsComparisonOperator(tokens[i + 1]);
                    }

                    if (isComparisonFollowing)
                    {
                        // This is a variable for the next comparison
                        currentValue = nextValue;
                        continue;
                    }

                    // Perform the operation
                    switch (lastOperator)
                    {
                        case OrOperator:
                            result = result || EvaluateValue(nextValue);
                            break;

                        case AndOperator:
                            result = result && EvaluateValue(nextValue);
                            break;

                        case EqOperator:
                            {
                                bool comparisonResult = AreEqual(currentValue, nextValue);
                                if (negateNext)
                                {
                                    comparisonResult = !comparisonResult;
                                    negateNext = false;
                                }
                                result = ApplyLogicalOperator(result, comparisonResult, pendingLogicalOperator);
                                pendingLogicalOperator = null;
                                break;
                            }

                        case NeOperator:
                            {
                                bool comparisonResult = !AreEqual(currentValue, nextValue);
                                if (negateNext)
                                {
                                    comparisonResult = !comparisonResult;
                                    negateNext = false;
                                }
                                result = ApplyLogicalOperator(result, comparisonResult, pendingLogicalOperator);
                                pendingLogicalOperator = null;
                                break;
                            }

                        case GtOperator:
                            {
                                bool comparisonResult = IsGreaterThan(currentValue, nextValue);
                                if (negateNext)
                                {
                                    comparisonResult = !comparisonResult;
                                    negateNext = false;
                                }
                                result = ApplyLogicalOperator(result, comparisonResult, pendingLogicalOperator);
                                pendingLogicalOperator = null;
                                break;
                            }

                        case LtOperator:
                            {
                                bool comparisonResult = IsLessThan(currentValue, nextValue);
                                if (negateNext)
                                {
                                    comparisonResult = !comparisonResult;
                                    negateNext = false;
                                }
                                result = ApplyLogicalOperator(result, comparisonResult, pendingLogicalOperator);
                                pendingLogicalOperator = null;
                                break;
                            }

                        case GteOperator:
                            {
                                bool comparisonResult = IsGreaterThan(currentValue, nextValue) || AreEqual(currentValue, nextValue);
                                if (negateNext)
                                {
                                    comparisonResult = !comparisonResult;
                                    negateNext = false;
                                }
                                result = ApplyLogicalOperator(result, comparisonResult, pendingLogicalOperator);
                                pendingLogicalOperator = null;
                                break;
                            }

                        case LteOperator:
                            {
                                bool comparisonResult = IsLessThan(currentValue, nextValue) || AreEqual(currentValue, nextValue);
                                if (negateNext)
                                {
                                    comparisonResult = !comparisonResult;
                                    negateNext = false;
                                }
                                result = ApplyLogicalOperator(result, comparisonResult, pendingLogicalOperator);
                                pendingLogicalOperator = null;
                                break;
                            }
                    }

                    lastOperator = null;
                }
            }
        }

        // If negateNext is still true at the end, it means we had "not Variable" with no comparison
        // In this case, negate the result
        if (negateNext && lastOperator == null)
        {
            result = !result;
        }

        return result;
    }

    /// <summary>
    /// Applies a logical operator between two boolean values.
    /// </summary>
    private bool ApplyLogicalOperator(bool currentResult, bool comparisonResult, string? pendingOperator)
    {
        if (pendingOperator == null)
        {
            return comparisonResult;
        }

        return pendingOperator == OrOperator
            ? currentResult || comparisonResult
            : currentResult && comparisonResult;
    }

    /// <summary>
    /// Resolves a value from context or returns it as a literal.
    /// </summary>
    private object? ResolveValueOrLiteral(string token, IEvaluationContext context)
    {
        // Try to resolve as variable first
        if (context.TryResolveVariable(token, out object? value))
        {
            return value;
        }

        // Return as literal
        return token;
    }

    /// <summary>
    /// Evaluates a variable from the evaluation context.
    /// </summary>
    private bool EvaluateVariable(string variablePath, IEvaluationContext context, out object? value)
    {
        if (context.TryResolveVariable(variablePath, out value))
        {
            return EvaluateValue(value);
        }

        value = null;
        return false;
    }

    /// <summary>
    /// Evaluates a value as a boolean.
    /// Follows OpenXMLTemplates rules:
    /// - null → false
    /// - bool → its value
    /// - "true"/"false" → true/false
    /// - 1/0 → true/false
    /// - "1"/"0" → true/false
    /// - empty string/whitespace → false
    /// - empty collection → false
    /// - non-empty string → true
    /// - non-empty collection → true
    /// </summary>
    private bool EvaluateValue(object? value)
    {
        if (value == null)
        {
            return false;
        }

        if (value is bool boolValue)
        {
            return boolValue;
        }

        if (value is string stringValue)
        {
            if (string.IsNullOrWhiteSpace(stringValue))
            {
                return false;
            }

            string lowerValue = stringValue.ToLower();
            if (lowerValue == "false" || lowerValue == "0")
            {
                return false;
            }

            if (lowerValue == "true" || lowerValue == "1")
            {
                return true;
            }

            // Non-empty string
            return true;
        }

        if (value is int intValue)
        {
            return intValue switch
            {
                0 => false,
                1 => true,
                _ => true // Non-zero/one integers are true
            };
        }

        if (value is ICollection collection)
        {
            return collection.Count > 0;
        }

        // Any other non-null value
        return true;
    }

    /// <summary>
    /// Compares two values for equality.
    /// </summary>
    private bool AreEqual(object? left, object? right)
    {
        if (left == null && right == null)
        {
            return true;
        }

        if (left == null || right == null)
        {
            return false;
        }

        return left.ToString() == right.ToString();
    }

    /// <summary>
    /// Checks if left is greater than right.
    /// </summary>
    private bool IsGreaterThan(object? left, object? right)
    {
        try
        {
            return double.Parse(left?.ToString() ?? "0") > double.Parse(right?.ToString() ?? "0");
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Checks if left is less than right.
    /// </summary>
    private bool IsLessThan(object? left, object? right)
    {
        try
        {
            return double.Parse(left?.ToString() ?? "0") < double.Parse(right?.ToString() ?? "0");
        }
        catch
        {
            return false;
        }
    }

    private bool IsLogicalOperator(string token)
    {
        string lower = token.ToLower();
        return lower == OrOperator || lower == AndOperator;
    }

    private bool IsComparisonOperator(string token)
    {
        string lower = token.ToLower();
        return lower == EqOperator || lower == NeOperator ||
               lower == GtOperator || lower == LtOperator ||
               lower == GteOperator || lower == LteOperator;
    }

    /// <summary>
    /// Normalizes typographic/curly quotes to ASCII quotes.
    /// Word auto-formats ASCII quotes to typographic quotes, which breaks string comparisons.
    /// </summary>
    private static string NormalizeQuotes(string expression)
    {
        return expression
            .Replace('\u201C', '"')  // U+201C Left Double Quotation Mark
            .Replace('\u201D', '"')  // U+201D Right Double Quotation Mark
            .Replace('\u201E', '"')  // U+201E Double Low-9 Quotation Mark (German)
            .Replace('\u201F', '"'); // U+201F Double High-Reversed-9 Quotation Mark
    }
}
