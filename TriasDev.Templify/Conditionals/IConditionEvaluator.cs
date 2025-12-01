// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using TriasDev.Templify.Core;

namespace TriasDev.Templify.Conditionals;

/// <summary>
/// Provides standalone condition evaluation functionality.
/// </summary>
/// <remarks>
/// <para>
/// This interface exposes the condition evaluation engine used by Templify templates
/// for use in standalone scenarios without Word document processing.
/// </para>
/// <para>
/// Supported operators: =, !=, &gt;, &lt;, &gt;=, &lt;=, and, or, not
/// </para>
/// <para>
/// Examples:
/// <code>
/// var evaluator = new ConditionEvaluator();
///
/// // Simple variable check
/// evaluator.Evaluate("IsActive", data);  // true if IsActive is truthy
///
/// // Comparison
/// evaluator.Evaluate("Status = \"Active\"", data);
///
/// // Complex expression
/// evaluator.Evaluate("Count > 0 and IsEnabled", data);
/// </code>
/// </para>
/// </remarks>
public interface IConditionEvaluator
{
    #region Evaluate (Synchronous)

    /// <summary>
    /// Evaluates a conditional expression against the provided data.
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
    /// <param name="data">The data dictionary containing variables to resolve.</param>
    /// <returns>True if the condition is met; otherwise, false.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="expression"/> or <paramref name="data"/> is null.</exception>
    bool Evaluate(string expression, Dictionary<string, object> data);

    /// <summary>
    /// Evaluates a conditional expression against JSON data.
    /// </summary>
    /// <param name="expression">The expression to evaluate.</param>
    /// <param name="jsonData">A JSON string representing the data object.</param>
    /// <returns>True if the condition is met; otherwise, false.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="expression"/> or <paramref name="jsonData"/> is null.</exception>
    /// <exception cref="System.Text.Json.JsonException">Thrown when JSON is invalid or root is not an object.</exception>
    bool Evaluate(string expression, string jsonData);

    /// <summary>
    /// Evaluates a conditional expression using a pre-created evaluation context.
    /// </summary>
    /// <param name="expression">The expression to evaluate.</param>
    /// <param name="context">The evaluation context containing variable data.</param>
    /// <returns>True if the condition is met; otherwise, false.</returns>
    /// <remarks>
    /// Use this overload with a context from <see cref="CreateContext(Dictionary{string, object})"/>
    /// for optimal performance when evaluating multiple expressions against the same data.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="expression"/> or <paramref name="context"/> is null.</exception>
    bool Evaluate(string expression, IEvaluationContext context);

    #endregion

    #region EvaluateAsync (Asynchronous)

    /// <summary>
    /// Asynchronously evaluates a conditional expression against the provided data.
    /// </summary>
    /// <param name="expression">The expression to evaluate.</param>
    /// <param name="data">The data dictionary containing variables to resolve.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that resolves to true if the condition is met; otherwise, false.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="expression"/> or <paramref name="data"/> is null.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is canceled.</exception>
    Task<bool> EvaluateAsync(string expression, Dictionary<string, object> data, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously evaluates a conditional expression against JSON data.
    /// </summary>
    /// <param name="expression">The expression to evaluate.</param>
    /// <param name="jsonData">A JSON string representing the data object.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that resolves to true if the condition is met; otherwise, false.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="expression"/> or <paramref name="jsonData"/> is null.</exception>
    /// <exception cref="System.Text.Json.JsonException">Thrown when JSON is invalid or root is not an object.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is canceled.</exception>
    Task<bool> EvaluateAsync(string expression, string jsonData, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously evaluates a conditional expression using a pre-created evaluation context.
    /// </summary>
    /// <param name="expression">The expression to evaluate.</param>
    /// <param name="context">The evaluation context containing variable data.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that resolves to true if the condition is met; otherwise, false.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="expression"/> or <paramref name="context"/> is null.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is canceled.</exception>
    Task<bool> EvaluateAsync(string expression, IEvaluationContext context, CancellationToken cancellationToken = default);

    #endregion

    #region CreateContext

    /// <summary>
    /// Creates an evaluation context from a data dictionary for batch evaluations.
    /// </summary>
    /// <param name="data">The data dictionary containing variables to resolve.</param>
    /// <returns>An evaluation context that can be reused for multiple evaluations.</returns>
    /// <remarks>
    /// Use this method when evaluating multiple expressions against the same data
    /// to avoid repeated context creation overhead.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="data"/> is null.</exception>
    IEvaluationContext CreateContext(Dictionary<string, object> data);

    /// <summary>
    /// Creates an evaluation context from JSON data for batch evaluations.
    /// </summary>
    /// <param name="jsonData">A JSON string representing the data object.</param>
    /// <returns>An evaluation context that can be reused for multiple evaluations.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="jsonData"/> is null.</exception>
    /// <exception cref="System.Text.Json.JsonException">Thrown when JSON is invalid or root is not an object.</exception>
    IEvaluationContext CreateContext(string jsonData);

    #endregion
}
