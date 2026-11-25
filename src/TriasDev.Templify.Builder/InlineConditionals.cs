// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using TriasDev.Templify.Builder.Model;

namespace TriasDev.Templify.Builder;

/// <summary>
/// Describes the text of one branch of an inline conditional: literal text and values (and nested inline
/// conditionals). Images, tabs and breaks are not available here. Valid only inside the lambda it is passed to.
/// </summary>
public sealed class TextComposer
{
    private readonly BuildContext _context;
    private readonly LoopScope? _scope;
    private readonly List<Inline> _inlines = new List<Inline>();
    private bool _isOpen = true;

    internal TextComposer(BuildContext context, LoopScope? scope)
    {
        _context = context;
        _scope = scope;
    }

    /// <summary>Adds literal text.</summary>
    /// <param name="text">The text.</param>
    /// <returns>This composer.</returns>
    public TextComposer Text(string text) => AddText(text, null);

    /// <summary>Adds literal text in a character style.</summary>
    /// <param name="text">The text.</param>
    /// <param name="style">The character style; the style sheet must define it.</param>
    /// <returns>This composer.</returns>
    public TextComposer Text(string text, CharacterStyleKey style)
    {
        ArgumentNullException.ThrowIfNull(style);
        return AddText(text, style);
    }

    /// <summary>Adds a value from the data.</summary>
    /// <param name="path">The data path.</param>
    /// <returns>This composer.</returns>
    public TextComposer Value(TemplatePath path) => AddValue(path, null, null);

    /// <summary>Adds a formatted value from the data.</summary>
    /// <param name="path">The data path.</param>
    /// <param name="format">How Templify formats the value.</param>
    /// <returns>This composer.</returns>
    public TextComposer Value(TemplatePath path, ValueFormat format)
    {
        ArgumentNullException.ThrowIfNull(format);
        return AddValue(path, format, null);
    }

    /// <summary>Adds a value from the data in a character style.</summary>
    /// <param name="path">The data path.</param>
    /// <param name="style">The character style.</param>
    /// <returns>This composer.</returns>
    public TextComposer Value(TemplatePath path, CharacterStyleKey style)
    {
        ArgumentNullException.ThrowIfNull(style);
        return AddValue(path, null, style);
    }

    /// <summary>Adds a nested inline conditional.</summary>
    /// <param name="condition">The condition.</param>
    /// <param name="then">Describes the text shown when it holds.</param>
    /// <returns>The chain, to add elseif and else parts.</returns>
    public InlineIfChain If(Condition condition, Action<TextComposer> then)
    {
        EnsureOpen();
        return InlineIfChain.Start(_context, _scope, _inlines, condition, then, () => _isOpen);
    }

    internal IReadOnlyList<Inline> Close()
    {
        _isOpen = false;
        InlineComposer.CheckLiteralText(_context, _inlines);
        return _inlines;
    }

    private TextComposer AddText(string text, CharacterStyleKey? style)
    {
        ArgumentNullException.ThrowIfNull(text);
        EnsureOpen();
        if (text.Any(c => c is '\t' or '\n' or '\r'))
        {
            throw new ArgumentException("Inline conditional text cannot hold tabs or line breaks; use a block conditional.", nameof(text));
        }

        if (text.Length > 0)
        {
            _inlines.Add(new TextInline(text, style));
        }

        return this;
    }

    private TextComposer AddValue(TemplatePath path, ValueFormat? format, CharacterStyleKey? style)
    {
        EnsureOpen();
        _inlines.Add(InlineComposer.CreateValue(_context, _scope, path, format, style));
        return this;
    }

    private void EnsureOpen()
    {
        if (!_isOpen)
        {
            throw new InvalidOperationException("A text composer was used outside the lambda it was passed to.");
        }
    }
}

/// <summary>Continues an inline conditional: <see cref="ElseIf"/> and a last <see cref="Else"/>.</summary>
public sealed class InlineIfChain
{
    private readonly BuildContext _context;
    private readonly LoopScope? _scope;
    private readonly InlineConditional _conditional;
    private readonly Func<bool> _isOpen;

    private InlineIfChain(BuildContext context, LoopScope? scope, InlineConditional conditional, Func<bool> isOpen)
    {
        _context = context;
        _scope = scope;
        _conditional = conditional;
        _isOpen = isOpen;
    }

    /// <summary>Adds text for a further condition: <c>{{#elseif …}}</c>.</summary>
    /// <param name="condition">The condition.</param>
    /// <param name="then">Describes the text.</param>
    /// <returns>This chain.</returns>
    public InlineIfChain ElseIf(Condition condition, Action<TextComposer> then)
    {
        ArgumentNullException.ThrowIfNull(condition);
        ArgumentNullException.ThrowIfNull(then);
        EnsureOpen();
        BlockComposer.CheckCondition(_context, _scope, condition);
        _conditional.Branches.Add((condition, Compose(_context, _scope, then)));
        return this;
    }

    /// <summary>Adds the text shown when no condition holds: <c>{{#else}}</c>. It ends the chain.</summary>
    /// <param name="otherwise">Describes the text.</param>
    public void Else(Action<TextComposer> otherwise)
    {
        ArgumentNullException.ThrowIfNull(otherwise);
        EnsureOpen();
        _conditional.Else = Compose(_context, _scope, otherwise);
    }

    internal static InlineIfChain Start(BuildContext context, LoopScope? scope, List<Inline> target, Condition condition, Action<TextComposer> then, Func<bool> isOpen)
    {
        ArgumentNullException.ThrowIfNull(condition);
        ArgumentNullException.ThrowIfNull(then);
        BlockComposer.CheckCondition(context, scope, condition);
        InlineConditional conditional = new InlineConditional();
        conditional.Branches.Add((condition, Compose(context, scope, then)));
        target.Add(conditional);
        return new InlineIfChain(context, scope, conditional, isOpen);
    }

    private static IReadOnlyList<Inline> Compose(BuildContext context, LoopScope? scope, Action<TextComposer> text)
    {
        TextComposer composer = new TextComposer(context, scope);
        text(composer);
        return composer.Close();
    }

    private void EnsureOpen()
    {
        if (_conditional.Else != null)
        {
            throw new InvalidOperationException("The inline conditional already has an else part; nothing can follow it.");
        }

        if (!_isOpen())
        {
            throw new InvalidOperationException("An inline conditional was continued outside the lambda it was started in.");
        }
    }
}
