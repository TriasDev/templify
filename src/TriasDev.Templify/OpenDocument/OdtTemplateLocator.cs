// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Xml.Linq;
using TriasDev.Templify.Core;

namespace TriasDev.Templify.OpenDocument;

/// <summary>
/// Describes where an element of an OpenDocument template is (see <see cref="TemplateLocation"/>): the part (body, or a
/// master page's header or footer) and the path of paragraphs, tables, rows, cells, lists, items, sections and frames.
/// </summary>
internal sealed class OdtTemplateLocator
{
    private readonly Dictionary<XElement, string> _partLabels = new Dictionary<XElement, string>(ReferenceEqualityComparer.Instance);

    public OdtTemplateLocator(XElement contentRoot, XElement? stylesRoot)
    {
        XElement? body = contentRoot.Element(OdfNames.OfficeBody)?.Element(OdfNames.OfficeText);
        if (body != null)
        {
            _partLabels[body] = "body";
        }

        if (stylesRoot == null)
        {
            return;
        }

        foreach (XElement headerOrFooter in OdtTemplateEngine.GetHeadersAndFooters(stylesRoot))
        {
            string masterPage = headerOrFooter.Parent?.Attribute(OdfNames.Style + "name")?.Value ?? string.Empty;
            string kind = headerOrFooter.Name.LocalName.Replace('-', ' ');
            _partLabels[headerOrFooter] = $"{kind} ({masterPage})";
        }
    }

    /// <summary>The location of <paramref name="element"/>, with its own text.</summary>
    public string Describe(XElement element)
    {
        List<string> path = new List<string>();
        string part = "document";
        for (XElement? current = element; current != null; current = current.Parent)
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
        return TemplateLocation.Format(part, path, OdtMarkerText.GetOwnText(element));
    }

    private static string? GetSegment(XElement element)
    {
        if (OdfNames.IsParagraph(element))
        {
            return $"paragraph {IndexAmongSiblings(element, OdfNames.IsParagraph)}";
        }

        if (element.Name == OdfNames.TableElement)
        {
            return $"table {IndexAmongSiblings(element, e => e.Name == OdfNames.TableElement)}";
        }

        if (OdtMarkerText.IsRow(element))
        {
            XElement? table = element.Ancestors(OdfNames.TableElement).FirstOrDefault();
            return table == null ? "row 1" : $"row {IndexOf(Nearest(table, OdtMarkerText.IsRow, OdfNames.TableElement), element)}";
        }

        if (OdtMarkerText.IsCell(element))
        {
            return element.Parent == null ? "cell 1" : $"cell {IndexOf(OdtMarkerText.GetRowCells(element.Parent), element)}";
        }

        if (element.Name == OdfNames.List)
        {
            return $"list {IndexAmongSiblings(element, e => e.Name == OdfNames.List)}";
        }

        if (element.Name == OdfNames.ListItem || element.Name == OdfNames.ListHeader)
        {
            return $"item {IndexAmongSiblings(element, e => e.Name == OdfNames.ListItem || e.Name == OdfNames.ListHeader)}";
        }

        if (element.Name == OdfNames.Section)
        {
            return $"section {IndexAmongSiblings(element, e => e.Name == OdfNames.Section)}";
        }

        if (element.Name == OdfNames.Draw + "frame")
        {
            XElement? anchor = element.Ancestors().FirstOrDefault(OdfNames.IsParagraph);
            return anchor == null
                ? "frame 1"
                : $"frame {IndexOf(Nearest(anchor, e => e.Name == OdfNames.Draw + "frame", null, OdfNames.IsParagraph), element)}";
        }

        return null;
    }

    private static int IndexAmongSiblings(XElement element, Func<XElement, bool> sameKind) =>
        element.Parent == null ? 1 : IndexOf(element.Parent.Elements().Where(sameKind), element);

    private static int IndexOf(IEnumerable<XElement> elements, XElement element) =>
        elements.TakeWhile(e => !ReferenceEquals(e, element)).Count() + 1;

    /// <summary>Descendants of <paramref name="root"/> matching <paramref name="match"/> whose nearest container is root.</summary>
    private static IEnumerable<XElement> Nearest(XElement root, Func<XElement, bool> match, XName? containerName, Func<XElement, bool>? isContainer = null) =>
        root.Descendants().Where(match).Where(e => ReferenceEquals(
            e.Ancestors().FirstOrDefault(a => containerName != null ? a.Name == containerName : isContainer!(a)),
            root));
}
