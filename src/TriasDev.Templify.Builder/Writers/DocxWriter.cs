// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.Security;
using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;
using TriasDev.Templify.Builder.Model;
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
        List<PackagePart> package = new List<PackagePart>
        {
            new PackagePart("[Content_Types].xml", Utf8(ContentTypes(parts))),
            new PackagePart("_rels/.rels", Utf8(PackageRelationships())),
            new PackagePart("docProps/core.xml", Utf8(CoreProperties(model))),
            new PackagePart("docProps/custom.xml", Utf8(CustomProperties(model))),
            new PackagePart("word/document.xml", Utf8(XmlDeclaration + MainDocument(model, parts).OuterXml)),
            new PackagePart("word/_rels/document.xml.rels", Utf8(DocumentRelationships(parts))),
            new PackagePart("word/styles.xml", Utf8(XmlDeclaration + StylesPart(model).OuterXml)),
        };

        BlockWriter writer = new BlockWriter(model);
        foreach (HeaderFooterPart part in parts)
        {
            OpenXmlCompositeElement root = part.IsHeader ? new Header() : new Footer();
            root.Append(writer.WriteBlocks(part.Blocks));
            if (!root.HasChildren)
            {
                root.Append(new Paragraph());
            }

            package.Add(new PackagePart("word/" + part.FileName, Utf8(XmlDeclaration + root.OuterXml)));
        }

        return DeterministicZip.Create(package);
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

    private static string ContentTypes(List<HeaderFooterPart> parts)
    {
        StringBuilder xml = new StringBuilder(XmlDeclaration)
            .Append("<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">")
            .Append("<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>")
            .Append("<Default Extension=\"xml\" ContentType=\"application/xml\"/>")
            .Append($"<Override PartName=\"/word/document.xml\" ContentType=\"{MainContentType}\"/>")
            .Append($"<Override PartName=\"/word/styles.xml\" ContentType=\"{StylesContentType}\"/>");
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

    private static string DocumentRelationships(List<HeaderFooterPart> parts)
    {
        StringBuilder xml = new StringBuilder(XmlDeclaration)
            .Append("<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">")
            .Append($"<Relationship Id=\"rId1\" Type=\"{StylesRelationship}\" Target=\"styles.xml\"/>");
        foreach (HeaderFooterPart part in parts)
        {
            string type = part.IsHeader ? "header" : "footer";
            xml.Append($"<Relationship Id=\"{part.RelationshipId}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/{type}\" Target=\"{part.FileName}\"/>");
        }

        return xml.Append("</Relationships>").ToString();
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

    private static Document MainDocument(DocumentModel model, List<HeaderFooterPart> parts)
    {
        Body body = new Body();
        body.Append(new BlockWriter(model).WriteBlocks(model.Body));
        body.Append(SectionProperties(model.Page, parts));
        return new Document(body);
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
    private sealed class BlockWriter(DocumentModel model)
    {
        public IEnumerable<OpenXmlElement> WriteBlocks(IEnumerable<Block> blocks) =>
            BlockFlattener.Flatten(blocks).Select(WriteBlock).ToList();

        private OpenXmlElement WriteBlock(Block block) => block switch
        {
            ParagraphBlock paragraph => WriteParagraph(paragraph),
            // A paragraph of its own carries the break, so it survives when the engine removes the paragraphs around it.
            PageBreakBlock => new Paragraph(new Run(new Break { Type = BreakValues.Page })),
            // A marker is a paragraph of its own with one run, which is what the engine's block detection reads.
            MarkerBlock marker => new Paragraph(
                new ParagraphProperties(new ParagraphStyleId { Val = BuilderStyles.TemplateMarker.Id }),
                new Run(new Text(marker.Text) { Space = SpaceProcessingModeValues.Preserve })),
            _ => throw new NotSupportedException($"Block {block.GetType().Name} is not supported by the DOCX writer."),
        };

        private Paragraph WriteParagraph(ParagraphBlock block)
        {
            Paragraph paragraph = new Paragraph();
            bool rightTab = block.Inlines.OfType<RightTabInline>().Any();
            if (block.Style != null || rightTab)
            {
                // CT_PPrBase order: pStyle, …, tabs.
                ParagraphProperties properties = new ParagraphProperties();
                if (block.Style is { } style)
                {
                    properties.Append(new ParagraphStyleId { Val = style.Id });
                }

                if (rightTab)
                {
                    // Word measures tab stops from the text frame's start, whatever the indent.
                    properties.Append(new Tabs(new TabStop { Val = TabStopValues.Right, Position = model.Page.FrameWidth.Twips }));
                }

                paragraph.Append(properties);
            }

            foreach (Inline inline in block.Inlines)
            {
                paragraph.Append(WriteInline(inline));
            }

            return paragraph;
        }

        private static IEnumerable<Run> WriteInline(Inline inline)
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
