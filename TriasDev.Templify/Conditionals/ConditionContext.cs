// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using TriasDev.Templify.Core;

namespace TriasDev.Templify.Conditionals;

/// <summary>
/// Provides a pre-loaded data context for efficient batch evaluation of multiple condition expressions.
/// </summary>
/// <remarks>
/// <para>
/// This class wraps an evaluation context and provides direct evaluation methods,
/// eliminating the need to pass the context to each evaluation call.
/// </para>
/// <para>
/// Thread Safety: This class is thread-safe. The underlying evaluator has no mutable
/// instance state (only immutable constants), so multiple threads can call Evaluate
/// concurrently without synchronization.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var evaluator = new ConditionEvaluator();
/// var context = evaluator.CreateConditionContext(data);
///
/// bool r1 = context.Evaluate("IsActive");
/// bool r2 = context.Evaluate("Count > 5");
/// </code>
/// </example>
public sealed class ConditionContext : IConditionContext
{
    private readonly ConditionalEvaluator _evaluator;
    private readonly IEvaluationContext _context;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConditionContext"/> class.
    /// </summary>
    /// <param name="evaluator">The evaluator to use for condition evaluation.</param>
    /// <param name="context">The evaluation context containing variable data.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="evaluator"/> or <paramref name="context"/> is null.</exception>
    internal ConditionContext(ConditionalEvaluator evaluator, IEvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(evaluator);
        ArgumentNullException.ThrowIfNull(context);
        _evaluator = evaluator;
        _context = context;
    }

    /// <inheritdoc/>
    public bool Evaluate(string expression)
    {
        ArgumentNullException.ThrowIfNull(expression);
        return _evaluator.Evaluate(expression, _context);
    }

    /// <inheritdoc/>
    public Task<bool> EvaluateAsync(string expression, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Evaluate(expression));
    }
}
