// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Text;
using TriasDev.Templify.Core;
using TriasDev.Templify.Placeholders;

namespace TriasDev.Templify.Conditionals;

/// <summary>
/// Evaluates conditional expressions for conditional blocks.
/// Supports operators: =, ==, !=, &gt;, &lt;, &gt;=, &lt;=, and, or, not
/// </summary>
internal sealed class ConditionalEvaluator
{
    private const string OrOperator = "or";
    private const string AndOperator = "and";
    private const string NotOperator = "not";
    private const string EqOperator = "=";
    private const string EqOperatorDouble = "==";
    private const string NeOperator = "!=";
    private const string GtOperator = ">";
    private const string LtOperator = "<";
    private const string GteOperator = ">=";
    private const string LteOperator = "<=";

    /// <summary>
    /// Known operator-like tokens that are common mistakes but not valid operators.
    /// </summary>
    private static readonly HashSet<string> _knownInvalidOperators = new(StringComparer.OrdinalIgnoreCase)
    {
        "===", "<>", "&&", "||"
    };

    /// <summary>
    /// Validates a conditional expression for syntactic correctness.
    /// This is a pure syntax check that does not require a data context.
    /// </summary>
    /// <param name="expression">The expression to validate.</param>
    /// <returns>A validation result indicating whether the expression is valid and any issues found.</returns>
    internal ConditionValidationResult Validate(string expression)
    {
        List<ConditionValidationIssue> issues = new();

        // Rule 1: Empty expression
        if (string.IsNullOrWhiteSpace(expression))
        {
            issues.Add(new ConditionValidationIssue(
                ConditionValidationIssueType.EmptyExpression,
                "Expression is empty."));
            return ConditionValidationResult.Failure(issues);
        }

        // Rule 2: Unbalanced quotes
        string normalized = NormalizeQuotes(expression);
        int quoteCount = 0;
        foreach (char c in normalized)
        {
            if (c == '"')
            {
                quoteCount++;
            }
        }

        if (quoteCount % 2 != 0)
        {
            issues.Add(new ConditionValidationIssue(
                ConditionValidationIssueType.UnbalancedQuotes,
                "Expression contains unbalanced quotes."));
        }

        // Tokenize
        List<string> tokens = ParseExpression(expression);

        if (tokens.Count == 0)
        {
            if (issues.Count == 0)
            {
                issues.Add(new ConditionValidationIssue(
                    ConditionValidationIssueType.EmptyExpression,
                    "Expression is empty."));
            }

            return ConditionValidationResult.Failure(issues);
        }

        // Walk tokens and check structure
        // Classify: "operator" (comparison/logical), "not", or "operand"
        string? previousType = null; // "operand", "comparison", "logical", "not"
        string? previousToken = null;

        for (int i = 0; i < tokens.Count; i++)
        {
            string token = tokens[i];
            string currentType;

            if (string.Equals(token, NotOperator, StringComparison.OrdinalIgnoreCase))
            {
                currentType = "not";
            }
            else if (string.Equals(token, "exists", StringComparison.OrdinalIgnoreCase)
                  || string.Equals(token, "empty", StringComparison.OrdinalIgnoreCase)
                  || string.Equals(token, "is", StringComparison.OrdinalIgnoreCase))
            {
                currentType = "comparison";
            }
            else if (IsComparisonOperator(token))
            {
                currentType = "comparison";
            }
            else if (IsLogicalOperator(token))
            {
                currentType = "logical";
            }
            else if (IsSuspectedUnknownOperator(token))
            {
                string hint = token == "<>" ? " Did you mean '!='?" : "";
                issues.Add(new ConditionValidationIssue(
                    ConditionValidationIssueType.UnknownOperator,
                    $"Unknown operator '{token}'.{hint}",
                    token));
                currentType = "comparison"; // Treat as operator for structural analysis
            }
            else
            {
                currentType = "operand";
            }

            // Structural checks
            if (i == 0 && (currentType == "comparison" || currentType == "logical"))
            {
                // Operator at start (not is OK)
                issues.Add(new ConditionValidationIssue(
                    ConditionValidationIssueType.MissingOperand,
                    $"Operator '{token}' is missing a left-hand operand.",
                    token));
            }
            else if (previousType != null)
            {
                bool prevIsOp = previousType == "comparison" || previousType == "logical";
                bool currIsOp = currentType == "comparison" || currentType == "logical";

                if (prevIsOp && currIsOp)
                {
                    issues.Add(new ConditionValidationIssue(
                        ConditionValidationIssueType.ConsecutiveOperators,
                        $"Consecutive operators '{previousToken}' and '{token}'.",
                        token));
                }
                else if (previousType == "operand" && currentType == "operand")
                {
                    issues.Add(new ConditionValidationIssue(
                        ConditionValidationIssueType.ConsecutiveOperands,
                        $"Missing operator between '{previousToken}' and '{token}'.",
                        token));
                }
            }

            previousType = currentType;
            previousToken = token;
        }

        // Check for trailing operator
        if (previousType == "comparison" || previousType == "logical")
        {
            issues.Add(new ConditionValidationIssue(
                ConditionValidationIssueType.MissingOperand,
                $"Operator '{previousToken}' is missing a right-hand operand.",
                previousToken));
        }

        return issues.Count == 0
            ? ConditionValidationResult.Success()
            : ConditionValidationResult.Failure(issues);
    }

    /// <summary>
    /// Checks if a token looks like an operator but is not recognized.
    /// </summary>
    private static bool IsSuspectedUnknownOperator(string token)
    {
        // Check known invalid operators first
        if (_knownInvalidOperators.Contains(token))
        {
            return true;
        }

        // Check if token consists entirely of operator-like punctuation
        foreach (char c in token)
        {
            if (!"=!<>$~^&|%#".Contains(c))
            {
                return false;
            }
        }

        // Non-empty punctuation-only token that isn't a valid operator
        return token.Length > 0;
    }

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

        try
        {
            IReadOnlyList<Engine.ConditionToken> tokens = new Engine.ConditionLexer().Tokenize(expression);
            Engine.ConditionNode node = new Engine.ConditionParser(Engine.ConditionOperatorRegistry.Shared).Parse(tokens);
            return new Engine.ConditionEvaluatorCore(context, Engine.DefaultConditionDialect.Instance).EvaluateBool(node);
        }
        catch (Engine.ConditionParseException)
        {
            return false;
        }
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

    private bool IsLogicalOperator(string token)
    {
        string lower = token.ToLower();
        return lower == OrOperator || lower == AndOperator;
    }

    private bool IsComparisonOperator(string token)
    {
        string lower = token.ToLower();
        return lower == EqOperator || lower == EqOperatorDouble || lower == NeOperator ||
               lower == GtOperator || lower == LtOperator ||
               lower == GteOperator || lower == LteOperator ||
               lower == "in" || lower == "contains" || lower == "startswith" || lower == "endswith";
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
            .Replace('\u201F', '"')  // U+201F Double High-Reversed-9 Quotation Mark
            .Replace('\u2018', '\'') // U+2018 Left Single Quotation Mark
            .Replace('\u2019', '\'') // U+2019 Right Single Quotation Mark
            .Replace('\u201A', '\'') // U+201A Single Low-9 Quotation Mark
            .Replace('\u201B', '\''); // U+201B Single High-Reversed-9 Quotation Mark
    }
}
