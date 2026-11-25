// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Globalization;

namespace TriasDev.Templify.Builder.Model;

/// <summary>The format-neutral description of a template that the writers turn into a package.</summary>
/// <param name="Body">The blocks of the document body, in order.</param>
/// <param name="Language">The document language.</param>
/// <param name="Styles">The named styles; <see cref="StyleSheet.Empty"/> when the definition sets none.</param>
/// <param name="Info">The standard document properties.</param>
/// <param name="Stamp">The builder's stamp, written as custom document properties.</param>
/// <param name="Page">The page setup, header and footer.</param>
internal sealed record DocumentModel(IReadOnlyList<Block> Body, CultureInfo Language, StyleSheet Styles, DocumentInfo Info, TemplateStamp Stamp, PageSetup Page);

/// <summary>
/// Page size, margins (page edge to body text), header/footer distances (page edge to header/footer), and the
/// header and footer parts. With <c>DistinctFirstPage</c>, <c>FirstHeader</c>/<c>FirstFooter</c> are the first
/// page's (empty for none).
/// </summary>
internal sealed record PageSetup(
    PageSize Size,
    Length Top,
    Length Right,
    Length Bottom,
    Length Left,
    Length HeaderDistance,
    Length FooterDistance,
    List<Block>? Header,
    List<Block>? Footer,
    bool DistinctFirstPage,
    List<Block>? FirstHeader,
    List<Block>? FirstFooter)
{
    /// <summary>Gets the width of the text frame: the page width minus the side margins.</summary>
    public Length FrameWidth => Size.Width - Left - Right;

    /// <summary>Gets every block of the header and footer parts.</summary>
    public IEnumerable<Block> AllBlocks =>
        (Header ?? []).Concat(Footer ?? []).Concat(FirstHeader ?? []).Concat(FirstFooter ?? []);
}

/// <summary>A block-level element of the document.</summary>
internal abstract record Block;

/// <summary>A paragraph.</summary>
/// <param name="Inlines">The paragraph content, in order.</param>
/// <param name="Style">The paragraph style; null for the format's default paragraph style.</param>
internal sealed record ParagraphBlock(IReadOnlyList<Inline> Inlines, ParagraphStyleKey? Style = null) : Block;

/// <summary>A page break: the content after it starts on a new page.</summary>
internal sealed record PageBreakBlock : Block;

/// <summary>An if / elseif / else chain. Branches and the else part are filled while the chain is composed.</summary>
internal sealed record ConditionalBlock : Block
{
    public List<ConditionalBranch> Branches { get; } = new List<ConditionalBranch>();

    /// <summary>Gets or sets the else part; null when there is none.</summary>
    public List<Block>? Else { get; set; }
}

/// <summary>A paragraph-level loop: <c>{{#foreach item in collection}}</c> … <c>{{/foreach}}</c>.</summary>
internal sealed record LoopBlock(TemplatePath Collection, string ItemName, List<Block> Body) : Block;

/// <summary>A line: width and colour.</summary>
internal sealed record BorderLine(Length Width, Color Color);

/// <summary>A column of a table: a fixed width, or a weight (when <see cref="Fixed"/> is null).</summary>
internal sealed record ColumnSpec(Length? Fixed, double Weight);

/// <summary>A table: columns, rows (header rows first) and options.</summary>
internal sealed record TableBlock : Block
{
    public TableStyleKey? Style { get; set; }

    public List<ColumnSpec> Columns { get; } = new List<ColumnSpec>();

    public List<TableRowItem> Rows { get; } = new List<TableRowItem>();

    public Length? SpaceAfter { get; set; }

    /// <summary>Every row of the table, also inside row loops and row conditionals.</summary>
    public IEnumerable<TableRowModel> AllRows() => TableRowItem.Rows(Rows);
}

/// <summary>An item of a table's row list: a row, or (later) a row loop or row conditional.</summary>
internal abstract class TableRowItem
{
    public static IEnumerable<TableRowModel> Rows(IEnumerable<TableRowItem> items)
    {
        foreach (TableRowItem item in items)
        {
            foreach (TableRowModel row in item.ContainedRows())
            {
                yield return row;
            }
        }
    }

    protected abstract IEnumerable<TableRowModel> ContainedRows();
}

/// <summary>A table row.</summary>
internal sealed class TableRowModel(bool isHeader) : TableRowItem
{
    public bool IsHeader { get; } = isHeader;

    public bool AllowSplit { get; set; }

    public List<TableCellModel> Cells { get; } = new List<TableCellModel>();

    protected override IEnumerable<TableRowModel> ContainedRows() => [this];
}

/// <summary>A table cell.</summary>
internal sealed class TableCellModel
{
    public List<Block> Content { get; } = new List<Block>();

    public int Span { get; set; } = 1;

    public Color? Background { get; set; }

    public CellVerticalAlignment? VerticalAlignment { get; set; }
}

/// <summary>A table style as defined in a <see cref="StyleSheet"/>.</summary>
internal sealed class TableStyleDefinition(TableStyleKey key)
{
    /// <summary>Word's default cell margin left and right (108 twips).</summary>
    public static readonly Length DefaultPaddingHorizontal = 5.4.Pt();

    public TableStyleKey Key { get; } = key;

    public BorderLine? Outer { get; set; }

    public BorderLine? RowRule { get; set; }

    public BorderLine? ColumnRule { get; set; }

    public BorderLine? HeaderRule { get; set; }

    public Color? HeaderBackground { get; set; }

    public Length PaddingVertical { get; set; } = Length.Zero;

    public Length PaddingHorizontal { get; set; } = DefaultPaddingHorizontal;
}

/// <summary>Walks the block tree.</summary>
internal static class BlockTree
{
    /// <summary>Every block, also inside conditionals, loops, components and table cells, in document order.</summary>
    public static IEnumerable<Block> All(IEnumerable<Block> blocks)
    {
        foreach (Block block in blocks)
        {
            yield return block;
            foreach (Block inner in All(Children(block, intoCells: true)))
            {
                yield return inner;
            }
        }
    }

    /// <summary>The tables of a container, through conditionals, loops and components but not into table cells.</summary>
    public static IEnumerable<TableBlock> Tables(IEnumerable<Block> blocks)
    {
        foreach (Block block in blocks)
        {
            if (block is TableBlock table)
            {
                yield return table;
                continue;
            }

            foreach (TableBlock inner in Tables(Children(block, intoCells: false)))
            {
                yield return inner;
            }
        }
    }

    /// <summary>Every paragraph at any depth.</summary>
    public static IEnumerable<ParagraphBlock> Paragraphs(IEnumerable<Block> blocks) => All(blocks).OfType<ParagraphBlock>();

    /// <summary>Whether conditionals or loops occur at any depth (they need the marker style).</summary>
    public static bool HasMarkers(IEnumerable<Block> blocks) => All(blocks).Any(b => b is ConditionalBlock or LoopBlock);

    private static IEnumerable<Block> Children(Block block, bool intoCells) => block switch
    {
        ConditionalBlock conditional => conditional.Branches.SelectMany(b => b.Body).Concat(conditional.Else ?? []),
        LoopBlock loop => loop.Body,
        ComponentBlock component => component.Body,
        TableBlock table when intoCells => table.AllRows().SelectMany(r => r.Cells).SelectMany(c => c.Content),
        _ => [],
    };
}

/// <summary>The content of a component, kept as a unit together with the component that produced it.</summary>
internal sealed record ComponentBlock(ITemplateComponent Component, List<Block> Body) : Block;

/// <summary>One condition of a <see cref="ConditionalBlock"/> and the blocks it guards.</summary>
internal sealed record ConditionalBranch(Condition Condition, List<Block> Body);

/// <summary>A marker paragraph, produced from a <see cref="ConditionalBlock"/> by <see cref="BlockFlattener"/>.</summary>
/// <param name="Text">The marker, for example <c>{{#if order.isPaid}}</c>.</param>
internal sealed record MarkerBlock(string Text) : Block;

/// <summary>Turns conditional blocks into marker paragraphs around their content, in document order.</summary>
internal static class BlockFlattener
{
    public static IEnumerable<Block> Flatten(IEnumerable<Block> blocks)
    {
        foreach (Block block in blocks)
        {
            if (block is LoopBlock loop)
            {
                yield return new MarkerBlock(TemplateSyntax.ForEach(loop.ItemName, loop.Collection));
                foreach (Block inner in Flatten(loop.Body))
                {
                    yield return inner;
                }

                yield return new MarkerBlock(TemplateSyntax.EndForEach);
                continue;
            }

            if (block is ComponentBlock component)
            {
                foreach (Block inner in Flatten(component.Body))
                {
                    yield return inner;
                }

                continue;
            }

            if (block is not ConditionalBlock conditional)
            {
                yield return block;
                continue;
            }

            for (int i = 0; i < conditional.Branches.Count; i++)
            {
                ConditionalBranch branch = conditional.Branches[i];
                yield return new MarkerBlock(i == 0 ? TemplateSyntax.If(branch.Condition) : TemplateSyntax.ElseIf(branch.Condition));
                foreach (Block inner in Flatten(branch.Body))
                {
                    yield return inner;
                }
            }

            if (conditional.Else is { } otherwise)
            {
                yield return new MarkerBlock(TemplateSyntax.Else);
                foreach (Block inner in Flatten(otherwise))
                {
                    yield return inner;
                }
            }

            yield return new MarkerBlock(TemplateSyntax.EndIf);
        }
    }

}

/// <summary>Content inside a paragraph.</summary>
/// <param name="Style">The character style; null for the paragraph's own formatting.</param>
internal abstract record Inline(CharacterStyleKey? Style);

/// <summary>Literal text. Tabs and line breaks in it are written as the format's tab and line break.</summary>
/// <param name="Text">The text.</param>
/// <param name="Style">The character style, or null.</param>
internal sealed record TextInline(string Text, CharacterStyleKey? Style = null) : Inline(Style);

/// <summary>The kinds of page field.</summary>
internal enum PageField
{
    Number,
    Count,
}

/// <summary>A page number or page count field.</summary>
internal sealed record PageFieldInline(PageField Field, CharacterStyleKey? Style = null) : Inline(Style);

/// <summary>A tab to a right-aligned tab stop at the end of the text frame.</summary>
internal sealed record RightTabInline() : Inline((CharacterStyleKey?)null);

/// <summary>A placeholder for a value from the data.</summary>
/// <param name="Path">The data path; never empty.</param>
/// <param name="Format">The format specifier, or null.</param>
/// <param name="Style">The character style, or null.</param>
internal sealed record ValueInline(TemplatePath Path, ValueFormat? Format = null, CharacterStyleKey? Style = null) : Inline(Style);
