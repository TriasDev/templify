// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using TriasDev.Templify.Builder.Model;

namespace TriasDev.Templify.Builder;

/// <summary>
/// Describes block-level content: the document body and, in later versions, cells, boxes, headers and the bodies
/// of conditionals and loops. Valid only inside the lambda it is passed to.
/// </summary>
public sealed class BlockComposer
{
    private readonly BuildContext _context;
    private readonly List<Block> _blocks;
    private readonly LoopScope? _scope;
    private bool _isOpen = true;

    internal BlockComposer(BuildContext context, List<Block> blocks, LoopScope? scope)
    {
        _context = context;
        _blocks = blocks;
        _scope = scope;
    }

    /// <summary>Adds a paragraph with literal text, in the default paragraph style.</summary>
    /// <param name="text">The text. Tabs and line breaks become the format's tab and line break.</param>
    public void Paragraph(string text) => AddText(null, text);

    /// <summary>Adds a paragraph with literal text.</summary>
    /// <param name="style">The paragraph style; the style sheet must define it.</param>
    /// <param name="text">The text. Tabs and line breaks become the format's tab and line break.</param>
    public void Paragraph(ParagraphStyleKey style, string text)
    {
        ArgumentNullException.ThrowIfNull(style);
        AddText(style, text);
    }

    /// <summary>Adds a paragraph that holds one value from the data, in the default paragraph style: <c>{{path}}</c>.</summary>
    /// <param name="value">The data path of the value.</param>
    public void Paragraph(TemplatePath value) => AddValue(null, value);

    /// <summary>Adds a paragraph that holds one value from the data: <c>{{path}}</c>.</summary>
    /// <param name="style">The paragraph style; the style sheet must define it.</param>
    /// <param name="value">The data path of the value.</param>
    public void Paragraph(ParagraphStyleKey style, TemplatePath value)
    {
        ArgumentNullException.ThrowIfNull(style);
        AddValue(style, value);
    }

    /// <summary>Adds a paragraph with mixed content, in the default paragraph style.</summary>
    /// <param name="content">Describes the paragraph's content.</param>
    public void Paragraph(Action<InlineComposer> content) => AddComposed(null, content);

    /// <summary>Adds a paragraph with mixed content: literal text and values.</summary>
    /// <param name="style">The paragraph style; the style sheet must define it.</param>
    /// <param name="content">Describes the paragraph's content.</param>
    public void Paragraph(ParagraphStyleKey style, Action<InlineComposer> content)
    {
        ArgumentNullException.ThrowIfNull(style);
        AddComposed(style, content);
    }

    /// <summary>Adds a page break: the content after it starts on a new page.</summary>
    public void PageBreak()
    {
        EnsureOpen();
        _blocks.Add(new PageBreakBlock());
    }

    /// <summary>
    /// Adds blocks shown only when a condition holds: <c>{{#if …}}</c> … <c>{{/if}}</c>. Continue with
    /// <see cref="IfChain.ElseIf"/> and <see cref="IfChain.Else"/>.
    /// </summary>
    /// <param name="condition">The condition.</param>
    /// <param name="then">Describes the blocks shown when it holds.</param>
    /// <returns>The chain, to add elseif and else parts.</returns>
    public IfChain If(Condition condition, Action<BlockComposer> then)
    {
        ArgumentNullException.ThrowIfNull(condition);
        ArgumentNullException.ThrowIfNull(then);
        EnsureOpen();
        CheckCondition(_context, _scope, condition);
        ConditionalBlock block = new ConditionalBlock();
        block.Branches.Add(new ConditionalBranch(condition, Compose(_context, _scope, then)));
        _blocks.Add(block);
        return new IfChain(_context, _scope, block, () => _isOpen);
    }

    /// <summary>
    /// Chooses blocks by a value: one case per value and a mandatory default, written as an if / elseif / else chain.
    /// Use it where the design depends on data, for example a box coloured by a status.
    /// </summary>
    /// <param name="value">The data path of the value.</param>
    /// <param name="cases">Describes the cases and the default.</param>
    public void Switch(TemplatePath value, Action<SwitchComposer> cases)
    {
        ArgumentNullException.ThrowIfNull(cases);
        EnsureOpen();
        _ = (Condition)value; // checks the path where it is written: not the root, not a number
        LoopScope.CheckUse(_context, _scope, value);
        SwitchComposer composer = new SwitchComposer(_context, _scope);
        cases(composer);
        _blocks.AddRange(composer.Close(value));
    }

    /// <summary>
    /// Repeats blocks for every item of a collection: <c>{{#foreach item in collection}}</c> … <c>{{/foreach}}</c>.
    /// The iteration variable is always named, so the template stays readable and an inner loop can still reach the
    /// outer item.
    /// </summary>
    /// <param name="collection">The data path of the collection; a path from the root or from an enclosing loop's item.</param>
    /// <param name="itemName">
    /// The iteration variable: letters, digits and <c>_</c>; not a Templify keyword and not the name of an enclosing
    /// loop's variable.
    /// </param>
    /// <param name="body">Describes the blocks repeated for each item; it receives the item.</param>
    public void ForEach(TemplatePath collection, string itemName, Action<LoopItem, BlockComposer> body)
    {
        ArgumentNullException.ThrowIfNull(body);
        EnsureOpen();
        if (collection.IsRoot || collection.IsMetadata)
        {
            throw new ArgumentException("A loop needs a collection path from the data or from a loop item.", nameof(collection));
        }

        LoopScope.CheckUse(_context, _scope, collection);
        LoopScope scope = new LoopScope(LoopScope.ValidateItemName(itemName, _scope), collection, _scope);
        List<Block> blocks = new List<Block>();
        BlockComposer composer = new BlockComposer(_context, blocks, scope);
        body(new LoopItem(scope), composer);
        composer.Close();
        _blocks.Add(new LoopBlock(collection, itemName, blocks));
    }

    /// <summary>Adds a reusable component's content.</summary>
    /// <param name="component">The component.</param>
    public void Component(ITemplateComponent component)
    {
        ArgumentNullException.ThrowIfNull(component);
        EnsureOpen();
        _blocks.Add(new ComponentBlock(component, Compose(_context, _scope, component.Compose)));
    }

    /// <summary>Reports paths of a condition that cannot be used here.</summary>
    internal static void CheckCondition(BuildContext context, LoopScope? scope, Condition condition)
    {
        foreach (TemplatePath path in condition.Paths)
        {
            LoopScope.CheckUse(context, scope, path);
        }
    }

    /// <summary>Composes the blocks of a nested part (a branch, a cell, a loop body) in the given loop scope.</summary>
    internal static List<Block> Compose(BuildContext context, LoopScope? scope, Action<BlockComposer> content)
    {
        List<Block> blocks = new List<Block>();
        BlockComposer composer = new BlockComposer(context, blocks, scope);
        content(composer);
        composer.Close();
        return blocks;
    }

    internal void Close() => _isOpen = false;

    private void AddText(ParagraphStyleKey? style, string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        EnsureOpen();
        Inline[] inlines = text.Length == 0 ? Array.Empty<Inline>() : new Inline[] { new TextInline(text) };
        InlineComposer.CheckLiteralText(_context, inlines);
        _blocks.Add(new ParagraphBlock(inlines, style));
    }

    private void AddValue(ParagraphStyleKey? style, TemplatePath value)
    {
        EnsureOpen();
        _blocks.Add(new ParagraphBlock(new Inline[] { InlineComposer.CreateValue(_context, _scope, value) }, style));
    }

    private void AddComposed(ParagraphStyleKey? style, Action<InlineComposer> content)
    {
        ArgumentNullException.ThrowIfNull(content);
        EnsureOpen();
        InlineComposer composer = new InlineComposer(_context, _scope);
        content(composer);
        _blocks.Add(new ParagraphBlock(composer.Close(), style));
    }

    private void EnsureOpen()
    {
        if (!_isOpen)
        {
            throw new InvalidOperationException(
                "A block composer was used outside the lambda it was passed to.");
        }
    }
}
