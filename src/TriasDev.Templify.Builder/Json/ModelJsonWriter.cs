// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Xml.Linq;
using TriasDev.Templify.Builder.Model;

namespace TriasDev.Templify.Builder.Json;

/// <summary>
/// Writes a composed definition as JSON (design spec phase 3, section 4.1): one key per block kind, shorthands where
/// the C# API has them, fixed key order, values in <see cref="JsonValues"/>' forms. Binary data goes to the assets.
/// </summary>
internal sealed class ModelJsonWriter
{
    /// <summary>The format version written as <c>templifyDefinition</c>.</summary>
    public const int FormatVersion = 1;

    private readonly Utf8JsonWriter _json;
    private readonly SortedDictionary<string, byte[]> _assets = new SortedDictionary<string, byte[]>(StringComparer.Ordinal);
    private readonly PageSetup _page;
    private readonly ComponentCatalog? _components;

    private ModelJsonWriter(Utf8JsonWriter json, PageSetup page, ComponentCatalog? components)
    {
        _json = json;
        _page = page;
        _components = components;
    }

    /// <summary>Writes the definition.</summary>
    public static (string Json, IReadOnlyDictionary<string, byte[]> Assets) Write(ComposedTemplate composed, TemplateIdentity identity, ComponentCatalog? components)
    {
        using MemoryStream stream = new MemoryStream();
        ModelJsonWriter writer;
        using (Utf8JsonWriter json = new Utf8JsonWriter(stream, new JsonWriterOptions
        {
            Indented = true,
            NewLine = "\n",
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        }))
        {
            writer = new ModelJsonWriter(json, composed.Model.Page, components);
            writer.Document(composed, identity);
        }

        return (Encoding.UTF8.GetString(stream.ToArray()), writer._assets);
    }

    private void Document(ComposedTemplate composed, TemplateIdentity identity)
    {
        DocumentModel model = composed.Model;
        _json.WriteStartObject();
        _json.WriteNumber("templifyDefinition", FormatVersion);
        _json.WriteStartObject("identity");
        _json.WriteString("id", identity.Id);
        _json.WriteNumber("version", identity.Version);
        _json.WriteEndObject();
        _json.WriteString("language", model.Language.Name);

        if (model.Info.Title != null || model.Info.Author != null || model.Info.Subject != null)
        {
            _json.WriteStartObject("properties");
            StringIf("title", model.Info.Title);
            StringIf("author", model.Info.Author);
            StringIf("subject", model.Info.Subject);
            _json.WriteEndObject();
        }

        Styles(composed.UserStyles);
        Page(model.Page);
        _json.WritePropertyName("content");
        Blocks(model.Body);
        _json.WriteEndObject();
    }

    // ── styles ───────────────────────────────────────────────────────────────

    private void Styles(StyleSheet sheet)
    {
        bool hasDefaults = HasText(sheet.Defaults);
        if (!hasDefaults && sheet.ParagraphStyles.Count == 0 && sheet.CharacterStyles.Count == 0 && sheet.TableStyles.Count == 0
            && sheet.BoxStyles.Count == 0 && sheet.ListStyles.Count == 0)
        {
            return;
        }

        _json.WriteStartObject("styles");
        if (hasDefaults)
        {
            _json.WriteStartObject("defaults");
            Text(sheet.Defaults);
            _json.WriteEndObject();
        }

        Section("paragraph", sheet.ParagraphStyles, style =>
        {
            _json.WriteStartObject(style.Key.Id);
            _json.WriteString("name", style.Key.DisplayName);
            StringIf("basedOn", style.BasedOn?.Id);
            StringIf("next", style.Next?.Id);
            Text(style.Text);
            ParagraphProperties p = style.Paragraph;
            LengthIf("spaceBefore", p.SpaceBefore);
            LengthIf("spaceAfter", p.SpaceAfter);
            if (p.LineHeight is { } line)
            {
                switch (line.Rule)
                {
                    case LineHeightRule.Proportional:
                        _json.WriteNumber("lineSpacing", line.Multiple);
                        break;
                    case LineHeightRule.Exact:
                        _json.WriteString("lineHeight", JsonValues.Length(line.Height));
                        break;
                    default:
                        _json.WriteString("minimumLineHeight", JsonValues.Length(line.Height));
                        break;
                }
            }

            if (p.Alignment is { } alignment)
            {
                _json.WriteString("alignment", Enum(alignment));
            }

            LengthIf("indentStart", p.IndentStart);
            LengthIf("indentEnd", p.IndentEnd);
            LengthIf("firstLineIndent", p.FirstLineIndent);
            BoolIf("keepWithNext", p.KeepWithNext);
            BoolIf("keepLinesTogether", p.KeepLinesTogether);
            if (p.OutlineLevel is { } level)
            {
                _json.WriteNumber("outlineLevel", level);
            }

            _json.WriteEndObject();
        });

        Section("character", sheet.CharacterStyles, style =>
        {
            _json.WriteStartObject(style.Key.Id);
            _json.WriteString("name", style.Key.DisplayName);
            StringIf("basedOn", style.BasedOn?.Id);
            Text(style.Text);
            _json.WriteEndObject();
        });

        Section("table", sheet.TableStyles, style =>
        {
            _json.WriteStartObject(style.Key.Id);
            BorderIf("outerBorder", style.Outer);
            BorderIf("rowRule", style.RowRule);
            BorderIf("columnRule", style.ColumnRule);
            BorderIf("headerRule", style.HeaderRule);
            ColorIf("headerBackground", style.HeaderBackground);
            if (style.PaddingVertical != Length.Zero || style.PaddingHorizontal != TableStyleDefinition.DefaultPaddingHorizontal)
            {
                _json.WriteString("cellPadding", JsonValues.Length(style.PaddingVertical) + " " + JsonValues.Length(style.PaddingHorizontal));
            }

            _json.WriteEndObject();
        });

        Section("box", sheet.BoxStyles, style =>
        {
            _json.WriteStartObject(style.Key.Id);
            BorderIf("border", style.Border);
            ColorIf("background", style.Background);
            if (style.PaddingVertical != 6.Pt() || style.PaddingHorizontal != 6.Pt())
            {
                _json.WriteString("padding", JsonValues.Box(style.PaddingVertical, style.PaddingHorizontal, style.PaddingVertical, style.PaddingHorizontal));
            }

            LengthIf("spaceAfter", style.SpaceAfter);
            _json.WriteEndObject();
        });

        Section("list", sheet.ListStyles, style =>
        {
            _json.WriteStartObject(style.Key.Id);
            StringIf("itemStyle", style.ItemStyle?.Id);
            _json.WriteStartArray("levels");
            foreach ((int level, ListLevel definition) in style.Levels)
            {
                _json.WriteStartObject();
                _json.WriteNumber("level", level);
                _json.WriteString("bullet", definition.Bullet);
                _json.WriteString("indent", JsonValues.Length(definition.Indent));
                _json.WriteString("hanging", JsonValues.Length(definition.Hanging));
                _json.WriteEndObject();
            }

            _json.WriteEndArray();
            _json.WriteEndObject();
        });

        _json.WriteEndObject();
    }

    private void Section<T>(string name, IReadOnlyList<T> items, Action<T> write)
    {
        if (items.Count == 0)
        {
            return;
        }

        _json.WriteStartObject(name);
        foreach (T item in items)
        {
            write(item);
        }

        _json.WriteEndObject();
    }

    private static bool HasText(TextProperties text) =>
        text.Font != null || text.Size != null || text.Color != null || text.Bold != null || text.Italic != null;

    private void Text(TextProperties text)
    {
        if (text.Font is { } font)
        {
            if (font.Embedding is { } file)
            {
                _json.WriteStartObject("font");
                _json.WriteString("family", font.Family);
                _json.WriteString("embedded", Asset(file.Hash, file.Data));
                _json.WriteEndObject();
            }
            else
            {
                _json.WriteString("font", font.Family);
            }
        }

        LengthIf("size", text.Size);
        ColorIf("color", text.Color);
        BoolIf("bold", text.Bold);
        BoolIf("italic", text.Italic);
    }

    // ── page ─────────────────────────────────────────────────────────────────

    private void Page(PageSetup page)
    {
        PageSetupBuilder defaults = new PageSetupBuilder();
        bool size = page.Size != defaults.Size;
        bool margins = page.Top != defaults.Top || page.Right != defaults.Right || page.Bottom != defaults.Bottom || page.Left != defaults.Left;
        bool distances = page.HeaderDistance != defaults.HeaderDistance || page.FooterDistance != defaults.FooterDistance;
        if (!size && !margins && !distances && page.Header == null && page.Footer == null && !page.DistinctFirstPage)
        {
            return;
        }

        _json.WriteStartObject("page");
        if (size)
        {
            if (JsonValues.PageSizeName(page.Size) is { } name)
            {
                _json.WriteString("size", name);
            }
            else
            {
                _json.WriteStartObject("size");
                _json.WriteString("width", JsonValues.Length(page.Size.Width));
                _json.WriteString("height", JsonValues.Length(page.Size.Height));
                _json.WriteEndObject();
            }
        }

        if (margins)
        {
            _json.WriteString("margins", JsonValues.Box(page.Top, page.Right, page.Bottom, page.Left));
        }

        if (distances)
        {
            _json.WriteString("headerFooterDistance", JsonValues.Length(page.HeaderDistance) + " " + JsonValues.Length(page.FooterDistance));
        }

        if (page.Header is { } header)
        {
            _json.WritePropertyName("header");
            Blocks(header);
        }

        if (page.Footer is { } footer)
        {
            _json.WritePropertyName("footer");
            Blocks(footer);
        }

        if (page.DistinctFirstPage)
        {
            _json.WriteStartObject("firstPage");
            FirstPagePart("header", page.FirstHeader, page.Header);
            FirstPagePart("footer", page.FirstFooter, page.Footer);
            _json.WriteEndObject();
        }

        _json.WriteEndObject();
    }

    /// <summary>The first page's part: omitted when it is the running one, <c>"none"</c> when empty.</summary>
    private void FirstPagePart(string name, List<Block>? first, List<Block>? running)
    {
        if (first is null || ReferenceEquals(first, running))
        {
            return;
        }

        if (first.Count == 0)
        {
            _json.WriteString(name, "none");
            return;
        }

        _json.WritePropertyName(name);
        Blocks(first);
    }

    // ── blocks ───────────────────────────────────────────────────────────────

    private void Blocks(IEnumerable<Block> blocks)
    {
        _json.WriteStartArray();
        foreach (Block block in blocks)
        {
            Block(block);
        }

        _json.WriteEndArray();
    }

    private void Block(Block block)
    {
        switch (block)
        {
            case ParagraphBlock paragraph when paragraph.Style is null && paragraph.List is null && IsPlainText(paragraph.Inlines, out string? text):
                _json.WriteStartObject();
                _json.WriteString("paragraph", text);
                _json.WriteEndObject();
                break;

            case ParagraphBlock paragraph:
                _json.WriteStartObject();
                _json.WriteStartObject("paragraph");
                StringIf("style", paragraph.Style?.Id);
                Content(paragraph.Inlines);
                _json.WriteEndObject();
                _json.WriteEndObject();
                break;

            case PageBreakBlock:
                _json.WriteStartObject();
                _json.WriteBoolean("pageBreak", true);
                _json.WriteEndObject();
                break;

            case ConditionalBlock conditional:
                Conditional(conditional.Branches.Select(b => (b.Condition, (Action)(() => Blocks(b.Body)))).ToList(), conditional.Else is { } e ? () => Blocks(e) : null);
                break;

            case LoopBlock loop:
                _json.WriteStartObject();
                _json.WriteString("forEach", loop.Collection.ToString());
                _json.WriteString("as", loop.ItemName);
                _json.WritePropertyName("body");
                Blocks(loop.Body);
                _json.WriteEndObject();
                break;

            case ComponentBlock component when _components?.Find(component.Component.GetType()) is { } registration:
                _json.WriteStartObject();
                _json.WriteString("component", registration.Name);
                _json.WritePropertyName("params");
                registration.WriteParameters(_json, component.Component);
                _json.WriteEndObject();
                break;

            case ComponentBlock component:
                _json.WriteStartObject();
                _json.WritePropertyName("group");
                Blocks(component.Body);
                StringIf("from", component.Component is JsonGroupComponent group ? group.From : component.Component.GetType().Name);
                _json.WriteEndObject();
                break;

            case ListBlock list:
                _json.WriteStartObject();
                _json.WriteStartObject("list");
                _json.WriteString("style", list.Style.Id);
                _json.WritePropertyName("items");
                ListItems(list.Items);
                _json.WriteEndObject();
                _json.WriteEndObject();
                break;

            case TableBlock { BoxStyle: { } boxStyle } box:
                _json.WriteStartObject();
                _json.WriteStartObject("box");
                _json.WriteString("style", boxStyle.Id);
                _json.WritePropertyName("content");
                Blocks(box.AllRows().Single().Cells.Single().Content);
                _json.WriteEndObject();
                _json.WriteEndObject();
                break;

            case TableBlock table:
                Table(table);
                break;

            case RuleBlock rule:
                _json.WriteStartObject();
                _json.WriteStartObject("rule");
                _json.WriteString("line", JsonValues.Border(rule.Line));
                if (rule.SpaceBefore != Length.Zero || rule.SpaceAfter != Length.Zero)
                {
                    _json.WriteString("spaceAbove", JsonValues.Length(rule.SpaceBefore));
                    _json.WriteString("spaceBelow", JsonValues.Length(rule.SpaceAfter));
                }

                _json.WriteEndObject();
                _json.WriteEndObject();
                break;

            case TocBlock toc:
                _json.WriteStartObject();
                _json.WriteStartObject("toc");
                _json.WriteNumber("maxLevel", toc.MaxLevel);
                _json.WriteEndObject();
                _json.WriteEndObject();
                break;

            case RawXmlBlock raw:
                RawContext context = new RawContext(raw.Raw, _page.FrameWidth);
                _json.WriteStartObject();
                _json.WriteStartObject("raw");
                _json.WriteString("docx", CanonicalXml(raw.Raw.Docx(context)));
                _json.WriteString("odt", CanonicalXml(raw.Raw.Odt(context)));
                Paths("uses", raw.Raw.Uses);
                _json.WriteEndObject();
                _json.WriteEndObject();
                break;

            default:
                throw new NotSupportedException($"The JSON export does not know the block {block.GetType().Name}.");
        }
    }

    private void Conditional(List<(Condition Condition, Action Write)> branches, Action? otherwise, string thenName = "then")
    {
        _json.WriteStartObject();
        _json.WritePropertyName("if");
        Condition(branches[0].Condition);
        _json.WritePropertyName(thenName);
        branches[0].Write();
        if (branches.Count > 1)
        {
            _json.WriteStartArray("elseIf");
            foreach ((Condition condition, Action write) in branches.Skip(1))
            {
                _json.WriteStartObject();
                _json.WritePropertyName("when");
                Condition(condition);
                _json.WritePropertyName(thenName);
                write();
                _json.WriteEndObject();
            }

            _json.WriteEndArray();
        }

        if (otherwise != null)
        {
            _json.WritePropertyName("else");
            otherwise();
        }

        _json.WriteEndObject();
    }

    private void ListItems(IEnumerable<Block> items)
    {
        _json.WriteStartArray();
        foreach (Block item in items)
        {
            switch (item)
            {
                case ParagraphBlock { List: { } info } paragraph:
                    _json.WriteStartObject();
                    if (info.Level == 0 && IsPlainText(paragraph.Inlines, out string? text))
                    {
                        _json.WriteString("item", text);
                    }
                    else
                    {
                        _json.WriteStartObject("item");
                        if (info.Level != 0)
                        {
                            _json.WriteNumber("level", info.Level);
                        }

                        Content(paragraph.Inlines);
                        _json.WriteEndObject();
                    }

                    _json.WriteEndObject();
                    break;

                case LoopBlock loop:
                    _json.WriteStartObject();
                    _json.WriteString("forEach", loop.Collection.ToString());
                    _json.WriteString("as", loop.ItemName);
                    _json.WritePropertyName("items");
                    ListItems(loop.Body);
                    _json.WriteEndObject();
                    break;

                case ConditionalBlock conditional:
                    Conditional(
                        conditional.Branches.Select(b => (b.Condition, (Action)(() => ListItems(b.Body)))).ToList(),
                        conditional.Else is { } e ? () => ListItems(e) : null,
                        "items");
                    break;

                default:
                    throw new NotSupportedException($"A list cannot hold {item.GetType().Name}.");
            }
        }

        _json.WriteEndArray();
    }

    // ── tables ───────────────────────────────────────────────────────────────

    private void Table(TableBlock table)
    {
        _json.WriteStartObject();
        _json.WriteStartObject("table");
        StringIf("style", table.Style?.Id);
        if (table.Columns.Count > 0)
        {
            _json.WriteStartArray("columns");
            foreach (ColumnSpec column in table.Columns)
            {
                if (column.Fixed is { } width)
                {
                    _json.WriteStringValue(JsonValues.Length(width));
                }
                else
                {
                    _json.WriteNumberValue(column.Weight);
                }
            }

            _json.WriteEndArray();
        }

        LengthIf("spaceAfter", table.SpaceAfter);
        _json.WritePropertyName("rows");
        Rows(table.Rows);
        _json.WriteEndObject();
        _json.WriteEndObject();
    }

    private void Rows(IEnumerable<TableRowItem> rows)
    {
        _json.WriteStartArray();
        foreach (TableRowItem item in rows)
        {
            switch (item)
            {
                case TableRowModel row:
                    _json.WriteStartObject();
                    _json.WriteStartObject("row");
                    if (row.IsHeader)
                    {
                        _json.WriteBoolean("header", true);
                    }

                    if (row.AllowSplit)
                    {
                        _json.WriteBoolean("allowSplit", true);
                    }

                    _json.WriteStartArray("cells");
                    foreach (TableCellModel cell in row.Cells)
                    {
                        _json.WriteStartObject();
                        if (cell.Span != 1)
                        {
                            _json.WriteNumber("span", cell.Span);
                        }

                        ColorIf("background", cell.Background);
                        if (cell.VerticalAlignment is { } alignment)
                        {
                            _json.WriteString("align", Enum(alignment));
                        }

                        _json.WritePropertyName("content");
                        Blocks(cell.Content);
                        _json.WriteEndObject();
                    }

                    _json.WriteEndArray();
                    _json.WriteEndObject();
                    _json.WriteEndObject();
                    break;

                case TableRowLoop loop:
                    _json.WriteStartObject();
                    _json.WriteString("forEachRow", loop.Collection.ToString());
                    _json.WriteString("as", loop.ItemName);
                    _json.WritePropertyName("rows");
                    Rows(loop.Rows);
                    _json.WriteEndObject();
                    break;

                case TableRowConditional conditional:
                    _json.WriteStartObject();
                    _json.WritePropertyName("rowsIf");
                    Condition(conditional.Branches[0].Condition);
                    _json.WritePropertyName("then");
                    Rows(conditional.Branches[0].Rows);
                    if (conditional.Branches.Count > 1)
                    {
                        _json.WriteStartArray("elseIf");
                        foreach ((Condition condition, List<TableRowItem> branchRows) in conditional.Branches.Skip(1))
                        {
                            _json.WriteStartObject();
                            _json.WritePropertyName("when");
                            Condition(condition);
                            _json.WritePropertyName("then");
                            Rows(branchRows);
                            _json.WriteEndObject();
                        }

                        _json.WriteEndArray();
                    }

                    if (conditional.Else is { } otherwise)
                    {
                        _json.WritePropertyName("else");
                        Rows(otherwise);
                    }

                    _json.WriteEndObject();
                    break;
            }
        }

        _json.WriteEndArray();
    }

    // ── inlines ──────────────────────────────────────────────────────────────

    private static bool IsPlainText(IReadOnlyList<Inline> inlines, out string? text)
    {
        text = inlines is [TextInline { Style: null } only] ? only.Text : null;
        return text != null;
    }

    private void Content(IReadOnlyList<Inline> inlines)
    {
        if (IsPlainText(inlines, out string? text))
        {
            _json.WriteString("content", text);
            return;
        }

        _json.WritePropertyName("content");
        Inlines(inlines);
    }

    private void Inlines(IEnumerable<Inline> inlines)
    {
        _json.WriteStartArray();
        foreach (Inline inline in inlines)
        {
            _json.WriteStartObject();
            switch (inline)
            {
                case TextInline { Text: "\t", Style: null }:
                    _json.WriteBoolean("tab", true);
                    break;
                case TextInline { Text: "\n", Style: null }:
                    _json.WriteBoolean("lineBreak", true);
                    break;
                case TextInline text:
                    _json.WriteString("text", text.Text);
                    StringIf("style", text.Style?.Id);
                    break;
                case ValueInline value:
                    _json.WriteString("value", value.Path.ToString());
                    StringIf("format", value.Format?.Specifier);
                    StringIf("style", value.Style?.Id);
                    break;
                case PageFieldInline field:
                    _json.WriteBoolean(field.Field == PageField.Number ? "pageNumber" : "pageCount", true);
                    StringIf("style", field.Style?.Id);
                    break;
                case RightTabInline:
                    _json.WriteBoolean("tabRight", true);
                    break;
                case ImageInline image:
                    _json.WriteString("image", Asset(image.Image.Hash, image.Image.Data));
                    _json.WriteString("alt", image.AltText);
                    _json.WriteString("width", JsonValues.Length(image.Width));
                    _json.WriteString("height", JsonValues.Length(image.Height));
                    break;
                case ExpressionInline expression:
                    _json.WritePropertyName("expression");
                    Condition(expression.Condition);
                    StringIf("format", expression.Format?.Specifier);
                    break;
                case InlineConditional conditional:
                    _json.WritePropertyName("if");
                    Condition(conditional.Branches[0].Condition);
                    _json.WritePropertyName("then");
                    Inlines(conditional.Branches[0].Inlines);
                    if (conditional.Branches.Count > 1)
                    {
                        _json.WriteStartArray("elseIf");
                        foreach ((Condition condition, IReadOnlyList<Inline> branch) in conditional.Branches.Skip(1))
                        {
                            _json.WriteStartObject();
                            _json.WritePropertyName("when");
                            Condition(condition);
                            _json.WritePropertyName("then");
                            Inlines(branch);
                            _json.WriteEndObject();
                        }

                        _json.WriteEndArray();
                    }

                    if (conditional.Else is { } otherwise)
                    {
                        _json.WritePropertyName("else");
                        Inlines(otherwise);
                    }

                    break;
                default:
                    throw new NotSupportedException($"The JSON export does not know the inline {inline.GetType().Name}.");
            }

            _json.WriteEndObject();
        }

        _json.WriteEndArray();
    }

    // ── values ───────────────────────────────────────────────────────────────

    private void Condition(Condition condition)
    {
        if (condition is RawCondition raw)
        {
            _json.WriteStartObject();
            _json.WriteString("raw", raw.Expression);
            Paths("uses", raw.Uses);
            _json.WriteEndObject();
            return;
        }

        _json.WriteStringValue(condition.ToString());
    }

    /// <summary>
    /// XML text that does not depend on how the element was built: an element built in code and the same element
    /// parsed back declare their namespace prefixes differently, so the text is parsed once and written again.
    /// </summary>
    private static string CanonicalXml(XElement element) =>
        XElement.Parse(element.ToString(SaveOptions.DisableFormatting)).ToString(SaveOptions.DisableFormatting);

    private void Paths(string name, IEnumerable<TemplatePath> paths)
    {
        _json.WriteStartArray(name);
        foreach (TemplatePath path in paths)
        {
            _json.WriteStringValue(path.ToString());
        }

        _json.WriteEndArray();
    }

    private string Asset(string hash, byte[] data)
    {
        string key = "sha256:" + hash;
        _assets[key] = data;
        return key;
    }

    private static string Enum<T>(T value)
        where T : struct, System.Enum
    {
        string name = value.ToString();
        return char.ToLowerInvariant(name[0]) + name[1..];
    }

    private void StringIf(string name, string? value)
    {
        if (value != null)
        {
            _json.WriteString(name, value);
        }
    }

    private void LengthIf(string name, Length? value)
    {
        if (value is { } length)
        {
            _json.WriteString(name, JsonValues.Length(length));
        }
    }

    private void ColorIf(string name, Color? value)
    {
        if (value is { } color)
        {
            _json.WriteString(name, JsonValues.Color(color));
        }
    }

    private void BorderIf(string name, BorderLine? value)
    {
        if (value is { } line)
        {
            _json.WriteString(name, JsonValues.Border(line));
        }
    }

    private void BoolIf(string name, bool? value)
    {
        if (value is { } flag)
        {
            _json.WriteBoolean(name, flag);
        }
    }
}
