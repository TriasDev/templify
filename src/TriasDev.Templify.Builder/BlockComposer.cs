// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using TriasDev.Templify.Builder.Model;

namespace TriasDev.Templify.Builder;

/// <summary>
/// Describes block-level content: the document body and, in later versions, cells, boxes, headers and the bodies
/// of conditionals and loops. Valid only inside the lambda it is passed to.
/// </summary>
public class BlockComposer
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

    /// <summary>Gets the loop scope this composer writes in.</summary>
    internal LoopScope? Scope => _scope;

    /// <summary>Gets the blocks this composer writes to.</summary>
    internal List<Block> Blocks => _blocks;

    /// <summary>Adds a paragraph with literal text, in the default paragraph style.</summary>
    /// <param name="text">The text. Tabs and line breaks become the format's tab and line break.</param>
    /// <returns>This composer.</returns>
    public BlockComposer Paragraph(string text) => AddText(null, text);

    /// <summary>Adds a paragraph with literal text.</summary>
    /// <param name="style">The paragraph style; the style sheet must define it.</param>
    /// <param name="text">The text. Tabs and line breaks become the format's tab and line break.</param>
    /// <returns>This composer.</returns>
    public BlockComposer Paragraph(ParagraphStyleKey style, string text)
    {
        ArgumentNullException.ThrowIfNull(style);
        AddText(style, text);
        return this;
    }

    /// <summary>Adds a paragraph that holds one value from the data, in the default paragraph style: <c>{{path}}</c>.</summary>
    /// <param name="value">The data path of the value.</param>
    /// <returns>This composer.</returns>
    public BlockComposer Paragraph(TemplatePath value) => AddValue(null, value);

    /// <summary>Adds a paragraph that holds one value from the data: <c>{{path}}</c>.</summary>
    /// <param name="style">The paragraph style; the style sheet must define it.</param>
    /// <param name="value">The data path of the value.</param>
    /// <returns>This composer.</returns>
    public BlockComposer Paragraph(ParagraphStyleKey style, TemplatePath value)
    {
        ArgumentNullException.ThrowIfNull(style);
        AddValue(style, value);
        return this;
    }

    /// <summary>Adds a paragraph with mixed content, in the default paragraph style.</summary>
    /// <param name="content">Describes the paragraph's content.</param>
    /// <returns>This composer.</returns>
    public BlockComposer Paragraph(Action<InlineComposer> content) => AddComposed(null, content);

    /// <summary>Adds a paragraph with mixed content: literal text and values.</summary>
    /// <param name="style">The paragraph style; the style sheet must define it.</param>
    /// <param name="content">Describes the paragraph's content.</param>
    /// <returns>This composer.</returns>
    public BlockComposer Paragraph(ParagraphStyleKey style, Action<InlineComposer> content)
    {
        ArgumentNullException.ThrowIfNull(style);
        AddComposed(style, content);
        return this;
    }

    /// <summary>Adds a paragraph holding one image at its natural size.</summary>
    /// <param name="image">The image.</param>
    /// <param name="altText">The text that describes the image; required.</param>
    /// <returns>This composer.</returns>
    public BlockComposer Image(ImageSource image, string altText) => AddImage(null, image, altText, ImageFit.Natural);

    /// <summary>Adds a paragraph holding one image.</summary>
    /// <param name="image">The image.</param>
    /// <param name="altText">The text that describes the image; required.</param>
    /// <param name="fit">How the image is sized; the aspect ratio is kept.</param>
    /// <returns>This composer.</returns>
    public BlockComposer Image(ImageSource image, string altText, ImageFit fit) => AddImage(null, image, altText, fit);

    /// <summary>Adds a paragraph in a style holding one image (the style aligns and spaces it).</summary>
    /// <param name="style">The paragraph style.</param>
    /// <param name="image">The image.</param>
    /// <param name="altText">The text that describes the image; required.</param>
    /// <param name="fit">How the image is sized; the aspect ratio is kept.</param>
    /// <returns>This composer.</returns>
    public BlockComposer Image(ParagraphStyleKey style, ImageSource image, string altText, ImageFit fit)
    {
        ArgumentNullException.ThrowIfNull(style);
        AddImage(style, image, altText, fit);
        return this;
    }

    private BlockComposer AddImage(ParagraphStyleKey? style, ImageSource image, string altText, ImageFit fit)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(fit);
        EnsureOpen();
        _blocks.Add(new ParagraphBlock(new Inline[] { InlineComposer.CreateImage(image, altText, fit) }, style));
        return this;
    }

    /// <summary>Adds a page break: the content after it starts on a new page.</summary>
    /// <returns>This composer.</returns>
    public BlockComposer PageBreak()
    {
        EnsureOpen();
        _blocks.Add(new PageBreakBlock());
        return this;
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
    /// <returns>This composer.</returns>
    public BlockComposer Switch(TemplatePath value, Action<SwitchComposer> cases)
    {
        ArgumentNullException.ThrowIfNull(cases);
        EnsureOpen();
        _ = (Condition)value; // checks the path where it is written: not the root, not a number
        LoopScope.CheckUse(_context, _scope, value);
        SwitchComposer composer = new SwitchComposer(_context, _scope);
        cases(composer);
        _blocks.AddRange(composer.Close(value));
        return this;
    }

    /// <summary>Repeats blocks for every item of a typed collection.</summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="collection">The collection, from <see cref="TemplatePath{T}.Items{TItem}"/>.</param>
    /// <param name="itemName">The iteration variable.</param>
    /// <param name="body">Describes the blocks for each item; it receives the typed item.</param>
    /// <returns>This composer.</returns>
    public BlockComposer ForEach<T>(TemplateCollection<T> collection, string itemName, Action<LoopItem<T>, BlockComposer> body)
    {
        ArgumentNullException.ThrowIfNull(body);
        ForEach(collection.Untyped, itemName, (item, b) => body(new LoopItem<T>(item, collection.Naming), b));
        return this;
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
    /// <returns>This composer.</returns>
    public BlockComposer ForEach(TemplatePath collection, string itemName, Action<LoopItem, BlockComposer> body)
    {
        ArgumentNullException.ThrowIfNull(body);
        EnsureOpen();
        if (collection.IsRoot || collection.IsMetadata)
        {
            throw new ArgumentException("A loop needs a collection path from the data or from a loop item.", nameof(collection));
        }

        LoopScope.CheckUse(_context, _scope, collection);
        LoopScope scope = new LoopScope(LoopScope.ValidateItemName(itemName, _scope), LoopScope.ValidateCollection(collection, itemName), _scope);
        List<Block> blocks = new List<Block>();
        BlockComposer composer = new BlockComposer(_context, blocks, scope);
        body(new LoopItem(scope), composer);
        composer.Close();
        _blocks.Add(new LoopBlock(collection, itemName, blocks));
        return this;
    }

    /// <summary>Adds a table.</summary>
    /// <param name="table">Describes the columns, header rows and rows.</param>
    /// <returns>This composer.</returns>
    public BlockComposer Table(Action<TableComposer> table)
    {
        ArgumentNullException.ThrowIfNull(table);
        EnsureOpen();
        TableBlock block = new TableBlock();
        TableComposer composer = new TableComposer(_context, _scope, block);
        table(composer);
        composer.Close();
        _blocks.Add(block);
        return this;
    }

    /// <summary>
    /// Adds a framed box around block content: a border, a background and padding from a box style. The content can
    /// be anything a cell holds: paragraphs, conditions, loops, tables.
    /// </summary>
    /// <param name="style">The box style; the style sheet must define it.</param>
    /// <param name="content">Describes the box's content.</param>
    /// <returns>This composer.</returns>
    public BlockComposer Box(BoxStyleKey style, Action<BlockComposer> content)
    {
        ArgumentNullException.ThrowIfNull(style);
        ArgumentNullException.ThrowIfNull(content);
        EnsureOpen();
        TableBlock box = new TableBlock { BoxStyle = style };
        box.Columns.Add(new ColumnSpec(null, 1));
        TableRowModel row = new TableRowModel(isHeader: false) { AllowSplit = true };
        TableCellModel cell = new TableCellModel();
        cell.Content.AddRange(Compose(_context, _scope, content));
        row.Cells.Add(cell);
        box.Rows.Add(row);
        _blocks.Add(box);
        return this;
    }

    /// <summary>Adds a horizontal rule across the container.</summary>
    /// <param name="thickness">The line width; more than 0 and at most 6 pt.</param>
    /// <param name="color">The colour.</param>
    /// <returns>This composer.</returns>
    public BlockComposer Rule(Length thickness, Color color) => Rule(thickness, color, Length.Zero, Length.Zero);

    /// <summary>Adds a horizontal rule across the container, with space above and below.</summary>
    /// <param name="thickness">The line width; more than 0 and at most 6 pt.</param>
    /// <param name="color">The colour.</param>
    /// <param name="spaceAbove">The space above the line.</param>
    /// <param name="spaceBelow">The space below the line.</param>
    /// <returns>This composer.</returns>
    public BlockComposer Rule(Length thickness, Color color, Length spaceAbove, Length spaceBelow)
    {
        EnsureOpen();
        if (thickness <= Length.Zero || thickness > 6.Pt())
        {
            throw new ArgumentOutOfRangeException(nameof(thickness), thickness, "A line is more than 0 and at most 6 pt wide.");
        }

        _blocks.Add(new RuleBlock(new BorderLine(thickness, color), StyleRanges.NotNegative(spaceAbove, nameof(spaceAbove)), StyleRanges.NotNegative(spaceBelow, nameof(spaceBelow))));
        return this;
    }

    /// <summary>Adds a bullet list: items at levels, loops and conditionals over items.</summary>
    /// <param name="style">The list style; the style sheet must define it and every level used.</param>
    /// <param name="items">Describes the items.</param>
    /// <returns>This composer.</returns>
    public BlockComposer List(ListStyleKey style, Action<ListComposer> items)
    {
        ArgumentNullException.ThrowIfNull(style);
        ArgumentNullException.ThrowIfNull(items);
        EnsureOpen();
        ListBlock list = new ListBlock(style, new List<Block>());
        ListComposer composer = new ListComposer(_context, _scope, style, list.Items);
        items(composer);
        composer.Close();
        _blocks.Add(list);
        return this;
    }

    /// <summary>Adds a block written as format XML, for a construct the vocabulary does not have; both formats are required.</summary>
    /// <param name="raw">The DOCX and ODT XML and the paths it reads.</param>
    /// <returns>This composer.</returns>
    public BlockComposer Raw(RawBlock raw)
    {
        ArgumentNullException.ThrowIfNull(raw);
        ArgumentNullException.ThrowIfNull(raw.Docx);
        ArgumentNullException.ThrowIfNull(raw.Odt);
        ArgumentNullException.ThrowIfNull(raw.Uses);
        EnsureOpen();
        foreach (TemplatePath path in raw.Uses)
        {
            LoopScope.CheckUse(_context, _scope, path);
        }

        _blocks.Add(new RawXmlBlock(raw));
        return this;
    }

    /// <summary>Adds a table of contents over the headings of outline levels 1 to 3.</summary>
    /// <returns>This composer.</returns>
    public BlockComposer TableOfContents() => TableOfContents(3);

    /// <summary>
    /// Adds a table of contents over the headings (paragraph styles with an outline level) up to a level. It is built
    /// by the application that opens the document: Word does for DOCX on open (fill with
    /// <c>UpdateFieldsOnOpen = Auto</c>); LibreOffice — also when converting to PDF — does not, so it stays empty there
    /// until a user updates it. The build reports this as a warning (<see cref="GeneratedTemplate.Warnings"/>).
    /// </summary>
    /// <param name="maxLevel">The deepest outline level listed, 1 to 9.</param>
    /// <returns>This composer.</returns>
    public BlockComposer TableOfContents(int maxLevel)
    {
        EnsureOpen();
        if (maxLevel is < 1 or > 9)
        {
            throw new ArgumentOutOfRangeException(nameof(maxLevel), maxLevel, "The deepest level is between 1 and 9.");
        }

        _blocks.Add(new TocBlock(maxLevel));
        return this;
    }

    /// <summary>Adds a reusable component's content.</summary>
    /// <param name="component">The component.</param>
    /// <returns>This composer.</returns>
    public BlockComposer Component(ITemplateComponent component)
    {
        ArgumentNullException.ThrowIfNull(component);
        EnsureOpen();
        _blocks.Add(new ComponentBlock(component, Compose(_context, _scope, component.Compose)));
        return this;
    }

    /// <summary>Gets the build's texts, for components: <c>c.Texts["title"]</c>.</summary>
    public TemplateTexts Texts => _context.Texts ?? throw new InvalidOperationException("No texts in this composition.");

    /// <summary>Gets an object the definition shared with <see cref="TemplateComposer.Share{T}"/> (or the JSON import's options).</summary>
    /// <typeparam name="T">The type it was shared as.</typeparam>
    /// <returns>The object.</returns>
    /// <exception cref="InvalidOperationException">Nothing was shared as <typeparamref name="T"/>.</exception>
    public T Shared<T>()
        where T : class =>
        _context.Shared.TryGetValue(typeof(T), out object? value) ? (T)value
        : _context.Shared.Values.OfType<T>().FirstOrDefault()
          ?? throw new InvalidOperationException($"Nothing of type {typeof(T).Name} was shared with the components; call template.Share(…) in the definition.");

    /// <summary>
    /// Adds a named section: a group of blocks with no output of its own. Its name marks the part in the JSON form
    /// (<c>{"section": "items", "blocks": […]}</c>), where hosts and agents read, replace and compare it as a unit.
    /// </summary>
    /// <param name="name">The section's name: letters, digits, <c>_</c>, <c>.</c> or <c>-</c>; unique in a definition.</param>
    /// <param name="content">Describes the blocks.</param>
    /// <returns>This composer.</returns>
    public BlockComposer Section(string name, Action<BlockComposer> content)
    {
        ArgumentNullException.ThrowIfNull(content);
        Component(new SectionComponent(SectionComponent.ValidateName(name), content));
        return this;
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

    private BlockComposer AddText(ParagraphStyleKey? style, string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        EnsureOpen();
        Inline[] inlines = text.Length == 0 ? Array.Empty<Inline>() : new Inline[] { new TextInline(text) };
        InlineComposer.CheckLiteralText(_context, inlines);
        _blocks.Add(new ParagraphBlock(inlines, style));
        return this;
    }

    private BlockComposer AddValue(ParagraphStyleKey? style, TemplatePath value)
    {
        EnsureOpen();
        _blocks.Add(new ParagraphBlock(new Inline[] { InlineComposer.CreateValue(_context, _scope, value) }, style));
        return this;
    }

    private BlockComposer AddComposed(ParagraphStyleKey? style, Action<InlineComposer> content)
    {
        ArgumentNullException.ThrowIfNull(content);
        EnsureOpen();
        InlineComposer composer = new InlineComposer(_context, _scope);
        content(composer);
        _blocks.Add(new ParagraphBlock(composer.Close(), style));
        return this;
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
