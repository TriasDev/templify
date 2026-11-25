// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.Security;
using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;
using TriasDev.Templify.Builder.Model;
using A = DocumentFormat.OpenXml.Drawing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;
using ParagraphProperties = DocumentFormat.OpenXml.Wordprocessing.ParagraphProperties;
using Styles = DocumentFormat.OpenXml.Wordprocessing.Styles;

namespace TriasDev.Templify.Builder.Writers;

/// <summary>
/// Writes a <see cref="DocumentModel"/> as a WordprocessingML package (<c>.docx</c>).
/// </summary>
/// <remarks>
/// The OpenXML SDK's typed elements build the XML parts; children are appended in the schema's order. The package
/// itself is written here rather than by the SDK's packaging: that would give every build random relationship ids, a
/// random core-properties part name and the current time on every entry.
/// </remarks>
internal static class DocxWriter
{
    private const string XmlDeclaration = "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>";
    private const string MainContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml";
    private const string StylesContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml";
    private const string CorePropertiesContentType = "application/vnd.openxmlformats-package.core-properties+xml";
    private const string OfficeDocumentRelationship = "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument";
    private const string StylesRelationship = "http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles";
    private const string CorePropertiesRelationship = "http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties";
    private const string CustomPropertiesContentType = "application/vnd.openxmlformats-officedocument.custom-properties+xml";
    private const string CustomPropertiesRelationship = "http://schemas.openxmlformats.org/officeDocument/2006/relationships/custom-properties";

    /// <summary>The format id every custom document property carries (FMTID_UserDefinedProperties).</summary>
    private const string UserDefinedPropertiesFormatId = "{D5CDD505-2E9C-101B-9397-08002B2CF9AE}";

    /// <summary>Word's default paragraph style; the builder reserves the id.</summary>
    private const string NormalStyleId = "Normal";

    /// <summary>Word's default character style; the builder reserves the id.</summary>
    private const string DefaultParagraphFontStyleId = "DefaultParagraphFont";

    public static byte[] Write(DocumentModel model)
    {
        List<HeaderFooterPart> parts = HeaderFooterParts(model.Page);
        MediaStore media = new MediaStore();

        // Lists: one numbering instance per list style used, in order of first use.
        Dictionary<ListStyleKey, int> numbering = BlockTree.Paragraphs(model.Body.Concat(model.Page.AllBlocks))
            .Select(p => p.List?.Style).OfType<ListStyleKey>().Distinct()
            .Select((style, index) => (style, index)).ToDictionary(e => e.style, e => e.index + 1);
        string numberingRelationship = "rId" + (parts.Count + 2).ToString(CultureInfo.InvariantCulture);

        // The body's image relationships follow styles (rId1), the header/footer parts and the numbering part.
        BlockWriter bodyWriter = new BlockWriter(model, media, parts.Count + 2 + (numbering.Count > 0 ? 1 : 0), numbering);
        string document = XmlDeclaration + MainDocument(model, parts, bodyWriter).OuterXml;

        List<PackagePart> partFiles = new List<PackagePart>();
        foreach (HeaderFooterPart part in parts)
        {
            BlockWriter writer = new BlockWriter(model, media, 1, numbering);
            OpenXmlCompositeElement root = part.IsHeader ? new Header() : new Footer();
            root.Append(EndWithParagraph(writer.WriteBlocks(part.Blocks, model.Page.FrameWidth)));
            partFiles.Add(new PackagePart("word/" + part.FileName, Utf8(XmlDeclaration + root.OuterXml)));
            if (writer.Images.Count > 0)
            {
                partFiles.Add(new PackagePart($"word/_rels/{part.FileName}.rels", Utf8(Relationships(writer.Images.Select(i => (i.Id, ImageRelationship, i.Target))))));
            }
        }

        List<PackagePart> package = new List<PackagePart>
        {
            new PackagePart("[Content_Types].xml", Utf8(ContentTypes(parts, media, numbering.Count > 0))),
            new PackagePart("_rels/.rels", Utf8(PackageRelationships())),
            new PackagePart("docProps/core.xml", Utf8(CoreProperties(model))),
            new PackagePart("docProps/custom.xml", Utf8(CustomProperties(model))),
            new PackagePart("word/document.xml", Utf8(document)),
            new PackagePart("word/_rels/document.xml.rels", Utf8(DocumentRelationships(parts, bodyWriter.Images, numbering.Count > 0 ? numberingRelationship : null))),
            new PackagePart("word/styles.xml", Utf8(XmlDeclaration + StylesPart(model).OuterXml)),
        };
        if (numbering.Count > 0)
        {
            package.Add(new PackagePart("word/numbering.xml", Utf8(XmlDeclaration + NumberingPart(model, numbering).OuterXml)));
        }

        package.AddRange(partFiles);
        package.AddRange(media.Files.Select(f => new PackagePart("word/" + f.Target, f.Data)));
        return DeterministicZip.Create(package);
    }

    private const string ImageRelationship = "http://schemas.openxmlformats.org/officeDocument/2006/relationships/image";

    /// <summary>The images of a document: each distinct image once in <c>word/media</c>, and drawing ids unique across all parts.</summary>
    private sealed class MediaStore
    {
        private readonly Dictionary<string, string> _targets = new Dictionary<string, string>(StringComparer.Ordinal);

        public List<(string Target, byte[] Data, string Extension)> Files { get; } = new List<(string, byte[], string)>();

        public uint NextDrawingId { get; set; } = 1;

        public string Target(ImageSource image)
        {
            if (!_targets.TryGetValue(image.Hash, out string? target))
            {
                target = $"media/image{(Files.Count + 1).ToString(CultureInfo.InvariantCulture)}.{image.Extension}";
                _targets[image.Hash] = target;
                Files.Add((target, image.Data, image.Extension));
            }

            return target;
        }
    }

    private static string Relationships(IEnumerable<(string Id, string Type, string Target)> relationships)
    {
        StringBuilder xml = new StringBuilder(XmlDeclaration).Append("<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">");
        foreach ((string id, string type, string target) in relationships)
        {
            xml.Append($"<Relationship Id=\"{id}\" Type=\"{type}\" Target=\"{target}\"/>");
        }

        return xml.Append("</Relationships>").ToString();
    }

    /// <summary>A header or footer part: what it holds and how the section refers to it.</summary>
    private sealed record HeaderFooterPart(bool IsHeader, HeaderFooterValues Type, string FileName, string RelationshipId, List<Block> Blocks);

    /// <summary>
    /// The header and footer parts of the page setup. With a distinct first page, the first page always gets its own
    /// parts (empty ones for "none"), so no application falls back to the default header there.
    /// </summary>
    private static List<HeaderFooterPart> HeaderFooterParts(PageSetup page)
    {
        List<(bool IsHeader, HeaderFooterValues Type, List<Block> Blocks)> wanted = new List<(bool, HeaderFooterValues, List<Block>)>();
        if (page.Header is { } header)
        {
            wanted.Add((true, HeaderFooterValues.Default, header));
        }

        if (page.DistinctFirstPage)
        {
            wanted.Add((true, HeaderFooterValues.First, page.FirstHeader ?? new List<Block>()));
        }

        if (page.Footer is { } footer)
        {
            wanted.Add((false, HeaderFooterValues.Default, footer));
        }

        if (page.DistinctFirstPage)
        {
            wanted.Add((false, HeaderFooterValues.First, page.FirstFooter ?? new List<Block>()));
        }

        List<HeaderFooterPart> parts = new List<HeaderFooterPart>();
        int headers = 0;
        int footers = 0;
        for (int i = 0; i < wanted.Count; i++)
        {
            (bool isHeader, HeaderFooterValues type, List<Block> blocks) = wanted[i];
            string fileName = isHeader ? $"header{++headers}.xml" : $"footer{++footers}.xml";
            parts.Add(new HeaderFooterPart(isHeader, type, fileName, "rId" + (i + 2).ToString(CultureInfo.InvariantCulture), blocks));
        }

        return parts;
    }

    private static string ContentTypes(List<HeaderFooterPart> parts, MediaStore media, bool hasNumbering)
    {
        StringBuilder xml = new StringBuilder(XmlDeclaration)
            .Append("<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">")
            .Append("<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>")
            .Append("<Default Extension=\"xml\" ContentType=\"application/xml\"/>");
        foreach (string extension in media.Files.Select(f => f.Extension).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            xml.Append($"<Default Extension=\"{extension}\" ContentType=\"image/{extension}\"/>");
        }

        xml.Append($"<Override PartName=\"/word/document.xml\" ContentType=\"{MainContentType}\"/>")
            .Append($"<Override PartName=\"/word/styles.xml\" ContentType=\"{StylesContentType}\"/>");
        if (hasNumbering)
        {
            xml.Append("<Override PartName=\"/word/numbering.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.numbering+xml\"/>");
        }

        foreach (HeaderFooterPart part in parts)
        {
            string type = part.IsHeader ? "header" : "footer";
            xml.Append($"<Override PartName=\"/word/{part.FileName}\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.{type}+xml\"/>");
        }

        return xml
            .Append($"<Override PartName=\"/docProps/core.xml\" ContentType=\"{CorePropertiesContentType}\"/>")
            .Append($"<Override PartName=\"/docProps/custom.xml\" ContentType=\"{CustomPropertiesContentType}\"/>")
            .Append("</Types>")
            .ToString();
    }

    private static string PackageRelationships() =>
        XmlDeclaration
        + "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">"
        + $"<Relationship Id=\"rId1\" Type=\"{OfficeDocumentRelationship}\" Target=\"word/document.xml\"/>"
        + $"<Relationship Id=\"rId2\" Type=\"{CorePropertiesRelationship}\" Target=\"docProps/core.xml\"/>"
        + $"<Relationship Id=\"rId3\" Type=\"{CustomPropertiesRelationship}\" Target=\"docProps/custom.xml\"/>"
        + "</Relationships>";

    private static string DocumentRelationships(List<HeaderFooterPart> parts, IReadOnlyList<(string Id, string Target)> images, string? numberingRelationship) =>
        Relationships(
            new[] { ("rId1", StylesRelationship, "styles.xml") }
                .Concat(parts.Select(p => (p.RelationshipId, "http://schemas.openxmlformats.org/officeDocument/2006/relationships/" + (p.IsHeader ? "header" : "footer"), p.FileName)))
                .Concat(numberingRelationship is null ? [] : new[] { (numberingRelationship, "http://schemas.openxmlformats.org/officeDocument/2006/relationships/numbering", "numbering.xml") })
                .Concat(images.Select(i => (i.Id, ImageRelationship, i.Target))));

    /// <summary>numbering.xml: an abstract numbering (bullet levels) and an instance per list style used.</summary>
    private static Numbering NumberingPart(DocumentModel model, Dictionary<ListStyleKey, int> numbering)
    {
        Numbering result = new Numbering();
        foreach ((ListStyleKey key, int id) in numbering)
        {
            AbstractNum abstractNum = new AbstractNum(new MultiLevelType { Val = MultiLevelValues.HybridMultilevel }) { AbstractNumberId = id - 1 };
            foreach ((int level, ListLevel definition) in model.Styles.Find(key)?.Levels ?? new SortedDictionary<int, ListLevel>())
            {
                // CT_Lvl order: start, numFmt, lvlText, lvlJc, pPr.
                abstractNum.Append(new Level(
                    new StartNumberingValue { Val = 1 },
                    new NumberingFormat { Val = NumberFormatValues.Bullet },
                    new LevelText { Val = definition.Bullet },
                    new LevelJustification { Val = LevelJustificationValues.Left },
                    new PreviousParagraphProperties(new Indentation { Left = Twips(definition.Indent), Hanging = Twips(definition.Hanging) }))
                { LevelIndex = level });
            }

            result.Append(abstractNum);
        }

        foreach (int id in numbering.Values)
        {
            result.Append(new NumberingInstance(new AbstractNumId { Val = id - 1 }) { NumberID = id });
        }

        return result;
    }

    private static string CoreProperties(DocumentModel model)
    {
        StringBuilder xml = new StringBuilder(XmlDeclaration)
            .Append("<cp:coreProperties")
            .Append(" xmlns:cp=\"http://schemas.openxmlformats.org/package/2006/metadata/core-properties\"")
            .Append(" xmlns:dc=\"http://purl.org/dc/elements/1.1/\">");
        AppendElement(xml, "dc:title", model.Info.Title);
        AppendElement(xml, "dc:subject", model.Info.Subject);
        AppendElement(xml, "dc:creator", model.Info.Author);
        AppendElement(xml, "dc:language", model.Language.Name);
        return xml.Append("</cp:coreProperties>").ToString();
    }

    private static string CustomProperties(DocumentModel model)
    {
        StringBuilder xml = new StringBuilder(XmlDeclaration)
            .Append("<Properties xmlns=\"http://schemas.openxmlformats.org/officeDocument/2006/custom-properties\"")
            .Append(" xmlns:vt=\"http://schemas.openxmlformats.org/officeDocument/2006/docPropsVTypes\">");
        int pid = 2;
        foreach ((string name, string value) in model.Stamp.ToProperties())
        {
            xml.Append("<property fmtid=\"").Append(UserDefinedPropertiesFormatId).Append("\" pid=\"")
                .Append(pid++.ToString(CultureInfo.InvariantCulture)).Append("\" name=\"").Append(SecurityElement.Escape(name)).Append("\">")
                .Append("<vt:lpwstr>").Append(SecurityElement.Escape(value)).Append("</vt:lpwstr></property>");
        }

        return xml.Append("</Properties>").ToString();
    }

    private static void AppendElement(StringBuilder xml, string name, string? value)
    {
        if (value != null)
        {
            xml.Append('<').Append(name).Append('>').Append(SecurityElement.Escape(value)).Append("</").Append(name).Append('>');
        }
    }

    // ── styles.xml ───────────────────────────────────────────────────────────────

    private static Styles StylesPart(DocumentModel model)
    {
        StyleSheet sheet = model.Styles;

        RunPropertiesBaseStyle defaultRun = new RunPropertiesBaseStyle();
        defaultRun.Append(RunPropertyElements(sheet.Defaults));
        defaultRun.Append(new Languages { Val = model.Language.Name });

        Styles styles = new Styles(
            new DocDefaults(
                new RunPropertiesDefault(defaultRun),
                new ParagraphPropertiesDefault(new ParagraphPropertiesBaseStyle(new WidowControl()))));

        Style normal = new Style { Type = StyleValues.Paragraph, StyleId = NormalStyleId, Default = true };
        normal.Append(new StyleName { Val = "Normal" }, new PrimaryStyle());
        styles.Append(normal);

        Style defaultFont = new Style { Type = StyleValues.Character, StyleId = DefaultParagraphFontStyleId, Default = true };
        defaultFont.Append(
            new StyleName { Val = "Default Paragraph Font" },
            new UIPriority { Val = 1 },
            new SemiHidden(),
            new UnhideWhenUsed());
        styles.Append(defaultFont);

        foreach (ParagraphStyleDefinition definition in sheet.ParagraphStyles)
        {
            Style style = new Style { Type = StyleValues.Paragraph, StyleId = definition.Key.Id, CustomStyle = true };
            style.Append(new StyleName { Val = definition.Key.DisplayName });
            style.Append(new BasedOn { Val = definition.BasedOn?.Id ?? NormalStyleId });
            if (definition.Next is { } next)
            {
                style.Append(new NextParagraphStyle { Val = next.Id });
            }

            style.Append(new PrimaryStyle());

            StyleParagraphProperties paragraph = new StyleParagraphProperties();
            paragraph.Append(ParagraphPropertyElements(definition.Paragraph));
            if (paragraph.HasChildren)
            {
                style.Append(paragraph);
            }

            StyleRunProperties run = new StyleRunProperties();
            run.Append(RunPropertyElements(definition.Text));
            if (run.HasChildren)
            {
                style.Append(run);
            }

            styles.Append(style);
        }

        foreach (CharacterStyleDefinition definition in sheet.CharacterStyles)
        {
            Style style = new Style { Type = StyleValues.Character, StyleId = definition.Key.Id, CustomStyle = true };
            style.Append(new StyleName { Val = definition.Key.DisplayName });
            style.Append(new BasedOn { Val = definition.BasedOn?.Id ?? DefaultParagraphFontStyleId });
            style.Append(new PrimaryStyle());

            StyleRunProperties run = new StyleRunProperties();
            run.Append(RunPropertyElements(definition.Text));
            if (run.HasChildren)
            {
                style.Append(run);
            }

            styles.Append(style);
        }

        return styles;
    }

    /// <summary>Run properties in the order of CT_RPr: rFonts, b, bCs, i, iCs, color, sz, szCs.</summary>
    private static IEnumerable<OpenXmlElement> RunPropertyElements(Model.TextProperties text)
    {
        if (text.Font is { } font)
        {
            yield return new RunFonts { Ascii = font.Family, HighAnsi = font.Family, ComplexScript = font.Family, EastAsia = font.Family };
        }

        if (text.Bold is bool bold)
        {
            yield return bold ? new Bold() : new Bold { Val = false };
            yield return bold ? new BoldComplexScript() : new BoldComplexScript { Val = false };
        }

        if (text.Italic is bool italic)
        {
            yield return italic ? new Italic() : new Italic { Val = false };
            yield return italic ? new ItalicComplexScript() : new ItalicComplexScript { Val = false };
        }

        if (text.Color is { } color)
        {
            yield return new DocumentFormat.OpenXml.Wordprocessing.Color { Val = color.Hex };
        }

        if (text.Size is { } size)
        {
            string halfPoints = size.HalfPoints.ToString(CultureInfo.InvariantCulture);
            yield return new FontSize { Val = halfPoints };
            yield return new FontSizeComplexScript { Val = halfPoints };
        }
    }

    /// <summary>Paragraph properties in the order of CT_PPrBase: keepNext, keepLines, spacing, ind, jc, outlineLvl.</summary>
    private static IEnumerable<OpenXmlElement> ParagraphPropertyElements(Model.ParagraphProperties paragraph)
    {
        if (paragraph.KeepWithNext is bool keepNext)
        {
            yield return keepNext ? new KeepNext() : new KeepNext { Val = false };
        }

        if (paragraph.KeepLinesTogether is bool keepLines)
        {
            yield return keepLines ? new KeepLines() : new KeepLines { Val = false };
        }

        if (paragraph.SpaceBefore != null || paragraph.SpaceAfter != null || paragraph.LineHeight != null)
        {
            SpacingBetweenLines spacing = new SpacingBetweenLines();
            if (paragraph.SpaceBefore is { } before)
            {
                spacing.Before = Twips(before);
            }

            if (paragraph.SpaceAfter is { } after)
            {
                spacing.After = Twips(after);
            }

            if (paragraph.LineHeight is { } line)
            {
                switch (line.Rule)
                {
                    case LineHeightRule.Proportional:
                        spacing.Line = ((int)Math.Round(line.Multiple * 240, MidpointRounding.AwayFromZero)).ToString(CultureInfo.InvariantCulture);
                        spacing.LineRule = LineSpacingRuleValues.Auto;
                        break;
                    case LineHeightRule.Exact:
                        spacing.Line = Twips(line.Height);
                        spacing.LineRule = LineSpacingRuleValues.Exact;
                        break;
                    default:
                        spacing.Line = Twips(line.Height);
                        spacing.LineRule = LineSpacingRuleValues.AtLeast;
                        break;
                }
            }

            yield return spacing;
        }

        if (paragraph.IndentStart != null || paragraph.IndentEnd != null || paragraph.FirstLineIndent != null)
        {
            Indentation indentation = new Indentation();
            if (paragraph.IndentStart is { } start)
            {
                indentation.Left = Twips(start);
            }

            if (paragraph.IndentEnd is { } end)
            {
                indentation.Right = Twips(end);
            }

            if (paragraph.FirstLineIndent is { } first)
            {
                if (first < Length.Zero)
                {
                    indentation.Hanging = Twips(-first);
                }
                else
                {
                    indentation.FirstLine = Twips(first);
                }
            }

            yield return indentation;
        }

        if (paragraph.Alignment is { } alignment)
        {
            yield return new Justification
            {
                Val = alignment switch
                {
                    TextAlignment.Center => JustificationValues.Center,
                    TextAlignment.End => JustificationValues.Right,
                    TextAlignment.Justify => JustificationValues.Both,
                    _ => JustificationValues.Left,
                },
            };
        }

        if (paragraph.OutlineLevel is int level)
        {
            yield return new OutlineLevel { Val = level - 1 };
        }
    }

    private static string Twips(Length length) => length.Twips.ToString(CultureInfo.InvariantCulture);

    // ── document.xml ─────────────────────────────────────────────────────────────

    private static Document MainDocument(DocumentModel model, List<HeaderFooterPart> parts, BlockWriter writer)
    {
        Body body = new Body();
        body.Append(EndWithParagraph(writer.WriteBlocks(model.Body, model.Page.FrameWidth)));
        body.Append(SectionProperties(model.Page, parts));
        return new Document(body);
    }

    /// <summary>
    /// The body, a header and a footer end with a paragraph, as Word keeps them: a part that ends with a table makes
    /// LibreOffice's text export loop forever (measured with LibreOffice 26.8 in #279), and Word adds the paragraph on
    /// open anyway. An empty part gets one empty paragraph.
    /// </summary>
    private static List<OpenXmlElement> EndWithParagraph(List<OpenXmlElement> elements)
    {
        if (elements.Count == 0 || elements[^1] is not Paragraph)
        {
            elements.Add(new Paragraph());
        }

        return elements;
    }

    /// <summary>The section properties in the order of CT_SectPr: references, pgSz, pgMar, titlePg.</summary>
    private static SectionProperties SectionProperties(PageSetup page, List<HeaderFooterPart> parts)
    {
        SectionProperties section = new SectionProperties();
        foreach (HeaderFooterPart part in parts)
        {
            section.Append(part.IsHeader
                ? new HeaderReference { Type = part.Type, Id = part.RelationshipId }
                : new FooterReference { Type = part.Type, Id = part.RelationshipId });
        }

        section.Append(new DocumentFormat.OpenXml.Wordprocessing.PageSize { Width = (uint)page.Size.Width.Twips, Height = (uint)page.Size.Height.Twips });
        section.Append(new PageMargin
        {
            Top = page.Top.Twips,
            Right = (uint)page.Right.Twips,
            Bottom = page.Bottom.Twips,
            Left = (uint)page.Left.Twips,
            Header = (uint)page.HeaderDistance.Twips,
            Footer = (uint)page.FooterDistance.Twips,
            Gutter = 0,
        });
        if (page.DistinctFirstPage)
        {
            section.Append(new TitlePage());
        }

        return section;
    }

    /// <summary>Writes blocks of the body, a header or a footer.</summary>
    private sealed class BlockWriter(DocumentModel model, MediaStore media, int firstRelationship, Dictionary<ListStyleKey, int> numbering)
    {
        private readonly Dictionary<string, string> _imageIds = new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>Gets the image relationships of this part (id, target relative to <c>word/</c>).</summary>
        public List<(string Id, string Target)> Images { get; } = new List<(string, string)>();

        /// <summary>Writes blocks in a container whose text frame is <paramref name="frame"/> wide.</summary>
        public List<OpenXmlElement> WriteBlocks(IEnumerable<Block> blocks, Length frame)
        {
            List<OpenXmlElement> elements = new List<OpenXmlElement>();
            foreach (Block block in BlockFlattener.Flatten(blocks))
            {
                // Word merges two tables that touch; a paragraph keeps them apart.
                if (block is TableBlock && elements.Count > 0 && elements[^1] is Table)
                {
                    elements.Add(new Paragraph());
                }

                elements.AddRange(WriteBlock(block, frame));
            }

            return elements;
        }

        private IEnumerable<OpenXmlElement> WriteBlock(Block block, Length frame) => block switch
        {
            ParagraphBlock paragraph => [WriteParagraph(paragraph, frame)],
            // A paragraph of its own carries the break, so it survives when the engine removes the paragraphs around it.
            PageBreakBlock => [new Paragraph(new Run(new Break { Type = BreakValues.Page }))],
            // A marker is a paragraph of its own with one run, which is what the engine's block detection reads.
            MarkerBlock marker => [MarkerParagraph(marker.Text)],
            TableBlock table => WriteTable(table, frame),
            RuleBlock rule => [RuleParagraph(rule)],
            _ => throw new NotSupportedException($"Block {block.GetType().Name} is not supported by the DOCX writer."),
        };

        /// <summary>A rule: an empty 1 pt paragraph whose top border is the line (CT_PPrBase order: pBdr, spacing).</summary>
        private static Paragraph RuleParagraph(RuleBlock rule) => new Paragraph(new ParagraphProperties(
            new ParagraphBorders(Border<TopBorder>(rule.Line)),
            new SpacingBetweenLines
            {
                Before = rule.SpaceBefore.Twips.ToString(CultureInfo.InvariantCulture),
                After = rule.SpaceAfter.Twips.ToString(CultureInfo.InvariantCulture),
                Line = "20",
                LineRule = LineSpacingRuleValues.Exact,
            }));

        private static Paragraph MarkerParagraph(string text) => new Paragraph(
            new ParagraphProperties(new ParagraphStyleId { Val = BuilderStyles.TemplateMarker.Id }),
            new Run(new Text(text) { Space = SpaceProcessingModeValues.Preserve }));

        // ── tables ───────────────────────────────────────────────────────────────

        private IEnumerable<OpenXmlElement> WriteTable(TableBlock table, Length frame)
        {
            TableStyleDefinition? style = model.Styles.Resolve(table);
            Length[] widths = TableLayout.Resolve(table.Columns, frame);
            Length paddingH = style?.PaddingHorizontal ?? TableStyleDefinition.DefaultPaddingHorizontal;
            Length paddingV = style?.PaddingVertical ?? Length.Zero;

            // CT_TblPr order: tblW, tblBorders, tblLayout, tblCellMar, tblLook.
            TableProperties properties = new TableProperties(
                new TableWidth { Width = widths.Sum(w => w.Twips).ToString(CultureInfo.InvariantCulture), Type = TableWidthUnitValues.Dxa });
            if (style != null && (style.Outer != null || style.RowRule != null || style.ColumnRule != null))
            {
                properties.Append(new TableBorders(
                    Border<TopBorder>(style.Outer),
                    Border<LeftBorder>(style.Outer),
                    Border<BottomBorder>(style.Outer),
                    Border<RightBorder>(style.Outer),
                    Border<InsideHorizontalBorder>(style.RowRule),
                    Border<InsideVerticalBorder>(style.ColumnRule)));
            }

            properties.Append(new DocumentFormat.OpenXml.Wordprocessing.TableLayout { Type = TableLayoutValues.Fixed });
            properties.Append(new TableCellMarginDefault(
                new TopMargin { Width = paddingV.Twips.ToString(CultureInfo.InvariantCulture), Type = TableWidthUnitValues.Dxa },
                new TableCellLeftMargin { Width = (short)paddingH.Twips, Type = TableWidthValues.Dxa },
                new BottomMargin { Width = paddingV.Twips.ToString(CultureInfo.InvariantCulture), Type = TableWidthUnitValues.Dxa },
                new TableCellRightMargin { Width = (short)paddingH.Twips, Type = TableWidthValues.Dxa }));

            Table result = new Table(properties, new TableGrid(widths.Select(w => new GridColumn { Width = w.Twips.ToString(CultureInfo.InvariantCulture) })));
            foreach (TableRowItem item in table.Rows)
            {
                result.Append(WriteRowItem(item, widths, style, paddingH));
            }

            List<OpenXmlElement> elements = [result];
            if (model.Styles.SpaceAfterOf(table) is { } space && space > Length.Zero)
            {
                // Word tables have no space after: a 1 pt paragraph with the space carries it.
                elements.Add(new Paragraph(new ParagraphProperties(new SpacingBetweenLines
                {
                    Before = "0",
                    After = space.Twips.ToString(CultureInfo.InvariantCulture),
                    Line = "20",
                    LineRule = LineSpacingRuleValues.Exact,
                })));
            }

            return elements;
        }

        private IEnumerable<OpenXmlElement> WriteRowItem(TableRowItem item, Length[] widths, TableStyleDefinition? style, Length paddingH)
        {
            switch (item)
            {
                case TableRowModel row:
                    yield return WriteRow(row, widths, style, paddingH);
                    break;

                case TableRowLoop loop:
                    yield return MarkerRow(TemplateSyntax.ForEach(loop.ItemName, loop.Collection), widths);
                    foreach (OpenXmlElement row in loop.Rows.SelectMany(r => WriteRowItem(r, widths, style, paddingH)))
                    {
                        yield return row;
                    }

                    yield return MarkerRow(TemplateSyntax.EndForEach, widths);
                    break;

                case TableRowConditional conditional:
                    for (int i = 0; i < conditional.Branches.Count; i++)
                    {
                        (Condition condition, List<TableRowItem> rows) = conditional.Branches[i];
                        yield return MarkerRow(i == 0 ? TemplateSyntax.If(condition) : TemplateSyntax.ElseIf(condition), widths);
                        foreach (OpenXmlElement row in rows.SelectMany(r => WriteRowItem(r, widths, style, paddingH)))
                        {
                            yield return row;
                        }
                    }

                    if (conditional.Else is { } otherwise)
                    {
                        yield return MarkerRow(TemplateSyntax.Else, widths);
                        foreach (OpenXmlElement row in otherwise.SelectMany(r => WriteRowItem(r, widths, style, paddingH)))
                        {
                            yield return row;
                        }
                    }

                    yield return MarkerRow(TemplateSyntax.EndIf, widths);
                    break;

                default:
                    throw new NotSupportedException($"Row item {item.GetType().Name} is not supported by the DOCX writer.");
            }
        }

        /// <summary>A marker row: one cell over the whole grid holding the marker paragraph; Templify removes it when filling.</summary>
        private static TableRow MarkerRow(string marker, Length[] widths)
        {
            TableCellProperties properties = new TableCellProperties(
                new TableCellWidth { Width = widths.Sum(w => w.Twips).ToString(CultureInfo.InvariantCulture), Type = TableWidthUnitValues.Dxa });
            if (widths.Length > 1)
            {
                properties.Append(new GridSpan { Val = widths.Length });
            }

            return new TableRow(new TableCell(properties, MarkerParagraph(marker)));
        }

        private TableRow WriteRow(TableRowModel row, Length[] widths, TableStyleDefinition? style, Length paddingH)
        {
            TableRow result = new TableRow();
            TableRowProperties properties = new TableRowProperties();
            if (!row.AllowSplit)
            {
                properties.Append(new CantSplit());
            }

            if (row.IsHeader)
            {
                properties.Append(new TableHeader());
            }

            if (properties.HasChildren)
            {
                result.Append(properties);
            }

            int column = 0;
            foreach (TableCellModel cell in row.Cells)
            {
                // The cell width is the sum of its grid columns as written (each rounded once), so tcW and tblGrid agree.
                int twips = widths.Skip(column).Take(cell.Span).Sum(w => w.Twips);
                result.Append(WriteCell(cell, row.IsHeader, twips, TableLayout.CellFrame(widths, column, cell.Span, paddingH), style));
                column += cell.Span;
            }

            return result;
        }

        private TableCell WriteCell(TableCellModel cell, bool isHeader, int widthTwips, Length contentFrame, TableStyleDefinition? style)
        {
            // CT_TcPr order: tcW, gridSpan, tcBorders, shd, vAlign.
            TableCellProperties properties = new TableCellProperties(
                new TableCellWidth { Width = widthTwips.ToString(CultureInfo.InvariantCulture), Type = TableWidthUnitValues.Dxa });
            if (cell.Span > 1)
            {
                properties.Append(new GridSpan { Val = cell.Span });
            }

            if (isHeader && style?.HeaderRule is { } rule)
            {
                properties.Append(new TableCellBorders(Border<BottomBorder>(rule)));
            }

            Color? background = cell.Background ?? (isHeader ? style?.HeaderBackground : style?.BodyBackground);
            if (background is { } fill)
            {
                properties.Append(new Shading { Val = ShadingPatternValues.Clear, Color = "auto", Fill = fill.Hex });
            }

            if (cell.VerticalAlignment is { } alignment)
            {
                properties.Append(new TableCellVerticalAlignment
                {
                    Val = alignment switch
                    {
                        CellVerticalAlignment.Center => TableVerticalAlignmentValues.Center,
                        CellVerticalAlignment.Bottom => TableVerticalAlignmentValues.Bottom,
                        _ => TableVerticalAlignmentValues.Top,
                    },
                });
            }

            TableCell result = new TableCell(properties);
            result.Append(WriteBlocks(cell.Content, contentFrame));

            // A cell ends with a paragraph (schema and Word); an empty cell has one.
            if (result.LastChild is not Paragraph)
            {
                result.Append(new Paragraph());
            }

            return result;
        }

        private static T Border<T>(BorderLine? line)
            where T : BorderType, new()
        {
            return line is null
                ? new T { Val = BorderValues.Nil }
                : new T
                {
                    Val = BorderValues.Single,
                    Size = (uint)Math.Clamp((int)Math.Round(line.Width.Points * 8, MidpointRounding.AwayFromZero), 2, 96),
                    Space = 0,
                    Color = line.Color.Hex,
                };
        }

        private Paragraph WriteParagraph(ParagraphBlock block, Length frame)
        {
            Paragraph paragraph = new Paragraph();
            bool rightTab = block.Inlines.OfType<RightTabInline>().Any();
            ParagraphStyleKey? paragraphStyle = block.Style ?? (block.List is { } listItem ? model.Styles.Find(listItem.Style)?.ItemStyle : null);
            if (paragraphStyle != null || rightTab || block.List != null)
            {
                // CT_PPrBase order: pStyle, …, numPr, …, tabs.
                ParagraphProperties properties = new ParagraphProperties();
                if (paragraphStyle is { } style)
                {
                    properties.Append(new ParagraphStyleId { Val = style.Id });
                }

                if (block.List is { } item && numbering.TryGetValue(item.Style, out int numId))
                {
                    properties.Append(new NumberingProperties(new NumberingLevelReference { Val = item.Level }, new NumberingId { Val = numId }));
                }

                if (rightTab)
                {
                    // Word measures tab stops from the text frame's start, whatever the indent.
                    properties.Append(new Tabs(new TabStop { Val = TabStopValues.Right, Position = frame.Twips }));
                }

                paragraph.Append(properties);
            }

            foreach (Inline inline in block.Inlines)
            {
                paragraph.Append(WriteInline(inline));
            }

            return paragraph;
        }

        private IEnumerable<Run> WriteInline(Inline inline)
        {
            switch (inline)
            {
                case TextInline text:
                    Run textRun = NewRun(inline.Style);
                    foreach ((TextPieceKind kind, string piece) in TextPieces.Split(text.Text))
                    {
                        textRun.Append(kind switch
                        {
                            TextPieceKind.Tab => new TabChar(),
                            TextPieceKind.LineBreak => new Break(),
                            _ => new Text(piece) { Space = SpaceProcessingModeValues.Preserve },
                        });
                    }

                    return [textRun];

                case ValueInline value:
                    // One run per placeholder: the engine then never has to join a placeholder across runs, and the
                    // value takes this run's formatting.
                    Run valueRun = NewRun(inline.Style);
                    valueRun.Append(new Text(TemplateSyntax.Placeholder(value.Path, value.Format)) { Space = SpaceProcessingModeValues.Preserve });
                    return [valueRun];

                case RightTabInline:
                    Run tabRun = NewRun(null);
                    tabRun.Append(new TabChar());
                    return [tabRun];

                case ImageInline image:
                    Run imageRun = NewRun(null);
                    imageRun.Append(Drawing(image));
                    return [imageRun];

                case InlineConditional conditional:
                    // Markers and content stay in their own runs; the engine resolves the conditional inside the paragraph.
                    List<Run> runs = new List<Run>();
                    for (int i = 0; i < conditional.Branches.Count; i++)
                    {
                        runs.Add(MarkerRun(i == 0 ? TemplateSyntax.If(conditional.Branches[i].Condition) : TemplateSyntax.ElseIf(conditional.Branches[i].Condition)));
                        runs.AddRange(conditional.Branches[i].Inlines.SelectMany(WriteInline));
                    }

                    if (conditional.Else is { } otherwise)
                    {
                        runs.Add(MarkerRun(TemplateSyntax.Else));
                        runs.AddRange(otherwise.SelectMany(WriteInline));
                    }

                    runs.Add(MarkerRun(TemplateSyntax.EndIf));
                    return runs;

                case ExpressionInline expression:
                    return [MarkerRun(TemplateSyntax.Expression(expression.Condition, expression.Format))];

                case PageFieldInline field:
                    // A complex field with a cached result; Word and LibreOffice compute the value when they lay out.
                    string instruction = field.Field == PageField.Number ? " PAGE " : " NUMPAGES ";
                    return
                    [
                        FieldRun(field.Style, new FieldChar { FieldCharType = FieldCharValues.Begin }),
                        FieldRun(field.Style, new FieldCode(instruction) { Space = SpaceProcessingModeValues.Preserve }),
                        FieldRun(field.Style, new FieldChar { FieldCharType = FieldCharValues.Separate }),
                        FieldRun(field.Style, new Text("1")),
                        FieldRun(field.Style, new FieldChar { FieldCharType = FieldCharValues.End }),
                    ];

                default:
                    throw new NotSupportedException($"Inline {inline.GetType().Name} is not supported by the DOCX writer.");
            }
        }

        /// <summary>An inline picture: DrawingML with the relationship of this part and a document-wide unique id.</summary>
        private Drawing Drawing(ImageInline image)
        {
            string target = media.Target(image.Image);
            if (!_imageIds.TryGetValue(target, out string? relationship))
            {
                relationship = "rId" + (firstRelationship + Images.Count).ToString(CultureInfo.InvariantCulture);
                _imageIds[target] = relationship;
                Images.Add((relationship, target));
            }

            uint id = media.NextDrawingId++;
            long cx = Emu(image.Width);
            long cy = Emu(image.Height);
            string name = "Picture " + id.ToString(CultureInfo.InvariantCulture);
            return new Drawing(new DW.Inline(
                new DW.Extent { Cx = cx, Cy = cy },
                new DW.EffectExtent { LeftEdge = 0, TopEdge = 0, RightEdge = 0, BottomEdge = 0 },
                new DW.DocProperties { Id = id, Name = name, Description = image.AltText },
                new DW.NonVisualGraphicFrameDrawingProperties(new A.GraphicFrameLocks { NoChangeAspect = true }),
                new A.Graphic(new A.GraphicData(
                    new PIC.Picture(
                        new PIC.NonVisualPictureProperties(
                            new PIC.NonVisualDrawingProperties { Id = 0U, Name = name, Description = image.AltText },
                            new PIC.NonVisualPictureDrawingProperties()),
                        new PIC.BlipFill(new A.Blip { Embed = relationship }, new A.Stretch(new A.FillRectangle())),
                        new PIC.ShapeProperties(
                            new A.Transform2D(new A.Offset { X = 0L, Y = 0L }, new A.Extents { Cx = cx, Cy = cy }),
                            new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle })))
                { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" }))
            {
                DistanceFromTop = 0U,
                DistanceFromBottom = 0U,
                DistanceFromLeft = 0U,
                DistanceFromRight = 0U,
            });
        }

        /// <summary>English metric units: 12 700 per point.</summary>
        private static long Emu(Length length) => (long)Math.Round(length.Points * 12700, MidpointRounding.AwayFromZero);

        private static Run MarkerRun(string text) => new Run(new Text(text) { Space = SpaceProcessingModeValues.Preserve });

        private static Run FieldRun(CharacterStyleKey? style, OpenXmlElement content)
        {
            Run run = NewRun(style);
            run.Append(content);
            return run;
        }

        private static Run NewRun(CharacterStyleKey? style)
        {
            Run run = new Run();
            if (style != null)
            {
                run.Append(new RunProperties(new RunStyle { Val = style.Id }));
            }

            return run;
        }
    }

    private static byte[] Utf8(string xml) => new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(xml);
}
