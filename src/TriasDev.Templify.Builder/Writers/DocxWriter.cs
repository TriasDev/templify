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

    /// <summary>Word's default paragraph style; the builder reserves the id.</summary>
    private const string NormalStyleId = "Normal";

    /// <summary>Word's default character style; the builder reserves the id.</summary>
    private const string DefaultParagraphFontStyleId = "DefaultParagraphFont";

    public static byte[] Write(DocumentModel model)
    {
        return DeterministicZip.Create(new[]
        {
            new PackagePart("[Content_Types].xml", Utf8(ContentTypes())),
            new PackagePart("_rels/.rels", Utf8(PackageRelationships())),
            new PackagePart("docProps/core.xml", Utf8(CoreProperties(model))),
            new PackagePart("word/document.xml", Utf8(XmlDeclaration + MainDocument(model).OuterXml)),
            new PackagePart("word/_rels/document.xml.rels", Utf8(DocumentRelationships())),
            new PackagePart("word/styles.xml", Utf8(XmlDeclaration + StylesPart(model).OuterXml)),
        });
    }

    private static string ContentTypes() =>
        XmlDeclaration
        + "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">"
        + "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>"
        + "<Default Extension=\"xml\" ContentType=\"application/xml\"/>"
        + $"<Override PartName=\"/word/document.xml\" ContentType=\"{MainContentType}\"/>"
        + $"<Override PartName=\"/word/styles.xml\" ContentType=\"{StylesContentType}\"/>"
        + $"<Override PartName=\"/docProps/core.xml\" ContentType=\"{CorePropertiesContentType}\"/>"
        + "</Types>";

    private static string PackageRelationships() =>
        XmlDeclaration
        + "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">"
        + $"<Relationship Id=\"rId1\" Type=\"{OfficeDocumentRelationship}\" Target=\"word/document.xml\"/>"
        + $"<Relationship Id=\"rId2\" Type=\"{CorePropertiesRelationship}\" Target=\"docProps/core.xml\"/>"
        + "</Relationships>";

    private static string DocumentRelationships() =>
        XmlDeclaration
        + "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">"
        + $"<Relationship Id=\"rId1\" Type=\"{StylesRelationship}\" Target=\"styles.xml\"/>"
        + "</Relationships>";

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

    private static Document MainDocument(DocumentModel model)
    {
        Body body = new Body();
        foreach (Block block in model.Body)
        {
            body.Append(WriteBlock(block));
        }

        return new Document(body);
    }

    private static OpenXmlElement WriteBlock(Block block) => block switch
    {
        ParagraphBlock paragraph => WriteParagraph(paragraph),
        // A paragraph of its own carries the break, so it survives when the engine removes the paragraphs around it.
        PageBreakBlock => new Paragraph(new Run(new Break { Type = BreakValues.Page })),
        _ => throw new NotSupportedException($"Block {block.GetType().Name} is not supported by the DOCX writer."),
    };

    private static Paragraph WriteParagraph(ParagraphBlock block)
    {
        Paragraph paragraph = new Paragraph();
        if (block.Style is { } style)
        {
            paragraph.Append(new ParagraphProperties(new ParagraphStyleId { Val = style.Id }));
        }

        foreach (Inline inline in block.Inlines)
        {
            paragraph.Append(WriteInline(inline));
        }

        return paragraph;
    }

    private static Run WriteInline(Inline inline)
    {
        Run run = new Run();
        if (inline.Style is { } style)
        {
            run.Append(new RunProperties(new RunStyle { Val = style.Id }));
        }

        switch (inline)
        {
            case TextInline text:
                foreach ((TextPieceKind kind, string piece) in TextPieces.Split(text.Text))
                {
                    run.Append(kind switch
                    {
                        TextPieceKind.Tab => new TabChar(),
                        TextPieceKind.LineBreak => new Break(),
                        _ => new Text(piece) { Space = SpaceProcessingModeValues.Preserve },
                    });
                }

                return run;

            case ValueInline value:
                // One run per placeholder: the engine then never has to join a placeholder across runs, and the
                // value takes this run's formatting.
                run.Append(new Text(TemplateSyntax.Placeholder(value.Path, value.Format)) { Space = SpaceProcessingModeValues.Preserve });
                return run;

            default:
                throw new NotSupportedException($"Inline {inline.GetType().Name} is not supported by the DOCX writer.");
        }
    }

    private static byte[] Utf8(string xml) => new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(xml);
}
