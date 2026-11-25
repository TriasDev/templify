// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using TriasDev.Templify.Builder.Model;

namespace TriasDev.Templify.Builder;

/// <summary>
/// Continues a conditional started with <see cref="BlockComposer.If"/>: more conditions with <see cref="ElseIf"/>, a
/// last part with <see cref="Else"/>. Nothing can follow <see cref="Else"/>, so the order Templify requires is the
/// only one that can be written.
/// </summary>
public sealed class IfChain
{
    private readonly BuildContext _context;
    private readonly LoopScope? _scope;
    private readonly ConditionalBlock _block;
    private readonly Func<bool> _isOpen;

    internal IfChain(BuildContext context, LoopScope? scope, ConditionalBlock block, Func<bool> isOpen)
    {
        _context = context;
        _scope = scope;
        _block = block;
        _isOpen = isOpen;
    }

    /// <summary>Adds a further condition, tested when the ones before it do not hold: <c>{{#elseif …}}</c>.</summary>
    /// <param name="condition">The condition.</param>
    /// <param name="then">Describes the blocks shown when the condition holds.</param>
    /// <returns>This chain.</returns>
    public IfChain ElseIf(Condition condition, Action<BlockComposer> then)
    {
        ArgumentNullException.ThrowIfNull(condition);
        ArgumentNullException.ThrowIfNull(then);
        EnsureOpen();
        BlockComposer.CheckCondition(_context, _scope, condition);
        _block.Branches.Add(new ConditionalBranch(condition, BlockComposer.Compose(_context, _scope, then)));
        return this;
    }

    /// <summary>Adds the part shown when no condition holds: <c>{{#else}}</c>. It ends the chain.</summary>
    /// <param name="otherwise">Describes the blocks.</param>
    public void Else(Action<BlockComposer> otherwise)
    {
        ArgumentNullException.ThrowIfNull(otherwise);
        EnsureOpen();
        _block.Else = BlockComposer.Compose(_context, _scope, otherwise);
    }

    private void EnsureOpen()
    {
        if (_block.Else != null)
        {
            throw new InvalidOperationException("The conditional already has an else part; nothing can follow it.");
        }

        if (!_isOpen())
        {
            throw new InvalidOperationException("A conditional was continued outside the lambda it was started in.");
        }
    }
}

/// <summary>
/// Describes the cases of <see cref="BlockComposer.Switch"/>: one per value, and a mandatory <see cref="Default"/>.
/// Valid only inside the lambda it is passed to.
/// </summary>
public sealed class SwitchComposer
{
    private readonly BuildContext _context;
    private readonly LoopScope? _scope;
    private readonly List<(IReadOnlyList<string> Values, List<Block> Body)> _cases = new List<(IReadOnlyList<string>, List<Block>)>();
    private List<Block>? _default;
    private bool _isOpen = true;

    internal SwitchComposer(BuildContext context, LoopScope? scope)
    {
        _context = context;
        _scope = scope;
    }

    /// <summary>Adds a case for one value: <c>value = "x"</c>.</summary>
    /// <param name="value">The value.</param>
    /// <param name="then">Describes the blocks for this value.</param>
    public void Case(string value, Action<BlockComposer> then)
    {
        ArgumentNullException.ThrowIfNull(value);
        Case(new[] { value }, then);
    }

    /// <summary>Adds a case for several values: <c>value in ("x", "y")</c>.</summary>
    /// <param name="values">The values; at least one.</param>
    /// <param name="then">Describes the blocks for these values.</param>
    public void Case(IEnumerable<string> values, Action<BlockComposer> then)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(then);
        EnsureOpen();
        string[] list = values.ToArray();
        if (list.Length == 0)
        {
            throw new ArgumentException("A case needs at least one value.", nameof(values));
        }

        foreach (string value in list)
        {
            ConditionText.String(value, nameof(values));
        }

        _cases.Add((list, BlockComposer.Compose(_context, _scope, then)));
    }

    /// <summary>
    /// Adds the part for every other value, including values added to the data later. Mandatory: a switch that maps
    /// some values and prints nothing for the rest is a silent gap.
    /// </summary>
    /// <param name="otherwise">Describes the blocks.</param>
    public void Default(Action<BlockComposer> otherwise)
    {
        ArgumentNullException.ThrowIfNull(otherwise);
        EnsureOpen();
        if (_default != null)
        {
            throw new InvalidOperationException("The switch already has a default.");
        }

        _default = BlockComposer.Compose(_context, _scope, otherwise);
    }

    /// <summary>Ends composing and returns the blocks: a conditional chain, or the default alone when there are no cases.</summary>
    internal IEnumerable<Block> Close(TemplatePath value)
    {
        _isOpen = false;

        if (_default is null)
        {
            _context.Error(
                DiagnosticCodes.SwitchWithoutDefault,
                $"The switch on '{value}' has no Default. Add one, so values without a case do not print nothing.");
        }

        foreach (IGrouping<string, string> duplicate in _cases.SelectMany(c => c.Values).GroupBy(v => v, StringComparer.Ordinal).Where(g => g.Count() > 1))
        {
            _context.Error(
                DiagnosticCodes.DuplicateSwitchCase,
                $"The switch on '{value}' has more than one case for \"{duplicate.Key}\"; only the first would ever apply.");
        }

        if (_cases.Count == 0)
        {
            return _default ?? new List<Block>();
        }

        ConditionalBlock block = new ConditionalBlock { Else = _default };
        foreach ((IReadOnlyList<string> values, List<Block> body) in _cases)
        {
            block.Branches.Add(new ConditionalBranch(values.Count == 1 ? value.Eq(values[0]) : value.In(values.ToArray()), body));
        }

        return new Block[] { block };
    }

    private void EnsureOpen()
    {
        if (!_isOpen)
        {
            throw new InvalidOperationException("A switch composer was used outside the lambda it was passed to.");
        }
    }
}
