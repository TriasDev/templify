// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using TriasDev.Templify.Core;

namespace TriasDev.Templify.Loops;

/// <summary>
/// Loop evaluation context that resolves variables from loop iteration context,
/// with fallback to parent context.
/// </summary>
/// <remarks>
/// This context represents a loop iteration scope in template evaluation.
/// It provides access to:
/// - Loop metadata (@index, @first, @last, @count)
/// - Current item properties (for object collections)
/// - Current item value (for primitive collections, via "." or "this")
/// - Parent context variables (global or outer loop)
///
/// Resolution order:
/// 1. Loop metadata variables (@index, @first, @last, @count)
/// 2. Current item properties or value
/// 3. Parent loop context (if nested loop)
/// 4. Parent evaluation context (typically GlobalEvaluationContext)
/// </remarks>
internal sealed class LoopEvaluationContext : IEvaluationContext
{
    private readonly LoopContext _loopContext;
    private readonly IEvaluationContext _parent;

    /// <summary>
    /// Initializes a new instance of the <see cref="LoopEvaluationContext"/> class.
    /// </summary>
    /// <param name="loopContext">The loop context containing iteration state.</param>
    /// <param name="parent">The parent evaluation context (global or outer loop).</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="loopContext"/> or <paramref name="parent"/> is null.
    /// </exception>
    public LoopEvaluationContext(LoopContext loopContext, IEvaluationContext parent)
    {
        _loopContext = loopContext ?? throw new ArgumentNullException(nameof(loopContext));
        _parent = parent ?? throw new ArgumentNullException(nameof(parent));
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Resolution order:
    /// 1. Try loop context (metadata + current item properties)
    /// 2. If not found, try parent context
    ///
    /// This enables conditionals inside loops to access both loop-scoped
    /// variables and global variables.
    /// </remarks>
    public bool TryResolveVariable(string variableName, out object? value)
    {
        // Try loop context first (handles @index, @first, @last, @count, current item properties)
        if (_loopContext.TryResolveVariable(variableName, out value))
        {
            return true;
        }

        // Fall back to parent context (root data or parent loop)
        return _parent.TryResolveVariable(variableName, out value);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Returns the parent evaluation context, which could be:
    /// - GlobalEvaluationContext (for top-level loops)
    /// - Another LoopEvaluationContext (for nested loops)
    /// </remarks>
    public IEvaluationContext? Parent => _parent;

    /// <inheritdoc/>
    /// <remarks>
    /// Delegates to the parent context's RootData property.
    /// This allows loop contexts to access global data even when deeply nested.
    /// </remarks>
    public IReadOnlyDictionary<string, object> RootData => _parent.RootData;
}
