// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace TriasDev.Templify.Core;

/// <summary>
/// Represents a context for evaluating variables in template expressions.
/// Supports hierarchical contexts (e.g., loop contexts nested in global context).
/// </summary>
/// <remarks>
/// This abstraction enables unified variable resolution across all template processors,
/// allowing features like conditionals to work inside loops by accessing loop-scoped data.
/// </remarks>
public interface IEvaluationContext
{
    /// <summary>
    /// Tries to resolve a variable by name.
    /// </summary>
    /// <param name="variableName">
    /// Variable name to resolve. Supports:
    /// - Simple names: "Name"
    /// - Dot notation: "Customer.Name"
    /// - Array indexing: "Items[0]"
    /// - Dictionary keys: "Settings[Theme]"
    /// - Loop metadata: "@index", "@first", "@last", "@count"
    /// </param>
    /// <param name="value">The resolved value if found; otherwise, null.</param>
    /// <returns>True if the variable was found; otherwise, false.</returns>
    bool TryResolveVariable(string variableName, out object? value);

    /// <summary>
    /// Gets the parent context (for nested contexts), or null for root context.
    /// </summary>
    /// <remarks>
    /// Hierarchical contexts use this for fallback resolution:
    /// 1. Try to resolve in current context
    /// 2. If not found and Parent is not null, try Parent.TryResolveVariable()
    /// 3. Continue up the chain until found or root reached
    /// </remarks>
    IEvaluationContext? Parent { get; }

    /// <summary>
    /// Gets the root data dictionary.
    /// </summary>
    /// <remarks>
    /// Useful for accessing global metadata or variables from nested contexts.
    /// For root contexts, this returns the actual data dictionary.
    /// For child contexts, this returns the parent's RootData.
    /// </remarks>
    IReadOnlyDictionary<string, object> RootData { get; }
}
