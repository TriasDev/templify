// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using TriasDev.Templify.Placeholders;

namespace TriasDev.Templify.Core;

/// <summary>
/// Global evaluation context that resolves variables from the root data dictionary.
/// This is the root-level context used for template processing.
/// </summary>
/// <remarks>
/// This context represents the global scope of template evaluation.
/// It has no parent context and directly accesses the root data dictionary.
/// All variable resolution goes through the ValueResolver to support
/// nested property paths, array indexing, and dictionary access.
/// </remarks>
public sealed class GlobalEvaluationContext : IEvaluationContext
{
    private readonly Dictionary<string, object> _data;
    private readonly ValueResolver _valueResolver;

    /// <summary>
    /// Initializes a new instance of the <see cref="GlobalEvaluationContext"/> class.
    /// </summary>
    /// <param name="data">The root data dictionary containing template variables.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="data"/> is null.</exception>
    public GlobalEvaluationContext(Dictionary<string, object> data)
    {
        _data = data ?? throw new ArgumentNullException(nameof(data));
        _valueResolver = new ValueResolver();
    }

    /// <inheritdoc/>
    public bool TryResolveVariable(string variableName, out object? value)
    {
        return _valueResolver.TryResolveValue(_data, variableName, out value);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Always returns null because this is the root context.
    /// </remarks>
    public IEvaluationContext? Parent => null;

    /// <inheritdoc/>
    /// <remarks>
    /// Returns the data dictionary passed to the constructor.
    /// </remarks>
    public IReadOnlyDictionary<string, object> RootData => _data;
}
