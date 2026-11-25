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

    /// <summary>The automatic paragraph style of a page break; the prefix is reserved for the builder.</summary>
    private const string PageBreakStyleName = "TemplifyPageBreak";

    private static readonly XNamespace _office = "urn:oasis:names:tc:opendocument:xmlns:office:1.0";
    private static readonly XNamespace _text = "urn:oasis:names:tc:opendocument:xmlns:text:1.0";
    private static readonly XNamespace _style = "urn:oasis:names:tc:opendocument:xmlns:style:1.0";
    private static readonly XNamespace _fo = "urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0";
    private static readonly XNamespace _svg = "urn:oasis:names:tc:opendocument:xmlns:svg-compatible:1.0";
    private static readonly XNamespace _meta = "urn:oasis:names:tc:opendocument:xmlns:meta:1.0";
    private static readonly XNamespace _dc = "http://purl.org/dc/elements/1.1/";
    private static readonly XNamespace _manifest = "urn:oasis:names:tc:opendocument:xmlns:manifest:1.0";

    public static byte[] Write(DocumentModel model)
    {
        return DeterministicZip.Create(new[]
        {
            // ODF 1.3 part 2, 3.3: the first entry, stored, without an extra field.
            new PackagePart("mimetype", Encoding.ASCII.GetBytes(MediaType), Stored: true),
            new PackagePart("content.xml", Serialize(Content(model))),
            new PackagePart("styles.xml", Serialize(Styles(model))),
            new PackagePart("meta.xml", Serialize(Meta(model))),
            new PackagePart("META-INF/manifest.xml", Serialize(Manifest())),
        });
    }

    // ── content.xml ──────────────────────────────────────────────────────────────

    private static XElement Content(DocumentModel model)
    {
        List<Block> blocks = BlockFlattener.Flatten(model.Body).ToList();
        XElement text = new XElement(_office + "text");
        foreach (Block block in blocks)
        {
            text.Add(WriteBlock(block, model.Styles));
        }

        XElement automaticStyles = new XElement(_office + "automatic-styles");
        if (blocks.OfType<PageBreakBlock>().Any())
        {
            automaticStyles.Add(new XElement(
                _style + "style",
                new XAttribute(_style + "name", PageBreakStyleName),
                new XAttribute(_style + "family", "paragraph"),
                new XAttribute(_style + "parent-style-name", StandardStyleName),
                new XElement(_style + "paragraph-properties", new XAttribute(_fo + "break-after", "page"))));
        }

        return new XElement(
            _office + "document-content",
            Namespaces(_office, _text, _style, _fo, _svg),
            new XAttribute(_office + "version", OdfVersion),
            automaticStyles,
            new XElement(_office + "body", text));
    }

    private static XElement WriteBlock(Block block, StyleSheet styles) => block switch
    {
        ParagraphBlock paragraph => WriteParagraph(paragraph, styles),
        // An empty paragraph of its own carries the break (break-after: no empty line on the new page), so it
        // survives when the engine removes the paragraphs around it.
        PageBreakBlock => new XElement(_text + "p", new XAttribute(_text + "style-name", PageBreakStyleName)),
        MarkerBlock marker => WriteMarker(marker),
        _ => throw new NotSupportedException($"Block {block.GetType().Name} is not supported by the ODT writer."),
    };

    private static XElement WriteMarker(MarkerBlock marker)
    {
        XElement paragraph = new XElement(_text + "p", new XAttribute(_text + "style-name", BuilderStyles.TemplateMarker.Id));
        WhitespaceEncoder encoder = new WhitespaceEncoder(paragraph);
        encoder.Text(marker.Text);
        encoder.Finish();
        return paragraph;
    }

    private static XElement WriteParagraph(ParagraphBlock block, StyleSheet styles)
    {
        int? outlineLevel = block.Style is { } key ? styles.OutlineLevelOf(key) : null;
        XAttribute styleName = new XAttribute(_text + "style-name", block.Style?.Id ?? StandardStyleName);
        XElement paragraph = outlineLevel is int level
            ? new XElement(_text + "h", styleName, new XAttribute(_text + "outline-level", level))
            : new XElement(_text + "p", styleName);

        WhitespaceEncoder encoder = new WhitespaceEncoder(paragraph);
        foreach (Inline inline in block.Inlines)
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

                default:
                    throw new NotSupportedException($"Inline {inline.GetType().Name} is not supported by the ODT writer.");
            }

            if (inline.Style is not null)
            {
                encoder.EndSpan();
            }
        }

        encoder.Finish();
        return paragraph;
    }

    // ── styles.xml ───────────────────────────────────────────────────────────────

    private static XElement Styles(DocumentModel model)
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

        return new XElement(
            _office + "document-styles",
            Namespaces(_office, _text, _style, _fo, _svg),
            new XAttribute(_office + "version", OdfVersion),
            FontFaceDeclarations(sheet),
            styles,
            new XElement(_office + "automatic-styles"),
            new XElement(_office + "master-styles"));
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

        return new XElement(
            _office + "document-meta",
            Namespaces(_office, _meta, _dc),
            new XAttribute(_office + "version", OdfVersion),
            meta);
    }

    private static XElement Manifest() =>
        new XElement(
            _manifest + "manifest",
            new XAttribute(XNamespace.Xmlns + "manifest", _manifest.NamespaceName),
            new XAttribute(_manifest + "version", OdfVersion),
            FileEntry("/", MediaType, version: true),
            FileEntry("content.xml", "text/xml"),
            FileEntry("styles.xml", "text/xml"),
            FileEntry("meta.xml", "text/xml"));

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
