// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using TriasDev.Templify.Builder.Model;

namespace TriasDev.Templify.Builder;

/// <summary>Names a bullet list style: a bullet, an indent and a hanging indent per level.</summary>
public sealed record ListStyleKey
{
    /// <summary>Initializes a list style key.</summary>
    /// <param name="id">Letters, digits, <c>_</c> and <c>-</c>, starting with a letter or <c>_</c>.</param>
    public ListStyleKey(string id)
    {
        Id = StyleKeyRules.ValidateId(id, nameof(id));
    }

    /// <summary>Gets the id.</summary>
    public string Id { get; }
}

/// <summary>Sets a list style's levels and the paragraph style of its items.</summary>
public sealed class ListStyleComposer
{
    private readonly StyleSheetComposer _owner;
    private readonly ListStyleDefinition _style;

    internal ListStyleComposer(StyleSheetComposer owner, ListStyleDefinition style)
    {
        _owner = owner;
        _style = style;
    }

    /// <summary>Defines a level.</summary>
    /// <param name="level">The level: 0 is the top level, at most 8.</param>
    /// <param name="bullet">The bullet, for example <c>•</c> or <c>–</c>.</param>
    /// <param name="indent">Where the item text starts, from the container's start.</param>
    /// <param name="hanging">How far left of the text the bullet sits.</param>
    /// <returns>This composer.</returns>
    public ListStyleComposer Level(int level, string bullet, Length indent, Length hanging)
    {
        _owner.EnsureOpen();
        if (level is < 0 or > 8)
        {
            throw new ArgumentOutOfRangeException(nameof(level), level, "A list level is between 0 and 8.");
        }

        ArgumentNullException.ThrowIfNull(bullet);
        if (bullet.Length is 0 or > 4 || bullet.Any(char.IsControl))
        {
            throw new ArgumentException("A bullet is one to four printable characters.", nameof(bullet));
        }

        if (hanging > indent)
        {
            throw new ArgumentOutOfRangeException(nameof(hanging), hanging, "The bullet cannot sit left of the container: the hanging indent is at most the indent.");
        }

        _style.Levels[level] = new ListLevel(bullet, StyleRanges.NotNegative(indent, nameof(indent)), StyleRanges.NotNegative(hanging, nameof(hanging)));
        return this;
    }

    /// <summary>Sets the paragraph style of the list's items.</summary>
    /// <param name="style">The paragraph style; the style sheet must define it.</param>
    /// <returns>This composer.</returns>
    public ListStyleComposer ItemStyle(ParagraphStyleKey style)
    {
        _owner.EnsureOpen();
        _style.ItemStyle = style ?? throw new ArgumentNullException(nameof(style));
        return this;
    }
}

/// <summary>
/// Describes the items of a bullet list: items at a level, loops and conditionals over items. Valid only inside the
/// lambda it is passed to.
/// </summary>
public sealed class ListComposer
{
    private readonly BuildContext _context;
    private readonly LoopScope? _scope;
    private readonly ListStyleKey _style;
    private readonly List<Block> _blocks;
    private readonly BlockComposer _blocksComposer;
    private bool _isOpen = true;

    internal ListComposer(BuildContext context, LoopScope? scope, ListStyleKey style, List<Block> blocks)
    {
        _context = context;
        _scope = scope;
        _style = style;
        _blocks = blocks;

        // Loops and conditionals over items are block loops and conditionals; this composer writes them and lives as
        // long as the list composer, so an if chain can be continued while the list is open.
        _blocksComposer = new BlockComposer(context, blocks, scope);
    }

    /// <summary>Adds a top-level item with literal text.</summary>
    /// <param name="text">The text.</param>
    public void Item(string text) => Item(0, text);

    /// <summary>Adds an item with literal text.</summary>
    /// <param name="level">The level: 0 is the top level.</param>
    /// <param name="text">The text.</param>
    public void Item(int level, string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        Item(level, x => x.Text(text));
    }

    /// <summary>Adds a top-level item holding one value.</summary>
    /// <param name="value">The data path.</param>
    public void Item(TemplatePath value) => Item(0, x => x.Value(value));

    /// <summary>Adds an item holding one value.</summary>
    /// <param name="level">The level: 0 is the top level.</param>
    /// <param name="value">The data path.</param>
    public void Item(int level, TemplatePath value) => Item(level, x => x.Value(value));

    /// <summary>Adds a top-level item with mixed content.</summary>
    /// <param name="content">Describes the item's text.</param>
    public void Item(Action<InlineComposer> content) => Item(0, content);

    /// <summary>Adds an item with mixed content.</summary>
    /// <param name="level">The level: 0 is the top level, at most 8.</param>
    /// <param name="content">Describes the item's text.</param>
    public void Item(int level, Action<InlineComposer> content)
    {
        ArgumentNullException.ThrowIfNull(content);
        EnsureOpen();
        if (level is < 0 or > 8)
        {
            throw new ArgumentOutOfRangeException(nameof(level), level, "A list level is between 0 and 8.");
        }

        InlineComposer composer = new InlineComposer(_context, _scope);
        content(composer);
        _blocks.Add(new ParagraphBlock(composer.Close(), null, new ListItemInfo(_style, level)));
    }

    /// <summary>Repeats items for every item of a typed collection.</summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="collection">The collection, from <see cref="TemplatePath{T}.Items{TItem}"/>.</param>
    /// <param name="itemName">The iteration variable.</param>
    /// <param name="items">Describes the items for each element; it receives the typed element.</param>
    public void ForEach<T>(TemplateCollection<T> collection, string itemName, Action<LoopItem<T>, ListComposer> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        ForEach(collection.Untyped, itemName, (item, l) => items(new LoopItem<T>(item, collection.Naming), l));
    }

    /// <summary>Repeats items for every item of a collection.</summary>
    /// <param name="collection">The data path of the collection.</param>
    /// <param name="itemName">The iteration variable.</param>
    /// <param name="items">Describes the items for each element; it receives the element.</param>
    public void ForEach(TemplatePath collection, string itemName, Action<LoopItem, ListComposer> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        EnsureOpen();
        _blocksComposer.ForEach(collection, itemName, (item, body) =>
        {
            ListComposer inner = new ListComposer(_context, body.Scope, _style, body.Blocks);
            items(item, inner);
            inner.Close();
        });
    }

    /// <summary>Adds items shown only when a condition holds.</summary>
    /// <param name="condition">The condition.</param>
    /// <param name="items">Describes the items.</param>
    /// <returns>The chain, to add elseif and else items.</returns>
    public ListIfChain If(Condition condition, Action<ListComposer> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        EnsureOpen();
        IfChain chain = _blocksComposer.If(condition, body => Compose(body, items));
        return new ListIfChain(chain, this);
    }

    internal void Close()
    {
        _isOpen = false;
        _blocksComposer.Close();
    }

    internal void Compose(BlockComposer body, Action<ListComposer> items)
    {
        ListComposer inner = new ListComposer(_context, body.Scope, _style, body.Blocks);
        items(inner);
        inner.Close();
    }

    private void EnsureOpen()
    {
        if (!_isOpen)
        {
            throw new InvalidOperationException("A list composer was used outside the lambda it was passed to.");
        }
    }
}

/// <summary>Continues conditional list items: <see cref="ElseIf"/> and a last <see cref="Else"/>.</summary>
public sealed class ListIfChain
{
    private readonly IfChain _chain;
    private readonly ListComposer _list;

    internal ListIfChain(IfChain chain, ListComposer list)
    {
        _chain = chain;
        _list = list;
    }

    /// <summary>Adds items for a further condition.</summary>
    /// <param name="condition">The condition.</param>
    /// <param name="items">Describes the items.</param>
    /// <returns>This chain.</returns>
    public ListIfChain ElseIf(Condition condition, Action<ListComposer> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        _chain.ElseIf(condition, body => _list.Compose(body, items));
        return this;
    }

    /// <summary>Adds the items shown when no condition holds. It ends the chain.</summary>
    /// <param name="items">Describes the items.</param>
    public void Else(Action<ListComposer> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        _chain.Else(body => _list.Compose(body, items));
    }
}
