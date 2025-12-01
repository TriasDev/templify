// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using TriasDev.Templify.Core;
using TriasDev.Templify.Utilities;

namespace TriasDev.Templify.Conditionals;

/// <summary>
/// Provides standalone condition evaluation functionality.
/// </summary>
/// <remarks>
/// <para>
/// This class exposes the condition evaluation engine used by Templify templates
/// for use in standalone scenarios without Word document processing.
/// </para>
/// <para>
/// Thread Safety: This class is thread-safe. The underlying evaluator has no mutable
/// instance state (only immutable constants), so multiple threads can call Evaluate
/// concurrently without synchronization.
/// </para>
/// </remarks>
/// <example>
/// Basic usage:
/// <code>
/// var evaluator = new ConditionEvaluator();
/// var data = new Dictionary&lt;string, object&gt;
/// {
///     ["IsActive"] = true,
///     ["Count"] = 5,
///     ["Status"] = "Active"
/// };
///
/// bool result1 = evaluator.Evaluate("IsActive", data);           // true
/// bool result2 = evaluator.Evaluate("Count > 3", data);          // true
/// bool result3 = evaluator.Evaluate("Status = \"Active\"", data); // true
/// </code>
/// </example>
/// <example>
/// Using JSON data:
/// <code>
/// var evaluator = new ConditionEvaluator();
/// string json = """{"IsActive": true, "Count": 5}""";
///
/// bool result = evaluator.Evaluate("IsActive and Count > 0", json);
/// </code>
/// </example>
/// <example>
/// Batch evaluation with context:
/// <code>
/// var evaluator = new ConditionEvaluator();
/// var context = evaluator.CreateContext(data);
///
/// // Evaluate multiple expressions efficiently
/// bool r1 = evaluator.Evaluate("Condition1", context);
/// bool r2 = evaluator.Evaluate("Condition2", context);
/// bool r3 = evaluator.Evaluate("Condition3", context);
/// </code>
/// </example>
public sealed class ConditionEvaluator : IConditionEvaluator
{
    private readonly ConditionalEvaluator _evaluator = new();

    /// <inheritdoc/>
    public bool Evaluate(string expression, Dictionary<string, object> data)
    {
        ArgumentNullException.ThrowIfNull(expression);
        ArgumentNullException.ThrowIfNull(data);
        return _evaluator.Evaluate(expression, data);
    }

    /// <inheritdoc/>
    public bool Evaluate(string expression, string jsonData)
    {
        ArgumentNullException.ThrowIfNull(expression);
        ArgumentNullException.ThrowIfNull(jsonData);
        Dictionary<string, object> data = JsonDataParser.ParseJsonToDataDictionary(jsonData);
        return _evaluator.Evaluate(expression, data);
    }

    /// <inheritdoc/>
    public bool Evaluate(string expression, IEvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(expression);
        ArgumentNullException.ThrowIfNull(context);
        return _evaluator.Evaluate(expression, context);
    }

    /// <inheritdoc/>
    public Task<bool> EvaluateAsync(string expression, Dictionary<string, object> data, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Evaluate(expression, data));
    }

    /// <inheritdoc/>
    public Task<bool> EvaluateAsync(string expression, string jsonData, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Evaluate(expression, jsonData));
    }

    /// <inheritdoc/>
    public Task<bool> EvaluateAsync(string expression, IEvaluationContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Evaluate(expression, context));
    }

    /// <inheritdoc/>
    public IEvaluationContext CreateContext(Dictionary<string, object> data)
    {
        ArgumentNullException.ThrowIfNull(data);
        return new GlobalEvaluationContext(data);
    }

    /// <inheritdoc/>
    public IEvaluationContext CreateContext(string jsonData)
    {
        ArgumentNullException.ThrowIfNull(jsonData);
        Dictionary<string, object> data = JsonDataParser.ParseJsonToDataDictionary(jsonData);
        return new GlobalEvaluationContext(data);
    }

    /// <inheritdoc/>
    public IConditionContext CreateConditionContext(Dictionary<string, object> data)
    {
        ArgumentNullException.ThrowIfNull(data);
        IEvaluationContext context = new GlobalEvaluationContext(data);
        return new ConditionContext(_evaluator, context);
    }

    /// <inheritdoc/>
    public IConditionContext CreateConditionContext(string jsonData)
    {
        ArgumentNullException.ThrowIfNull(jsonData);
        Dictionary<string, object> data = JsonDataParser.ParseJsonToDataDictionary(jsonData);
        IEvaluationContext context = new GlobalEvaluationContext(data);
        return new ConditionContext(_evaluator, context);
    }
}
