// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using TriasDev.Templify.Core;

namespace TriasDev.Templify.Builder.Testing;

/// <summary>
/// A canonical text dump of a document's content for snapshot (golden) tests: one line per paragraph with its style,
/// tables as indented rows and cells, then the headers and footers. Works for templates and filled documents.
/// </summary>
/// <remarks>
/// <para>The notation is the same for both formats:</para>
/// <code>
/// == body
/// Heading1 | Order {{order.number}}
/// TemplifyMarker | {{#if order.isPaid}}
/// Normal | Total:→{{order.total:currency}}
/// table
///   row
///     cell
///       Normal | Owner
/// ListBullet [list 1] | first item
/// == header
/// Normal | {PAGE} / {NUMPAGES}
/// </code>
/// <para>
/// Text: a tab is <c>→</c>, a line break <c>↵</c>, an image <c>[image]</c>, a field <c>{NAME}</c> (its first word;
/// <c>{TOC}</c> for a table of contents). A paragraph without a style, and ODT's "Standard", is <c>Normal</c>; an ODT
/// automatic style is shown as its parent. Layout-only paragraphs one format needs stay visible (DOCX puts a paragraph
/// after a table that ends a part and between tables that touch), so compare snapshots per format.
/// </para>
/// </remarks>
public static class TemplateSnapshot
{
    private static readonly XNamespace _w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private static readonly XNamespace _r = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace _rel = "http://schemas.openxmlformats.org/package/2006/relationships";
    private static readonly XNamespace _office = "urn:oasis:names:tc:opendocument:xmlns:office:1.0";
    private static readonly XNamespace _text = "urn:oasis:names:tc:opendocument:xmlns:text:1.0";
    private static readonly XNamespace _table = "urn:oasis:names:tc:opendocument:xmlns:table:1.0";
    private static readonly XNamespace _style = "urn:oasis:names:tc:opendocument:xmlns:style:1.0";
    private static readonly XNamespace _draw = "urn:oasis:names:tc:opendocument:xmlns:drawing:1.0";

    /// <summary>Dumps a built template.</summary>
    /// <param name="template">The template.</param>
    /// <returns>The dump, lines separated by <c>\n</c>.</returns>
    public static string Create(GeneratedTemplate template)
    {
        ArgumentNullException.ThrowIfNull(template);
        return Create(template.Format, template.Content);
    }

    /// <summary>Dumps a document package (a template or a filled document).</summary>
    /// <param name="format">The format.</param>
    /// <param name="package">The package bytes.</param>
    /// <returns>The dump, lines separated by <c>\n</c>.</returns>
    public static string Create(TemplateFormat format, ReadOnlyMemory<byte> package)
    {
        using MemoryStream stream = new MemoryStream(package.ToArray(), writable: false);
        using ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Read);
        List<string> lines = format switch
        {
            TemplateFormat.Docx => new DocxDump(archive).Dump(),
            TemplateFormat.Odt => new OdtDump(archive).Dump(),
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Only DOCX and ODT can be dumped."),
        };
        return string.Join("\n", lines);
    }

    private static XDocument? Load(ZipArchive archive, string name)
    {
        ZipArchiveEntry? entry = archive.GetEntry(name);
        if (entry is null)
        {
            return null;
        }

        using Stream stream = entry.Open();
        return XDocument.Load(stream);
    }

    // An empty paragraph ends with "|", not "| ": editors strip trailing spaces from golden files.
    private static string Line(int depth, string style, string? list, string text) =>
        new string(' ', depth * 2) + style + (list is null ? string.Empty : " [list " + list + "]") + (text.Length == 0 ? " |" : " | " + text);

    private sealed class DocxDump(ZipArchive archive)
    {
        private readonly List<string> _lines = new List<string>();

        public List<string> Dump()
        {
            XDocument document = Load(archive, "word/document.xml") ?? throw new InvalidDataException("The package has no word/document.xml.");
            XElement body = document.Root?.Element(_w + "body") ?? throw new InvalidDataException("word/document.xml has no body.");
            _lines.Add("== body");
            Blocks(body, 0);

            Dictionary<string, string> targets = (Load(archive, "word/_rels/document.xml.rels")?.Root?.Elements(_rel + "Relationship") ?? [])
                .ToDictionary(r => (string)r.Attribute("Id")!, r => (string)r.Attribute("Target")!, StringComparer.Ordinal);
            XElement? section = body.Element(_w + "sectPr");
            foreach ((string kind, string type, string title) in new[]
            {
                ("header", "default", "header"), ("header", "first", "header first"),
                ("footer", "default", "footer"), ("footer", "first", "footer first"),
            })
            {
                XElement? reference = section?.Elements(_w + kind + "Reference").FirstOrDefault(e => (string?)e.Attribute(_w + "type") == type);
                if (reference?.Attribute(_r + "id") is { } id && targets.TryGetValue(id.Value, out string? target))
                {
                    XDocument? part = Load(archive, "word/" + target.TrimStart('/').Replace("word/", string.Empty, StringComparison.Ordinal));
                    if (part?.Root is { } root)
                    {
                        _lines.Add("== " + title);
                        Blocks(root, 0);
                    }
                }
            }

            return _lines;
        }

        private void Blocks(XElement container, int depth)
        {
            foreach (XElement element in container.Elements())
            {
                if (element.Name == _w + "p")
                {
                    Paragraph(element, depth);
                }
                else if (element.Name == _w + "tbl")
                {
                    _lines.Add(new string(' ', depth * 2) + "table");
                    foreach (XElement row in element.Elements(_w + "tr"))
                    {
                        _lines.Add(new string(' ', (depth + 1) * 2) + "row");
                        foreach (XElement cell in row.Elements(_w + "tc"))
                        {
                            _lines.Add(new string(' ', (depth + 2) * 2) + "cell");
                            Blocks(cell, depth + 3);
                        }
                    }
                }
                else if (element.Name == _w + "sdt")
                {
                    if (element.Element(_w + "sdtContent") is { } content)
                    {
                        Blocks(content, depth);
                    }
                }
                else if (element.Name != _w + "sectPr" && element.Name != _w + "tcPr" && element.Name.LocalName != "bookmarkStart" && element.Name.LocalName != "bookmarkEnd")
                {
                    _lines.Add(new string(' ', depth * 2) + "<" + element.Name.LocalName + ">");
                }
            }
        }

        private void Paragraph(XElement paragraph, int depth)
        {
            XElement? properties = paragraph.Element(_w + "pPr");
            string style = (string?)properties?.Element(_w + "pStyle")?.Attribute(_w + "val") ?? "Normal";
            string? list = null;
            if (properties?.Element(_w + "numPr") is { } numbering)
            {
                int level = (int?)numbering.Element(_w + "ilvl")?.Attribute(_w + "val") ?? 0;
                list = (level + 1).ToString(CultureInfo.InvariantCulture);
            }

            StringBuilder text = new StringBuilder();
            bool inFieldResult = false;
            bool inFieldCode = false;
            StringBuilder code = new StringBuilder();
            foreach (XElement element in paragraph.Descendants())
            {
                if (element.Name == _w + "fldChar")
                {
                    switch ((string?)element.Attribute(_w + "fldCharType"))
                    {
                        case "begin":
                            inFieldCode = true;
                            code.Clear();
                            break;
                        case "separate":
                            text.Append(FieldName(code.ToString()));
                            inFieldCode = false;
                            inFieldResult = true;
                            break;
                        case "end":
                            if (inFieldCode)
                            {
                                text.Append(FieldName(code.ToString()));
                            }

                            inFieldCode = false;
                            inFieldResult = false;
                            break;
                    }

                    continue;
                }

                if (inFieldCode)
                {
                    if (element.Name == _w + "instrText")
                    {
                        code.Append(element.Value);
                    }

                    continue;
                }

                if (inFieldResult || element.Ancestors(_w + "fldSimple").Any())
                {
                    continue;
                }

                if (element.Name == _w + "fldSimple")
                {
                    text.Append(FieldName((string?)element.Attribute(_w + "instr") ?? string.Empty));
                }
                else if (element.Name == _w + "t")
                {
                    text.Append(element.Value);
                }
                else if (element.Name == _w + "tab" && element.Parent?.Name == _w + "r")
                {
                    text.Append('→');
                }
                else if (element.Name == _w + "br")
                {
                    string? type = (string?)element.Attribute(_w + "type");
                    if (type is null or "textWrapping")
                    {
                        text.Append('↵');
                    }
                }
                else if (element.Name == _w + "drawing")
                {
                    text.Append("[image]");
                }
            }

            _lines.Add(Line(depth, style, list, text.ToString()));
        }

        private static string FieldName(string instruction)
        {
            string name = instruction.Trim().Split(' ', 2)[0];
            return "{" + name.ToUpperInvariant() + "}";
        }
    }

    private sealed class OdtDump(ZipArchive archive)
    {
        private readonly List<string> _lines = new List<string>();
        private readonly Dictionary<string, string> _automaticParents = new Dictionary<string, string>(StringComparer.Ordinal);

        public List<string> Dump()
        {
            XDocument content = Load(archive, "content.xml") ?? throw new InvalidDataException("The package has no content.xml.");
            XDocument? styles = Load(archive, "styles.xml");
            foreach (XDocument document in new[] { content, styles }.OfType<XDocument>())
            {
                foreach (XElement style in document.Root?.Element(_office + "automatic-styles")?.Elements(_style + "style") ?? [])
                {
                    _automaticParents[(string)style.Attribute(_style + "name")!] = (string?)style.Attribute(_style + "parent-style-name") ?? "Standard";
                }
            }

            XElement text = content.Root?.Element(_office + "body")?.Element(_office + "text") ?? throw new InvalidDataException("content.xml has no office:text.");
            _lines.Add("== body");
            Blocks(text, 0, 0);

            XElement? master = styles?.Root?.Element(_office + "master-styles")?.Elements(_style + "master-page").FirstOrDefault();
            foreach ((string name, string title) in new[] { ("header", "header"), ("header-first", "header first"), ("footer", "footer"), ("footer-first", "footer first") })
            {
                if (master?.Element(_style + name) is { } region)
                {
                    _lines.Add("== " + title);
                    Blocks(region, 0, 0);
                }
            }

            return _lines;
        }

        private void Blocks(XElement container, int depth, int listLevel)
        {
            foreach (XElement element in container.Elements())
            {
                if (element.Name == _text + "p" || element.Name == _text + "h")
                {
                    Paragraph(element, depth, listLevel);
                }
                else if (element.Name == _text + "list")
                {
                    foreach (XElement item in element.Elements().Where(e => e.Name == _text + "list-item" || e.Name == _text + "list-header"))
                    {
                        Blocks(item, depth, listLevel + 1);
                    }
                }
                else if (element.Name == _table + "table")
                {
                    _lines.Add(new string(' ', depth * 2) + "table");
                    foreach (XElement row in Rows(element))
                    {
                        _lines.Add(new string(' ', (depth + 1) * 2) + "row");
                        foreach (XElement cell in row.Elements(_table + "table-cell"))
                        {
                            _lines.Add(new string(' ', (depth + 2) * 2) + "cell");
                            Blocks(cell, depth + 3, 0);
                        }
                    }
                }
                else if (element.Name == _text + "table-of-content")
                {
                    _lines.Add(Line(depth, "Normal", null, "{TOC}"));
                }
                else if (element.Name == _text + "section")
                {
                    Blocks(element, depth, listLevel);
                }
                else if (element.Name.Namespace != _text || element.Name.LocalName is not ("sequence-decls" or "variable-decls" or "user-field-decls" or "soft-page-break"))
                {
                    if (element.Name != _office + "forms" && element.Name != _table + "table-column" && element.Name != _table + "table-columns")
                    {
                        _lines.Add(new string(' ', depth * 2) + "<" + element.Name.LocalName + ">");
                    }
                }
            }
        }

        private static IEnumerable<XElement> Rows(XElement table)
        {
            foreach (XElement child in table.Elements())
            {
                if (child.Name == _table + "table-row")
                {
                    yield return child;
                }
                else if (child.Name.Namespace == _table && child.Name.LocalName is "table-header-rows" or "table-rows" or "table-row-group")
                {
                    foreach (XElement row in Rows(child))
                    {
                        yield return row;
                    }
                }
            }
        }

        private void Paragraph(XElement paragraph, int depth, int listLevel)
        {
            string style = (string?)paragraph.Attribute(_text + "style-name") ?? "Standard";
            if (_automaticParents.TryGetValue(style, out string? parent))
            {
                style = parent;
            }

            if (style == "Standard")
            {
                style = "Normal";
            }

            StringBuilder text = new StringBuilder();
            Inline(paragraph, text);
            _lines.Add(Line(depth, style, listLevel > 0 ? listLevel.ToString(CultureInfo.InvariantCulture) : null, text.ToString()));
        }

        private static void Inline(XElement element, StringBuilder text)
        {
            foreach (XNode node in element.Nodes())
            {
                if (node is XText t)
                {
                    text.Append(t.Value);
                    continue;
                }

                if (node is not XElement child)
                {
                    continue;
                }

                if (child.Name == _text + "s")
                {
                    text.Append(' ', (int?)child.Attribute(_text + "c") ?? 1);
                }
                else if (child.Name == _text + "tab")
                {
                    text.Append('→');
                }
                else if (child.Name == _text + "line-break")
                {
                    text.Append('↵');
                }
                else if (child.Name == _text + "span" || child.Name == _text + "a")
                {
                    Inline(child, text);
                }
                else if (child.Name == _draw + "frame")
                {
                    text.Append("[image]");
                }
                else if (child.Name == _text + "page-number")
                {
                    text.Append("{PAGE}");
                }
                else if (child.Name == _text + "page-count")
                {
                    text.Append("{NUMPAGES}");
                }
                else if (child.Name.Namespace == _text && child.Name.LocalName is not ("bookmark" or "bookmark-start" or "bookmark-end" or "soft-page-break"))
                {
                    text.Append('{').Append(child.Name.LocalName.ToUpperInvariant()).Append('}');
                }
            }
        }
    }
}
