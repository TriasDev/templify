// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using TriasDev.Templify.Builder.Model;

namespace TriasDev.Templify.Builder;

/// <summary>Vertical alignment of a table cell's content.</summary>
public enum CellVerticalAlignment
{
    /// <summary>At the top.</summary>
    Top = 0,

    /// <summary>Centered.</summary>
    Center = 1,

    /// <summary>At the bottom.</summary>
    Bottom = 2,
}

/// <summary>
/// Names a table style: borders, rules, padding and header shading. Table styles are a builder concept: Word table
/// styles have no ODF counterpart, so each writer turns the style into the format's own formatting.
/// </summary>
public sealed record TableStyleKey
{
    /// <summary>Initializes a table style key.</summary>
    /// <param name="id">Letters, digits, <c>_</c> and <c>-</c>, starting with a letter or <c>_</c>.</param>
    public TableStyleKey(string id)
    {
        Id = StyleKeyRules.ValidateId(id, nameof(id));
    }

    /// <summary>Gets the id.</summary>
    public string Id { get; }
}

/// <summary>Sets a table style's borders, rules, padding and header shading. Unset parts are absent.</summary>
public sealed class TableStyleComposer
{
    private readonly StyleSheetComposer _owner;
    private readonly TableStyleDefinition _style;

    internal TableStyleComposer(StyleSheetComposer owner, TableStyleDefinition style)
    {
        _owner = owner;
        _style = style;
    }

    /// <summary>Sets the paragraph style of body-row paragraphs that have no style of their own.</summary>
    /// <param name="style">The paragraph style; the style sheet must define it.</param>
    /// <returns>This composer.</returns>
    public TableStyleComposer TextStyle(ParagraphStyleKey style)
    {
        _owner.EnsureOpen();
        _style.TextStyle = style ?? throw new ArgumentNullException(nameof(style));
        return this;
    }

    /// <summary>Sets the paragraph style of header-row paragraphs that have no style of their own.</summary>
    /// <param name="style">The paragraph style; the style sheet must define it.</param>
    /// <returns>This composer.</returns>
    public TableStyleComposer HeaderTextStyle(ParagraphStyleKey style)
    {
        _owner.EnsureOpen();
        _style.HeaderTextStyle = style ?? throw new ArgumentNullException(nameof(style));
        return this;
    }

    /// <summary>Sets the border around the table.</summary>
    /// <param name="width">The line width.</param>
    /// <param name="color">The colour.</param>
    /// <returns>This composer.</returns>
    public TableStyleComposer OuterBorder(Length width, Color color)
    {
        _owner.EnsureOpen();
        _style.Outer = Line(width, color);
        return this;
    }

    /// <summary>Sets the rules between rows.</summary>
    /// <param name="width">The line width.</param>
    /// <param name="color">The colour.</param>
    /// <returns>This composer.</returns>
    public TableStyleComposer RowRule(Length width, Color color)
    {
        _owner.EnsureOpen();
        _style.RowRule = Line(width, color);
        return this;
    }

    /// <summary>Sets the rules between columns.</summary>
    /// <param name="width">The line width.</param>
    /// <param name="color">The colour.</param>
    /// <returns>This composer.</returns>
    public TableStyleComposer ColumnRule(Length width, Color color)
    {
        _owner.EnsureOpen();
        _style.ColumnRule = Line(width, color);
        return this;
    }

    /// <summary>Sets the rule under the header rows.</summary>
    /// <param name="width">The line width.</param>
    /// <param name="color">The colour.</param>
    /// <returns>This composer.</returns>
    public TableStyleComposer HeaderRule(Length width, Color color)
    {
        _owner.EnsureOpen();
        _style.HeaderRule = Line(width, color);
        return this;
    }

    /// <summary>Sets the background of the header rows.</summary>
    /// <param name="color">The colour.</param>
    /// <returns>This composer.</returns>
    public TableStyleComposer HeaderBackground(Color color)
    {
        _owner.EnsureOpen();
        _style.HeaderBackground = color;
        return this;
    }

    /// <summary>Sets the space between a cell's edges and its content.</summary>
    /// <param name="vertical">Top and bottom.</param>
    /// <param name="horizontal">Left and right.</param>
    /// <returns>This composer.</returns>
    public TableStyleComposer CellPadding(Length vertical, Length horizontal)
    {
        _owner.EnsureOpen();
        _style.PaddingVertical = StyleRanges.NotNegative(vertical, nameof(vertical));
        _style.PaddingHorizontal = StyleRanges.NotNegative(horizontal, nameof(horizontal));
        return this;
    }

    private static BorderLine Line(Length width, Color color)
    {
        if (width <= Length.Zero || width > 6.Pt())
        {
            throw new ArgumentOutOfRangeException(nameof(width), width, "A line is more than 0 and at most 6 pt wide.");
        }

        return new BorderLine(width, color);
    }
}

/// <summary>Defines a table's columns. Valid only inside the lambda it is passed to.</summary>
public sealed class ColumnsComposer
{
    private readonly List<ColumnSpec> _columns;

    internal ColumnsComposer(List<ColumnSpec> columns)
    {
        _columns = columns;
    }

    /// <summary>Adds a column of a fixed width.</summary>
    /// <param name="width">The width; greater than zero.</param>
    public void Fixed(Length width) => _columns.Add(new ColumnSpec(StyleRanges.Positive(width, nameof(width)), 0));

    /// <summary>Adds a column that takes a share of the width the fixed columns leave: <c>Relative(2)</c> is twice <c>Relative(1)</c>.</summary>
    /// <param name="weight">The share; greater than zero.</param>
    public void Relative(double weight)
    {
        if (double.IsNaN(weight) || weight <= 0 || weight > 1000)
        {
            throw new ArgumentOutOfRangeException(nameof(weight), weight, "A relative column weight is greater than 0 and at most 1000.");
        }

        _columns.Add(new ColumnSpec(null, weight));
    }
}

/// <summary>
/// Describes a table: columns, header rows and rows. Valid only inside the lambda passed to
/// <see cref="BlockComposer.Table"/>. Tables always have a fixed layout: the widths are the definition's, not the content's.
/// </summary>
public sealed class TableComposer
{
    private readonly BuildContext _context;
    private readonly LoopScope? _scope;
    private readonly TableBlock _table;
    private bool _isOpen = true;

    internal TableComposer(BuildContext context, LoopScope? scope, TableBlock table)
    {
        _context = context;
        _scope = scope;
        _table = table;
    }

    /// <summary>Applies a table style.</summary>
    /// <param name="style">The style; the style sheet must define it.</param>
    /// <returns>This composer.</returns>
    public TableComposer Style(TableStyleKey style)
    {
        EnsureOpen();
        _table.Style = style ?? throw new ArgumentNullException(nameof(style));
        return this;
    }

    /// <summary>Defines the columns. Relative columns share what fixed columns leave of the container's width.</summary>
    /// <param name="columns">Adds the columns.</param>
    /// <returns>This composer.</returns>
    public TableComposer Columns(Action<ColumnsComposer> columns)
    {
        ArgumentNullException.ThrowIfNull(columns);
        EnsureOpen();
        _table.Columns.Clear();
        columns(new ColumnsComposer(_table.Columns));
        return this;
    }

    /// <summary>Sets the space after the table.</summary>
    /// <param name="space">The space; not negative.</param>
    /// <returns>This composer.</returns>
    public TableComposer SpaceAfter(Length space)
    {
        EnsureOpen();
        _table.SpaceAfter = StyleRanges.NotNegative(space, nameof(space));
        return this;
    }

    /// <summary>Adds a header row, repeated at the top of every page. Header rows come before all other rows.</summary>
    /// <param name="row">Describes the row's cells.</param>
    public void HeaderRow(Action<RowComposer> row)
    {
        ArgumentNullException.ThrowIfNull(row);
        EnsureOpen();
        if (_table.Rows.Count > _table.Rows.OfType<TableRowModel>().Count(r => r.IsHeader))
        {
            throw new InvalidOperationException("Header rows come before all other rows of a table.");
        }

        _table.Rows.Add(ComposeRow(row, isHeader: true));
    }

    /// <summary>Adds a row.</summary>
    /// <param name="row">Describes the row's cells.</param>
    public void Row(Action<RowComposer> row)
    {
        ArgumentNullException.ThrowIfNull(row);
        EnsureOpen();
        _table.Rows.Add(ComposeRow(row, isHeader: false));
    }

    /// <summary>Repeats rows for every item of a typed collection.</summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="collection">The collection, from <see cref="TemplatePath{T}.Items{TItem}"/>.</param>
    /// <param name="itemName">The iteration variable.</param>
    /// <param name="rows">Describes the rows for each item; it receives the typed item.</param>
    public void ForEachRow<T>(TemplateCollection<T> collection, string itemName, Action<LoopItem<T>, RowsComposer> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ForEachRow(collection.Untyped, itemName, (item, r) => rows(new LoopItem<T>(item, collection.Naming), r));
    }

    /// <summary>
    /// Repeats rows for every item of a collection: <c>{{#foreach item in collection}}</c> and <c>{{/foreach}}</c> in marker
    /// rows of their own, which Templify removes when it fills the table.
    /// </summary>
    /// <param name="collection">The data path of the collection.</param>
    /// <param name="itemName">The iteration variable (rules as for <see cref="BlockComposer.ForEach"/>).</param>
    /// <param name="rows">Describes the rows repeated for each item; it receives the item.</param>
    public void ForEachRow(TemplatePath collection, string itemName, Action<LoopItem, RowsComposer> rows)
    {
        EnsureOpen();
        _table.Rows.Add(RowsComposer.ComposeLoop(_context, _scope, collection, itemName, rows));
    }

    /// <summary>Adds rows shown only when a condition holds: <c>{{#if …}}</c> … <c>{{/if}}</c> in marker rows.</summary>
    /// <param name="condition">The condition.</param>
    /// <param name="rows">Describes the rows.</param>
    /// <returns>The chain, to add elseif and else rows.</returns>
    public RowsIfChain RowsIf(Condition condition, Action<RowsComposer> rows)
    {
        EnsureOpen();
        return RowsComposer.ComposeConditional(_context, _scope, _table.Rows, condition, rows, () => _isOpen);
    }

    internal void Close() => _isOpen = false;

    private TableRowModel ComposeRow(Action<RowComposer> row, bool isHeader) => RowsComposer.ComposeRow(_context, _scope, row, isHeader);

    private void EnsureOpen()
    {
        if (!_isOpen)
        {
            throw new InvalidOperationException("A table composer was used outside the lambda it was passed to.");
        }
    }
}

/// <summary>
/// Describes body rows inside a row loop or a row conditional: rows, nested row conditionals and loops. Valid only
/// inside the lambda it is passed to.
/// </summary>
public sealed class RowsComposer
{
    private readonly BuildContext _context;
    private readonly LoopScope? _scope;
    private readonly List<TableRowItem> _rows;
    private bool _isOpen = true;

    internal RowsComposer(BuildContext context, LoopScope? scope, List<TableRowItem> rows)
    {
        _context = context;
        _scope = scope;
        _rows = rows;
    }

    /// <summary>Adds a row.</summary>
    /// <param name="row">Describes the row's cells.</param>
    public void Row(Action<RowComposer> row)
    {
        ArgumentNullException.ThrowIfNull(row);
        EnsureOpen();
        _rows.Add(ComposeRow(_context, _scope, row, isHeader: false));
    }

    /// <summary>Repeats rows for every item of a typed collection.</summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="collection">The collection, from <see cref="TemplatePath{T}.Items{TItem}"/>.</param>
    /// <param name="itemName">The iteration variable.</param>
    /// <param name="rows">Describes the rows for each item; it receives the typed item.</param>
    public void ForEachRow<T>(TemplateCollection<T> collection, string itemName, Action<LoopItem<T>, RowsComposer> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ForEachRow(collection.Untyped, itemName, (item, r) => rows(new LoopItem<T>(item, collection.Naming), r));
    }

    /// <summary>Repeats rows for every item of a collection, in marker rows of their own.</summary>
    /// <param name="collection">The data path of the collection.</param>
    /// <param name="itemName">The iteration variable.</param>
    /// <param name="rows">Describes the rows repeated for each item.</param>
    public void ForEachRow(TemplatePath collection, string itemName, Action<LoopItem, RowsComposer> rows)
    {
        EnsureOpen();
        _rows.Add(ComposeLoop(_context, _scope, collection, itemName, rows));
    }

    /// <summary>Adds rows shown only when a condition holds, in marker rows of their own.</summary>
    /// <param name="condition">The condition.</param>
    /// <param name="rows">Describes the rows.</param>
    /// <returns>The chain, to add elseif and else rows.</returns>
    public RowsIfChain RowsIf(Condition condition, Action<RowsComposer> rows)
    {
        EnsureOpen();
        return ComposeConditional(_context, _scope, _rows, condition, rows, () => _isOpen);
    }

    internal void Close() => _isOpen = false;

    internal static TableRowModel ComposeRow(BuildContext context, LoopScope? scope, Action<RowComposer> row, bool isHeader)
    {
        TableRowModel model = new TableRowModel(isHeader);
        RowComposer composer = new RowComposer(context, scope, model);
        row(composer);
        composer.Close();
        return model;
    }

    internal static List<TableRowItem> Compose(BuildContext context, LoopScope? scope, Action<RowsComposer> rows)
    {
        List<TableRowItem> items = new List<TableRowItem>();
        RowsComposer composer = new RowsComposer(context, scope, items);
        rows(composer);
        composer.Close();
        return items;
    }

    internal static TableRowLoop ComposeLoop(BuildContext context, LoopScope? scope, TemplatePath collection, string itemName, Action<LoopItem, RowsComposer> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        if (collection.IsRoot || collection.IsMetadata)
        {
            throw new ArgumentException("A row loop needs a collection path from the data or from a loop item.", nameof(collection));
        }

        LoopScope.CheckUse(context, scope, collection);
        LoopScope loopScope = new LoopScope(LoopScope.ValidateItemName(itemName, scope), collection, scope);
        TableRowLoop loop = new TableRowLoop(collection, itemName);
        RowsComposer composer = new RowsComposer(context, loopScope, loop.Rows);
        rows(new LoopItem(loopScope), composer);
        composer.Close();
        return loop;
    }

    internal static RowsIfChain ComposeConditional(BuildContext context, LoopScope? scope, List<TableRowItem> target, Condition condition, Action<RowsComposer> rows, Func<bool> isOpen)
    {
        ArgumentNullException.ThrowIfNull(condition);
        ArgumentNullException.ThrowIfNull(rows);
        BlockComposer.CheckCondition(context, scope, condition);
        TableRowConditional conditional = new TableRowConditional();
        conditional.Branches.Add((condition, Compose(context, scope, rows)));
        target.Add(conditional);
        return new RowsIfChain(context, scope, conditional, isOpen);
    }

    private void EnsureOpen()
    {
        if (!_isOpen)
        {
            throw new InvalidOperationException("A rows composer was used outside the lambda it was passed to.");
        }
    }
}

/// <summary>Continues conditional rows: <see cref="ElseIf"/> and a last <see cref="Else"/>.</summary>
public sealed class RowsIfChain
{
    private readonly BuildContext _context;
    private readonly LoopScope? _scope;
    private readonly TableRowConditional _conditional;
    private readonly Func<bool> _isOpen;

    internal RowsIfChain(BuildContext context, LoopScope? scope, TableRowConditional conditional, Func<bool> isOpen)
    {
        _context = context;
        _scope = scope;
        _conditional = conditional;
        _isOpen = isOpen;
    }

    /// <summary>Adds rows for a further condition: <c>{{#elseif …}}</c>.</summary>
    /// <param name="condition">The condition.</param>
    /// <param name="rows">Describes the rows.</param>
    /// <returns>This chain.</returns>
    public RowsIfChain ElseIf(Condition condition, Action<RowsComposer> rows)
    {
        ArgumentNullException.ThrowIfNull(condition);
        ArgumentNullException.ThrowIfNull(rows);
        EnsureOpen();
        BlockComposer.CheckCondition(_context, _scope, condition);
        _conditional.Branches.Add((condition, RowsComposer.Compose(_context, _scope, rows)));
        return this;
    }

    /// <summary>Adds the rows shown when no condition holds: <c>{{#else}}</c>. It ends the chain.</summary>
    /// <param name="rows">Describes the rows.</param>
    public void Else(Action<RowsComposer> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        EnsureOpen();
        _conditional.Else = RowsComposer.Compose(_context, _scope, rows);
    }

    private void EnsureOpen()
    {
        if (_conditional.Else != null)
        {
            throw new InvalidOperationException("The conditional rows already have an else part; nothing can follow it.");
        }

        if (!_isOpen())
        {
            throw new InvalidOperationException("Conditional rows were continued outside the lambda they were started in.");
        }
    }
}

/// <summary>Describes the cells of a row. Valid only inside the lambda it is passed to.</summary>
public sealed class RowComposer
{
    private readonly BuildContext _context;
    private readonly LoopScope? _scope;
    private readonly TableRowModel _row;
    private bool _isOpen = true;

    internal RowComposer(BuildContext context, LoopScope? scope, TableRowModel row)
    {
        _context = context;
        _scope = scope;
        _row = row;
    }

    /// <summary>Adds a cell with literal text in the inherited paragraph style (row, table style, style sheet default).</summary>
    /// <param name="text">The text.</param>
    public void Cell(string text) => Cell(c => c.Paragraph(text));

    /// <summary>Adds a cell with literal text.</summary>
    /// <param name="style">The paragraph style.</param>
    /// <param name="text">The text.</param>
    public void Cell(ParagraphStyleKey style, string text) => Cell(c => c.Paragraph(style, text));

    /// <summary>Adds a cell holding one value from the data.</summary>
    /// <param name="value">The data path.</param>
    public void Cell(TemplatePath value) => Cell(c => c.Paragraph(value));

    /// <summary>Adds a cell holding one value from the data.</summary>
    /// <param name="style">The paragraph style.</param>
    /// <param name="value">The data path.</param>
    public void Cell(ParagraphStyleKey style, TemplatePath value) => Cell(c => c.Paragraph(style, value));

    /// <summary>Adds a cell with any block content and the cell's own options (span, background, alignment).</summary>
    /// <param name="cell">Describes the cell.</param>
    public void Cell(Action<CellComposer> cell)
    {
        ArgumentNullException.ThrowIfNull(cell);
        EnsureOpen();
        TableCellModel model = new TableCellModel();
        CellComposer composer = new CellComposer(_context, model, _scope);
        cell(composer);
        composer.Close();
        _row.Cells.Add(model);
    }

    /// <summary>
    /// Sets the paragraph style of this row's paragraphs that have no style of their own, instead of the table
    /// style's text style (for example a totals row).
    /// </summary>
    /// <param name="style">The paragraph style.</param>
    public void TextStyle(ParagraphStyleKey style)
    {
        ArgumentNullException.ThrowIfNull(style);
        EnsureOpen();
        _row.TextStyle = style;
    }

    /// <summary>Lets the row break across pages; by default a row is kept on one page.</summary>
    public void AllowSplit()
    {
        EnsureOpen();
        _row.AllowSplit = true;
    }

    internal void Close() => _isOpen = false;

    private void EnsureOpen()
    {
        if (!_isOpen)
        {
            throw new InvalidOperationException("A row composer was used outside the lambda it was passed to.");
        }
    }
}

/// <summary>
/// Describes a table cell: any block content (paragraphs, conditions, loops, nested tables) plus the cell's options.
/// Valid only inside the lambda it is passed to.
/// </summary>
public sealed class CellComposer : BlockComposer
{
    private readonly TableCellModel _cell;

    internal CellComposer(BuildContext context, TableCellModel cell, LoopScope? scope)
        : base(context, cell.Content, scope)
    {
        _cell = cell;
    }

    /// <summary>Lets the cell span several columns.</summary>
    /// <param name="columns">The number of columns; at least 1.</param>
    /// <returns>This composer.</returns>
    public CellComposer ColumnSpan(int columns)
    {
        if (columns < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(columns), columns, "A cell spans at least one column.");
        }

        _cell.Span = columns;
        return this;
    }

    /// <summary>Sets the cell's background.</summary>
    /// <param name="color">The colour.</param>
    /// <returns>This composer.</returns>
    public CellComposer Background(Color color)
    {
        _cell.Background = color;
        return this;
    }

    /// <summary>Sets the vertical alignment of the cell's content.</summary>
    /// <param name="alignment">The alignment.</param>
    /// <returns>This composer.</returns>
    public CellComposer VerticalAlignment(CellVerticalAlignment alignment)
    {
        if (!Enum.IsDefined(alignment))
        {
            throw new ArgumentOutOfRangeException(nameof(alignment), alignment, "Unknown alignment.");
        }

        _cell.VerticalAlignment = alignment;
        return this;
    }
}

/// <summary>Resolves column widths against the frame a table sits in, and checks that they fit.</summary>
internal static class TableLayout
{
    /// <summary>The widths of the columns: fixed ones as given, relative ones sharing the rest.</summary>
    public static Length[] Resolve(IReadOnlyList<ColumnSpec> columns, Length frame)
    {
        Length fixedSum = columns.Where(c => c.Fixed is not null).Aggregate(Length.Zero, (sum, c) => sum + c.Fixed!.Value);
        Length rest = frame - fixedSum;
        double weights = columns.Where(c => c.Fixed is null).Sum(c => c.Weight);
        Length[] widths = new Length[columns.Count];
        Length assigned = Length.Zero;
        int lastRelative = columns.ToList().FindLastIndex(c => c.Fixed is null);
        for (int i = 0; i < columns.Count; i++)
        {
            if (columns[i].Fixed is { } fixedWidth)
            {
                widths[i] = fixedWidth;
            }
            else if (i == lastRelative)
            {
                widths[i] = rest - assigned;
            }
            else
            {
                widths[i] = Length.FromPoints(rest.Points * columns[i].Weight / weights);
                assigned += widths[i];
            }
        }

        return widths;
    }

    /// <summary>The width a cell's content has: its columns minus the horizontal padding.</summary>
    public static Length CellFrame(Length[] widths, int firstColumn, int span, Length horizontalPadding)
    {
        Length width = Length.Zero;
        for (int i = firstColumn; i < Math.Min(firstColumn + span, widths.Length); i++)
        {
            width += widths[i];
        }

        return width - horizontalPadding - horizontalPadding;
    }

    /// <summary>Reports tables whose columns do not fit, whose rows have the wrong number of cells, or whose style is undefined.</summary>
    public static void Check(IEnumerable<Block> blocks, Length frame, StyleSheet styles, BuildContext context)
    {
        foreach (TableBlock table in BlockTree.Tables(blocks))
        {
            CheckTable(table, frame, styles, context);
        }
    }

    private static void CheckTable(TableBlock table, Length frame, StyleSheet styles, BuildContext context)
    {
        context.At(table);
        if (table.Columns.Count == 0)
        {
            context.Error(DiagnosticCodes.TableWithoutColumns, "A table has no columns; define them with Columns(...).");
            return;
        }

        if (table.Style is { } style && styles.Find(style) == null)
        {
            context.Error(DiagnosticCodes.UndefinedStyle, $"A table uses the table style '{style.Id}', which the template's style sheet does not define.");
        }

        if (table.BoxStyle is { } box && styles.Find(box) == null)
        {
            context.Error(DiagnosticCodes.UndefinedStyle, $"A box uses the box style '{box.Id}', which the template's style sheet does not define.");
        }

        Length fixedSum = table.Columns.Where(c => c.Fixed is not null).Aggregate(Length.Zero, (sum, c) => sum + c.Fixed!.Value);
        bool hasRelative = table.Columns.Any(c => c.Fixed is null);
        if (fixedSum > frame || (hasRelative && fixedSum == frame))
        {
            context.Error(
                DiagnosticCodes.TableTooWide,
                $"A table's fixed columns take {fixedSum} of the {frame} available{(hasRelative ? ", leaving nothing for its relative columns" : string.Empty)}.");
            return;
        }

        Length[] widths = Resolve(table.Columns, frame);
        Length padding = styles.Resolve(table)?.PaddingHorizontal ?? TableStyleDefinition.DefaultPaddingHorizontal;
        foreach (TableRowModel row in table.AllRows())
        {
            int cells = row.Cells.Sum(c => c.Span);
            if (cells != table.Columns.Count)
            {
                context.Error(
                    DiagnosticCodes.RowCellCount,
                    $"A table row spans {cells} column(s), but the table has {table.Columns.Count}.");
                continue;
            }

            int column = 0;
            foreach (TableCellModel cell in row.Cells)
            {
                foreach (TableBlock nested in BlockTree.Tables(cell.Content))
                {
                    CheckTable(nested, CellFrame(widths, column, cell.Span, padding), styles, context);
                }

                column += cell.Span;
            }
        }
    }
}
