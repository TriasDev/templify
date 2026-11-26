// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Text.Json;
using System.Xml;
using System.Xml.Linq;

namespace TriasDev.Templify.Builder.Json;

/// <summary>
/// A definition read from JSON. <see cref="Compose"/> replays the JSON through the same composers the fluent API
/// uses, so every rule is checked once and reported like a C# definition's, with the JSON pointer as location.
/// </summary>
/// <remarks>
/// Problems are reported, never thrown: a malformed element (unknown key or kind, wrong value type, unknown asset)
/// is TB9200 at its pointer, a value a composer rejects is TB9201, raw XML without permission TB9202. The element is
/// skipped and composing goes on, so one build lists every problem.
/// </remarks>
internal sealed class JsonDefinition(JsonElement root, TemplateIdentity identity, TemplateJsonImportOptions options) : ITemplateDefinition
{
    public TemplateIdentity Identity { get; } = identity;

    /// <summary>Gets the definition's own translations (its <c>texts</c> section).</summary>
    internal EmbeddedTexts Texts { get; } = EmbeddedTexts.Read(root);

    /// <summary>Composes through a fresh <see cref="JsonComposition"/>: the definition holds no per-build state, so one import can be built concurrently.</summary>
    public void Compose(TemplateComposer template) => new JsonComposition(root, Identity, options, Texts).Compose(template);
}

/// <summary>One composition of a JSON definition: the loop scope and diagnostics of a single build.</summary>
internal sealed class JsonComposition
{
    private readonly JsonElement _source;
    private readonly TemplateJsonImportOptions _options;
    private readonly EmbeddedTexts _texts;

    /// <summary>The parameters of a component reference without <c>params</c>.</summary>
    private static readonly JsonElement NoParameters = JsonDocument.Parse("{}").RootElement.Clone();
    private JsonElement _root;
    private readonly Dictionary<string, ParagraphStyleKey> _paragraphStyles = new Dictionary<string, ParagraphStyleKey>(StringComparer.Ordinal);
    private readonly Dictionary<string, CharacterStyleKey> _characterStyles = new Dictionary<string, CharacterStyleKey>(StringComparer.Ordinal);
    private readonly List<(string Name, LoopItem Item)> _scope = new List<(string, LoopItem)>();
    private readonly HashSet<string> _sectionNames = new HashSet<string>(StringComparer.Ordinal);
    private readonly Dictionary<string, byte[]> _assetsByName = new Dictionary<string, byte[]>(StringComparer.Ordinal);
    private readonly Dictionary<string, Builder.Color> _themeColors = new Dictionary<string, Builder.Color>(StringComparer.Ordinal);
    private readonly Dictionary<string, FontFace> _themeFonts = new Dictionary<string, FontFace>(StringComparer.Ordinal);
    private BuildContext _context = null!;
    private TemplateComposer _template = null!;

    public JsonComposition(JsonElement root, TemplateIdentity identity, TemplateJsonImportOptions options, EmbeddedTexts texts)
    {
        _texts = texts;
        _source = root;
        _root = root;
        Identity = identity;
        _options = options;
        if (_root.TryGetProperty("styles", out JsonElement styles) && styles.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty style in Members(styles, "paragraph"))
            {
                _paragraphStyles[style.Name] = style.Name == BuilderStyles.TemplateMarker.Id
                    ? BuilderStyles.TemplateMarker
                    : new ParagraphStyleKey(style.Name, PlainString(style.Value, "name") ?? style.Name);
            }

            foreach (JsonProperty style in Members(styles, "character"))
            {
                _characterStyles[style.Name] = new CharacterStyleKey(style.Name, PlainString(style.Value, "name") ?? style.Name);
            }
        }
    }

    public TemplateIdentity Identity { get; }

    public void Compose(TemplateComposer template)
    {
        _template = template;
        _context = template.Context;
        _scope.Clear();
        _sectionNames.Clear();
        foreach (object shared in _options.Shared ?? [])
        {
            _context.Shared[shared.GetType()] = shared;
        }

        // Components read c.Texts: the JSON's texts first, then the catalog, like [[key]] in the JSON itself.
        _context.Texts = template.Texts.Preferring(_texts.Catalog);

        _root = WithTranslations(_source);

        Keys(_root, string.Empty, "$schema", "templifyDefinition", "identity", "texts", "assets", "theme", "properties", "styles", "page", "content", "components");
        ReadAssets();
        ReadTheme();
        if (_root.TryGetProperty("components", out _))
        {
            Fail("/components", "The root key 'components' is reserved for components defined inside the definition (a later version).");
        }
        _ = EmbeddedTexts.Read(_root, Fail);
        if (_root.TryGetProperty("styles", out JsonElement styles))
        {
            Guard("/styles", () =>
            {
                if (Keys(styles, "/styles", "defaults", "paragraph", "character", "table", "box", "list"))
                {
                    template.Styles(StyleSheet.Create(s => Styles(s, styles)));
                }
            });
        }

        if (_root.TryGetProperty("properties", out JsonElement properties))
        {
            Guard("/properties", () =>
            {
                Keys(properties, "/properties", "title", "author", "subject");
                template.Properties(p =>
                {
                    Apply(String(properties, "title", "/properties"), v => p.Title(v));
                    Apply(String(properties, "author", "/properties"), v => p.Author(v));
                    Apply(String(properties, "subject", "/properties"), v => p.Subject(v));
                });
            });
        }

        if (_root.TryGetProperty("page", out JsonElement page))
        {
            Guard("/page", () => template.Page(p => Page(p, page, "/page")));
        }

        if (_root.TryGetProperty("content", out JsonElement content))
        {
            template.Content(c => Blocks(c, content, "/content"));
        }

        _context.CurrentLocation = null;
    }

    // ── styles ───────────────────────────────────────────────────────────────

    private void Styles(StyleSheetComposer s, JsonElement styles)
    {
        if (styles.TryGetProperty("defaults", out JsonElement defaults))
        {
            const string at = "/styles/defaults";
            Guard(at, () =>
            {
                Keys(defaults, at, "font", "size", "color", "paragraphStyle");
                s.Defaults(d =>
                {
                    Apply(Font(defaults, at), v => d.Font(v));
                    Apply(Length(defaults, "size", at), v => d.Size(v));
                    Apply(Color(defaults, "color", at), v => d.Color(v));
                    Apply(String(defaults, "paragraphStyle", at), v => d.ParagraphStyle(ParagraphStyle(v)));
                });
            });
        }

        foreach (JsonProperty style in Members(styles, "paragraph"))
        {
            JsonElement e = style.Value;
            string at = "/styles/paragraph/" + Escape(style.Name);
            Guard(at, () =>
            {
                Keys(e, at, "name", "basedOn", "next", "font", "size", "color", "bold", "italic", "spaceBefore", "spaceAfter", "lineSpacing",
                    "lineHeight", "minimumLineHeight", "alignment", "indentStart", "indentEnd", "firstLineIndent", "keepWithNext", "keepLinesTogether", "outlineLevel");
                s.Paragraph(_paragraphStyles[style.Name], p =>
                {
                    Apply(String(e, "basedOn", at), v => p.BasedOn(ParagraphStyle(v)));
                    Apply(String(e, "next", at), v => p.Next(ParagraphStyle(v)));
                    Apply(Font(e, at), v => p.Font(v));
                    Apply(Length(e, "size", at), v => p.Size(v));
                    Apply(Color(e, "color", at), v => p.Color(v));
                    Apply(Bool(e, "bold", at), v => p.Bold(v));
                    Apply(Bool(e, "italic", at), v => p.Italic(v));
                    Apply(Length(e, "spaceBefore", at), v => p.SpaceBefore(v));
                    Apply(Length(e, "spaceAfter", at), v => p.SpaceAfter(v));
                    Apply(Number(e, "lineSpacing", at), v => p.LineSpacing(v));
                    Apply(Length(e, "lineHeight", at), v => p.LineHeight(v));
                    Apply(Length(e, "minimumLineHeight", at), v => p.MinimumLineHeight(v));
                    Apply(String(e, "alignment", at), v => p.Alignment(Enum<TextAlignment>(v, at + "/alignment")));
                    Apply(Length(e, "indentStart", at), v => p.IndentStart(v));
                    Apply(Length(e, "indentEnd", at), v => p.IndentEnd(v));
                    Apply(Length(e, "firstLineIndent", at), v => p.FirstLineIndent(v));
                    Apply(Bool(e, "keepWithNext", at), v => p.KeepWithNext(v));
                    Apply(Bool(e, "keepLinesTogether", at), v => p.KeepLinesTogether(v));
                    Apply(Int(e, "outlineLevel", at), v => p.OutlineLevel(v));
                });
            });
        }

        foreach (JsonProperty style in Members(styles, "character"))
        {
            JsonElement e = style.Value;
            string at = "/styles/character/" + Escape(style.Name);
            Guard(at, () =>
            {
                Keys(e, at, "name", "basedOn", "font", "size", "color", "bold", "italic");
                s.Character(_characterStyles[style.Name], c =>
                {
                    Apply(String(e, "basedOn", at), v => c.BasedOn(CharacterStyle(v)));
                    Apply(Font(e, at), v => c.Font(v));
                    Apply(Length(e, "size", at), v => c.Size(v));
                    Apply(Color(e, "color", at), v => c.Color(v));
                    Apply(Bool(e, "bold", at), v => c.Bold(v));
                    Apply(Bool(e, "italic", at), v => c.Italic(v));
                });
            });
        }

        foreach (JsonProperty style in Members(styles, "table"))
        {
            JsonElement e = style.Value;
            string at = "/styles/table/" + Escape(style.Name);
            Guard(at, () =>
            {
                Keys(e, at, "outerBorder", "rowRule", "columnRule", "headerRule", "headerBackground", "cellPadding", "paragraphStyle", "headerParagraphStyle");
                s.Table(new TableStyleKey(style.Name), t =>
                {
                    Apply(Border(e, "outerBorder", at), v => t.OuterBorder(v.Width, v.Color));
                    Apply(Border(e, "rowRule", at), v => t.RowRule(v.Width, v.Color));
                    Apply(Border(e, "columnRule", at), v => t.ColumnRule(v.Width, v.Color));
                    Apply(Border(e, "headerRule", at), v => t.HeaderRule(v.Width, v.Color));
                    Apply(Color(e, "headerBackground", at), v => t.HeaderBackground(v));
                    if (Padding(e, "cellPadding", at) is { } padding)
                    {
                        t.CellPadding(padding.Top, padding.Right);
                    }

                    Apply(String(e, "paragraphStyle", at), v => t.ParagraphStyle(ParagraphStyle(v)));
                    Apply(String(e, "headerParagraphStyle", at), v => t.HeaderParagraphStyle(ParagraphStyle(v)));
                });
            });
        }

        foreach (JsonProperty style in Members(styles, "box"))
        {
            JsonElement e = style.Value;
            string at = "/styles/box/" + Escape(style.Name);
            Guard(at, () =>
            {
                Keys(e, at, "border", "background", "padding", "spaceAfter");
                s.Box(new BoxStyleKey(style.Name), b =>
                {
                    Apply(Border(e, "border", at), v => b.Border(v.Width, v.Color));
                    Apply(Color(e, "background", at), v => b.Background(v));
                    if (Padding(e, "padding", at) is { } padding)
                    {
                        b.Padding(padding.Top, padding.Right);
                    }

                    Apply(Length(e, "spaceAfter", at), v => b.SpaceAfter(v));
                });
            });
        }

        foreach (JsonProperty style in Members(styles, "list"))
        {
            JsonElement e = style.Value;
            string at = "/styles/list/" + Escape(style.Name);
            Guard(at, () =>
            {
                Keys(e, at, "itemStyle", "levels");
                s.List(new ListStyleKey(style.Name), l =>
                {
                    Apply(String(e, "itemStyle", at), v => l.ItemStyle(ParagraphStyle(v)));
                    int i = 0;
                    foreach (JsonElement level in Array(e, "levels", at))
                    {
                        string levelAt = at + "/levels/" + i++;
                        Guard(levelAt, () =>
                        {
                            Keys(level, levelAt, "level", "bullet", "indent", "hanging");
                            l.Level(
                                Int(level, "level", levelAt) ?? 0,
                                String(level, "bullet", levelAt) ?? throw Missing(levelAt, "bullet"),
                                Length(level, "indent", levelAt) ?? throw Missing(levelAt, "indent"),
                                Length(level, "hanging", levelAt) ?? throw Missing(levelAt, "hanging"));
                        });
                    }
                });
            });
        }
    }

    // ── page ─────────────────────────────────────────────────────────────────

    private void Page(PageComposer p, JsonElement page, string at)
    {
        Keys(page, at, "size", "margins", "headerFooterDistance", "header", "footer", "firstPage");
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
            else if (Keys(size, at + "/size", "width", "height")
                && Length(size, "width", at + "/size") is { } width && Length(size, "height", at + "/size") is { } height)
            {
                p.Size(new PageSize(width, height));
            }
        }

        if (Box(page, "margins", at) is { } margins)
        {
            p.Margins(margins.Top, margins.Right, margins.Bottom, margins.Left);
        }

        if (String(page, "headerFooterDistance", at) is { } distances)
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

        if (page.TryGetProperty("firstPage", out JsonElement first) && Keys(first, at + "/firstPage", "header", "footer"))
        {
            p.FirstPage(f =>
            {
                if (first.TryGetProperty("header", out JsonElement firstHeader))
                {
                    if (IsNone(firstHeader))
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
                    if (IsNone(firstFooter))
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

    private static bool IsNone(JsonElement element) => element.ValueKind == JsonValueKind.String && element.GetString() == "none";

    // ── blocks ───────────────────────────────────────────────────────────────

    private void Blocks(BlockComposer c, JsonElement blocks, string at)
    {
        int i = 0;
        foreach (JsonElement block in Items(blocks, at, "a list of blocks ([ … ])"))
        {
            string blockAt = at + "/" + i++;
            int before = c.Blocks.Count;
            Guard(blockAt, () => Block(c, block, blockAt));
            for (int added = before; added < c.Blocks.Count; added++)
            {
                _context.Locations.AddOrUpdate(c.Blocks[added], blockAt);
            }
        }
    }

    private void Block(BlockComposer c, JsonElement block, string at)
    {
        if (block.ValueKind == JsonValueKind.String)
        {
            Paragraph(c, null, block, at);
            return;
        }

        if (block.ValueKind != JsonValueKind.Object)
        {
            throw new JsonImportException(at, "A block is a string (a paragraph) or an object such as { \"paragraph\": … }.");
        }

        if (block.TryGetProperty("paragraph", out JsonElement paragraph))
        {
            Keys(block, at, "style", "paragraph");
            Paragraph(c, String(block, "style", at), paragraph, at + "/paragraph");
        }
        else if (block.TryGetProperty("pageBreak", out _))
        {
            Keys(block, at, "pageBreak");
            Flag(block, "pageBreak", at);
            c.PageBreak();
        }
        else if (block.TryGetProperty("if", out JsonElement condition))
        {
            Keys(block, at, "if", "then", "elseIf", "else");
            IfChain chain = c.If(Condition(condition, at + "/if"), b => Blocks(b, Required(block, "then", at), at + "/then"));
            int i = 0;
            foreach (JsonElement branch in Array(block, "elseIf", at))
            {
                string branchAt = at + "/elseIf/" + i++;
                Keys(branch, branchAt, "if", "then");
                chain = chain.ElseIf(Condition(Required(branch, "if", branchAt), branchAt + "/if"), b => Blocks(b, Required(branch, "then", branchAt), branchAt + "/then"));
            }

            if (block.TryGetProperty("else", out JsonElement otherwise))
            {
                chain.Else(b => Blocks(b, otherwise, at + "/else"));
            }
        }
        else if (block.TryGetProperty("forEach", out JsonElement collection))
        {
            Keys(block, at, "forEach", "as", "body");
            string name = String(block, "as", at) ?? throw Missing(at, "as");
            JsonElement body = Required(block, "body", at);
            c.ForEach(Path(collection, at + "/forEach", DataKind.Collection), name, (item, b) => InScope(name, item, () => Blocks(b, body, at + "/body")));
        }
        else if (block.TryGetProperty("component", out JsonElement component))
        {
            Keys(block, at, "component", "params");
            string name = String(block, "component", at)!;
            ComponentCatalog.Registration registration = _options.Components?.Find(name)
                ?? throw new JsonImportException(at, $"The component '{name}' is not registered (TemplateJsonImportOptions.Components).");
            JsonElement parameters = block.TryGetProperty("params", out JsonElement given) ? given : NoParameters;
            c.Component(registration.Create(parameters, _options.Components!.DataNaming, (text, kind) => Resolve(text, at + "/params", kind), ParagraphStyle, CharacterStyle, at + "/params"));
        }
        else if (block.TryGetProperty("section", out _))
        {
            Keys(block, at, "section", "blocks");
            string name = String(block, "section", at) ?? throw Missing(at, "section");
            if (!SectionComponent.IsValidName(name))
            {
                throw new JsonImportException(at + "/section", $"'{name}' is not a section name: use letters, digits, '_', '.' or '-'.");
            }

            if (!_sectionNames.Add(name))
            {
                throw new JsonImportException(at + "/section", $"The section name '{name}' is used twice; names are unique in a definition.");
            }

            JsonElement blocks = Required(block, "blocks", at);
            c.Section(name, b => Blocks(b, blocks, at + "/blocks"));
        }
        else if (block.TryGetProperty("list", out JsonElement list))
        {
            Keys(block, at, "style", "list");
            string style = String(block, "style", at) ?? throw Missing(at, "style");
            c.List(new ListStyleKey(style), l => ListItems(l, list, at + "/list"));
        }
        else if (block.TryGetProperty("box", out JsonElement box))
        {
            Keys(block, at, "style", "box");
            string style = String(block, "style", at) ?? throw Missing(at, "style");
            c.Box(new BoxStyleKey(style), b => Blocks(b, box, at + "/box"));
        }
        else if (block.TryGetProperty("table", out _))
        {
            Keys(block, at, "style", "columns", "spaceAfter", "table");
            c.Table(t => Table(t, block, at));
        }
        else if (block.TryGetProperty("rule", out JsonElement rule))
        {
            Keys(block, at, "rule");
            at += "/rule";
            Keys(rule, at, "line", "spaceAbove", "spaceBelow");
            Model.BorderLine line = Border(rule, "line", at) ?? throw Missing(at, "line");
            c.Rule(line.Width, line.Color, Length(rule, "spaceAbove", at) ?? Builder.Length.Zero, Length(rule, "spaceBelow", at) ?? Builder.Length.Zero);
        }
        else if (block.TryGetProperty("toc", out JsonElement toc))
        {
            Keys(block, at, "toc");
            Keys(toc, at + "/toc", "maxLevel");
            c.TableOfContents(Int(toc, "maxLevel", at + "/toc") ?? 3);
        }
        else if (block.TryGetProperty("raw", out JsonElement raw))
        {
            Keys(block, at, "raw");
            Raw(c, raw, at + "/raw");
        }
        else
        {
            throw new JsonImportException(at, $"Unknown block {Names(block)}; expected a string (a paragraph), paragraph, pageBreak, if, forEach, component, section, list, box, table, rule, toc or raw.");
        }
    }

    /// <summary>A paragraph: its content is a string (text with placeholders) or a list of inlines.</summary>
    private void Paragraph(BlockComposer c, string? styleId, JsonElement content, string at)
    {
        if (content.ValueKind is not (JsonValueKind.String or JsonValueKind.Array))
        {
            throw new JsonImportException(at, "A paragraph holds a string or a list of inlines, for example { \"style\": \"Heading\", \"paragraph\": \"Title\" }.");
        }

        ParagraphStyleKey? style = styleId is { } id ? ParagraphStyle(id) : null;
        if (IsPlainText(content))
        {
            if (style is null)
            {
                c.Paragraph(content.GetString()!);
            }
            else
            {
                c.Paragraph(style, content.GetString()!);
            }
        }
        else if (style is null)
        {
            c.Paragraph(x => Inlines(x, content, at));
        }
        else
        {
            c.Paragraph(style, x => Inlines(x, content, at));
        }
    }

    private void ItemContent(ListComposer l, int level, JsonElement content, string at)
    {
        if (content.ValueKind is not (JsonValueKind.String or JsonValueKind.Array))
        {
            throw new JsonImportException(at, "A list item holds a string or a list of inlines.");
        }

        if (IsPlainText(content))
        {
            l.Item(level, content.GetString()!);
        }
        else
        {
            l.Item(level, x => Inlines(x, content, at));
        }
    }

    /// <summary>The content of a cell: a string (one paragraph) or a list of blocks.</summary>
    private void CellBlocks(BlockComposer c, JsonElement content, string at, string? style = null)
    {
        if (content.ValueKind == JsonValueKind.String)
        {
            int before = c.Blocks.Count;
            Guard(at, () => Paragraph(c, style, content, at));
            for (int added = before; added < c.Blocks.Count; added++)
            {
                _context.Locations.AddOrUpdate(c.Blocks[added], at);
            }

            return;
        }

        Blocks(c, content, at);
    }

    private void ListItems(ListComposer l, JsonElement items, string at)
    {
        int i = 0;
        foreach (JsonElement item in Items(items, at, "a list of items"))
        {
            string itemAt = at + "/" + i++;
            Guard(itemAt, () => ListItem(l, item, itemAt));
        }
    }

    private void ListItem(ListComposer l, JsonElement item, string at)
    {
        if (item.ValueKind == JsonValueKind.String)
        {
            ItemContent(l, 0, item, at);
        }
        else if (item.ValueKind != JsonValueKind.Object)
        {
            throw new JsonImportException(at, "A list item is a string, { \"level\": …, \"item\": … }, forEach or if.");
        }
        else if (item.TryGetProperty("item", out JsonElement content))
        {
            Keys(item, at, "level", "item");
            ItemContent(l, Int(item, "level", at) ?? 0, content, at + "/item");
        }
        else if (item.TryGetProperty("forEach", out JsonElement collection))
        {
            Keys(item, at, "forEach", "as", "items");
            string name = String(item, "as", at) ?? throw Missing(at, "as");
            JsonElement body = Required(item, "items", at);
            l.ForEach(Path(collection, at + "/forEach", DataKind.Collection), name, (loopItem, li) => InScope(name, loopItem, () => ListItems(li, body, at + "/items")));
        }
        else if (item.TryGetProperty("if", out JsonElement condition))
        {
            Keys(item, at, "if", "then", "elseIf", "else");
            ListIfChain chain = l.If(Condition(condition, at + "/if"), li => ListItems(li, Required(item, "then", at), at + "/then"));
            int b = 0;
            foreach (JsonElement branch in Array(item, "elseIf", at))
            {
                string branchAt = at + "/elseIf/" + b++;
                Keys(branch, branchAt, "if", "then");
                chain = chain.ElseIf(Condition(Required(branch, "if", branchAt), branchAt + "/if"), li => ListItems(li, Required(branch, "then", branchAt), branchAt + "/then"));
            }

            if (item.TryGetProperty("else", out JsonElement otherwise))
            {
                chain.Else(li => ListItems(li, otherwise, at + "/else"));
            }
        }
        else
        {
            throw new JsonImportException(at, $"Unknown list item {Names(item)}; expected item, forEach or if.");
        }
    }

    private void Raw(BlockComposer c, JsonElement raw, string at)
    {
        Keys(raw, at, "docx", "odt", "uses");
        if (!_options.AllowRawXml)
        {
            _context.Error(DiagnosticCodes.RawXmlNotAllowed, "Raw XML is not allowed in this import (TemplateJsonImportOptions.AllowRawXml).", at);
            return;
        }

        XElement docx = WithoutNamespaceDeclarations(XElement.Parse(String(raw, "docx", at) ?? throw Missing(at, "docx")));
        XElement odt = WithoutNamespaceDeclarations(XElement.Parse(String(raw, "odt", at) ?? throw Missing(at, "odt")));
        List<TemplatePath> uses = new List<TemplatePath>();
        int i = 0;
        foreach (JsonElement use in Array(raw, "uses", at))
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

    /// <summary>A table block: its options beside the <c>table</c> key, which holds the rows.</summary>
    private void Table(TableComposer t, JsonElement table, string at)
    {
        Apply(String(table, "style", at), v => t.Style(new TableStyleKey(v)));
        if (table.TryGetProperty("columns", out JsonElement columns))
        {
            t.Columns(cols =>
            {
                int i = 0;
                foreach (JsonElement column in Items(columns, at + "/columns", "a list of columns"))
                {
                    string columnAt = at + "/columns/" + i++;
                    Guard(columnAt, () =>
                    {
                        if (column.ValueKind == JsonValueKind.Number)
                        {
                            cols.Relative(column.GetDouble());
                        }
                        else if (JsonValues.TryLength(column.ValueKind == JsonValueKind.String ? column.GetString() : null, out Length width))
                        {
                            cols.Fixed(width);
                        }
                        else
                        {
                            Fail(columnAt, "A column is a relative weight (a number) or a fixed width (\"30mm\").");
                        }
                    });
                }
            });
        }

        Apply(Length(table, "spaceAfter", at), v => t.SpaceAfter(v));
        Rows(new RowTarget(r => t.Row(r), (c, n, r) => t.ForEachRow(c, n, r), t.RowsIf, r => t.HeaderRow(r)), Required(table, "table", at), at + "/table");
    }

    private sealed record RowTarget(
        Action<Action<RowComposer>> Row,
        Action<TemplatePath, string, Action<LoopItem, RowsComposer>> ForEachRow,
        Func<Condition, Action<RowsComposer>, RowsIfChain> RowsIf,
        Action<Action<RowComposer>>? HeaderRow);

    private static RowTarget Target(RowsComposer rows) => new RowTarget(r => rows.Row(r), (c, n, r) => rows.ForEachRow(c, n, r), rows.RowsIf, null);

    private void Rows(RowTarget target, JsonElement rows, string at)
    {
        int i = 0;
        foreach (JsonElement item in Items(rows, at, "a list of rows"))
        {
            string rowAt = at + "/" + i++;
            Guard(rowAt, () => RowItem(target, item, rowAt));
        }
    }

    private void RowItem(RowTarget target, JsonElement item, string at)
    {
        if (item.ValueKind != JsonValueKind.Object)
        {
            throw new JsonImportException(at, "A row item is an object: row, forEachRow or rowsIf.");
        }

        if (item.TryGetProperty("row", out _))
        {
            Keys(item, at, "header", "allowSplit", "paragraphStyle", "row");
            Action<RowComposer> cells = r => Row(r, item, at);
            if (Bool(item, "header", at) == true)
            {
                if (target.HeaderRow is null)
                {
                    throw new JsonImportException(at, "A header row cannot be inside a row loop or row conditional.");
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
            Keys(item, at, "forEachRow", "as", "rows");
            string name = String(item, "as", at) ?? throw Missing(at, "as");
            JsonElement body = Required(item, "rows", at);
            target.ForEachRow(Path(collection, at + "/forEachRow", DataKind.Collection), name, (loopItem, inner) => InScope(name, loopItem, () => Rows(Target(inner), body, at + "/rows")));
        }
        else if (item.TryGetProperty("rowsIf", out JsonElement condition))
        {
            Keys(item, at, "rowsIf", "then", "elseIf", "else");
            RowsIfChain chain = target.RowsIf(Condition(condition, at + "/rowsIf"), inner => Rows(Target(inner), Required(item, "then", at), at + "/then"));
            int b = 0;
            foreach (JsonElement branch in Array(item, "elseIf", at))
            {
                string branchAt = at + "/elseIf/" + b++;
                Keys(branch, branchAt, "if", "then");
                chain = chain.ElseIf(Condition(Required(branch, "if", branchAt), branchAt + "/if"), inner => Rows(Target(inner), Required(branch, "then", branchAt), branchAt + "/then"));
            }

            if (item.TryGetProperty("else", out JsonElement otherwise))
            {
                chain.Else(inner => Rows(Target(inner), otherwise, at + "/else"));
            }
        }
        else
        {
            throw new JsonImportException(at, $"Unknown row {Names(item)}; expected row, forEachRow or rowsIf.");
        }
    }

    private void Row(RowComposer r, JsonElement row, string at)
    {
        if (Bool(row, "allowSplit", at) == true)
        {
            r.AllowSplit();
        }

        if (String(row, "paragraphStyle", at) is { } paragraphStyle)
        {
            r.ParagraphStyle(ParagraphStyle(paragraphStyle));
        }

        int i = 0;
        foreach (JsonElement cell in Items(Required(row, "row", at), at + "/row", "a list of cells"))
        {
            string cellAt = at + "/row/" + i++;
            Guard(cellAt, () =>
            {
                if (cell.ValueKind == JsonValueKind.String)
                {
                    r.Cell(c => CellBlocks(c, cell, cellAt));
                    return;
                }

                if (cell.ValueKind != JsonValueKind.Object)
                {
                    throw new JsonImportException(cellAt, "A cell is a string (one paragraph) or an object: { \"content\": [ blocks ] }, { \"style\": \"…\", \"content\": \"…\" }, with span, background and verticalAlign as options.");
                }

                Keys(cell, cellAt, "span", "background", "verticalAlign", "style", "content");
                JsonElement content = Required(cell, "content", cellAt);
                string? style = String(cell, "style", cellAt);
                if (style is not null && content.ValueKind != JsonValueKind.String)
                {
                    throw new JsonImportException(cellAt + "/content", "With \"style\" a cell's content is one string, a paragraph in that style; without it, write blocks as a list.");
                }

                r.Cell(c =>
                {
                    Apply(Int(cell, "span", cellAt), v => c.ColumnSpan(v));
                    Apply(Color(cell, "background", cellAt), v => c.Background(v));
                    Apply(String(cell, "verticalAlign", cellAt), v => c.VerticalAlignment(Enum<CellVerticalAlignment>(v, cellAt + "/verticalAlign")));
                    CellBlocks(c, content, cellAt + "/content", style);
                });
            });
        }
    }

    // ── inlines ──────────────────────────────────────────────────────────────

    /// <summary>Inline content: a string (text with placeholders) or a list of inlines.</summary>
    private void Inlines(InlineComposer x, JsonElement inlines, string at)
    {
        if (inlines.ValueKind == JsonValueKind.String)
        {
            Guard(at, () => Text(x, inlines.GetString()!, null, at));
            return;
        }

        int i = 0;
        foreach (JsonElement inline in Items(inlines, at, "a string or a list of inlines"))
        {
            string inlineAt = at + "/" + i++;
            Guard(inlineAt, () => Inline(x, inline, inlineAt));
        }
    }

    private void Inline(InlineComposer x, JsonElement inline, string at)
    {
        if (inline.ValueKind == JsonValueKind.String)
        {
            Text(x, inline.GetString()!, null, at);
            return;
        }

        if (inline.ValueKind != JsonValueKind.Object)
        {
            throw new JsonImportException(at, "An inline is a string or an object such as { \"text\": …, \"style\": … }.");
        }

        CharacterStyleKey? style = String(inline, "style", at) is { } id ? CharacterStyle(id) : null;
        if (inline.TryGetProperty("text", out _))
        {
            Keys(inline, at, "text", "style");
            Text(x, String(inline, "text", at) ?? throw Missing(at, "text"), style, at);
        }
        else if (inline.TryGetProperty("tabRight", out _))
        {
            Keys(inline, at, "tabRight");
            Flag(inline, "tabRight", at);
            x.TabRight();
        }
        else if (inline.TryGetProperty("pageNumber", out _))
        {
            Keys(inline, at, "pageNumber", "style");
            Flag(inline, "pageNumber", at);
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
            Keys(inline, at, "pageCount", "style");
            Flag(inline, "pageCount", at);
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
            Keys(inline, at, "image", "alt", "width", "height");
            string name = image.ValueKind == JsonValueKind.String ? image.GetString()! : throw new JsonImportException(at + "/image", "An image names an entry of \"assets\".");
            byte[] data = Asset(image, at + "/image");
            Length? width = Length(inline, "width", at);
            Length? height = Length(inline, "height", at);

            // One size is enough: the other follows from the image's proportions.
            ImageFit fit = (width, height) switch
            {
                ({ } w, { } h) => ImageFit.Exact(w, h),
                ({ } w, null) => ImageFit.Width(w),
                (null, { } h) => ImageFit.Height(h),
                _ => ImageFit.Natural,
            };
            x.Image(ImageSource.FromBytes(data).WithName(name), String(inline, "alt", at) ?? string.Empty, fit);
        }
        else if (inline.TryGetProperty("expression", out JsonElement expression))
        {
            // Expressions are written in text as {{(condition):format}}; the object form is only for raw conditions,
            // which have no text form.
            Keys(inline, at, "expression", "format");
            if (expression.ValueKind != JsonValueKind.Object)
            {
                throw new JsonImportException(at + "/expression", "Write an expression in text as \"{{(condition):format}}\"; the expression object is only for a raw condition { \"raw\": …, \"uses\": … }.");
            }

            x.Expression(Condition(expression, at + "/expression"), Format(String(inline, "format", at) ?? throw Missing(at, "format"), at));
        }
        else if (inline.TryGetProperty("if", out JsonElement condition))
        {
            Keys(inline, at, "if", "then", "elseIf", "else");
            InlineIfChain chain = x.If(Condition(condition, at + "/if"), t => TextInlines(t, Required(inline, "then", at), at + "/then"));
            int i = 0;
            foreach (JsonElement branch in Array(inline, "elseIf", at))
            {
                string branchAt = at + "/elseIf/" + i++;
                Keys(branch, branchAt, "if", "then");
                chain = chain.ElseIf(Condition(Required(branch, "if", branchAt), branchAt + "/if"), t => TextInlines(t, Required(branch, "then", branchAt), branchAt + "/then"));
            }

            if (inline.TryGetProperty("else", out JsonElement otherwise))
            {
                chain.Else(t => TextInlines(t, otherwise, at + "/else"));
            }
        }
        else
        {
            throw new JsonImportException(at, $"Unknown inline {Names(inline)}; expected a string (a tab or line break is \\t or \\n in it), text, tabRight, pageNumber, pageCount, image, expression or if.");
        }
    }

    /// <summary>Text with placeholders, every piece in <paramref name="style"/>.</summary>
    private void Text(InlineComposer x, string text, CharacterStyleKey? style, string at)
    {
        foreach (TextPiece piece in Pieces(text, at))
        {
            switch (piece)
            {
                case TextPiece.Literal literal when style is null:
                    x.Text(literal.Text);
                    break;
                case TextPiece.Literal literal:
                    x.Text(style, literal.Text);
                    break;
                case TextPiece.Value value:
                    (TemplatePath path, ValueFormat? format) = ValuePiece(value, at);
                    switch (format, style)
                    {
                        case (null, null):
                            x.Value(path);
                            break;
                        case (null, { } s):
                            x.Value(s, path);
                            break;
                        case ({ } f, null):
                            x.Value(path, f);
                            break;
                        default:
                            x.Value(style!, path, format!);
                            break;
                    }

                    break;
                case TextPiece.Expression expression:
                    if (style is not null)
                    {
                        throw new JsonImportException(at, $"The expression '{TextTemplate.Write([expression])}' cannot have a character style.");
                    }

                    x.Expression(ConditionText(expression.Condition, at), Format(expression.Format ?? throw new JsonImportException(at, $"The expression '{TextTemplate.Write([expression])}' needs a format, for example {{{{(…):yesno}}}}."), at));
                    break;
            }
        }
    }

    /// <summary>The inlines of an inline conditional: a string, or a list of strings, styled text and nested conditionals.</summary>
    private void TextInlines(TextComposer t, JsonElement inlines, string at)
    {
        if (inlines.ValueKind == JsonValueKind.String)
        {
            Guard(at, () => Text(t, inlines.GetString()!, null, at));
            return;
        }

        int i = 0;
        foreach (JsonElement inline in Items(inlines, at, "a string or a list of inlines"))
        {
            string inlineAt = at + "/" + i++;
            Guard(inlineAt, () => TextInline(t, inline, inlineAt));
        }
    }

    private void TextInline(TextComposer t, JsonElement inline, string at)
    {
        if (inline.ValueKind == JsonValueKind.String)
        {
            Text(t, inline.GetString()!, null, at);
            return;
        }

        CharacterStyleKey? style = String(inline, "style", at) is { } id ? CharacterStyle(id) : null;
        if (inline.TryGetProperty("text", out _))
        {
            Keys(inline, at, "text", "style");
            Text(t, String(inline, "text", at) ?? throw Missing(at, "text"), style, at);
        }
        else if (inline.TryGetProperty("if", out JsonElement condition))
        {
            Keys(inline, at, "if", "then", "elseIf", "else");
            InlineIfChain chain = t.If(Condition(condition, at + "/if"), inner => TextInlines(inner, Required(inline, "then", at), at + "/then"));
            int b = 0;
            foreach (JsonElement branch in Array(inline, "elseIf", at))
            {
                string branchAt = at + "/elseIf/" + b++;
                Keys(branch, branchAt, "if", "then");
                chain = chain.ElseIf(Condition(Required(branch, "if", branchAt), branchAt + "/if"), inner => TextInlines(inner, Required(branch, "then", branchAt), branchAt + "/then"));
            }

            if (inline.TryGetProperty("else", out JsonElement otherwise))
            {
                chain.Else(inner => TextInlines(inner, otherwise, at + "/else"));
            }
        }
        else
        {
            throw new JsonImportException(at, "Inside an inline conditional only strings, { \"text\": …, \"style\": … } and if are allowed.");
        }
    }

    /// <summary>Text with placeholders inside an inline conditional (no expressions, no formatted styled values).</summary>
    private void Text(TextComposer t, string text, CharacterStyleKey? style, string at)
    {
        foreach (TextPiece piece in Pieces(text, at))
        {
            switch (piece)
            {
                case TextPiece.Literal literal:
                    _ = style is null ? t.Text(literal.Text) : t.Text(style, literal.Text);
                    break;
                case TextPiece.Value value:
                    (TemplatePath path, ValueFormat? format) = ValuePiece(value, at);
                    if (format is not null && style is not null)
                    {
                        throw new JsonImportException(at, $"Inside an inline conditional the value '{TextTemplate.Write([value])}' cannot have both a format and a character style.");
                    }

                    _ = format is not null ? t.Value(path, format) : style is null ? t.Value(path) : t.Value(style, path);
                    break;
                case TextPiece.Expression expression:
                    throw new JsonImportException(at, $"Inside an inline conditional an expression ('{TextTemplate.Write([expression])}') is not allowed.");
            }
        }
    }

    /// <summary>A string without placeholders: composed as literal text, exactly like the C# overloads that take a string.</summary>
    private static bool IsPlainText(JsonElement element) =>
        element.ValueKind == JsonValueKind.String && !element.GetString()!.Contains("{{", StringComparison.Ordinal);

    private static IReadOnlyList<TextPiece> Pieces(string text, string at)
    {
        try
        {
            return TextTemplate.Parse(text);
        }
        catch (FormatException exception)
        {
            throw new JsonImportException(at, exception.Message);
        }
    }

    /// <summary>The path and format of a value placeholder; <c>:join</c> reads a collection, every other value a single value.</summary>
    private (TemplatePath Path, ValueFormat? Format) ValuePiece(TextPiece.Value value, string at)
    {
        bool join = value.Format is "join" || value.Format?.StartsWith("join:", StringComparison.Ordinal) == true;
        return (Resolve(value.Path, at, join ? null : DataKind.Value), value.Format is { } specifier ? Format(specifier, at) : null);
    }

    private static readonly HashSet<string> _plainFormats = new HashSet<string>(StringComparer.Ordinal)
    {
        "currency", "uppercase", "lowercase", "raw", "yesno", "truefalse", "onoff", "enabled", "active", "checkbox", "checkmark", "check", "join",
    };

    /// <summary>A value format Templify knows (or the host registered); anything else is an error instead of a silently ignored format.</summary>
    private ValueFormat Format(string specifier, string at)
    {
        bool known = _plainFormats.Contains(specifier)
            || (_options.ValueFormats?.Contains(specifier) ?? false)
            || specifier.StartsWith("join:", StringComparison.Ordinal)
            || (specifier.StartsWith("date:", StringComparison.Ordinal) && specifier.Length > "date:".Length)
            || (specifier.StartsWith("number:", StringComparison.Ordinal) && specifier.Length > "number:".Length);
        if (!known)
        {
            throw new JsonImportException(
                at,
                $"Unknown value format '{specifier}'. Known: currency, number:<pattern> (number:N2), date:<pattern> (date:d MMMM yyyy), yesno, truefalse, onoff, enabled, active, checkbox, checkmark, uppercase, lowercase, raw, join, join:<item path>, join:<item path>:\"<separator>\".");
        }

        return ValueFormat.Custom(specifier);
    }

    // ── text keys ──────────────────────────────────────────────────────────────

    /// <summary>
    /// The definition with every <c>[[key]]</c> replaced by the build language's text (key markers while exporting).
    /// Malformed keys are TB9200, missing texts TB5001, a text containing <c>{{</c> TB9201, each at the string.
    /// </summary>
    private JsonElement WithTranslations(JsonElement source)
    {
        using MemoryStream stream = new MemoryStream();
        using (Utf8JsonWriter output = new Utf8JsonWriter(stream))
        {
            Translate(source, string.Empty, output);
        }

        using JsonDocument document = JsonDocument.Parse(stream.ToArray());
        return document.RootElement.Clone();
    }

    private void Translate(JsonElement element, string at, Utf8JsonWriter output)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                output.WriteStartObject();
                foreach (JsonProperty property in element.EnumerateObject())
                {
                    output.WritePropertyName(property.Name);
                    if (at.Length == 0 && property.Name == "texts")
                    {
                        // The translations themselves are literal: a "[[" inside one is text.
                        property.Value.WriteTo(output);
                        continue;
                    }

                    Translate(property.Value, at + "/" + Escape(property.Name), output);
                }

                output.WriteEndObject();
                break;
            case JsonValueKind.Array:
                output.WriteStartArray();
                int i = 0;
                foreach (JsonElement item in element.EnumerateArray())
                {
                    Translate(item, at + "/" + i++, output);
                }

                output.WriteEndArray();
                break;
            case JsonValueKind.String:
                string text = element.GetString()!;
                try
                {
                    // Raw XML is markup: a translation goes in as XML text (C# put it into an XElement).
                    bool markup = at.EndsWith("/raw/docx", StringComparison.Ordinal) || at.EndsWith("/raw/odt", StringComparison.Ordinal);
                    text = TextKeys.Resolve(text, key => markup ? System.Security.SecurityElement.Escape(Text(key, at)) : Text(key, at));
                }
                catch (FormatException exception)
                {
                    Fail(at, exception.Message);
                }

                output.WriteStringValue(text);
                break;
            default:
                element.WriteTo(output);
                break;
        }
    }

    private string Text(string key, string at)
    {
        string? outer = _context.CurrentLocation;
        _context.CurrentLocation = at;
        try
        {
            string text = _template.Texts.Resolve(key, _texts.Catalog);
            if (!_template.Texts.WritesKeys && text.Contains("{{", StringComparison.Ordinal))
            {
                _context.Error(DiagnosticCodes.InvalidJsonValue, $"The text '{key}' ({_template.Texts.Language.Name}) contains '{{{{', which would read as a value; texts are literal.", at);
                return "[" + key + "]";
            }

            return text;
        }
        finally
        {
            _context.CurrentLocation = outer;
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

    private TemplatePath Path(JsonElement path, string at, DataKind? expected = null)
    {
        if (path.ValueKind != JsonValueKind.String)
        {
            throw new JsonImportException(at, "A path is a string such as \"order.customer.name\".");
        }

        return Resolve(path.GetString()!, at, expected);
    }

    /// <summary>
    /// Resolves a path and, with a data catalogue, checks that the data has it and that its kind fits the use
    /// (a value for a value, a collection for a loop, an object for an object parameter; open shapes fit anything).
    /// Problems are TB9203 at the pointer.
    /// </summary>
    private TemplatePath Resolve(string text, string at, DataKind? expected = null)
    {
        TemplatePath path = ResolvePath(text);
        if (_options.Data is { } catalog && !path.IsMetadata && !path.IsRoot)
        {
            string canonical = DataContract.Canonical(path);
            DataKind? kind = catalog.KindOf(canonical);
            if (kind is null)
            {
                _context.Error(DiagnosticCodes.UnknownDataPath, $"The data has no '{canonical}' (written as '{text}').", at);
            }
            else if (expected is { } wanted && kind != wanted && kind != DataKind.Open)
            {
                _context.Error(DiagnosticCodes.UnknownDataPath, $"'{canonical}' is {Article(kind.Value)}, not {Article(wanted)}.", at);
            }
        }

        return path;
    }

    private static string Article(DataKind kind) => kind switch
    {
        DataKind.Value => "a value",
        DataKind.Object => "an object",
        DataKind.Collection => "a collection",
        _ => "an open shape",
    };

    /// <summary>Conditions longer than this, or nested deeper, are refused before they are parsed (stack safety).</summary>
    private const int MaximumConditionLength = 10_000;

    private const int MaximumConditionDepth = 64;

    private Condition Condition(JsonElement condition, string at)
    {
        if (condition.ValueKind == JsonValueKind.Object)
        {
            Keys(condition, at, "raw", "uses");
            List<TemplatePath> uses = new List<TemplatePath>();
            int i = 0;
            foreach (JsonElement use in Array(condition, "uses", at))
            {
                uses.Add(Path(use, at + "/uses/" + i++));
            }

            string raw = String(condition, "raw", at) ?? throw Missing(at, "raw");
            CheckConditionSize(raw, at);
            return Builder.Condition.Raw(raw, uses.ToArray());
        }

        if (condition.ValueKind != JsonValueKind.String)
        {
            throw new JsonImportException(at, "A condition is a string such as \"order.total > 1000\".");
        }

        return ConditionText(condition.GetString()!, at);
    }

    private Condition ConditionText(string text, string at)
    {
        CheckConditionSize(text, at);
        try
        {
            return ConditionParser.Parse(text, path => Resolve(path, at));
        }
        catch (FormatException exception)
        {
            throw new JsonImportException(at, exception.Message);
        }
    }

    /// <summary>Refuses conditions whose length or parenthesis depth could exhaust the stack of a recursive parser.</summary>
    private static void CheckConditionSize(string text, string at)
    {
        int depth = 0;
        bool inString = false;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (inString)
            {
                if (c == '\\')
                {
                    i++;
                }
                else if (c == '"')
                {
                    inString = false;
                }

                continue;
            }

            switch (c)
            {
                case '"':
                    inString = true;
                    break;
                case '(':
                    if (++depth > MaximumConditionDepth)
                    {
                        throw new JsonImportException(at, $"The condition is nested deeper than {MaximumConditionDepth} levels.");
                    }

                    break;
                case ')':
                    depth--;
                    break;
            }
        }

        if (text.Length > MaximumConditionLength)
        {
            throw new JsonImportException(at, $"The condition is longer than {MaximumConditionLength} characters.");
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

        if (font.ValueKind == JsonValueKind.String && font.GetString()!.StartsWith('$'))
        {
            string token = font.GetString()![1..];
            return _themeFonts.TryGetValue(token, out FontFace? themed)
                ? FontFace.Token(token, themed)
                : throw new JsonImportException(at + "/font", $"The theme has no font '{token}' ({string.Join(", ", _themeFonts.Keys)}).");
        }

        if (font.ValueKind == JsonValueKind.String)
        {
            return new FontFace(font.GetString()!);
        }

        Keys(font, at + "/font", "family", "embedded");
        string family = String(font, "family", at + "/font") ?? throw Missing(at + "/font", "family");
        return FontFace.Embedded(family, Asset(Required(font, "embedded", at + "/font"), at + "/font/embedded"));
    }

    /// <summary>The content of an asset named in the definition's <c>assets</c>.</summary>
    private byte[] Asset(JsonElement key, string at)
    {
        string? name = key.ValueKind == JsonValueKind.String ? key.GetString() : null;
        return name != null && _assetsByName.TryGetValue(name, out byte[]? data)
            ? data
            : throw new JsonImportException(at, $"The asset '{name}' is not in the definition's \"assets\" ({string.Join(", ", _assetsByName.Keys)}).");
    }

    /// <summary>The <c>assets</c> section: names to content keys, each key in <see cref="TemplateJsonImportOptions.Assets"/>.</summary>
    private void ReadAssets()
    {
        _assetsByName.Clear();
        if (!_root.TryGetProperty("assets", out JsonElement assets))
        {
            return;
        }

        if (assets.ValueKind != JsonValueKind.Object)
        {
            Fail("/assets", "The assets are an object of names and content keys, for example { \"logo\": \"sha256:…\" }.");
            return;
        }

        foreach (JsonProperty asset in assets.EnumerateObject())
        {
            string at = "/assets/" + Escape(asset.Name);
            if (!AssetNames.IsValid(asset.Name))
            {
                Fail(at, $"'{asset.Name}' is not an asset name: use letters, digits, '_', '.' or '-'.");
            }
            else if (asset.Value.ValueKind != JsonValueKind.String || _options.Assets is not { } contents || !contents.TryGetValue(asset.Value.GetString()!, out byte[]? data))
            {
                Fail(at, $"The asset '{asset.Name}' refers to content that is not in TemplateJsonImportOptions.Assets.");
            }
            else
            {
                _assetsByName[asset.Name] = data;
            }
        }
    }

    private Length? Length(JsonElement owner, string name, string at) =>
        Value(owner, name, at, "a length such as \"10pt\" or \"25mm\"", (string text, out Length value) => JsonValues.TryLength(text, out value));

    private Color? Color(JsonElement owner, string name, string at) =>
        PlainString(owner, name) is { } token && token.StartsWith('$')
            ? ThemeColor(token[1..], at + "/" + Escape(name))
            : Value(owner, name, at, "a colour such as \"#1F4E79\" or a theme colour \"$accent\"", (string text, out Color value) => JsonValues.TryColor(text, out value));

    private Builder.Color ThemeColor(string token, string at) =>
        _themeColors.TryGetValue(token, out Builder.Color color)
            ? Builder.Color.Token(token, color)
            : throw new JsonImportException(at, $"The theme has no colour '{token}' ({string.Join(", ", _themeColors.Keys)}).");

    /// <summary>The <c>theme</c> section: <c>colors</c> and <c>fonts</c> by token name; the host's values replace the file's.</summary>
    private void ReadTheme()
    {
        _themeColors.Clear();
        _themeFonts.Clear();
        if (_root.TryGetProperty("theme", out JsonElement theme))
        {
            const string at = "/theme";
            if (theme.ValueKind != JsonValueKind.Object)
            {
                Fail(at, "The theme is an object: { \"colors\": { \"accent\": \"#1F4E79\" }, \"fonts\": { \"body\": \"Liberation Sans\" } }.");
            }
            else if (Keys(theme, at, "colors", "fonts"))
            {
                foreach (JsonProperty color in Members(theme, "colors"))
                {
                    string colorAt = at + "/colors/" + Escape(color.Name);
                    if (!ThemeTokens.IsValid(color.Name))
                    {
                        Fail(colorAt, $"'{color.Name}' is not a theme token name: a letter, then letters, digits, '_', '.' or '-'.");
                    }
                    else if (color.Value.ValueKind == JsonValueKind.String && JsonValues.TryColor(color.Value.GetString(), out Builder.Color value))
                    {
                        _themeColors[color.Name] = value;
                    }
                    else
                    {
                        Fail(colorAt, "A theme colour is \"#RRGGBB\".");
                    }
                }

                foreach (JsonProperty font in Members(theme, "fonts"))
                {
                    string fontAt = at + "/fonts/" + Escape(font.Name);
                    if (!ThemeTokens.IsValid(font.Name))
                    {
                        Fail(fontAt, $"'{font.Name}' is not a theme token name: a letter, then letters, digits, '_', '.' or '-'.");
                        continue;
                    }

                    Guard(fontAt, () =>
                    {
                        if (font.Value.ValueKind == JsonValueKind.String)
                        {
                            _themeFonts[font.Name] = new FontFace(font.Value.GetString()!);
                            return;
                        }

                        Keys(font.Value, fontAt, "family", "embedded");
                        string family = String(font.Value, "family", fontAt) ?? throw Missing(fontAt, "family");
                        _themeFonts[font.Name] = FontFace.Embedded(family, Asset(Required(font.Value, "embedded", fontAt), fontAt + "/embedded"));
                    });
                }
            }
        }

        foreach ((string token, string value) in _options.Theme ?? new Dictionary<string, string>())
        {
            if (JsonValues.TryColor(value, out Builder.Color color))
            {
                _themeColors[token] = color;
            }
            else
            {
                _themeFonts[token] = new FontFace(value);
            }
        }
    }

    private (Length Top, Length Right, Length Bottom, Length Left)? Box(JsonElement owner, string name, string at) =>
        Value(owner, name, at, "one, two or four lengths", (string text, out (Length, Length, Length, Length) value) => JsonValues.TryBox(text, out value));

    /// <summary>Padding as the model holds it: one value, or vertical and horizontal; four different values are not dropped silently.</summary>
    private (Length Top, Length Right, Length Bottom, Length Left)? Padding(JsonElement owner, string name, string at)
    {
        (Length Top, Length Right, Length Bottom, Length Left)? box = Box(owner, name, at);
        if (box is { } value && (value.Top != value.Bottom || value.Right != value.Left))
        {
            Fail(at + "/" + Escape(name), $"'{name}' takes one value or two (vertical horizontal), for example \"6pt 8pt\"; different top/bottom or left/right padding is not supported.");
            return null;
        }

        return box;
    }

    private Model.BorderLine? Border(JsonElement owner, string name, string at)
    {
        if (String(owner, name, at) is not { } text)
        {
            return null;
        }

        string[] parts = text.Split(' ');
        if (parts.Length == 2 && parts[1].StartsWith('$') && JsonValues.TryLength(parts[0], out Length width))
        {
            return new Model.BorderLine(width, ThemeColor(parts[1][1..], at + "/" + Escape(name)));
        }

        return JsonValues.TryBorder(text, out Model.BorderLine? line)
            ? line
            : throw new JsonImportException(at + "/" + name, $"'{text}' is not a line such as \"0.5pt #D4D4D4\" or \"0.5pt $line\".");
    }

    private delegate bool TryParse<T>(string text, out T value);

    private static T? Value<T>(JsonElement owner, string name, string at, string expected, TryParse<T> parse)
        where T : struct
    {
        if (String(owner, name, at) is not { } text)
        {
            return null;
        }

        return parse(text, out T value) ? value : throw new JsonImportException(at + "/" + name, $"'{text}' is not {expected}.");
    }

    private static T Enum<T>(string text, string at)
        where T : struct, System.Enum =>
        !text.Any(char.IsDigit) && System.Enum.TryParse(text, ignoreCase: true, out T value) && System.Enum.IsDefined(value)
            ? value
            : throw new JsonImportException(at, $"'{text}' is not one of {string.Join(", ", System.Enum.GetNames<T>().Select(n => char.ToLowerInvariant(n[0]) + n[1..]))}.");

    /// <summary>A string member; another JSON type is a problem at its pointer.</summary>
    private static string? String(JsonElement owner, string name, string at) =>
        Member(owner, name, at, JsonValueKind.String, "a string")?.GetString();

    private static bool? Bool(JsonElement owner, string name, string at) =>
        Member(owner, name, at, JsonValueKind.True, "true or false")?.GetBoolean();

    private static int? Int(JsonElement owner, string name, string at) =>
        Member(owner, name, at, JsonValueKind.Number, "a whole number") is { } number
            ? number.TryGetInt32(out int value) ? value : throw new JsonImportException(at + "/" + name, "A whole number is expected.")
            : null;

    private static double? Number(JsonElement owner, string name, string at) =>
        Member(owner, name, at, JsonValueKind.Number, "a number")?.GetDouble();

    private static JsonElement? Member(JsonElement owner, string name, string at, JsonValueKind kind, string expected)
    {
        if (owner.ValueKind != JsonValueKind.Object || !owner.TryGetProperty(name, out JsonElement value))
        {
            return null;
        }

        bool matches = kind == JsonValueKind.True ? value.ValueKind is JsonValueKind.True or JsonValueKind.False : value.ValueKind == kind;
        return matches ? value : throw new JsonImportException(at + "/" + name, $"'{name}' must be {expected}, not {value.ValueKind.ToString().ToLowerInvariant()}.");
    }

    private static string? PlainString(JsonElement owner, string name) =>
        owner.ValueKind == JsonValueKind.Object && owner.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private IEnumerable<JsonElement> Array(JsonElement owner, string name, string at) =>
        owner.ValueKind == JsonValueKind.Object && owner.TryGetProperty(name, out JsonElement value) ? Items(value, at + "/" + name, "a list") : [];

    private IEnumerable<JsonElement> Items(JsonElement list, string at, string expected)
    {
        if (list.ValueKind == JsonValueKind.Array)
        {
            return list.EnumerateArray();
        }

        Fail(at, char.ToUpperInvariant(expected[0]) + expected[1..] + " is expected.");
        return [];
    }

    private static IEnumerable<JsonProperty> Members(JsonElement owner, string name) =>
        owner.ValueKind == JsonValueKind.Object && owner.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.Object ? value.EnumerateObject() : [];

    private static JsonElement Required(JsonElement owner, string name, string at) =>
        owner.ValueKind == JsonValueKind.Object && owner.TryGetProperty(name, out JsonElement value) ? value : throw Missing(at, name);

    /// <summary>A flag such as <c>"pageBreak": true</c>: anything but <c>true</c> is a problem, never a silent element.</summary>
    private static void Flag(JsonElement owner, string name, string at)
    {
        if (owner.GetProperty(name).ValueKind != JsonValueKind.True)
        {
            throw new JsonImportException(at + "/" + name, $"'{name}' must be true; leave the element out instead.");
        }
    }

    private static JsonImportException Missing(string at, string name) => new JsonImportException(at, $"'{name}' is missing.");

    /// <summary>Reports every key of an object that its kind does not have; false when the element is not an object.</summary>
    private bool Keys(JsonElement element, string at, params string[] allowed)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            Fail(at, "An object { … } is expected.");
            return false;
        }

        foreach (JsonProperty property in element.EnumerateObject().Where(p => !allowed.Contains(p.Name, StringComparer.Ordinal)))
        {
            Fail(at + "/" + Escape(property.Name), $"Unknown key '{property.Name}'; allowed here: {string.Join(", ", allowed)}.");
        }

        return true;
    }

    private static string Names(JsonElement element) =>
        element.ValueKind == JsonValueKind.Object ? string.Join(", ", element.EnumerateObject().Select(p => "'" + p.Name + "'")) : element.ValueKind.ToString();

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

    /// <summary>
    /// Composes one element at its pointer: errors composers record get the pointer; a structural problem is TB9200,
    /// a value a composer rejects (<see cref="ArgumentException"/>) TB9201; the element is skipped either way.
    /// </summary>
    private void Guard(string at, Action compose)
    {
        string? outer = _context.CurrentLocation;
        _context.CurrentLocation = at;
        try
        {
            compose();
        }
        catch (JsonImportException exception)
        {
            Fail(exception.Location, exception.Message);
        }
        catch (XmlException exception)
        {
            Fail(at, "The XML is not well-formed: " + exception.Message);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            _context.Error(DiagnosticCodes.InvalidJsonValue, exception.Message, at);
        }
        finally
        {
            _context.CurrentLocation = outer;
        }
    }
}

/// <summary>A structural problem found while composing a JSON definition; caught per element.</summary>
internal sealed class JsonImportException(string location, string message) : Exception(message)
{
    public string Location { get; } = location;
}

