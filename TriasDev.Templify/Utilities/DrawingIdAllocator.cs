// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Globalization;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using Dw = DocumentFormat.OpenXml.Drawing.Wordprocessing;

namespace TriasDev.Templify.Utilities;

/// <summary>
/// Makes drawing object ids unique after template processing.
/// </summary>
/// <remarks>
/// Cloning loop content copies every drawing and VML shape together with its id, so each clone
/// repeats the id of the original. Duplicate <c>wp:docPr</c> ids are a known cause of Word's
/// "unreadable content" repair prompt, and duplicate VML shape ids break shape references.
/// This pass runs once over all story parts (body, headers, footers, footnotes, endnotes,
/// comments) and renumbers only the duplicates, so the first occurrence of an id - usually the
/// original template content - keeps its value. Ids are made unique across the whole package,
/// which also satisfies the per-part uniqueness the schema requires.
/// Relationship ids (e.g. <c>r:embed</c>) are left untouched: clones share the image part.
/// </remarks>
internal static class DrawingIdAllocator
{
    private const string VmlNamespace = "urn:schemas-microsoft-com:vml";
    private const string OfficeNamespace = "urn:schemas-microsoft-com:office:office";
    private const string WordNamespace = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private const string SpidPrefix = "_x0000_s";

    /// <summary>
    /// Renumbers duplicate drawing (<c>wp:docPr</c>) ids and duplicate VML shape ids
    /// (<c>id</c>, <c>o:spid</c>) in all story parts of the document.
    /// </summary>
    public static void EnsureUniqueIds(WordprocessingDocument document)
    {
        List<OpenXmlPartRootElement> roots = GetStoryRoots(document).ToList();

        RenumberDocPrIds(roots);
        RenumberVmlIds(roots);
    }

    private static IEnumerable<OpenXmlPartRootElement> GetStoryRoots(WordprocessingDocument document)
    {
        MainDocumentPart? main = document.MainDocumentPart;
        if (main == null)
        {
            yield break;
        }

        if (main.Document != null)
        {
            yield return main.Document;
        }

        foreach (HeaderPart header in main.HeaderParts)
        {
            if (header.Header != null)
            {
                yield return header.Header;
            }
        }

        foreach (FooterPart footer in main.FooterParts)
        {
            if (footer.Footer != null)
            {
                yield return footer.Footer;
            }
        }

        if (main.FootnotesPart?.Footnotes != null)
        {
            yield return main.FootnotesPart.Footnotes;
        }

        if (main.EndnotesPart?.Endnotes != null)
        {
            yield return main.EndnotesPart.Endnotes;
        }

        if (main.WordprocessingCommentsPart?.Comments != null)
        {
            yield return main.WordprocessingCommentsPart.Comments;
        }
    }

    private static void RenumberDocPrIds(List<OpenXmlPartRootElement> roots)
    {
        List<Dw.DocProperties> docPrs = roots.SelectMany(r => r.Descendants<Dw.DocProperties>()).ToList();
        if (docPrs.Count < 2)
        {
            return;
        }

        uint nextId = docPrs.Max(d => d.Id?.Value ?? 0U);
        HashSet<uint> seen = new HashSet<uint>();

        foreach (Dw.DocProperties docPr in docPrs)
        {
            uint? id = docPr.Id?.Value;
            if (id.HasValue && seen.Add(id.Value))
            {
                continue;
            }

            uint newId = ++nextId;
            seen.Add(newId);
            docPr.Id = newId;

            if (id.HasValue)
            {
                SyncGraphicIds(docPr, id.Value, newId);
            }
        }
    }

    /// <summary>
    /// Pictures and shapes repeat the drawing id in their own <c>cNvPr</c> (e.g. <c>pic:cNvPr</c>).
    /// Keep them in sync with the renumbered <c>docPr</c>; ids of group/canvas children that differ
    /// from the drawing id are local to the drawing and stay unchanged.
    /// </summary>
    private static void SyncGraphicIds(Dw.DocProperties docPr, uint oldId, uint newId)
    {
        OpenXmlElement? drawing = docPr.Parent;
        if (drawing == null)
        {
            return;
        }

        string oldValue = oldId.ToString(CultureInfo.InvariantCulture);
        foreach (OpenXmlElement element in drawing.Descendants())
        {
            if (element.LocalName != "cNvPr")
            {
                continue;
            }

            OpenXmlAttribute idAttribute = element.GetAttribute("id", string.Empty);
            if (idAttribute.Value == oldValue)
            {
                element.SetAttribute(new OpenXmlAttribute(idAttribute.Prefix, "id", string.Empty, newId.ToString(CultureInfo.InvariantCulture)));
            }
        }
    }

    private static void RenumberVmlIds(List<OpenXmlPartRootElement> roots)
    {
        List<OpenXmlElement> shapes = roots
            .SelectMany(r => r.Descendants())
            .Where(e => e.NamespaceUri == VmlNamespace && e.LocalName != "shapetype")
            .ToList();
        if (shapes.Count < 2)
        {
            return;
        }

        RenumberVmlShapeIds(shapes);
        RenumberVmlSpids(shapes);
    }

    private static void RenumberVmlShapeIds(List<OpenXmlElement> shapes)
    {
        HashSet<string> used = new HashSet<string>(StringComparer.Ordinal);
        foreach (OpenXmlElement shape in shapes)
        {
            string? id = GetAttributeValue(shape, "id", string.Empty);
            if (id != null)
            {
                used.Add(id);
            }
        }

        HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (OpenXmlElement shape in shapes)
        {
            string? id = GetAttributeValue(shape, "id", string.Empty);
            if (id == null || seen.Add(id))
            {
                continue;
            }

            string newId = NextFreeSuffix(id, used);
            used.Add(newId);
            seen.Add(newId);
            shape.SetAttribute(new OpenXmlAttribute("id", string.Empty, newId));
            UpdateShapeReferences(shape, id, newId);
        }
    }

    /// <summary>
    /// Embedded objects and ActiveX controls reference their VML shape by id
    /// (<c>o:OLEObject/@ShapeID</c>, <c>w:control/@w:shapeid</c>) inside the same <c>w:object</c>.
    /// </summary>
    private static void UpdateShapeReferences(OpenXmlElement shape, string oldId, string newId)
    {
        OpenXmlElement? container = shape.Ancestors().FirstOrDefault(a =>
            a.NamespaceUri == WordNamespace && (a.LocalName == "object" || a.LocalName == "pict"));
        if (container == null)
        {
            return;
        }

        foreach (OpenXmlElement element in container.Descendants())
        {
            if (element.NamespaceUri == OfficeNamespace && element.LocalName == "OLEObject"
                && GetAttributeValue(element, "ShapeID", string.Empty) == oldId)
            {
                element.SetAttribute(new OpenXmlAttribute("ShapeID", string.Empty, newId));
            }
            else if (element.NamespaceUri == WordNamespace && element.LocalName == "control"
                && GetAttributeValue(element, "shapeid", WordNamespace) == oldId)
            {
                element.SetAttribute(new OpenXmlAttribute("w", "shapeid", WordNamespace, newId));
            }
        }
    }

    private static void RenumberVmlSpids(List<OpenXmlElement> shapes)
    {
        HashSet<string> used = new HashSet<string>(StringComparer.Ordinal);
        long maxNumber = 0;
        foreach (OpenXmlElement shape in shapes)
        {
            string? spid = GetAttributeValue(shape, "spid", OfficeNamespace);
            if (spid == null)
            {
                continue;
            }

            used.Add(spid);
            if (TryParseSpidNumber(spid, out long number))
            {
                maxNumber = Math.Max(maxNumber, number);
            }
        }

        HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (OpenXmlElement shape in shapes)
        {
            string? spid = GetAttributeValue(shape, "spid", OfficeNamespace);
            if (spid == null || seen.Add(spid))
            {
                continue;
            }

            // Keep Word's "_x0000_s1026" format when the original uses it.
            string newSpid = TryParseSpidNumber(spid, out _)
                ? SpidPrefix + (++maxNumber).ToString(CultureInfo.InvariantCulture)
                : NextFreeSuffix(spid, used);
            used.Add(newSpid);
            seen.Add(newSpid);
            shape.SetAttribute(new OpenXmlAttribute("o", "spid", OfficeNamespace, newSpid));
        }
    }

    private static bool TryParseSpidNumber(string spid, out long number)
    {
        number = 0;
        return spid.StartsWith(SpidPrefix, StringComparison.Ordinal)
            && long.TryParse(spid.AsSpan(SpidPrefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out number);
    }

    private static string NextFreeSuffix(string id, HashSet<string> used)
    {
        for (int n = 2; ; n++)
        {
            string candidate = id + "_" + n.ToString(CultureInfo.InvariantCulture);
            if (!used.Contains(candidate))
            {
                return candidate;
            }
        }
    }

    private static string? GetAttributeValue(OpenXmlElement element, string localName, string namespaceUri)
    {
        foreach (OpenXmlAttribute attribute in element.GetAttributes())
        {
            if (attribute.LocalName == localName && attribute.NamespaceUri == namespaceUri)
            {
                return attribute.Value;
            }
        }

        return null;
    }
}
