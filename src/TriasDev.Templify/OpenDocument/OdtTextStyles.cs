// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.Xml.Linq;
using TriasDev.Templify.Utilities;

namespace TriasDev.Templify.OpenDocument;

/// <summary>
/// Creates or reuses the automatic text styles that render markdown formatting (bold, italic,
/// strikethrough and their combinations) in OpenDocument parts.
/// </summary>
/// <remarks>
/// <para>
/// Each part (<c>content.xml</c>, <c>styles.xml</c>) has its own <c>office:automatic-styles</c>; a span
/// can only use an automatic style of its own part, so styles are looked up and created per part.
/// </para>
/// <para>
/// A style is reused when the part already has an automatic text style with exactly the markdown
/// properties (e.g. one created by an earlier replacement, or an identical one written by LibreOffice).
/// New styles are named <c>T{n}</c> with the lowest number not used by any style of the part, nor by a
/// reserved name: the common styles of <c>styles.xml</c> are visible from <c>content.xml</c>, and a span
/// referencing a common style <c>T3</c> would take the formatting of an automatic style of the same name.
/// </para>
/// <para>
/// The formatted span is nested inside the span holding the placeholder; ODF combines the properties
/// of nested spans, so template formatting (font, color) is kept and the markdown formatting is added,
/// as for Word documents.
/// </para>
/// </remarks>
internal sealed class OdtTextStyles
{
    private static readonly XName _styleName = OdfNames.Style + "name";
    private static readonly XName _styleFamily = OdfNames.Style + "family";
    private static readonly XName _styleElement = OdfNames.Style + "style";
    private static readonly XName _textProperties = OdfNames.Style + "text-properties";

    private readonly Dictionary<(XDocument Part, int Key), string> _cache = new Dictionary<(XDocument, int), string>();
    private readonly HashSet<string> _reservedNames = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>
    /// Reserves style names that new styles must not take (the names of the common styles of <c>styles.xml</c>).
    /// </summary>
    public void ReserveNames(IEnumerable<string> names) => _reservedNames.UnionWith(names);

    /// <summary>
    /// Gets the name of an automatic text style of <paramref name="part"/> that renders the formatting of
    /// <paramref name="piece"/>, creating it if needed; null for a piece without formatting.
    /// </summary>
    public string? GetStyleName(XDocument part, ReplacementPiece piece)
    {
        if (!piece.HasFormatting || part.Root == null)
        {
            return null;
        }

        int key = (piece.IsBold ? 1 : 0) | (piece.IsItalic ? 2 : 0) | (piece.IsStrikethrough ? 4 : 0);
        if (_cache.TryGetValue((part, key), out string? cached))
        {
            return cached;
        }

        Dictionary<XName, string> properties = GetProperties(piece);
        XElement automaticStyles = GetOrCreateAutomaticStyles(part.Root);

        string? name = automaticStyles.Elements(_styleElement)
            .Where(s => (string?)s.Attribute(_styleFamily) == "text" && HasExactly(s, properties))
            .Select(s => (string?)s.Attribute(_styleName))
            .FirstOrDefault(n => !string.IsNullOrEmpty(n));

        if (name == null)
        {
            name = CreateUniqueName(part.Root, _reservedNames);
            automaticStyles.Add(new XElement(
                _styleElement,
                new XAttribute(_styleName, name),
                new XAttribute(_styleFamily, "text"),
                new XElement(_textProperties, properties.Select(p => new XAttribute(p.Key, p.Value)))));
        }

        _cache[(part, key)] = name;
        return name;
    }

    private static Dictionary<XName, string> GetProperties(ReplacementPiece piece)
    {
        Dictionary<XName, string> properties = new Dictionary<XName, string>();
        if (piece.IsBold)
        {
            properties[OdfNames.Fo + "font-weight"] = "bold";
            properties[OdfNames.Style + "font-weight-asian"] = "bold";
            properties[OdfNames.Style + "font-weight-complex"] = "bold";
        }

        if (piece.IsItalic)
        {
            properties[OdfNames.Fo + "font-style"] = "italic";
            properties[OdfNames.Style + "font-style-asian"] = "italic";
            properties[OdfNames.Style + "font-style-complex"] = "italic";
        }

        if (piece.IsStrikethrough)
        {
            properties[OdfNames.Style + "text-line-through-style"] = "solid";
            properties[OdfNames.Style + "text-line-through-type"] = "single";
        }

        return properties;
    }

    /// <summary>
    /// Checks whether a style has no parent, no other attributes than name and family, and exactly the given
    /// text properties.
    /// </summary>
    private static bool HasExactly(XElement style, Dictionary<XName, string> properties)
    {
        if (style.Attributes().Any(a => !a.IsNamespaceDeclaration && a.Name != _styleName && a.Name != _styleFamily))
        {
            return false;
        }

        List<XElement> children = style.Elements().ToList();
        if (children.Count != 1 || children[0].Name != _textProperties || children[0].HasElements)
        {
            return false;
        }

        List<XAttribute> attributes = children[0].Attributes().Where(a => !a.IsNamespaceDeclaration).ToList();
        return attributes.Count == properties.Count
            && attributes.All(a => properties.TryGetValue(a.Name, out string? value) && value == a.Value);
    }

    private static XElement GetOrCreateAutomaticStyles(XElement root)
    {
        XElement? automaticStyles = root.Element(OdfNames.OfficeAutomaticStyles);
        if (automaticStyles != null)
        {
            return automaticStyles;
        }

        // Schema order: … font-face-decls, (styles,) automatic-styles, master-styles / body.
        automaticStyles = new XElement(OdfNames.OfficeAutomaticStyles);
        XElement? next = root.Element(OdfNames.OfficeMasterStyles) ?? root.Element(OdfNames.OfficeBody);
        if (next != null)
        {
            next.AddBeforeSelf(automaticStyles);
        }
        else
        {
            root.Add(automaticStyles);
        }

        return automaticStyles;
    }

    private static string CreateUniqueName(XElement root, HashSet<string> reservedNames)
    {
        HashSet<string> used = new HashSet<string>(
            root.Descendants().Select(e => (string?)e.Attribute(_styleName)).OfType<string>(),
            StringComparer.Ordinal);
        used.UnionWith(reservedNames);

        for (int n = 1; ; n++)
        {
            string candidate = "T" + n.ToString(CultureInfo.InvariantCulture);
            if (!used.Contains(candidate))
            {
                return candidate;
            }
        }
    }
}
