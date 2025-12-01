// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace TriasDev.Templify.Conditionals;

/// <summary>
/// Represents a pre-loaded data context for efficient batch evaluation of multiple condition expressions.
/// </summary>
/// <remarks>
/// <para>
/// Use this interface when evaluating many conditions against the same data set.
/// Creating a context once and reusing it is more efficient than parsing data for each evaluation.
/// </para>
/// <para>
/// Create instances via <see cref="IConditionEvaluator.CreateConditionContext(Dictionary{string, object})"/>
/// or <see cref="IConditionEvaluator.CreateConditionContext(string)"/>.
/// </para>
/// </remarks>
/// <example>
/// Batch evaluation example:
/// <code>
/// var evaluator = new ConditionEvaluator();
/// var context = evaluator.CreateConditionContext(data);
///
/// // Evaluate multiple expressions efficiently against the same data
/// bool r1 = context.Evaluate("IsActive");
/// bool r2 = context.Evaluate("Count > 0");
/// bool r3 = context.Evaluate("Status = \"Active\" and IsEnabled");
/// </code>
/// </example>
public interface IConditionContext
{
    /// <summary>
    /// Evaluates a conditional expression against the pre-loaded data.
    /// </summary>
    /// <param name="expression">
    /// The expression to evaluate. Supports:
    /// <list type="bullet">
    /// <item>Simple variables: "IsActive"</item>
    /// <item>Nested paths: "Customer.Address.City"</item>
    /// <item>Comparisons: "Status = \"Active\"", "Count > 0"</item>
    /// <item>Logical operators: "IsEnabled and HasAccess", "A or B"</item>
    /// <item>Negation: "not IsDisabled"</item>
    /// </list>
    /// </param>
    /// <returns>True if the condition is met; otherwise, false.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="expression"/> is null.</exception>
    bool Evaluate(string expression);

    /// <summary>
    /// Asynchronously evaluates a conditional expression against the pre-loaded data.
    /// </summary>
    /// <param name="expression">The expression to evaluate.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that resolves to true if the condition is met; otherwise, false.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="expression"/> is null.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is canceled.</exception>
    Task<bool> EvaluateAsync(string expression, CancellationToken cancellationToken = default);
}
