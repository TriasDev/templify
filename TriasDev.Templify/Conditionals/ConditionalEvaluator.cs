// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Collections.Frozen;
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
    /// <summary>
    /// Known operator-like tokens that are common mistakes but not valid operators.
    /// </summary>
    private static readonly FrozenSet<string> _knownInvalidOperators =
        new[] { "===", "<>", "&&", "||" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Validates a conditional expression for syntactic correctness.
    /// This is a pure syntax check that does not require a data context.
    /// </summary>
    /// <remarks>
    /// Validation is parser-based so that it accepts exactly what <see cref="Evaluate(string, IEvaluationContext)"/>
    /// accepts (including the operator syntax added by the expression engine, e.g. <c>in</c>, <c>contains</c>,
    /// <c>exists</c>). Empty expressions and unterminated string literals are reported as dedicated issue types;
    /// the unterminated-string check is done by the lexer itself, so escaped quotes are handled identically. When the parser rejects an expression, a heuristic
    /// structural analysis classifies the failure into a typed issue.
    /// </remarks>
    /// <param name="expression">The expression to validate.</param>
    /// <returns>A validation result indicating whether the expression is valid and any issues found.</returns>
    internal ConditionValidationResult Validate(string expression)
    {
        List<ConditionValidationIssue> issues = new();

        // Pre-check (a): Empty/whitespace expression.
        if (string.IsNullOrWhiteSpace(expression))
        {
            issues.Add(new ConditionValidationIssue(
                ConditionValidationIssueType.EmptyExpression,
                "Expression is empty."));
            return ConditionValidationResult.Failure(issues);
        }

        // Pre-check (b): Unbalanced quotes. The lexer reports unterminated string literals and uses the
        // same escape rules (\" and \\) as evaluation, so validation and evaluation agree.
        try
        {
            _ = new Engine.ConditionLexer().Tokenize(expression);
        }
        catch (Engine.ConditionParseException ex) when (ex.IsUnterminatedString)
        {
            issues.Add(new ConditionValidationIssue(
                ConditionValidationIssueType.UnbalancedQuotes,
                "Expression contains unbalanced quotes."));
            return ConditionValidationResult.Failure(issues);
        }

        // Attempt a real parse. Validate must accept everything the parser (and therefore Evaluate)
        // accepts, including the new operator syntax.
        if (TryParse(expression))
        {
            // Parser accepts the expression; only pre-check issues (if any) remain.
            return issues.Count == 0
                ? ConditionValidationResult.Success()
                : ConditionValidationResult.Failure(issues);
        }

        // Parser rejected the expression. Fall back to the heuristic structural analysis to classify
        // the failure into a typed issue, combined with any pre-check issues.
        AnalyzeStructure(expression, issues);

        if (issues.Count == 0)
        {
            // Parser failed but the heuristic found nothing specific; still report the expression as
            // invalid rather than silently succeeding.
            issues.Add(new ConditionValidationIssue(
                ConditionValidationIssueType.MissingOperand,
                "Expression is not a valid condition."));
        }

        return ConditionValidationResult.Failure(issues);
    }

    /// <summary>
    /// Attempts to fully parse the expression using the expression engine.
    /// </summary>
    /// <returns><c>true</c> if the parser accepts the expression; otherwise <c>false</c>.</returns>
    private static bool TryParse(string expression)
    {
        try
        {
            _ = Parse(expression);
            return true;
        }
        catch (Engine.ConditionParseException)
        {
            return false;
        }
    }

    /// <summary>
    /// Heuristic structural analysis used only when the parser rejects an expression. It reproduces the
    /// legacy whitespace-split classification so parser failures map onto the historical typed issues
    /// (<see cref="ConditionValidationIssueType.MissingOperand"/>, <c>ConsecutiveOperators</c>,
    /// <c>ConsecutiveOperands</c>, <c>UnknownOperator</c>). Discovered issues are appended to
    /// <paramref name="issues"/>.
    /// </summary>
    private static void AnalyzeStructure(string expression, List<ConditionValidationIssue> issues)
    {
        List<string> tokens = SplitForStructuralAnalysis(expression);

        if (tokens.Count == 0)
        {
            return;
        }

        // Walk tokens and check structure.
        // Classify: "operator" (comparison/logical), "not", "postfix", or "operand".
        string? previousType = null;
        string? previousToken = null;

        for (int i = 0; i < tokens.Count; i++)
        {
            string token = tokens[i];
            string currentType;

            if (IsPrefixOperator(token))
            {
                currentType = "not";
            }
            else if (Engine.ConditionOperatorRegistry.Shared.IsPostfixToken(token))
            {
                // Postfix existence/emptiness keywords (`exists`, `is empty`, `is not empty`).
                // Kept distinct from "comparison"/"logical" so they are exempt from the
                // trailing-operator check (an expression is allowed to end in one of these)
                // and from the consecutive-operator/operand checks around them.
                currentType = "postfix";
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
        return Evaluate(expression, context, warningCollector: null);
    }

    /// <summary>
    /// Evaluates a conditional expression and reports expressions that cannot be parsed.
    /// </summary>
    /// <param name="expression">The expression to evaluate.</param>
    /// <param name="context">The evaluation context for variable resolution.</param>
    /// <param name="warningCollector">
    /// Receives a <see cref="ProcessingWarningType.ExpressionFailed"/> warning when the expression is malformed.
    /// Malformed expressions still evaluate to <see langword="false"/>.
    /// </param>
    internal bool Evaluate(string expression, IEvaluationContext context, IWarningCollector? warningCollector)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            warningCollector?.AddWarning(ProcessingWarning.ConditionFailed(expression ?? string.Empty, "Expression is empty."));
            return false;
        }

        Engine.ConditionNode node;
        try
        {
            node = Parse(expression);
        }
        catch (Engine.ConditionParseException ex)
        {
            warningCollector?.AddWarning(ProcessingWarning.ConditionFailed(expression, ex.Message));
            return false;
        }

        return new Engine.ConditionEvaluatorCore(context, Engine.DefaultConditionDialect.Instance).EvaluateBool(node);
    }

    /// <summary>
    /// Parses a <c>{{#if}}</c>/<c>{{#elseif}}</c> expression into an AST.
    /// </summary>
    /// <exception cref="Engine.ConditionParseException">The expression is malformed.</exception>
    /// <remarks>Parsed expressions are cached (see <see cref="Engine.ConditionAstCache"/>).</remarks>
    internal static Engine.ConditionNode Parse(string expression)
        => Engine.ConditionAstCache.Conditions.GetOrParse(expression ?? string.Empty);

    /// <summary>
    /// Collects the variables referenced by a parsed expression (literals, operators and list items are skipped).
    /// </summary>
    internal static IEnumerable<Engine.VariableNode> CollectVariables(Engine.ConditionNode node)
    {
        switch (node)
        {
            case Engine.VariableNode variable:
                yield return variable;
                break;
            case Engine.OperatorNode op:
                foreach (Engine.ConditionNode operand in op.Operands)
                {
                    foreach (Engine.VariableNode v in CollectVariables(operand))
                    {
                        yield return v;
                    }
                }
                break;
            case Engine.ListNode list:
                foreach (Engine.ConditionNode item in list.Items)
                {
                    foreach (Engine.VariableNode v in CollectVariables(item))
                    {
                        yield return v;
                    }
                }
                break;
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
    /// Splits an expression at whitespace outside of string literals (the quotes are dropped), for the
    /// structural analysis only.
    /// </summary>
    /// <remarks>
    /// This is intentionally not the engine lexer: the typed validation issues are defined on
    /// whitespace-separated words (e.g. <c>===</c> or <c>&lt;&gt;</c> is one unknown operator, where the lexer
    /// would split it into known operators). Operator classification uses the registry.
    /// </remarks>
    private static List<string> SplitForStructuralAnalysis(string expression)
    {
        expression = Engine.ConditionLexer.NormalizeQuotes(expression);

        List<string> tokens = new List<string>();
        StringBuilder currentToken = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < expression.Length; i++)
        {
            char c = expression[i];

            if (inQuotes && c == '\\' && i + 1 < expression.Length && (expression[i + 1] == '"' || expression[i + 1] == '\\'))
            {
                // Same escape rules as the lexer
                currentToken.Append(expression[i + 1]);
                i++;
                continue;
            }

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

    // Operator classification for the structural analysis comes from the operator registry, so a new
    // operator is recognized here without further changes. Registry tokens are lower case.

    private static bool IsPrefixOperator(string token)
        => Engine.ConditionOperatorRegistry.Shared.FindPrefix(token.ToLowerInvariant()) != null;

    private static bool IsLogicalOperator(string token)
        => Engine.ConditionOperatorRegistry.Shared.FindInfix(token.ToLowerInvariant()) is { Precedence: < Engine.OperatorPrecedence.Comparison };

    private static bool IsComparisonOperator(string token)
        => Engine.ConditionOperatorRegistry.Shared.FindInfix(token.ToLowerInvariant()) is { Precedence: Engine.OperatorPrecedence.Comparison };
}
