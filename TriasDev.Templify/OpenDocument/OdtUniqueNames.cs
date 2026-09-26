// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.Xml.Linq;

namespace TriasDev.Templify.OpenDocument;

/// <summary>
/// Makes object names and ids unique after template processing. The OpenDocument counterpart of
/// <c>DrawingIdAllocator</c>.
/// </summary>
/// <remarks>
/// <para>
/// Cloning loop content copies every frame, shape, table, section and note together with its name or id.
/// ODF requires these to be unique: LibreOffice renames duplicate frames and tables on load (breaking
/// references to them), and duplicate <c>xml:id</c> values make the document invalid.
/// </para>
/// <para>
/// The pass runs once over the body and the headers and footers (<c>content.xml</c> and <c>styles.xml</c>
/// share one name space) and changes only duplicates, so the first occurrence (usually the one the template
/// author named and references) keeps its name. Duplicate <c>xml:id</c> values are removed from the later
/// occurrences, since they only link RDF metadata, which belongs to the original.
/// </para>
/// </remarks>
internal static class OdtUniqueNames
{
    private static readonly XName _xmlId = XNamespace.Xml + "id";
    private static readonly XName _drawName = OdfNames.Draw + "name";
    private static readonly XName _tableName = OdfNames.Table + "name";
    private static readonly XName _textName = OdfNames.Text + "name";
    private static readonly XName _textId = OdfNames.Text + "id";

    /// <summary>
    /// Renames duplicate frame/shape, table and section names and note ids, and removes duplicate xml:ids.
    /// </summary>
    public static void EnsureUnique(IReadOnlyList<XElement> roots)
    {
        List<XElement> elements = roots.SelectMany(r => r.DescendantsAndSelf()).ToList();

        Rename(elements, e => e.Name.Namespace == OdfNames.Draw ? e.Attribute(_drawName) : null);
        Rename(elements, e => e.Name == OdfNames.TableElement ? e.Attribute(_tableName) : null);
        Rename(elements, e => e.Name == OdfNames.Section ? e.Attribute(_textName) : null);
        Rename(elements, e => e.Name == OdfNames.Note ? e.Attribute(_textId) : null);

        HashSet<string> xmlIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (XElement element in elements)
        {
            XAttribute? id = element.Attribute(_xmlId);
            if (id != null && !xmlIds.Add(id.Value))
            {
                id.Remove();
            }
        }
    }

    private static void Rename(List<XElement> elements, Func<XElement, XAttribute?> select)
    {
        List<XAttribute> attributes = elements.Select(select).OfType<XAttribute>().ToList();
        HashSet<string> used = new HashSet<string>(attributes.Select(a => a.Value), StringComparer.Ordinal);
        HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (XAttribute attribute in attributes)
        {
            if (seen.Add(attribute.Value))
            {
                continue;
            }

            string baseName = attribute.Value;
            int counter = 2;
            string candidate;
            do
            {
                candidate = baseName + "_" + counter.ToString(CultureInfo.InvariantCulture);
                counter++;
            }
            while (used.Contains(candidate));

            used.Add(candidate);
            seen.Add(candidate);
            attribute.Value = candidate;
        }
    }
}
