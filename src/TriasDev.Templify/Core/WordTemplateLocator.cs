// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using TriasDev.Templify.Utilities;

namespace TriasDev.Templify.Core;

/// <summary>
/// Describes where an element of a Word template is (see <see cref="TemplateLocation"/>): the part (body, header,
/// footer, note) and the path of paragraphs, tables, rows, cells, text boxes and content controls down to it.
/// </summary>
internal sealed class WordTemplateLocator
{
    private readonly Dictionary<OpenXmlElement, string> _partLabels = new Dictionary<OpenXmlElement, string>(ReferenceEqualityComparer.Instance);

    public WordTemplateLocator(WordprocessingDocument document)
    {
        MainDocumentPart? main = document.MainDocumentPart;
        Body? body = main?.Document?.Body;
        if (main == null || body == null)
        {
            return;
        }

        _partLabels[body] = "body";
        LabelHeadersAndFooters(main, body);
        LabelNotes(main.FootnotesPart?.Footnotes?.Elements<Footnote>().Where(n => IsNormalNote(n.Type)), "footnote");
        LabelNotes(main.EndnotesPart?.Endnotes?.Elements<Endnote>().Where(n => IsNormalNote(n.Type)), "endnote");
    }

    /// <summary>The location of <paramref name="element"/>, with its own text.</summary>
    public string Describe(OpenXmlElement element)
    {
        List<string> path = new List<string>();
        string part = "document";
        for (OpenXmlElement? current = element; current != null; current = current.Parent)
        {
            if (_partLabels.TryGetValue(current, out string? label))
            {
                part = label;
                break;
            }

            string? segment = GetSegment(current);
            if (segment != null)
            {
                path.Add(segment);
            }
        }

        path.Reverse();
        return TemplateLocation.Format(part, path, TemplateElementText.GetOwnText(element));
    }

    /// <summary>
    /// Headers and footers are named after the section reference that uses them, in document order:
    /// <c>header (default)</c>, <c>header (first)</c>, a second part of the same kind and type <c>header 2 (default)</c>.
    /// </summary>
    private void LabelHeadersAndFooters(MainDocumentPart main, Body body)
    {
        Dictionary<string, int> counters = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (HeaderFooterReferenceType reference in body.Descendants<SectionProperties>()
            .SelectMany(s => s.Elements<HeaderFooterReferenceType>()))
        {
            if (reference.Id?.Value is not string id || !main.TryGetPartById(id, out OpenXmlPart? part))
            {
                continue;
            }

            OpenXmlElement? root = part switch
            {
                HeaderPart header => header.Header,
                FooterPart footer => footer.Footer,
                _ => null,
            };
            if (root == null || _partLabels.ContainsKey(root))
            {
                continue;
            }

            string kind = reference is HeaderReference ? "header" : "footer";
            string type = reference.Type?.Value is { } value && value == HeaderFooterValues.First ? "first"
                : reference.Type?.Value is { } even && even == HeaderFooterValues.Even ? "even"
                : "default";
            string key = $"{kind} ({type})";
            int number = counters.TryGetValue(key, out int count) ? count + 1 : 1;
            counters[key] = number;
            _partLabels[root] = number == 1 ? key : $"{kind} {number} ({type})";
        }

        foreach (Header header in main.HeaderParts.Select(p => p.Header).OfType<Header>().Where(h => !_partLabels.ContainsKey(h)))
        {
            _partLabels[header] = "header";
        }

        foreach (Footer footer in main.FooterParts.Select(p => p.Footer).OfType<Footer>().Where(f => !_partLabels.ContainsKey(f)))
        {
            _partLabels[footer] = "footer";
        }
    }

    private void LabelNotes(IEnumerable<OpenXmlElement>? notes, string kind)
    {
        int number = 0;
        foreach (OpenXmlElement note in notes ?? Enumerable.Empty<OpenXmlElement>())
        {
            _partLabels[note] = $"{kind} {++number}";
        }
    }

    private static bool IsNormalNote(EnumValue<FootnoteEndnoteValues>? type) =>
        type?.Value is not FootnoteEndnoteValues value || value == FootnoteEndnoteValues.Normal;

    private static string? GetSegment(OpenXmlElement element) => element switch
    {
        Paragraph paragraph => $"paragraph {IndexAmongSiblings(paragraph)}",
        Table table => $"table {IndexAmongSiblings(table)}",
        TableRow row => $"row {RowIndex(row)}",
        TableCell cell => $"cell {CellIndex(cell)}",
        TextBoxContent box => $"text box {TextBoxIndex(box)}",
        SdtBlock contentControl => $"content control {IndexAmongSiblings(contentControl)}",
        _ => null,
    };

    private static int IndexAmongSiblings<T>(T element)
        where T : OpenXmlElement =>
        element.Parent == null ? 1 : element.Parent.Elements<T>().TakeWhile(e => !ReferenceEquals(e, element)).Count() + 1;

    private static int RowIndex(TableRow row)
    {
        Table? table = row.Ancestors<Table>().FirstOrDefault();
        return table == null
            ? 1
            : table.Descendants<TableRow>()
                .Where(r => ReferenceEquals(r.Ancestors<Table>().FirstOrDefault(), table))
                .TakeWhile(r => !ReferenceEquals(r, row))
                .Count() + 1;
    }

    private static int CellIndex(TableCell cell)
    {
        TableRow? row = cell.Ancestors<TableRow>().FirstOrDefault();
        return row == null
            ? 1
            : TemplateElementText.GetRowCells(row).TakeWhile(c => !ReferenceEquals(c, cell)).Count() + 1;
    }

    private static int TextBoxIndex(TextBoxContent box)
    {
        Paragraph? anchor = box.Ancestors<Paragraph>().FirstOrDefault();
        return anchor == null
            ? 1
            : anchor.Descendants<TextBoxContent>()
                .Where(b => ReferenceEquals(b.Ancestors<Paragraph>().FirstOrDefault(), anchor))
                .TakeWhile(b => !ReferenceEquals(b, box))
                .Count() + 1;
    }
}
