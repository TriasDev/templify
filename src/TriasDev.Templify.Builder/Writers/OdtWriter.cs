// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using TriasDev.Templify.Builder.Model;

namespace TriasDev.Templify.Builder.Writers;

/// <summary>
/// Writes a <see cref="DocumentModel"/> as an OpenDocument Text package (<c>.odt</c>, ODF 1.3).
/// </summary>
internal static class OdtWriter
{
    private const string MediaType = "application/vnd.oasis.opendocument.text";
    private const string OdfVersion = "1.3";

    /// <summary>LibreOffice's default paragraph style ("Default Paragraph Style"); the builder reserves the name.</summary>
    private const string StandardStyleName = "Standard";

    private static readonly XNamespace _office = "urn:oasis:names:tc:opendocument:xmlns:office:1.0";
    private static readonly XNamespace _text = "urn:oasis:names:tc:opendocument:xmlns:text:1.0";
    private static readonly XNamespace _style = "urn:oasis:names:tc:opendocument:xmlns:style:1.0";
    private static readonly XNamespace _fo = "urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0";
    private static readonly XNamespace _svg = "urn:oasis:names:tc:opendocument:xmlns:svg-compatible:1.0";
    private static readonly XNamespace _table = "urn:oasis:names:tc:opendocument:xmlns:table:1.0";
    private static readonly XNamespace _draw = "urn:oasis:names:tc:opendocument:xmlns:drawing:1.0";
    private static readonly XNamespace _xlink = "http://www.w3.org/1999/xlink";
    private static readonly XNamespace _meta = "urn:oasis:names:tc:opendocument:xmlns:meta:1.0";
    private static readonly XNamespace _dc = "http://purl.org/dc/elements/1.1/";
    private static readonly XNamespace _manifest = "urn:oasis:names:tc:opendocument:xmlns:manifest:1.0";

    public static byte[] Write(DocumentModel model)
    {
        PictureStore pictures = new PictureStore();
        byte[] content = Serialize(Content(model, pictures));
        byte[] styles = Serialize(Styles(model, pictures));
        List<PackagePart> parts = new List<PackagePart>
        {
            // ODF 1.3 part 2, 3.3: the first entry, stored, without an extra field.
            new PackagePart("mimetype", Encoding.ASCII.GetBytes(MediaType), Stored: true),
            new PackagePart("content.xml", content),
            new PackagePart("styles.xml", styles),
            new PackagePart("meta.xml", Serialize(Meta(model))),
        };
        parts.AddRange(pictures.Files.Select(f => new PackagePart(f.Path, f.Image.Data)));
        parts.Add(new PackagePart("META-INF/manifest.xml", Serialize(Manifest(pictures))));
        return DeterministicZip.Create(parts);
    }

    /// <summary>The pictures of a package: each distinct image once in <c>Pictures/</c>, named by its content.</summary>
    private sealed class PictureStore
    {
        private readonly Dictionary<string, string> _paths = new Dictionary<string, string>(StringComparer.Ordinal);

        public List<(string Path, ImageSource Image)> Files { get; } = new List<(string, ImageSource)>();

        public string Path(ImageSource image)
        {
            if (!_paths.TryGetValue(image.Hash, out string? path))
            {
                path = $"Pictures/{image.Hash[..32]}.{image.Extension}";
                _paths[image.Hash] = path;
                Files.Add((path, image));
            }

            return path;
        }
    }

    // ── content.xml ──────────────────────────────────────────────────────────────

    private static XElement Content(DocumentModel model, PictureStore pictures)
    {
        BlockWriter writer = new BlockWriter(model, "Templify", pictures);
        XElement text = new XElement(_office + "text", writer.WriteBlocks(model.Body, model.Page.FrameWidth));

        return new XElement(
            _office + "document-content",
            Namespaces(_office, _text, _style, _fo, _svg, _table, _draw, _xlink),
            new XAttribute(_office + "version", OdfVersion),
            new XElement(_office + "automatic-styles", writer.AutomaticStyles),
            new XElement(_office + "body", text));
    }

    /// <summary>
    /// Writes blocks of the body (content.xml) or of headers and footers (styles.xml) and collects the automatic
    /// styles they need; each part gets its own name prefix.
    /// </summary>
    private sealed class BlockWriter(DocumentModel model, string prefix, PictureStore pictures)
    {
        private int _images;

        private readonly Dictionary<(string Parent, int Position), string> _tabStyles = new Dictionary<(string, int), string>();
        private bool _pageBreakStyle;

        public List<XElement> AutomaticStyles { get; } = new List<XElement>();

        private readonly Dictionary<string, string> _cellStyles = new Dictionary<string, string>(StringComparer.Ordinal);
        private int _tables;
        private bool _rowKeepStyle;

        /// <summary>Gets the width of the page's text frame.</summary>
        public Length PageFrame => model.Page.FrameWidth;

        /// <summary>Writes blocks in a container whose text frame is <paramref name="frame"/> wide.</summary>
        public List<XElement> WriteBlocks(IEnumerable<Block> blocks, Length frame) =>
            BlockFlattener.Flatten(blocks).Select(b => WriteBlock(b, frame)).ToList();

        private XElement WriteBlock(Block block, Length frame) => block switch
        {
            ParagraphBlock paragraph => WriteParagraph(paragraph, frame),
            TableBlock table => WriteTable(table, frame),
            RuleBlock rule => new XElement(_text + "p", new XAttribute(_text + "style-name", RuleStyle(rule))),
            // An empty paragraph of its own carries the break (break-after: no empty line on the new page), so it
            // survives when the engine removes the paragraphs around it.
            PageBreakBlock => new XElement(_text + "p", new XAttribute(_text + "style-name", PageBreakStyle())),
            MarkerBlock marker => WriteMarker(marker),
            _ => throw new NotSupportedException($"Block {block.GetType().Name} is not supported by the ODT writer."),
        };

        private string PageBreakStyle()
        {
            string name = prefix + "PageBreak";
            if (!_pageBreakStyle)
            {
                _pageBreakStyle = true;
                AutomaticStyles.Add(new XElement(
                    _style + "style",
                    new XAttribute(_style + "name", name),
                    new XAttribute(_style + "family", "paragraph"),
                    new XAttribute(_style + "parent-style-name", StandardStyleName),
                    new XElement(_style + "paragraph-properties", new XAttribute(_fo + "break-after", "page"))));
            }

            return name;
        }

        /// <summary>
        /// An automatic paragraph style with a right tab stop at the end of the frame. ODF measures tab stops from
        /// the paragraph's indent, so the style's start indent is subtracted.
        /// </summary>
        private string RightTabStyle(ParagraphStyleKey? parent, Length frame)
        {
            string parentName = parent?.Id ?? StandardStyleName;
            int position = (frame - model.Styles.IndentStartOf(parent)).Millipoints;
            if (!_tabStyles.TryGetValue((parentName, position), out string? name))
            {
                name = prefix + "Tab" + (_tabStyles.Count + 1).ToString(CultureInfo.InvariantCulture);
                _tabStyles[(parentName, position)] = name;
                AutomaticStyles.Add(new XElement(
                    _style + "style",
                    new XAttribute(_style + "name", name),
                    new XAttribute(_style + "family", "paragraph"),
                    new XAttribute(_style + "parent-style-name", parentName),
                    new XElement(
                        _style + "paragraph-properties",
                        new XElement(
                            _style + "tab-stops",
                            new XElement(
                                _style + "tab-stop",
                                new XAttribute(_style + "position", (frame - model.Styles.IndentStartOf(parent)).ToString()),
                                new XAttribute(_style + "type", "right"))))));
            }

            return name;
        }

        // ── tables ───────────────────────────────────────────────────────────────

        private XElement WriteTable(TableBlock table, Length frame)
        {
            TableStyleDefinition? style = model.Styles.Resolve(table);
            Length[] widths = TableLayout.Resolve(table.Columns, frame);
            Length paddingH = style?.PaddingHorizontal ?? TableStyleDefinition.DefaultPaddingHorizontal;
            string name = prefix + "Table" + (++_tables).ToString(CultureInfo.InvariantCulture);

            XElement tableProperties = new XElement(
                _style + "table-properties",
                new XAttribute(_style + "width", widths.Aggregate(Length.Zero, (a, b) => a + b).ToString()),
                new XAttribute(_table + "align", "left"),
                new XAttribute(_table + "border-model", "collapsing"));
            if (model.Styles.SpaceAfterOf(table) is { } space)
            {
                tableProperties.Add(new XAttribute(_fo + "margin-bottom", space.ToString()));
            }

            AutomaticStyles.Add(new XElement(
                _style + "style",
                new XAttribute(_style + "name", name),
                new XAttribute(_style + "family", "table"),
                tableProperties));

            XElement result = new XElement(_table + "table", new XAttribute(_table + "name", name), new XAttribute(_table + "style-name", name));
            for (int i = 0; i < widths.Length; i++)
            {
                string columnStyle = $"{name}.C{(i + 1).ToString(CultureInfo.InvariantCulture)}";
                AutomaticStyles.Add(new XElement(
                    _style + "style",
                    new XAttribute(_style + "name", columnStyle),
                    new XAttribute(_style + "family", "table-column"),
                    new XElement(_style + "table-column-properties", new XAttribute(_style + "column-width", widths[i].ToString()))));
                result.Add(new XElement(_table + "table-column", new XAttribute(_table + "style-name", columnStyle)));
            }

            // Collapsed borders: every cell carries the lines of its edges.
            List<TableRowModel> rows = table.AllRows().ToList();
            XElement? headerRows = null;
            foreach (TableRowItem item in table.Rows)
            {
                foreach (XElement row in WriteRowItem(item, rows, widths, style, paddingH))
                {
                    if (item is TableRowModel { IsHeader: true })
                    {
                        headerRows ??= new XElement(_table + "table-header-rows");
                        headerRows.Add(row);
                        if (headerRows.Parent is null)
                        {
                            result.Add(headerRows);
                        }
                    }
                    else
                    {
                        result.Add(row);
                    }
                }
            }

            return result;
        }

        private IEnumerable<XElement> WriteRowItem(TableRowItem item, List<TableRowModel> rows, Length[] widths, TableStyleDefinition? style, Length paddingH)
        {
            switch (item)
            {
                case TableRowModel row:
                    yield return WriteRow(row, rows, widths, style, paddingH);
                    break;

                case TableRowLoop loop:
                    yield return MarkerRow(TemplateSyntax.ForEach(loop.ItemName, loop.Collection), widths.Length);
                    foreach (XElement row in loop.Rows.SelectMany(r => WriteRowItem(r, rows, widths, style, paddingH)))
                    {
                        yield return row;
                    }

                    yield return MarkerRow(TemplateSyntax.EndForEach, widths.Length);
                    break;

                case TableRowConditional conditional:
                    for (int i = 0; i < conditional.Branches.Count; i++)
                    {
                        (Condition condition, List<TableRowItem> branchRows) = conditional.Branches[i];
                        yield return MarkerRow(i == 0 ? TemplateSyntax.If(condition) : TemplateSyntax.ElseIf(condition), widths.Length);
                        foreach (XElement row in branchRows.SelectMany(r => WriteRowItem(r, rows, widths, style, paddingH)))
                        {
                            yield return row;
                        }
                    }

                    if (conditional.Else is { } otherwise)
                    {
                        yield return MarkerRow(TemplateSyntax.Else, widths.Length);
                        foreach (XElement row in otherwise.SelectMany(r => WriteRowItem(r, rows, widths, style, paddingH)))
                        {
                            yield return row;
                        }
                    }

                    yield return MarkerRow(TemplateSyntax.EndIf, widths.Length);
                    break;

                default:
                    throw new NotSupportedException($"Row item {item.GetType().Name} is not supported by the ODT writer.");
            }
        }

        /// <summary>A marker row: one cell spanning every column with the marker paragraph; Templify removes it when filling.</summary>
        private XElement MarkerRow(string marker, int columns)
        {
            XElement cell = new XElement(
                _table + "table-cell",
                new XAttribute(_table + "style-name", CellStyle(null, null, null, null, null, null, Length.Zero, Length.Zero)),
                new XAttribute(_office + "value-type", "string"),
                WriteMarker(new MarkerBlock(marker)));
            if (columns > 1)
            {
                cell.Add(new XAttribute(_table + "number-columns-spanned", columns));
            }

            XElement row = new XElement(_table + "table-row", cell);
            for (int i = 1; i < columns; i++)
            {
                row.Add(new XElement(_table + "covered-table-cell"));
            }

            return row;
        }

        private XElement WriteRow(TableRowModel row, List<TableRowModel> rows, Length[] widths, TableStyleDefinition? style, Length paddingH)
        {
            XElement result = new XElement(_table + "table-row");
            if (!row.AllowSplit)
            {
                result.Add(new XAttribute(_table + "style-name", RowKeepStyle()));
            }

            int index = rows.IndexOf(row);
            bool first = index == 0;
            bool last = index == rows.Count - 1;
            bool afterHeader = index > 0 && rows[index - 1].IsHeader && !row.IsHeader;
            int column = 0;
            foreach (TableCellModel cell in row.Cells)
            {
                BorderLine? top = first ? style?.Outer : afterHeader && style?.HeaderRule is { } headerRule ? headerRule : style?.RowRule;
                BorderLine? bottom = row.IsHeader && style?.HeaderRule is { } rule ? rule : last ? style?.Outer : style?.RowRule;
                BorderLine? left = column == 0 ? style?.Outer : style?.ColumnRule;
                BorderLine? right = column + cell.Span >= widths.Length ? style?.Outer : style?.ColumnRule;
                Color? background = cell.Background ?? (row.IsHeader ? style?.HeaderBackground : style?.BodyBackground);

                XElement element = new XElement(
                    _table + "table-cell",
                    new XAttribute(_table + "style-name", CellStyle(top, bottom, left, right, background, cell.VerticalAlignment, style?.PaddingVertical ?? Length.Zero, paddingH)),
                    new XAttribute(_office + "value-type", "string"));
                if (cell.Span > 1)
                {
                    element.Add(new XAttribute(_table + "number-columns-spanned", cell.Span));
                }

                List<XElement> content = WriteBlocks(cell.Content, TableLayout.CellFrame(widths, column, cell.Span, paddingH));
                element.Add(content.Count > 0 && content[^1].Name != _table + "table"
                    ? content
                    : content.Append(new XElement(_text + "p", new XAttribute(_text + "style-name", StandardStyleName))));
                result.Add(element);
                for (int i = 1; i < cell.Span; i++)
                {
                    result.Add(new XElement(_table + "covered-table-cell"));
                }

                column += cell.Span;
            }

            return result;
        }

        private readonly Dictionary<string, string> _ruleStyles = new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>An automatic paragraph style for a rule: a 1 pt line height and the line as the top border.</summary>
        private string RuleStyle(RuleBlock rule)
        {
            XElement properties = new XElement(
                _style + "paragraph-properties",
                new XAttribute(_fo + "margin-top", rule.SpaceBefore.ToString()),
                new XAttribute(_fo + "margin-bottom", rule.SpaceAfter.ToString()),
                new XAttribute(_fo + "line-height", "1pt"),
                new XAttribute(_fo + "border-top", Border(rule.Line)),
                new XAttribute(_fo + "border-bottom", "none"),
                new XAttribute(_fo + "border-left", "none"),
                new XAttribute(_fo + "border-right", "none"),
                new XAttribute(_fo + "padding", "0pt"));
            string signature = properties.ToString(SaveOptions.DisableFormatting);
            if (!_ruleStyles.TryGetValue(signature, out string? name))
            {
                name = prefix + "Rule" + (_ruleStyles.Count + 1).ToString(CultureInfo.InvariantCulture);
                _ruleStyles[signature] = name;
                AutomaticStyles.Add(new XElement(
                    _style + "style",
                    new XAttribute(_style + "name", name),
                    new XAttribute(_style + "family", "paragraph"),
                    new XAttribute(_style + "parent-style-name", StandardStyleName),
                    properties,
                    new XElement(_style + "text-properties", new XAttribute(_fo + "font-size", "1pt"))));
            }

            return name;
        }

        private string RowKeepStyle()
        {
            string name = prefix + "RowKeep";
            if (!_rowKeepStyle)
            {
                _rowKeepStyle = true;
                AutomaticStyles.Add(new XElement(
                    _style + "style",
                    new XAttribute(_style + "name", name),
                    new XAttribute(_style + "family", "table-row"),
                    new XElement(_style + "table-row-properties", new XAttribute(_fo + "keep-together", "always"))));
            }

            return name;
        }

        /// <summary>A cell style for a set of borders, background, alignment and padding; equal cells share one.</summary>
        private string CellStyle(BorderLine? top, BorderLine? bottom, BorderLine? left, BorderLine? right, Color? background, CellVerticalAlignment? alignment, Length paddingV, Length paddingH)
        {
            XElement properties = new XElement(
                _style + "table-cell-properties",
                new XAttribute(_fo + "padding-top", paddingV.ToString()),
                new XAttribute(_fo + "padding-bottom", paddingV.ToString()),
                new XAttribute(_fo + "padding-left", paddingH.ToString()),
                new XAttribute(_fo + "padding-right", paddingH.ToString()),
                new XAttribute(_fo + "border-top", Border(top)),
                new XAttribute(_fo + "border-bottom", Border(bottom)),
                new XAttribute(_fo + "border-left", Border(left)),
                new XAttribute(_fo + "border-right", Border(right)));
            if (background is { } fill)
            {
                properties.Add(new XAttribute(_fo + "background-color", "#" + fill.Hex.ToLowerInvariant()));
            }

            if (alignment is { } vertical)
            {
                properties.Add(new XAttribute(_style + "vertical-align", vertical switch
                {
                    CellVerticalAlignment.Center => "middle",
                    CellVerticalAlignment.Bottom => "bottom",
                    _ => "top",
                }));
            }

            string signature = properties.ToString(SaveOptions.DisableFormatting);
            if (!_cellStyles.TryGetValue(signature, out string? name))
            {
                name = prefix + "Cell" + (_cellStyles.Count + 1).ToString(CultureInfo.InvariantCulture);
                _cellStyles[signature] = name;
                AutomaticStyles.Add(new XElement(
                    _style + "style",
                    new XAttribute(_style + "name", name),
                    new XAttribute(_style + "family", "table-cell"),
                    properties));
            }

            return name;
        }

        private static string Border(BorderLine? line) =>
            line is null ? "none" : $"{line.Width} solid #{line.Color.Hex.ToLowerInvariant()}";

        /// <summary>An image as a character: <c>draw:frame</c> with the picture, its title and description (alternative text).</summary>
        private XElement Frame(ImageInline image) =>
            new XElement(
                _draw + "frame",
                new XAttribute(_draw + "name", prefix + "Image" + (++_images).ToString(CultureInfo.InvariantCulture)),
                new XAttribute(_text + "anchor-type", "as-char"),
                new XAttribute(_svg + "width", image.Width.ToString()),
                new XAttribute(_svg + "height", image.Height.ToString()),
                new XAttribute(_draw + "z-index", 0),
                new XElement(
                    _draw + "image",
                    new XAttribute(_xlink + "href", pictures.Path(image.Image)),
                    new XAttribute(_xlink + "type", "simple"),
                    new XAttribute(_xlink + "show", "embed"),
                    new XAttribute(_xlink + "actuate", "onLoad")),
                new XElement(_svg + "title", image.AltText),
                new XElement(_svg + "desc", image.AltText));

        private void WriteInlines(WhitespaceEncoder encoder, IEnumerable<Inline> inlines)
        {
            foreach (Inline inline in inlines)
            {
                if (inline.Style is { } characterStyle)
                {
                    encoder.BeginSpan(new XElement(_text + "span", new XAttribute(_text + "style-name", characterStyle.Id)));
                }

                switch (inline)
                {
                    case TextInline text:
                        foreach ((TextPieceKind kind, string piece) in TextPieces.Split(text.Text))
                        {
                            switch (kind)
                            {
                                case TextPieceKind.Tab:
                                    encoder.Element(new XElement(_text + "tab"));
                                    break;
                                case TextPieceKind.LineBreak:
                                    encoder.Element(new XElement(_text + "line-break"));
                                    break;
                                default:
                                    encoder.Text(piece);
                                    break;
                            }
                        }

                        break;

                    case ValueInline value:
                        encoder.Text(TemplateSyntax.Placeholder(value.Path, value.Format));
                        break;

                    case RightTabInline:
                        encoder.Element(new XElement(_text + "tab"));
                        break;

                    case ImageInline image:
                        encoder.Element(Frame(image));
                        break;

                    case InlineConditional conditional:
                        for (int i = 0; i < conditional.Branches.Count; i++)
                        {
                            encoder.Text(i == 0 ? TemplateSyntax.If(conditional.Branches[i].Condition) : TemplateSyntax.ElseIf(conditional.Branches[i].Condition));
                            WriteInlines(encoder, conditional.Branches[i].Inlines);
                        }

                        if (conditional.Else is { } otherwise)
                        {
                            encoder.Text(TemplateSyntax.Else);
                            WriteInlines(encoder, otherwise);
                        }

                        encoder.Text(TemplateSyntax.EndIf);
                        break;

                    case ExpressionInline expression:
                        encoder.Text(TemplateSyntax.Expression(expression.Condition, expression.Format));
                        break;

                    case PageFieldInline field:
                        encoder.Element(field.Field == PageField.Number
                            ? new XElement(_text + "page-number", new XAttribute(_text + "select-page", "current"), "1")
                            : new XElement(_text + "page-count", "1"));
                        break;

                    default:
                        throw new NotSupportedException($"Inline {inline.GetType().Name} is not supported by the ODT writer.");
                }

                if (inline.Style is not null)
                {
                    encoder.EndSpan();
                }
            }
        }

        private static XElement WriteMarker(MarkerBlock marker)
        {
            XElement paragraph = new XElement(_text + "p", new XAttribute(_text + "style-name", BuilderStyles.TemplateMarker.Id));
            WhitespaceEncoder encoder = new WhitespaceEncoder(paragraph);
            encoder.Text(marker.Text);
            encoder.Finish();
            return paragraph;
        }

        private XElement WriteParagraph(ParagraphBlock block, Length frame)
        {
            int? outlineLevel = block.Style is { } key ? model.Styles.OutlineLevelOf(key) : null;
            string styleName = block.Inlines.OfType<RightTabInline>().Any() ? RightTabStyle(block.Style, frame) : block.Style?.Id ?? StandardStyleName;
            XAttribute styleAttribute = new XAttribute(_text + "style-name", styleName);
            XElement paragraph = outlineLevel is int level
                ? new XElement(_text + "h", styleAttribute, new XAttribute(_text + "outline-level", level))
                : new XElement(_text + "p", styleAttribute);

            WhitespaceEncoder encoder = new WhitespaceEncoder(paragraph);
            WriteInlines(encoder, block.Inlines);
            encoder.Finish();
            return paragraph;
        }
    }

    // ── styles.xml ───────────────────────────────────────────────────────────────

    private static XElement Styles(DocumentModel model, PictureStore pictures)
    {
        StyleSheet sheet = model.Styles;

        XElement defaultText = TextProperties(sheet.Defaults);
        (string language, string? country) = LanguageAndCountry(model.Language);
        defaultText.Add(new XAttribute(_fo + "language", language));
        if (country != null)
        {
            defaultText.Add(new XAttribute(_fo + "country", country));
        }

        XElement styles = new XElement(
            _office + "styles",
            new XElement(
                _style + "default-style",
                new XAttribute(_style + "family", "paragraph"),
                // Word turns widow control on by default; this is the same rule.
                new XElement(_style + "paragraph-properties", new XAttribute(_fo + "orphans", 2), new XAttribute(_fo + "widows", 2)),
                defaultText),
            new XElement(
                _style + "style",
                new XAttribute(_style + "name", StandardStyleName),
                new XAttribute(_style + "family", "paragraph"),
                new XAttribute(_style + "class", "text")));

        foreach (ParagraphStyleDefinition definition in sheet.ParagraphStyles)
        {
            XElement style = new XElement(
                _style + "style",
                new XAttribute(_style + "name", definition.Key.Id),
                new XAttribute(_style + "display-name", definition.Key.DisplayName),
                new XAttribute(_style + "family", "paragraph"),
                new XAttribute(_style + "parent-style-name", definition.BasedOn?.Id ?? StandardStyleName));
            if (definition.Next is { } next)
            {
                style.Add(new XAttribute(_style + "next-style-name", next.Id));
            }

            if (definition.Paragraph.OutlineLevel is int level)
            {
                style.Add(new XAttribute(_style + "default-outline-level", level));
            }

            style.Add(new XAttribute(_style + "class", "text"));
            AddIfNotEmpty(style, ParagraphProperties(definition.Paragraph));
            AddIfNotEmpty(style, TextProperties(definition.Text));
            styles.Add(style);
        }

        foreach (CharacterStyleDefinition definition in sheet.CharacterStyles)
        {
            XElement style = new XElement(
                _style + "style",
                new XAttribute(_style + "name", definition.Key.Id),
                new XAttribute(_style + "display-name", definition.Key.DisplayName),
                new XAttribute(_style + "family", "text"));
            if (definition.BasedOn is { } basedOn)
            {
                style.Add(new XAttribute(_style + "parent-style-name", basedOn.Id));
            }

            AddIfNotEmpty(style, TextProperties(definition.Text));
            styles.Add(style);
        }

        BlockWriter headerFooter = new BlockWriter(model, "TemplifyHf", pictures);
        XElement masterPage = MasterPage(model.Page, headerFooter);
        return new XElement(
            _office + "document-styles",
            Namespaces(_office, _text, _style, _fo, _svg, _table, _draw, _xlink),
            new XAttribute(_office + "version", OdfVersion),
            FontFaceDeclarations(sheet),
            styles,
            new XElement(_office + "automatic-styles", PageLayout(model.Page), headerFooter.AutomaticStyles),
            new XElement(_office + "master-styles", masterPage));
    }

    private const string PageLayoutName = "TemplifyPageLayout";

    private static bool HasHeader(PageSetup page) => page.Header != null || (page.DistinctFirstPage && page.FirstHeader is { Count: > 0 });

    private static bool HasFooter(PageSetup page) => page.Footer != null || (page.DistinctFirstPage && page.FirstFooter is { Count: > 0 });

    /// <summary>
    /// The page layout. ODF measures the top margin to the header, Word to the body text: with a header, the page
    /// margin is the header distance and the header is at least (top margin − header distance) high, so the body starts
    /// where Word starts it. The footer mirrors this.
    /// </summary>
    private static XElement PageLayout(PageSetup page)
    {
        bool header = HasHeader(page);
        bool footer = HasFooter(page);
        XElement layout = new XElement(
            _style + "page-layout",
            new XAttribute(_style + "name", PageLayoutName),
            new XElement(
                _style + "page-layout-properties",
                new XAttribute(_fo + "page-width", page.Size.Width.ToString()),
                new XAttribute(_fo + "page-height", page.Size.Height.ToString()),
                new XAttribute(_style + "print-orientation", "portrait"),
                new XAttribute(_fo + "margin-top", (header ? page.HeaderDistance : page.Top).ToString()),
                new XAttribute(_fo + "margin-bottom", (footer ? page.FooterDistance : page.Bottom).ToString()),
                new XAttribute(_fo + "margin-left", page.Left.ToString()),
                new XAttribute(_fo + "margin-right", page.Right.ToString())));

        layout.Add(new XElement(
            _style + "header-style",
            header ? HeaderFooterProperties(page.Top - page.HeaderDistance, "margin-bottom") : null));
        layout.Add(new XElement(
            _style + "footer-style",
            footer ? HeaderFooterProperties(page.Bottom - page.FooterDistance, "margin-top") : null));
        return layout;
    }

    private static XElement HeaderFooterProperties(Length minHeight, string spacing) =>
        new XElement(
            _style + "header-footer-properties",
            new XAttribute(_fo + "min-height", minHeight.ToString()),
            new XAttribute(_fo + "margin-left", "0pt"),
            new XAttribute(_fo + "margin-right", "0pt"),
            new XAttribute(_fo + spacing, "0pt"));

    /// <summary>The master page "Standard" with the headers and footers (ODF 1.3 header-first / footer-first for the first page).</summary>
    private static XElement MasterPage(PageSetup page, BlockWriter writer)
    {
        XElement master = new XElement(
            _style + "master-page",
            new XAttribute(_style + "name", StandardStyleName),
            new XAttribute(_style + "page-layout-name", PageLayoutName));

        if (HasHeader(page))
        {
            master.Add(Region("header", page.Header, writer));
            if (page.DistinctFirstPage)
            {
                master.Add(Region("header-first", page.FirstHeader, writer));
            }
        }

        if (HasFooter(page))
        {
            master.Add(Region("footer", page.Footer, writer));
            if (page.DistinctFirstPage)
            {
                master.Add(Region("footer-first", page.FirstFooter, writer));
            }
        }

        return master;
    }

    private static XElement Region(string name, List<Block>? blocks, BlockWriter writer)
    {
        List<XElement> content = blocks is null ? new List<XElement>() : writer.WriteBlocks(blocks, writer.PageFrame);
        return new XElement(_style + name, content.Count > 0 ? content : new XElement(_text + "p", new XAttribute(_text + "style-name", StandardStyleName)));
    }

    /// <summary>One <c>style:font-face</c> per family the styles use, in first-use order.</summary>
    private static XElement FontFaceDeclarations(StyleSheet sheet)
    {
        IEnumerable<FontFace?> fonts = new[] { sheet.Defaults.Font }
            .Concat(sheet.ParagraphStyles.Select(s => s.Text.Font))
            .Concat(sheet.CharacterStyles.Select(s => s.Text.Font));

        XElement declarations = new XElement(_office + "font-face-decls");
        foreach (string family in fonts.OfType<FontFace>().Select(f => f.Family).Distinct(StringComparer.Ordinal))
        {
            declarations.Add(new XElement(
                _style + "font-face",
                new XAttribute(_style + "name", family),
                new XAttribute(_svg + "font-family", family.Contains(' ', StringComparison.Ordinal) ? "'" + family + "'" : family)));
        }

        return declarations;
    }

    private static XElement TextProperties(Model.TextProperties text)
    {
        XElement properties = new XElement(_style + "text-properties");
        if (text.Font is { } font)
        {
            properties.Add(new XAttribute(_style + "font-name", font.Family));
        }

        if (text.Size is { } size)
        {
            properties.Add(new XAttribute(_fo + "font-size", size.ToString()));
        }

        if (text.Color is { } color)
        {
            properties.Add(new XAttribute(_fo + "color", "#" + color.Hex.ToLowerInvariant()));
        }

        if (text.Bold is bool bold)
        {
            properties.Add(new XAttribute(_fo + "font-weight", bold ? "bold" : "normal"));
        }

        if (text.Italic is bool italic)
        {
            properties.Add(new XAttribute(_fo + "font-style", italic ? "italic" : "normal"));
        }

        return properties;
    }

    private static XElement ParagraphProperties(Model.ParagraphProperties paragraph)
    {
        XElement properties = new XElement(_style + "paragraph-properties");
        if (paragraph.SpaceBefore is { } before)
        {
            properties.Add(new XAttribute(_fo + "margin-top", before.ToString()));
        }

        if (paragraph.SpaceAfter is { } after)
        {
            properties.Add(new XAttribute(_fo + "margin-bottom", after.ToString()));
        }

        if (paragraph.LineHeight is { } line)
        {
            properties.Add(line.Rule switch
            {
                LineHeightRule.Proportional => new XAttribute(_fo + "line-height", (line.Multiple * 100).ToString("0.#", CultureInfo.InvariantCulture) + "%"),
                LineHeightRule.Exact => new XAttribute(_fo + "line-height", line.Height.ToString()),
                _ => new XAttribute(_style + "line-height-at-least", line.Height.ToString()),
            });
        }

        if (paragraph.Alignment is { } alignment)
        {
            properties.Add(new XAttribute(_fo + "text-align", alignment switch
            {
                TextAlignment.Center => "center",
                TextAlignment.End => "end",
                TextAlignment.Justify => "justify",
                _ => "start",
            }));
        }

        if (paragraph.IndentStart is { } start)
        {
            properties.Add(new XAttribute(_fo + "margin-left", start.ToString()));
        }

        if (paragraph.IndentEnd is { } end)
        {
            properties.Add(new XAttribute(_fo + "margin-right", end.ToString()));
        }

        if (paragraph.FirstLineIndent is { } first)
        {
            properties.Add(new XAttribute(_fo + "text-indent", first.ToString()));
        }

        if (paragraph.KeepWithNext is bool keepNext)
        {
            properties.Add(new XAttribute(_fo + "keep-with-next", keepNext ? "always" : "auto"));
        }

        if (paragraph.KeepLinesTogether is bool keepTogether)
        {
            properties.Add(new XAttribute(_fo + "keep-together", keepTogether ? "always" : "auto"));
        }

        return properties;
    }

    private static void AddIfNotEmpty(XElement style, XElement properties)
    {
        if (properties.HasAttributes)
        {
            style.Add(properties);
        }
    }

    /// <summary>The ODF language and country of a culture: <c>de-DE</c> → (de, DE), <c>en</c> → (en, none).</summary>
    private static (string Language, string? Country) LanguageAndCountry(CultureInfo culture)
    {
        string[] parts = culture.Name.Split('-');
        string? country = parts.Length > 1 && parts[^1].Length == 2 && parts[^1].All(char.IsAsciiLetterUpper) ? parts[^1] : null;
        return (parts[0], country);
    }

    // ── meta.xml and the manifest ────────────────────────────────────────────────

    private static XElement Meta(DocumentModel model)
    {
        XElement meta = new XElement(_office + "meta");
        if (model.Info.Title is { } title)
        {
            meta.Add(new XElement(_dc + "title", title));
        }

        if (model.Info.Subject is { } subject)
        {
            meta.Add(new XElement(_dc + "subject", subject));
        }

        if (model.Info.Author is { } author)
        {
            meta.Add(new XElement(_meta + "initial-creator", author), new XElement(_dc + "creator", author));
        }

        meta.Add(new XElement(_dc + "language", model.Language.Name));
        foreach ((string name, string value) in model.Stamp.ToProperties())
        {
            meta.Add(new XElement(_meta + "user-defined", new XAttribute(_meta + "name", name), value));
        }

        return new XElement(
            _office + "document-meta",
            Namespaces(_office, _meta, _dc),
            new XAttribute(_office + "version", OdfVersion),
            meta);
    }

    private static XElement Manifest(PictureStore pictures) =>
        new XElement(
            _manifest + "manifest",
            new XAttribute(XNamespace.Xmlns + "manifest", _manifest.NamespaceName),
            new XAttribute(_manifest + "version", OdfVersion),
            FileEntry("/", MediaType, version: true),
            FileEntry("content.xml", "text/xml"),
            FileEntry("styles.xml", "text/xml"),
            FileEntry("meta.xml", "text/xml"),
            pictures.Files.Select(f => FileEntry(f.Path, f.Image.MediaType)));

    private static XElement FileEntry(string path, string mediaType, bool version = false) =>
        new XElement(
            _manifest + "file-entry",
            new XAttribute(_manifest + "full-path", path),
            version ? new XAttribute(_manifest + "version", OdfVersion) : null,
            new XAttribute(_manifest + "media-type", mediaType));

    private static IEnumerable<XAttribute> Namespaces(params XNamespace[] namespaces)
    {
        foreach (XNamespace ns in namespaces)
        {
            string prefix = ns == _office ? "office"
                : ns == _text ? "text"
                : ns == _style ? "style"
                : ns == _fo ? "fo"
                : ns == _svg ? "svg"
                : ns == _table ? "table"
                : ns == _draw ? "draw"
                : ns == _xlink ? "xlink"
                : ns == _meta ? "meta"
                : ns == _dc ? "dc"
                : throw new ArgumentOutOfRangeException(nameof(namespaces), ns.NamespaceName, "Unknown namespace.");
            yield return new XAttribute(XNamespace.Xmlns + prefix, ns.NamespaceName);
        }
    }

    private static byte[] Serialize(XElement root)
    {
        XmlWriterSettings settings = new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            Indent = false,
            NewLineHandling = NewLineHandling.Entitize,
        };

        using MemoryStream buffer = new MemoryStream();
        using (XmlWriter writer = XmlWriter.Create(buffer, settings))
        {
            new XDocument(new XDeclaration("1.0", "UTF-8", null), root).Save(writer);
        }

        return buffer.ToArray();
    }

    /// <summary>
    /// Writes text so that it renders exactly under ODF white-space rules: literal white space collapses and is
    /// dropped at the start of a paragraph, so every space that would be lost becomes <c>text:s</c>.
    /// </summary>
    /// <remarks>
    /// The rule is conservative: after the paragraph start, a tab or a line break, and after a literal space, every
    /// space is written as <c>text:s</c>, which always renders. A trailing literal space becomes <c>text:s</c> too.
    /// </remarks>
    private sealed class WhitespaceEncoder
    {
        private readonly XElement _paragraph;
        private readonly StringBuilder _pending = new StringBuilder();
        private XElement _target;
        private bool _collapsible = true;

        public WhitespaceEncoder(XElement paragraph)
        {
            _paragraph = paragraph;
            _target = paragraph;
        }

        public void Text(string text)
        {
            int i = 0;
            while (i < text.Length)
            {
                if (text[i] != ' ')
                {
                    _pending.Append(text[i]);
                    _collapsible = false;
                    i++;
                    continue;
                }

                int count = 0;
                while (i < text.Length && text[i] == ' ')
                {
                    count++;
                    i++;
                }

                if (!_collapsible)
                {
                    _pending.Append(' ');
                    count--;
                }

                if (count > 0)
                {
                    FlushText();
                    _target.Add(Spaces(count));
                }

                _collapsible = true;
            }
        }

        public void Element(XElement element)
        {
            FlushText();
            _target.Add(element);
            _collapsible = true;
        }

        /// <summary>Continues inside a span; white-space state carries over, because spans do not reset it.</summary>
        public void BeginSpan(XElement span)
        {
            FlushText();
            _target.Add(span);
            _target = span;
        }

        public void EndSpan()
        {
            FlushText();
            _target = _paragraph;
        }

        public void Finish()
        {
            FlushText();

            // A trailing literal space, possibly inside the last span, becomes text:s.
            if (_paragraph.DescendantNodes().LastOrDefault() is XText last && last.Value.EndsWith(' '))
            {
                last.Value = last.Value[..^1];
                last.AddAfterSelf(Spaces(1));
                if (last.Value.Length == 0)
                {
                    last.Remove();
                }
            }
        }

        private void FlushText()
        {
            if (_pending.Length > 0)
            {
                _target.Add(new XText(_pending.ToString()));
                _pending.Clear();
            }
        }

        private static XElement Spaces(int count) =>
            count == 1
                ? new XElement(_text + "s")
                : new XElement(_text + "s", new XAttribute(_text + "c", count));
    }
}
