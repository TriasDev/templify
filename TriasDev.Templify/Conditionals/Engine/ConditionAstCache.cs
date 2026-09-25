// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;

namespace TriasDev.Templify.Conditionals.Engine;

/// <summary>
/// A thread-safe, bounded cache of parsed condition expressions (expression string → AST).
/// </summary>
/// <remarks>
/// <para>
/// Loops evaluate the same <c>{{#if}}</c> / <c>{{(…)}}</c> expressions once per item, and templates are
/// typically processed many times; parsing is deterministic and the AST is immutable, so a parsed
/// expression can be shared by all evaluations, threads and documents.
/// </para>
/// <para>
/// The cache is process-wide (rather than per <c>ProcessTemplate</c> call) so that repeated processing of
/// the same template and the standalone <see cref="ConditionEvaluator"/> benefit too. It is bounded:
/// when <see cref="Capacity"/> distinct expressions are cached, it is cleared and refilled, which keeps
/// memory bounded in long-running processes that see many different templates without the bookkeeping
/// of an LRU. Expressions that fail to parse are not cached; the exception propagates as without cache.
/// </para>
/// </remarks>
internal sealed class ConditionAstCache
{
    /// <summary>The default maximum number of cached expressions per cache.</summary>
    internal const int DefaultCapacity = 1024;

    private readonly ConcurrentDictionary<string, ConditionNode> _entries = new(StringComparer.Ordinal);
    private readonly Func<string, ConditionNode> _parse;

    internal ConditionAstCache(Func<string, ConditionNode> parse, int capacity = DefaultCapacity)
    {
        ArgumentNullException.ThrowIfNull(parse);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        _parse = parse;
        Capacity = capacity;
    }

    /// <summary>
    /// Gets the cache for <c>{{#if}}</c>/<c>{{#elseif}}</c> and standalone conditions (double-quoted strings).
    /// </summary>
    public static ConditionAstCache Conditions { get; } = new(expression =>
        new ConditionParser(ConditionOperatorRegistry.Shared).Parse(new ConditionLexer().Tokenize(expression)));

    /// <summary>
    /// Gets the cache for inline expression placeholders <c>{{(…)}}</c> (single- and double-quoted strings).
    /// </summary>
    public static ConditionAstCache InlineExpressions { get; } = new(expression =>
        new ConditionParser(ConditionOperatorRegistry.Shared).Parse(new ConditionLexer(allowSingleQuotedStrings: true).Tokenize(expression)));

    /// <summary>Gets the maximum number of cached expressions.</summary>
    public int Capacity { get; }

    /// <summary>Gets the number of cached expressions.</summary>
    internal int Count => _entries.Count;

    /// <summary>
    /// Returns the AST of <paramref name="expression"/>, parsing it on first use.
    /// </summary>
    /// <exception cref="ConditionParseException">The expression is malformed.</exception>
    public ConditionNode GetOrParse(string expression)
    {
        ArgumentNullException.ThrowIfNull(expression);

        if (_entries.TryGetValue(expression, out ConditionNode? node))
        {
            return node;
        }

        node = _parse(expression);

        if (_entries.Count >= Capacity)
        {
            _entries.Clear();
        }

        // A concurrent caller may have added the same expression; either AST is equivalent.
        _entries.TryAdd(expression, node);
        return node;
    }
}
