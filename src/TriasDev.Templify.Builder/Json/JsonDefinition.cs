// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Text.Json;
using System.Xml.Linq;

namespace TriasDev.Templify.Builder.Json;

/// <summary>
/// A definition read from JSON. <see cref="Compose"/> replays the JSON through the same composers the fluent API
/// uses, so every rule is checked once and reported like a C# definition's, with the JSON pointer as location.
/// </summary>
internal sealed class JsonDefinition : ITemplateDefinition
{
    private readonly JsonElement _root;
    private readonly TemplateJsonImportOptions _options;
    private readonly Dictionary<string, ParagraphStyleKey> _paragraphStyles = new Dictionary<string, ParagraphStyleKey>(StringComparer.Ordinal);
    private readonly Dictionary<string, CharacterStyleKey> _characterStyles = new Dictionary<string, CharacterStyleKey>(StringComparer.Ordinal);
    private readonly List<(string Name, LoopItem Item)> _scope = new List<(string, LoopItem)>();
    private BuildContext _context = null!;
    private TemplateComposer _template = null!;

    public JsonDefinition(JsonElement root, TemplateIdentity identity, TemplateJsonImportOptions options)
    {
        _root = root;
        Identity = identity;
        _options = options;
        if (_root.TryGetProperty("styles", out JsonElement styles))
        {
            foreach (JsonProperty style in Properties(styles, "paragraph"))
            {
                _paragraphStyles[style.Name] = style.Name == BuilderStyles.TemplateMarker.Id
                    ? BuilderStyles.TemplateMarker
                    : new ParagraphStyleKey(style.Name, String(style.Value, "name") ?? style.Name);
            }

            foreach (JsonProperty style in Properties(styles, "character"))
            {
                _characterStyles[style.Name] = new CharacterStyleKey(style.Name, String(style.Value, "name") ?? style.Name);
            }
        }
    }

    public TemplateIdentity Identity { get; }

    public void Compose(TemplateComposer template)
    {
        _template = template;
        _context = template.Context;
        _scope.Clear();

        if (_root.TryGetProperty("styles", out JsonElement styles))
        {
            Guard("/styles", () => template.Styles(StyleSheet.Create(s => Styles(s, styles))));
        }

        if (_root.TryGetProperty("properties", out JsonElement properties))
        {
            template.Properties(p =>
            {
                if (String(properties, "title") is { } title)
                {
                    p.Title(title);
                }

                if (String(properties, "author") is { } author)
                {
                    p.Author(author);
                }

                if (String(properties, "subject") is { } subject)
                {
                    p.Subject(subject);
                }
            });
        }

        if (_root.TryGetProperty("page", out JsonElement page))
        {
            template.Page(p => Page(p, page, "/page"));
        }

        if (_root.TryGetProperty("content", out JsonElement content))
        {
            template.Content(c => Blocks(c, content, "/content"));
        }
    }

    // ── styles ───────────────────────────────────────────────────────────────

    private void Styles(StyleSheetComposer s, JsonElement styles)
    {
        if (styles.TryGetProperty("defaults", out JsonElement defaults))
        {
            s.Defaults(d =>
            {
                if (Font(defaults, "/styles/defaults") is { } font)
                {
                    d.Font(font);
                }

                if (Length(defaults, "size", "/styles/defaults") is { } size)
                {
                    d.Size(size);
                }

                if (Color(defaults, "color", "/styles/defaults") is { } color)
                {
                    d.Color(color);
                }
            });
        }

        foreach (JsonProperty style in Properties(styles, "paragraph"))
        {
            JsonElement e = style.Value;
            string at = "/styles/paragraph/" + Escape(style.Name);
            s.Paragraph(_paragraphStyles[style.Name], p =>
            {
                if (String(e, "basedOn") is { } basedOn)
                {
                    p.BasedOn(ParagraphStyle(basedOn));
                }

                if (String(e, "next") is { } next)
                {
                    p.Next(ParagraphStyle(next));
                }

                if (Font(e, at) is { } font)
                {
                    p.Font(font);
                }

                Apply(Length(e, "size", at), v => p.Size(v));
                Apply(Color(e, "color", at), v => p.Color(v));
                Apply(Bool(e, "bold"), v => p.Bold(v));
                Apply(Bool(e, "italic"), v => p.Italic(v));
                Apply(Length(e, "spaceBefore", at), v => p.SpaceBefore(v));
                Apply(Length(e, "spaceAfter", at), v => p.SpaceAfter(v));
                if (e.TryGetProperty("lineSpacing", out JsonElement spacing) && spacing.ValueKind == JsonValueKind.Number)
                {
                    p.LineSpacing(spacing.GetDouble());
                }

                Apply(Length(e, "lineHeight", at), v => p.LineHeight(v));
                Apply(Length(e, "minimumLineHeight", at), v => p.MinimumLineHeight(v));
                if (String(e, "alignment") is { } alignment)
                {
                    p.Alignment(Enum<TextAlignment>(alignment, at + "/alignment"));
                }

                Apply(Length(e, "indentStart", at), v => p.IndentStart(v));
                Apply(Length(e, "indentEnd", at), v => p.IndentEnd(v));
                Apply(Length(e, "firstLineIndent", at), v => p.FirstLineIndent(v));
                Apply(Bool(e, "keepWithNext"), v => p.KeepWithNext(v));
                Apply(Bool(e, "keepLinesTogether"), v => p.KeepLinesTogether(v));
                if (e.TryGetProperty("outlineLevel", out JsonElement level) && level.ValueKind == JsonValueKind.Number)
                {
                    p.OutlineLevel(level.GetInt32());
                }
            });
        }

        foreach (JsonProperty style in Properties(styles, "character"))
        {
            JsonElement e = style.Value;
            string at = "/styles/character/" + Escape(style.Name);
            s.Character(_characterStyles[style.Name], c =>
            {
                if (String(e, "basedOn") is { } basedOn)
                {
                    c.BasedOn(CharacterStyle(basedOn));
                }

                if (Font(e, at) is { } font)
                {
                    c.Font(font);
                }

                Apply(Length(e, "size", at), v => c.Size(v));
                Apply(Color(e, "color", at), v => c.Color(v));
                Apply(Bool(e, "bold"), v => c.Bold(v));
                Apply(Bool(e, "italic"), v => c.Italic(v));
            });
        }

        foreach (JsonProperty style in Properties(styles, "table"))
        {
            JsonElement e = style.Value;
            string at = "/styles/table/" + Escape(style.Name);
            s.Table(new TableStyleKey(style.Name), t =>
            {
                Apply(Border(e, "outerBorder", at), v => t.OuterBorder(v.Width, v.Color));
                Apply(Border(e, "rowRule", at), v => t.RowRule(v.Width, v.Color));
                Apply(Border(e, "columnRule", at), v => t.ColumnRule(v.Width, v.Color));
                Apply(Border(e, "headerRule", at), v => t.HeaderRule(v.Width, v.Color));
                Apply(Color(e, "headerBackground", at), v => t.HeaderBackground(v));
                if (Box(e, "cellPadding", at) is { } padding)
                {
                    t.CellPadding(padding.Top, padding.Right);
                }
            });
        }

        foreach (JsonProperty style in Properties(styles, "box"))
        {
            JsonElement e = style.Value;
            string at = "/styles/box/" + Escape(style.Name);
            s.Box(new BoxStyleKey(style.Name), b =>
            {
                Apply(Border(e, "border", at), v => b.Border(v.Width, v.Color));
                Apply(Color(e, "background", at), v => b.Background(v));
                if (Box(e, "padding", at) is { } padding)
                {
                    b.Padding(padding.Top, padding.Right);
                }

                Apply(Length(e, "spaceAfter", at), v => b.SpaceAfter(v));
            });
        }

        foreach (JsonProperty style in Properties(styles, "list"))
        {
            JsonElement e = style.Value;
            string at = "/styles/list/" + Escape(style.Name);
            s.List(new ListStyleKey(style.Name), l =>
            {
                if (String(e, "itemStyle") is { } itemStyle)
                {
                    l.ItemStyle(ParagraphStyle(itemStyle));
                }

                int i = 0;
                foreach (JsonElement level in Array(e, "levels"))
                {
                    string levelAt = at + "/levels/" + i++;
                    if (Length(level, "indent", levelAt) is { } indent && Length(level, "hanging", levelAt) is { } hanging)
                    {
                        l.Level(Int(level, "level") ?? 0, String(level, "bullet") ?? "•", indent, hanging);
                    }
                }
            });
        }
    }

    // ── page ─────────────────────────────────────────────────────────────────

    private void Page(PageComposer p, JsonElement page, string at)
    {
        if (page.TryGetProperty("size", out JsonElement size))
        {
            if (size.ValueKind == JsonValueKind.String)
            {
                if (JsonValues.TryPageSize(size.GetString(), out PageSize? named))
                {
                    p.Size(named!);
                }
                else
                {
                    Fail(at + "/size", $"'{size.GetString()}' is not a page size (A4, A5, Letter, Legal, or {{ \"width\", \"height\" }}).");
                }
            }
            else if (Length(size, "width", at + "/size") is { } width && Length(size, "height", at + "/size") is { } height)
            {
                p.Size(new PageSize(width, height));
            }
        }

        if (Box(page, "margins", at) is { } margins)
        {
            p.Margins(margins.Top, margins.Right, margins.Bottom, margins.Left);
        }

        if (String(page, "headerFooterDistance") is { } distances)
        {
            string[] parts = distances.Split(' ');
            if (parts.Length == 2 && JsonValues.TryLength(parts[0], out Length header) && JsonValues.TryLength(parts[1], out Length footer))
            {
                p.HeaderFooterDistance(header, footer);
            }
            else
            {
                Fail(at + "/headerFooterDistance", $"'{distances}' is not two lengths (header, footer).");
            }
        }

        if (page.TryGetProperty("header", out JsonElement headerBlocks))
        {
            p.Header(b => Blocks(b, headerBlocks, at + "/header"));
        }

        if (page.TryGetProperty("footer", out JsonElement footerBlocks))
        {
            p.Footer(b => Blocks(b, footerBlocks, at + "/footer"));
        }

        if (page.TryGetProperty("firstPage", out JsonElement first))
        {
            p.FirstPage(f =>
            {
                if (first.TryGetProperty("header", out JsonElement firstHeader))
                {
                    if (firstHeader.ValueKind == JsonValueKind.String && firstHeader.GetString() == "none")
                    {
                        f.NoHeader();
                    }
                    else
                    {
                        f.Header(b => Blocks(b, firstHeader, at + "/firstPage/header"));
                    }
                }

                if (first.TryGetProperty("footer", out JsonElement firstFooter))
                {
                    if (firstFooter.ValueKind == JsonValueKind.String && firstFooter.GetString() == "none")
                    {
                        f.NoFooter();
                    }
                    else
                    {
                        f.Footer(b => Blocks(b, firstFooter, at + "/firstPage/footer"));
                    }
                }
            });
        }
    }

    // ── blocks ───────────────────────────────────────────────────────────────

    private void Blocks(BlockComposer c, JsonElement blocks, string at)
    {
        if (blocks.ValueKind != JsonValueKind.Array)
        {
            Fail(at, "A list of blocks ([ … ]) is expected.");
            return;
        }

        int i = 0;
        foreach (JsonElement block in blocks.EnumerateArray())
        {
            string blockAt = at + "/" + i++;
            Guard(blockAt, () => Block(c, block, blockAt));
        }
    }

    private void Block(BlockComposer c, JsonElement block, string at)
    {
        if (block.ValueKind != JsonValueKind.Object)
        {
            Fail(at, "A block is an object such as { \"paragraph\": … }.");
            return;
        }

        if (block.TryGetProperty("paragraph", out JsonElement paragraph))
        {
            Paragraph(c, paragraph, at + "/paragraph");
        }
        else if (block.TryGetProperty("pageBreak", out _))
        {
            c.PageBreak();
        }
        else if (block.TryGetProperty("if", out JsonElement condition))
        {
            IfChain chain = c.If(Condition(condition, at + "/if"), b => Blocks(b, Required(block, "then", at), at + "/then"));
            int i = 0;
            foreach (JsonElement branch in Array(block, "elseIf"))
            {
                string branchAt = at + "/elseIf/" + i++;
                chain = chain.ElseIf(Condition(Required(branch, "when", branchAt), branchAt + "/when"), b => Blocks(b, Required(branch, "then", branchAt), branchAt + "/then"));
            }

            if (block.TryGetProperty("else", out JsonElement otherwise))
            {
                chain.Else(b => Blocks(b, otherwise, at + "/else"));
            }
        }
        else if (block.TryGetProperty("forEach", out JsonElement collection))
        {
            string name = String(block, "as") ?? string.Empty;
            JsonElement body = Required(block, "body", at);
            c.ForEach(Path(collection, at + "/forEach"), name, (item, b) => InScope(name, item, () => Blocks(b, body, at + "/body")));
        }
        else if (block.TryGetProperty("group", out JsonElement group))
        {
            c.Component(new JsonGroupComponent(String(block, "from"), b => Blocks(b, group, at + "/group")));
        }
        else if (block.TryGetProperty("list", out JsonElement list))
        {
            string style = String(list, "style") ?? string.Empty;
            c.List(new ListStyleKey(style), l => ListItems(l, Required(list, "items", at + "/list"), at + "/list/items"));
        }
        else if (block.TryGetProperty("box", out JsonElement box))
        {
            c.Box(new BoxStyleKey(String(box, "style") ?? string.Empty), b => Blocks(b, Required(box, "content", at + "/box"), at + "/box/content"));
        }
        else if (block.TryGetProperty("table", out JsonElement table))
        {
            c.Table(t => Table(t, table, at + "/table"));
        }
        else if (block.TryGetProperty("rule", out JsonElement rule))
        {
            at += "/rule";
            if (Border(rule, "line", at) is { } line)
            {
                c.Rule(line.Width, line.Color, Length(rule, "spaceAbove", at) ?? Builder.Length.Zero, Length(rule, "spaceBelow", at) ?? Builder.Length.Zero);
            }
        }
        else if (block.TryGetProperty("toc", out JsonElement toc))
        {
            c.TableOfContents(Int(toc, "maxLevel") ?? 3);
        }
        else if (block.TryGetProperty("raw", out JsonElement raw))
        {
            Raw(c, raw, at + "/raw");
        }
        else
        {
            Fail(at, $"Unknown block: {string.Join(", ", block.EnumerateObject().Select(p => "'" + p.Name + "'"))}.");
        }
    }

    private void Paragraph(BlockComposer c, JsonElement paragraph, string at)
    {
        if (paragraph.ValueKind == JsonValueKind.String)
        {
            c.Paragraph(paragraph.GetString()!);
            return;
        }

        ParagraphStyleKey? style = String(paragraph, "style") is { } id ? ParagraphStyle(id) : null;
        JsonElement content = Required(paragraph, "content", at);
        if (content.ValueKind == JsonValueKind.String)
        {
            if (style is null)
            {
                c.Paragraph(content.GetString()!);
            }
            else
            {
                c.Paragraph(style, content.GetString()!);
            }

            return;
        }

        if (style is null)
        {
            c.Paragraph(x => Inlines(x, content, at + "/content"));
        }
        else
        {
            c.Paragraph(style, x => Inlines(x, content, at + "/content"));
        }
    }

    private void ListItems(ListComposer l, JsonElement items, string at)
    {
        int i = 0;
        foreach (JsonElement item in Items(items, at, "A list of items is expected."))
        {
            string itemAt = at + "/" + i++;
            Guard(itemAt, () =>
            {
                if (item.TryGetProperty("item", out JsonElement content))
                {
                    if (content.ValueKind == JsonValueKind.String)
                    {
                        l.Item(content.GetString()!);
                        return;
                    }

                    int level = Int(content, "level") ?? 0;
                    JsonElement inlines = Required(content, "content", itemAt + "/item");
                    if (inlines.ValueKind == JsonValueKind.String)
                    {
                        l.Item(level, inlines.GetString()!);
                    }
                    else
                    {
                        l.Item(level, x => Inlines(x, inlines, itemAt + "/item/content"));
                    }
                }
                else if (item.TryGetProperty("forEach", out JsonElement collection))
                {
                    string name = String(item, "as") ?? string.Empty;
                    JsonElement body = Required(item, "items", itemAt);
                    l.ForEach(Path(collection, itemAt + "/forEach"), name, (loopItem, li) => InScope(name, loopItem, () => ListItems(li, body, itemAt + "/items")));
                }
                else if (item.TryGetProperty("if", out JsonElement condition))
                {
                    ListIfChain chain = l.If(Condition(condition, itemAt + "/if"), li => ListItems(li, Required(item, "items", itemAt), itemAt + "/items"));
                    int b = 0;
                    foreach (JsonElement branch in Array(item, "elseIf"))
                    {
                        string branchAt = itemAt + "/elseIf/" + b++;
                        chain = chain.ElseIf(Condition(Required(branch, "when", branchAt), branchAt + "/when"), li => ListItems(li, Required(branch, "items", branchAt), branchAt + "/items"));
                    }

                    if (item.TryGetProperty("else", out JsonElement otherwise))
                    {
                        chain.Else(li => ListItems(li, otherwise, itemAt + "/else"));
                    }
                }
                else
                {
                    Fail(itemAt, "Unknown list item: expected 'item', 'forEach' or 'if'.");
                }
            });
        }
    }

    private void Raw(BlockComposer c, JsonElement raw, string at)
    {
        if (!_options.AllowRawXml)
        {
            _context.Error(DiagnosticCodes.RawXmlNotAllowed, "Raw XML is not allowed in this import (TemplateJsonImportOptions.AllowRawXml).", at);
            return;
        }

        XElement docx = WithoutNamespaceDeclarations(XElement.Parse(String(raw, "docx") ?? string.Empty));
        XElement odt = WithoutNamespaceDeclarations(XElement.Parse(String(raw, "odt") ?? string.Empty));
        List<TemplatePath> uses = new List<TemplatePath>();
        int i = 0;
        foreach (JsonElement use in Array(raw, "uses"))
        {
            uses.Add(Path(use, at + "/uses/" + i++));
        }

        c.Raw(new RawBlock { Docx = _ => new XElement(docx), Odt = _ => new XElement(odt), Uses = uses });
    }

    /// <summary>
    /// The element as code would build it: parsed XML carries its <c>xmlns</c> declarations as attributes, which the
    /// writer would then repeat inside the part; without them the part's own prefixes apply, as for a C# raw block.
    /// </summary>
    private static XElement WithoutNamespaceDeclarations(XElement element)
    {
        foreach (XElement e in element.DescendantsAndSelf())
        {
            e.Attributes().Where(a => a.IsNamespaceDeclaration).Remove();
        }

        return element;
    }

    // ── tables ───────────────────────────────────────────────────────────────

    private void Table(TableComposer t, JsonElement table, string at)
    {
        if (String(table, "style") is { } style)
        {
            t.Style(new TableStyleKey(style));
        }

        if (table.TryGetProperty("columns", out JsonElement columns))
        {
            t.Columns(cols =>
            {
                int i = 0;
                foreach (JsonElement column in columns.EnumerateArray())
                {
                    string columnAt = at + "/columns/" + i++;
                    if (column.ValueKind == JsonValueKind.Number)
                    {
                        cols.Relative(column.GetDouble());
                    }
                    else if (JsonValues.TryLength(column.GetString(), out Length width))
                    {
                        cols.Fixed(width);
                    }
                    else
                    {
                        Fail(columnAt, "A column is a relative weight (a number) or a fixed width (\"30mm\").");
                    }
                }
            });
        }

        Apply(Length(table, "spaceAfter", at), v => t.SpaceAfter(v));
        Rows(new RowTarget(t.Row, t.ForEachRow, t.RowsIf, t.HeaderRow), Required(table, "rows", at), at + "/rows");
    }

    private sealed record RowTarget(
        Action<Action<RowComposer>> Row,
        Action<TemplatePath, string, Action<LoopItem, RowsComposer>> ForEachRow,
        Func<Condition, Action<RowsComposer>, RowsIfChain> RowsIf,
        Action<Action<RowComposer>>? HeaderRow);

    private static RowTarget Target(RowsComposer rows) => new RowTarget(rows.Row, rows.ForEachRow, rows.RowsIf, null);

    private void Rows(RowTarget target, JsonElement rows, string at)
    {
        int i = 0;
        foreach (JsonElement item in Items(rows, at, "A list of rows is expected."))
        {
            string rowAt = at + "/" + i++;
            Guard(rowAt, () =>
            {
                if (item.TryGetProperty("row", out JsonElement row))
                {
                    bool header = Bool(row, "header") == true;
                    Action<RowComposer> cells = r => Row(r, row, rowAt + "/row");
                    if (header)
                    {
                        if (target.HeaderRow is null)
                        {
                            Fail(rowAt, "A header row cannot be inside a row loop or row conditional.");
                            return;
                        }

                        target.HeaderRow(cells);
                    }
                    else
                    {
                        target.Row(cells);
                    }
                }
                else if (item.TryGetProperty("forEachRow", out JsonElement collection))
                {
                    string name = String(item, "as") ?? string.Empty;
                    JsonElement body = Required(item, "rows", rowAt);
                    target.ForEachRow(Path(collection, rowAt + "/forEachRow"), name, (loopItem, inner) => InScope(name, loopItem, () => Rows(Target(inner), body, rowAt + "/rows")));
                }
                else if (item.TryGetProperty("rowsIf", out JsonElement condition))
                {
                    RowsIfChain chain = target.RowsIf(Condition(condition, rowAt + "/rowsIf"), inner => Rows(Target(inner), Required(item, "then", rowAt), rowAt + "/then"));
                    int b = 0;
                    foreach (JsonElement branch in Array(item, "elseIf"))
                    {
                        string branchAt = rowAt + "/elseIf/" + b++;
                        chain = chain.ElseIf(Condition(Required(branch, "when", branchAt), branchAt + "/when"), inner => Rows(Target(inner), Required(branch, "then", branchAt), branchAt + "/then"));
                    }

                    if (item.TryGetProperty("else", out JsonElement otherwise))
                    {
                        chain.Else(inner => Rows(Target(inner), otherwise, rowAt + "/else"));
                    }
                }
                else
                {
                    Fail(rowAt, "Unknown row: expected 'row', 'forEachRow' or 'rowsIf'.");
                }
            });
        }
    }

    private void Row(RowComposer r, JsonElement row, string at)
    {
        if (Bool(row, "allowSplit") == true)
        {
            r.AllowSplit();
        }

        int i = 0;
        foreach (JsonElement cell in Array(row, "cells"))
        {
            string cellAt = at + "/cells/" + i++;
            r.Cell(c =>
            {
                if (Int(cell, "span") is { } span)
                {
                    c.ColumnSpan(span);
                }

                Apply(Color(cell, "background", cellAt), v => c.Background(v));
                if (String(cell, "align") is { } align)
                {
                    c.VerticalAlignment(Enum<CellVerticalAlignment>(align, cellAt + "/align"));
                }

                Blocks(c, Required(cell, "content", cellAt), cellAt + "/content");
            });
        }
    }

    // ── inlines ──────────────────────────────────────────────────────────────

    private void Inlines(InlineComposer x, JsonElement inlines, string at)
    {
        int i = 0;
        foreach (JsonElement inline in Items(inlines, at, "A list of inlines is expected."))
        {
            string inlineAt = at + "/" + i++;
            Guard(inlineAt, () => Inline(x, inline, inlineAt));
        }
    }

    private void Inline(InlineComposer x, JsonElement inline, string at)
    {
        CharacterStyleKey? style = String(inline, "style") is { } id ? CharacterStyle(id) : null;
        if (inline.TryGetProperty("text", out JsonElement text))
        {
            if (style is null)
            {
                x.Text(text.GetString()!);
            }
            else
            {
                x.Text(text.GetString()!, style);
            }
        }
        else if (inline.TryGetProperty("value", out JsonElement value))
        {
            TemplatePath path = Path(value, at + "/value");
            ValueFormat? format = String(inline, "format") is { } specifier ? ValueFormat.Custom(specifier) : null;
            switch (format, style)
            {
                case (null, null):
                    x.Value(path);
                    break;
                case (null, { } s):
                    x.Value(path, s);
                    break;
                case ({ } f, null):
                    x.Value(path, f);
                    break;
                default:
                    x.Value(path, format!, style!);
                    break;
            }
        }
        else if (inline.TryGetProperty("tab", out _))
        {
            x.Tab();
        }
        else if (inline.TryGetProperty("lineBreak", out _))
        {
            x.LineBreak();
        }
        else if (inline.TryGetProperty("tabRight", out _))
        {
            x.TabRight();
        }
        else if (inline.TryGetProperty("pageNumber", out _))
        {
            if (style is null)
            {
                x.PageNumber();
            }
            else
            {
                x.PageNumber(style);
            }
        }
        else if (inline.TryGetProperty("pageCount", out _))
        {
            if (style is null)
            {
                x.PageCount();
            }
            else
            {
                x.PageCount(style);
            }
        }
        else if (inline.TryGetProperty("image", out JsonElement image))
        {
            if (Asset(image, at + "/image") is { } data && Length(inline, "width", at) is { } width && Length(inline, "height", at) is { } height)
            {
                x.Image(ImageSource.FromBytes(data), String(inline, "alt") ?? string.Empty, ImageFit.Exact(width, height));
            }
        }
        else if (inline.TryGetProperty("expression", out JsonElement expression))
        {
            x.Expression(Condition(expression, at + "/expression"), ValueFormat.Custom(String(inline, "format") ?? throw new JsonImportException(at, "'format' is missing.")));
        }
        else if (inline.TryGetProperty("if", out JsonElement condition))
        {
            InlineIfChain chain = x.If(Condition(condition, at + "/if"), t => TextInlines(t, Required(inline, "then", at), at + "/then"));
            int i = 0;
            foreach (JsonElement branch in Array(inline, "elseIf"))
            {
                string branchAt = at + "/elseIf/" + i++;
                chain = chain.ElseIf(Condition(Required(branch, "when", branchAt), branchAt + "/when"), t => TextInlines(t, Required(branch, "then", branchAt), branchAt + "/then"));
            }

            if (inline.TryGetProperty("else", out JsonElement otherwise))
            {
                chain.Else(t => TextInlines(t, otherwise, at + "/else"));
            }
        }
        else
        {
            Fail(at, $"Unknown inline: {string.Join(", ", inline.EnumerateObject().Select(p => "'" + p.Name + "'"))}.");
        }
    }

    /// <summary>The inlines of an inline conditional: text, values and nested conditionals.</summary>
    private void TextInlines(TextComposer t, JsonElement inlines, string at)
    {
        int i = 0;
        foreach (JsonElement inline in Items(inlines, at, "A list of inlines is expected."))
        {
            string inlineAt = at + "/" + i++;
            Guard(inlineAt, () =>
            {
                CharacterStyleKey? style = String(inline, "style") is { } id ? CharacterStyle(id) : null;
                if (inline.TryGetProperty("text", out JsonElement text))
                {
                    _ = style is null ? t.Text(text.GetString()!) : t.Text(text.GetString()!, style);
                }
                else if (inline.TryGetProperty("value", out JsonElement value))
                {
                    TemplatePath path = Path(value, inlineAt + "/value");
                    _ = String(inline, "format") is { } specifier
                        ? t.Value(path, ValueFormat.Custom(specifier))
                        : style is null ? t.Value(path) : t.Value(path, style);
                }
                else if (inline.TryGetProperty("if", out JsonElement condition))
                {
                    InlineIfChain chain = t.If(Condition(condition, inlineAt + "/if"), inner => TextInlines(inner, Required(inline, "then", inlineAt), inlineAt + "/then"));
                    int b = 0;
                    foreach (JsonElement branch in Array(inline, "elseIf"))
                    {
                        string branchAt = inlineAt + "/elseIf/" + b++;
                        chain = chain.ElseIf(Condition(Required(branch, "when", branchAt), branchAt + "/when"), inner => TextInlines(inner, Required(branch, "then", branchAt), branchAt + "/then"));
                    }

                    if (inline.TryGetProperty("else", out JsonElement otherwise))
                    {
                        chain.Else(inner => TextInlines(inner, otherwise, inlineAt + "/else"));
                    }
                }
                else
                {
                    Fail(inlineAt, "Inside an inline conditional only 'text', 'value' and 'if' are allowed.");
                }
            });
        }
    }

    // ── paths, conditions, scope ─────────────────────────────────────────────

    private void InScope(string name, LoopItem item, Action compose)
    {
        _scope.Add((name, item));
        try
        {
            compose();
        }
        finally
        {
            _scope.RemoveAt(_scope.Count - 1);
        }
    }

    /// <summary>
    /// A path as written in JSON: a first segment naming an enclosing loop's item reads from that item, <c>@index</c>
    /// and its kin from the innermost loop, anything else from the data root — as the fluent API would build it.
    /// </summary>
    internal TemplatePath ResolvePath(string text)
    {
        if (text.StartsWith('@'))
        {
            if (_scope.Count == 0)
            {
                throw new ArgumentException($"'{text}' is loop metadata and can only be used inside a loop.");
            }

            return TemplatePath.ForMetadata(_scope[^1].Item.Scope, text[1..]);
        }

        int dot = text.IndexOf('.', StringComparison.Ordinal);
        string first = dot < 0 ? text : text[..dot];
        for (int i = _scope.Count - 1; i >= 0; i--)
        {
            if (_scope[i].Name == first)
            {
                return dot < 0 ? _scope[i].Item.Self : _scope[i].Item[text[(dot + 1)..]];
            }
        }

        return _template.Data[text];
    }

    private TemplatePath Path(JsonElement path, string at)
    {
        if (path.ValueKind != JsonValueKind.String)
        {
            throw new JsonImportException(at, "A path is a string such as \"order.customer.name\".");
        }

        return ResolvePath(path.GetString()!);
    }

    private Condition Condition(JsonElement condition, string at)
    {
        if (condition.ValueKind == JsonValueKind.Object && condition.TryGetProperty("raw", out JsonElement raw))
        {
            List<TemplatePath> uses = new List<TemplatePath>();
            int i = 0;
            foreach (JsonElement use in Array(condition, "uses"))
            {
                uses.Add(Path(use, at + "/uses/" + i++));
            }

            return Builder.Condition.Raw(raw.GetString() ?? string.Empty, uses.ToArray());
        }

        if (condition.ValueKind != JsonValueKind.String)
        {
            throw new JsonImportException(at, "A condition is a string such as \"order.total > 1000\".");
        }

        try
        {
            return ConditionParser.Parse(condition.GetString()!, ResolvePath);
        }
        catch (FormatException exception)
        {
            throw new JsonImportException(at, exception.Message);
        }
    }

    // ── value helpers ────────────────────────────────────────────────────────

    private ParagraphStyleKey ParagraphStyle(string id) =>
        _paragraphStyles.TryGetValue(id, out ParagraphStyleKey? key) ? key
        : id == BuilderStyles.TemplateMarker.Id ? BuilderStyles.TemplateMarker
        : new ParagraphStyleKey(id, id);

    private CharacterStyleKey CharacterStyle(string id) =>
        _characterStyles.TryGetValue(id, out CharacterStyleKey? key) ? key : new CharacterStyleKey(id, id);

    private FontFace? Font(JsonElement owner, string at)
    {
        if (!owner.TryGetProperty("font", out JsonElement font))
        {
            return null;
        }

        if (font.ValueKind == JsonValueKind.String)
        {
            return new FontFace(font.GetString()!);
        }

        string family = String(font, "family") ?? string.Empty;
        return font.TryGetProperty("embedded", out JsonElement embedded) && Asset(embedded, at + "/font/embedded") is { } data
            ? FontFace.Embedded(family, data)
            : null;
    }

    private byte[]? Asset(JsonElement key, string at)
    {
        string? name = key.ValueKind == JsonValueKind.String ? key.GetString() : null;
        if (name != null && _options.Assets is { } assets && assets.TryGetValue(name, out byte[]? data))
        {
            return data;
        }

        Fail(at, $"The asset '{name}' is not in TemplateJsonImportOptions.Assets.");
        return null;
    }

    private Length? Length(JsonElement owner, string name, string at)
    {
        if (!owner.TryGetProperty(name, out JsonElement value))
        {
            return null;
        }

        if (JsonValues.TryLength(value.ValueKind == JsonValueKind.String ? value.GetString() : null, out Length length))
        {
            return length;
        }

        Fail(at + "/" + name, $"{value} is not a length such as \"10pt\" or \"25mm\".");
        return null;
    }

    private Color? Color(JsonElement owner, string name, string at)
    {
        if (!owner.TryGetProperty(name, out JsonElement value))
        {
            return null;
        }

        if (JsonValues.TryColor(value.ValueKind == JsonValueKind.String ? value.GetString() : null, out Color color))
        {
            return color;
        }

        Fail(at + "/" + name, $"{value} is not a colour such as \"#1F4E79\".");
        return null;
    }

    private Model.BorderLine? Border(JsonElement owner, string name, string at)
    {
        if (!owner.TryGetProperty(name, out JsonElement value))
        {
            return null;
        }

        if (JsonValues.TryBorder(value.ValueKind == JsonValueKind.String ? value.GetString() : null, out Model.BorderLine? line))
        {
            return line;
        }

        Fail(at + "/" + name, $"{value} is not a line such as \"0.5pt #D4D4D4\".");
        return null;
    }

    private (Length Top, Length Right, Length Bottom, Length Left)? Box(JsonElement owner, string name, string at)
    {
        if (!owner.TryGetProperty(name, out JsonElement value))
        {
            return null;
        }

        if (JsonValues.TryBox(value.ValueKind == JsonValueKind.String ? value.GetString() : null, out (Length, Length, Length, Length) box))
        {
            return box;
        }

        Fail(at + "/" + name, $"{value} is not one, two or four lengths.");
        return null;
    }

    private T Enum<T>(string text, string at)
        where T : struct, System.Enum =>
        System.Enum.TryParse(text, ignoreCase: true, out T value) && System.Enum.IsDefined(value)
            ? value
            : throw new JsonImportException(at, $"'{text}' is not one of {string.Join(", ", System.Enum.GetNames<T>().Select(n => char.ToLowerInvariant(n[0]) + n[1..]))}.");

    private static string? String(JsonElement owner, string name) =>
        owner.ValueKind == JsonValueKind.Object && owner.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static bool? Bool(JsonElement owner, string name) =>
        owner.ValueKind == JsonValueKind.Object && owner.TryGetProperty(name, out JsonElement value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False ? value.GetBoolean() : null;

    private static int? Int(JsonElement owner, string name) =>
        owner.ValueKind == JsonValueKind.Object && owner.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int number) ? number : null;

    private static IEnumerable<JsonElement> Array(JsonElement owner, string name) =>
        owner.ValueKind == JsonValueKind.Object && owner.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.Array ? value.EnumerateArray() : [];

    private static IEnumerable<JsonProperty> Properties(JsonElement owner, string name) =>
        owner.ValueKind == JsonValueKind.Object && owner.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.Object ? value.EnumerateObject() : [];

    private static JsonElement Required(JsonElement owner, string name, string at) =>
        owner.ValueKind == JsonValueKind.Object && owner.TryGetProperty(name, out JsonElement value)
            ? value
            : throw new JsonImportException(at, $"'{name}' is missing.");

    private static void Apply<T>(T? value, Action<T> apply)
        where T : struct
    {
        if (value is { } v)
        {
            apply(v);
        }
    }

    private static void Apply<T>(T? value, Action<T> apply)
        where T : class
    {
        if (value is not null)
        {
            apply(value);
        }
    }

    /// <summary>RFC 6901: '~' and '/' in a key.</summary>
    internal static string Escape(string key) => key.Replace("~", "~0", StringComparison.Ordinal).Replace("/", "~1", StringComparison.Ordinal);

    private void Fail(string at, string message) => _context.Error(DiagnosticCodes.InvalidJson, message, at);

    private IEnumerable<JsonElement> Items(JsonElement list, string at, string message)
    {
        if (list.ValueKind == JsonValueKind.Array)
        {
            return list.EnumerateArray();
        }

        Fail(at, message);
        return [];
    }

    /// <summary>Composes one element; a structural problem in it is reported at its pointer and the element is skipped.</summary>
    private void Guard(string at, Action compose)
    {
        try
        {
            compose();
        }
        catch (JsonImportException exception)
        {
            Fail(exception.Location, exception.Message);
        }
    }
}

/// <summary>A structural problem found while composing a JSON definition; caught per element.</summary>
internal sealed class JsonImportException(string location, string message) : Exception(message)
{
    public string Location { get; } = location;
}

/// <summary>A group of blocks read from JSON that was a component in C#; keeps the component's type name for re-export.</summary>
internal sealed class JsonGroupComponent(string? from, Action<BlockComposer> compose) : ITemplateComponent
{
    public string? From { get; } = from;

    public void Compose(BlockComposer content) => compose(content);
}
