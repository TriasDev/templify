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
    private StyleSheet _styles = StyleSheet.Empty;

    private readonly HashSet<string> _sectionNames = new HashSet<string>(StringComparer.Ordinal);

    // The assets section: name → "sha256:…", in first-use order; unnamed images get one name per content (image-1, …).
    private readonly List<KeyValuePair<string, string>> _assetNames = new List<KeyValuePair<string, string>>();
    private readonly Dictionary<string, string> _unnamed = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>The paragraph style the blocks being written inherit; a paragraph with this style is written without one.</summary>
    private ParagraphStyleKey? _inherited;

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
    public static (string Json, IReadOnlyDictionary<string, byte[]> Assets) Write(ComposedTemplate composed, TemplateIdentity identity, ComponentCatalog? components, Func<IReadOnlyList<string>, IReadOnlyList<TextSection>>? texts = null)
    {
        using MemoryStream stream = new MemoryStream();
        ModelJsonWriter writer;
        using (Utf8JsonWriter json = new Utf8JsonWriter(stream, new JsonWriterOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        }))
        {
            writer = new ModelJsonWriter(json, composed.Model.Page, components);
            writer.Document(composed, identity);
        }

        return (JsonLayout.Format(WithTextKeys(Encoding.UTF8.GetString(stream.ToArray()), texts, writer._assetNames)), writer._assets);
    }

    /// <summary>
    /// Turns the key markers of the composed texts into <c>[[key]]</c> in every string. A literal <c>[[</c> (the import
    /// would read a key) or a key outside the key syntax cannot be written: TB9204 at the string's pointer.
    /// </summary>
    private static string WithTextKeys(string json, Func<IReadOnlyList<string>, IReadOnlyList<TextSection>>? texts, IReadOnlyList<KeyValuePair<string, string>> assets)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        List<BuildDiagnostic> problems = new List<BuildDiagnostic>();
        List<string> keys = UsedKeys(document.RootElement).Distinct(StringComparer.Ordinal).Where(TextKeys.IsKey).ToList();
        IReadOnlyList<TextSection> sections = texts?.Invoke(keys) ?? [];
        using MemoryStream stream = new MemoryStream();
        using (Utf8JsonWriter output = new Utf8JsonWriter(stream, new JsonWriterOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        {
            output.WriteStartObject();
            foreach (JsonProperty property in document.RootElement.EnumerateObject())
            {
                output.WritePropertyName(property.Name);
                Copy(property.Value, "/" + JsonComposition.Escape(property.Name), output, problems);
                if (property.Name == "identity")
                {
                    if (sections.Count > 0)
                    {
                        Texts(output, sections);
                    }

                    if (assets.Count > 0)
                    {
                        output.WriteStartObject("assets");
                        foreach ((string name, string key) in assets)
                        {
                            output.WriteString(name, key);
                        }

                        output.WriteEndObject();
                    }
                }
            }

            output.WriteEndObject();
        }

        if (problems.Count > 0)
        {
            throw new TemplateBuildException(problems);
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void Texts(Utf8JsonWriter output, IReadOnlyList<TextSection> sections)
    {
        output.WriteStartObject("texts");
        foreach (TextSection section in sections)
        {
            output.WriteStartObject(section.Key);
            foreach ((string language, string text) in section.Translations)
            {
                output.WriteString(language, text);
            }

            output.WriteEndObject();
        }

        output.WriteEndObject();
    }


    /// <summary>Whether the import reads <paramref name="written"/> back into exactly <paramref name="text"/> (literal "[[" or "[" before a key would not).</summary>
    private static bool ReadsBack(string written, string text)
    {
        try
        {
            return TextKeys.Resolve(written, TextKeys.Marker) == text;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    /// <summary>The keys of the text markers, in document order.</summary>
    private static IEnumerable<string> UsedKeys(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Object => element.EnumerateObject().SelectMany(p => UsedKeys(p.Value)),
        JsonValueKind.Array => element.EnumerateArray().SelectMany(UsedKeys),
        JsonValueKind.String => TextKeys.MarkedKeys(element.GetString()!),
        _ => [],
    };

    private static void Copy(JsonElement element, string at, Utf8JsonWriter output, List<BuildDiagnostic> problems)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                output.WriteStartObject();
                foreach (JsonProperty property in element.EnumerateObject())
                {
                    output.WritePropertyName(property.Name);
                    Copy(property.Value, at + "/" + JsonComposition.Escape(property.Name), output, problems);
                }

                output.WriteEndObject();
                break;
            case JsonValueKind.Array:
                output.WriteStartArray();
                int i = 0;
                foreach (JsonElement item in element.EnumerateArray())
                {
                    Copy(item, at + "/" + i++, output, problems);
                }

                output.WriteEndArray();
                break;
            case JsonValueKind.String:
                string text = element.GetString()!;
                string written = TextKeys.MarkersToKeys(text);
                List<string> invalid = TextKeys.MarkedKeys(text).Where(k => !TextKeys.IsKey(k)).Distinct(StringComparer.Ordinal).ToList();
                foreach (string key in invalid)
                {
                    problems.Add(new BuildDiagnostic(DiagnosticCodes.TextNotExportable, $"The text key '{key}' cannot be written as [[key]]: use letters, digits, '_', '.' or '-'.") { Location = at });
                }

                if (invalid.Count == 0 && written.Contains("[[", StringComparison.Ordinal) && !ReadsBack(written, text))
                {
                    problems.Add(new BuildDiagnostic(DiagnosticCodes.TextNotExportable, $"The text \"{written}\" contains '[[' that a JSON definition would not read back as written (a text key is [[key]]); change the text.") { Location = at });
                }

                output.WriteStringValue(written);
                break;
            default:
                element.WriteTo(output);
                break;
        }
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

        if (model.Info.Title != null || model.Info.Author != null || model.Info.Subject != null)
        {
            _json.WriteStartObject("properties");
            StringIf("title", model.Info.Title);
            StringIf("author", model.Info.Author);
            StringIf("subject", model.Info.Subject);
            _json.WriteEndObject();
        }

        _styles = composed.UserStyles;
        _inherited = _styles.DefaultParagraphStyle;
        Styles(composed.UserStyles);
        Page(model.Page);
        _json.WritePropertyName("content");
        Blocks(model.Body);
        _json.WriteEndObject();
    }

    // ── styles ───────────────────────────────────────────────────────────────

    private void Styles(StyleSheet sheet)
    {
        bool hasDefaults = HasText(sheet.Defaults) || sheet.DefaultParagraphStyle != null;
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
            StringIf("paragraphStyle", sheet.DefaultParagraphStyle?.Id);
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

            StringIf("paragraphStyle", style.ParagraphStyle?.Id);
            StringIf("headerParagraphStyle", style.HeaderParagraphStyle?.Id);
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
                _json.WriteString("embedded", Asset(file.Hash, file.Data, null, "font-" + AssetNames.From(font.Family)));
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
            case ParagraphBlock paragraph when IsPlainParagraph(paragraph, out string? text):
                _json.WriteStringValue(text);
                break;

            case ParagraphBlock paragraph:
                _json.WriteStartObject();
                StringIf("style", Equals(paragraph.Style, _inherited) ? null : paragraph.Style?.Id);
                _json.WritePropertyName("paragraph");
                InlineContent(paragraph.Inlines);
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
                registration.WriteParameters(_json, component.Component, _components!.DataNaming);
                _json.WriteEndObject();
                break;

            case ComponentBlock component:
                // A section, or a component the catalog does not know: its blocks under a name unique in the file.
                _json.WriteStartObject();
                _json.WriteString("section", SectionName(component.Component is SectionComponent section ? section.Name : component.Component.GetType().Name));
                _json.WritePropertyName("blocks");
                Blocks(component.Body);
                _json.WriteEndObject();
                break;

            case ListBlock list:
                _json.WriteStartObject();
                _json.WriteString("style", list.Style.Id);
                _json.WritePropertyName("list");
                ListItems(list.Items);
                _json.WriteEndObject();
                break;

            case TableBlock { BoxStyle: { } boxStyle } box:
                _json.WriteStartObject();
                _json.WriteString("style", boxStyle.Id);
                _json.WritePropertyName("box");
                TableRowModel boxRow = box.AllRows().Single();
                Inheriting(StyleInheritance.ForCell(_styles, box, boxRow), () => Blocks(boxRow.Cells.Single().Content));
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

    /// <summary>The name, or with "-2", "-3", … when the file already has it (names are unique in a file).</summary>
    private string SectionName(string name)
    {
        string unique = name;
        for (int i = 2; !_sectionNames.Add(unique); i++)
        {
            unique = name + "-" + i.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        return unique;
    }

    /// <summary>A paragraph written as a bare string: inherited style, not a list item, one unstyled run.</summary>
    private bool IsPlainParagraph(ParagraphBlock paragraph, out string? text)
    {
        text = null;
        return paragraph.List is null && Equals(paragraph.Style, _inherited) && IsSingleString(paragraph.Inlines, out text);
    }

    private void Inheriting(ParagraphStyleKey? inherited, Action write)
    {
        ParagraphStyleKey? outer = _inherited;
        _inherited = inherited;
        try
        {
            write();
        }
        finally
        {
            _inherited = outer;
        }
    }

    private void Conditional(List<(Condition Condition, Action Write)> branches, Action? otherwise)
    {
        _json.WriteStartObject();
        _json.WritePropertyName("if");
        Condition(branches[0].Condition);
        _json.WritePropertyName("then");
        branches[0].Write();
        if (branches.Count > 1)
        {
            _json.WriteStartArray("elseIf");
            foreach ((Condition condition, Action write) in branches.Skip(1))
            {
                _json.WriteStartObject();
                _json.WritePropertyName("if");
                Condition(condition);
                _json.WritePropertyName("then");
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
                case ParagraphBlock { List: { } info } paragraph when info.Level == 0 && IsSingleString(paragraph.Inlines, out string? text):
                    _json.WriteStringValue(text);
                    break;

                case ParagraphBlock { List: { } info } paragraph:
                    _json.WriteStartObject();
                    if (info.Level != 0)
                    {
                        _json.WriteNumber("level", info.Level);
                    }

                    _json.WritePropertyName("item");
                    InlineContent(paragraph.Inlines);
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
                        conditional.Else is { } e ? () => ListItems(e) : null);
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
        _json.WritePropertyName("table");
        Rows(table, table.Rows);
        _json.WriteEndObject();
    }

    private void Rows(TableBlock table, IEnumerable<TableRowItem> rows)
    {
        _json.WriteStartArray();
        foreach (TableRowItem item in rows)
        {
            switch (item)
            {
                case TableRowModel row:
                    _json.WriteStartObject();
                    if (row.IsHeader)
                    {
                        _json.WriteBoolean("header", true);
                    }

                    if (row.AllowSplit)
                    {
                        _json.WriteBoolean("allowSplit", true);
                    }

                    StringIf("paragraphStyle", row.ParagraphStyle?.Id);
                    _json.WriteStartArray("row");
                    Inheriting(StyleInheritance.ForCell(_styles, table, row), () =>
                    {
                        foreach (TableCellModel cell in row.Cells)
                        {
                            Cell(cell);
                        }
                    });
                    _json.WriteEndArray();
                    _json.WriteEndObject();
                    break;

                case TableRowLoop loop:
                    _json.WriteStartObject();
                    _json.WriteString("forEachRow", loop.Collection.ToString());
                    _json.WriteString("as", loop.ItemName);
                    _json.WritePropertyName("rows");
                    Rows(table, loop.Rows);
                    _json.WriteEndObject();
                    break;

                case TableRowConditional conditional:
                    _json.WriteStartObject();
                    _json.WritePropertyName("rowsIf");
                    Condition(conditional.Branches[0].Condition);
                    _json.WritePropertyName("then");
                    Rows(table, conditional.Branches[0].Rows);
                    if (conditional.Branches.Count > 1)
                    {
                        _json.WriteStartArray("elseIf");
                        foreach ((Condition condition, List<TableRowItem> branchRows) in conditional.Branches.Skip(1))
                        {
                            _json.WriteStartObject();
                            _json.WritePropertyName("if");
                            Condition(condition);
                            _json.WritePropertyName("then");
                            Rows(table, branchRows);
                            _json.WriteEndObject();
                        }

                        _json.WriteEndArray();
                    }

                    if (conditional.Else is { } otherwise)
                    {
                        _json.WritePropertyName("else");
                        Rows(table, otherwise);
                    }

                    _json.WriteEndObject();
                    break;
            }
        }

        _json.WriteEndArray();
    }

    /// <summary>A cell: a string (one plain paragraph), a list of blocks, or an object when it has options.</summary>
    private void Cell(TableCellModel cell)
    {
        bool hasOptions = cell.Span != 1 || cell.Background != null || cell.VerticalAlignment != null;
        string? text = null;
        ParagraphBlock? single = cell.Content is [ParagraphBlock { List: null } only] && IsSingleString(only.Inlines, out text) ? only : null;
        bool ownStyle = single is not null && !Equals(single.Style, _inherited) && single.Style is not null;
        if (!hasOptions && single is not null && !ownStyle)
        {
            _json.WriteStringValue(text);
            return;
        }

        // A cell is a string or an object; one paragraph in its own style is {"style", "content": "…"}.
        _json.WriteStartObject();
        if (cell.Span != 1)
        {
            _json.WriteNumber("span", cell.Span);
        }

        ColorIf("background", cell.Background);
        if (cell.VerticalAlignment is { } alignment)
        {
            _json.WriteString("verticalAlign", Enum(alignment));
        }

        if (single is not null)
        {
            StringIf("style", ownStyle ? single.Style!.Id : null);
            _json.WriteString("content", text);
        }
        else
        {
            _json.WritePropertyName("content");
            Blocks(cell.Content);
        }

        _json.WriteEndObject();
    }


    // ── inlines ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Inline content: one string when it is a single unstyled run of text and placeholders, else a list of runs
    /// (strings, styled <c>{"text": …}</c>) and inline objects.
    /// </summary>
    private void InlineContent(IReadOnlyList<Inline> inlines)
    {
        List<object> parts = Runs(inlines);
        if (parts is [TextRun { Style: null } only])
        {
            _json.WriteStringValue(TextTemplate.Write(only.Pieces));
            return;
        }

        _json.WriteStartArray();
        foreach (object part in parts)
        {
            if (part is TextRun run)
            {
                if (run.Style is null)
                {
                    _json.WriteStringValue(TextTemplate.Write(run.Pieces));
                }
                else
                {
                    _json.WriteStartObject();
                    _json.WriteString("text", TextTemplate.Write(run.Pieces));
                    _json.WriteString("style", run.Style.Id);
                    _json.WriteEndObject();
                }
            }
            else
            {
                InlineObject((Inline)part);
            }
        }

        _json.WriteEndArray();
    }

    /// <summary>Whether the inlines are written as one string (a single unstyled run).</summary>
    private static bool IsSingleString(IReadOnlyList<Inline> inlines, out string? text)
    {
        text = Runs(inlines) is [TextRun { Style: null } only] ? TextTemplate.Write(only.Pieces) : null;
        return text != null;
    }

    /// <summary>A run of text and placeholders in one character style, written as one string.</summary>
    private sealed record TextRun(List<TextPiece> Pieces, CharacterStyleKey? Style);

    /// <summary>
    /// Groups text, values and expressions of one character style into runs, but only where the import splits the
    /// string back into the same inlines: two texts never join (the import would read one text), and a text ending
    /// with '{' never joins a following placeholder (the import would read "{{{").
    /// </summary>
    private static List<object> Runs(IReadOnlyList<Inline> inlines)
    {
        List<object> parts = new List<object>();
        TextRun? current = null;
        foreach (Inline inline in inlines)
        {
            (TextPiece? piece, CharacterStyleKey? style) = inline switch
            {
                TextInline text => ((TextPiece?)new TextPiece.Literal(text.Text), text.Style),
                ValueInline value => (new TextPiece.Value(value.Path.ToString(), value.Format?.Specifier), value.Style),
                ExpressionInline { Condition: not RawCondition } expression => (new TextPiece.Expression(expression.Condition.ToString(), expression.Format?.Specifier), null),
                _ => (null, null),
            };

            if (piece is null)
            {
                current = null;
                parts.Add(inline);
                continue;
            }

            TextPiece? last = current?.Pieces[^1];
            bool joins = current != null
                && Equals(current.Style, style)
                && !(piece is TextPiece.Literal { Text: var literal } && last is TextPiece.Literal { Text: var previous } && !IsControl(literal) && !IsControl(previous))
                && !(piece is not TextPiece.Literal && last is TextPiece.Literal { Text: var before } && before.EndsWith('{'));
            if (joins)
            {
                current!.Pieces.Add(piece);
            }
            else
            {
                current = new TextRun([piece], style);
                parts.Add(current);
            }
        }

        return parts;
    }

    /// <summary>A tab or line break: the import splits a string at it, so it joins the texts around it.</summary>
    private static bool IsControl(string text) => text is "\t" or "\n";

    private void InlineObject(Inline inline)
    {
        _json.WriteStartObject();
        switch (inline)
        {
            case PageFieldInline field:
                _json.WriteBoolean(field.Field == PageField.Number ? "pageNumber" : "pageCount", true);
                StringIf("style", field.Style?.Id);
                break;
            case RightTabInline:
                _json.WriteBoolean("tabRight", true);
                break;
            case ImageInline image:
                _json.WriteString("image", ImageAsset(image.Image));
                _json.WriteString("alt", image.AltText);

                // One size when the other follows from the image's proportions (the import computes it the same way).
                if (ImageFit.Height(image.Height).Resolve(image.Image) == (image.Width, image.Height))
                {
                    _json.WriteString("height", JsonValues.Length(image.Height));
                }
                else if (ImageFit.Width(image.Width).Resolve(image.Image) == (image.Width, image.Height))
                {
                    _json.WriteString("width", JsonValues.Length(image.Width));
                }
                else
                {
                    _json.WriteString("width", JsonValues.Length(image.Width));
                    _json.WriteString("height", JsonValues.Length(image.Height));
                }
                break;
            case ExpressionInline expression:
                // Only raw conditions get here: every other expression is written in text as {{(condition):format}}.
                _json.WritePropertyName("expression");
                Condition(expression.Condition);
                StringIf("format", expression.Format?.Specifier);
                break;
            case InlineConditional conditional:
                _json.WritePropertyName("if");
                Condition(conditional.Branches[0].Condition);
                _json.WritePropertyName("then");
                InlineContent(conditional.Branches[0].Inlines);
                if (conditional.Branches.Count > 1)
                {
                    _json.WriteStartArray("elseIf");
                    foreach ((Condition condition, IReadOnlyList<Inline> branch) in conditional.Branches.Skip(1))
                    {
                        _json.WriteStartObject();
                        _json.WritePropertyName("if");
                        Condition(condition);
                        _json.WritePropertyName("then");
                        InlineContent(branch);
                        _json.WriteEndObject();
                    }

                    _json.WriteEndArray();
                }

                if (conditional.Else is { } otherwise)
                {
                    _json.WritePropertyName("else");
                    InlineContent(otherwise);
                }

                break;
            default:
                throw new NotSupportedException($"The JSON export does not know the inline {inline.GetType().Name}.");
        }

        _json.WriteEndObject();
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

    /// <summary>The asset name for content; the name is kept unless it already names other content.</summary>
    private string Asset(string hash, byte[] data, string? name, string generated)
    {
        string key = "sha256:" + hash;
        _assets[key] = data;
        if (name is null && _unnamed.TryGetValue(key, out string? known))
        {
            return known;
        }

        string wanted = name ?? generated;
        string unique = wanted;
        for (int i = 2; _assetNames.Any(a => a.Key == unique && a.Value != key); i++)
        {
            unique = wanted + "-" + i.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        if (!_assetNames.Any(a => a.Key == unique))
        {
            _assetNames.Add(new KeyValuePair<string, string>(unique, key));
        }

        if (name is null)
        {
            _unnamed[key] = unique;
        }

        return unique;
    }

    private string ImageAsset(ImageSource image) =>
        Asset(image.Hash, image.Data, image.Name, "image-" + (_unnamed.Count + 1).ToString(System.Globalization.CultureInfo.InvariantCulture));

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

/// <summary>A key of the <c>texts</c> section and its translations (language name as written, text), in order.</summary>
internal sealed record TextSection(string Key, IReadOnlyList<KeyValuePair<string, string>> Translations);
