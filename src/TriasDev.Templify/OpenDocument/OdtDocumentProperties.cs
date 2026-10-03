// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Xml.Linq;
using TriasDev.Templify.Core;

namespace TriasDev.Templify.OpenDocument;

/// <summary>
/// Applies <see cref="DocumentProperties"/> to the OpenDocument metadata (<c>meta.xml</c>).
/// </summary>
/// <remarks>
/// Mapping (only non-null properties are applied; the others keep the template's values):
/// <list type="bullet">
/// <item><see cref="DocumentProperties.Author"/> → <c>meta:initial-creator</c> (the document's author)</item>
/// <item><see cref="DocumentProperties.LastModifiedBy"/> → <c>dc:creator</c> (in ODF the last person who modified it)</item>
/// <item><see cref="DocumentProperties.Title"/> → <c>dc:title</c>, <see cref="DocumentProperties.Subject"/> → <c>dc:subject</c>,
/// <see cref="DocumentProperties.Description"/> → <c>dc:description</c></item>
/// <item><see cref="DocumentProperties.Keywords"/> → a single <c>meta:keyword</c> (replacing existing ones)</item>
/// <item><see cref="DocumentProperties.Category"/> → the user-defined property <c>Category</c> (ODF has no category field;
/// LibreOffice shows it under custom properties)</item>
/// </list>
/// </remarks>
internal static class OdtDocumentProperties
{
    private static readonly XName _documentMeta = OdfNames.Office + "document-meta";
    private static readonly XName _meta = OdfNames.Office + "meta";
    private static readonly XName _version = OdfNames.Office + "version";
    private static readonly XName _keyword = OdfNames.Meta + "keyword";
    private static readonly XName _userDefined = OdfNames.Meta + "user-defined";
    private static readonly XName _name = OdfNames.Meta + "name";
    private static readonly XName _valueType = OdfNames.Meta + "value-type";

    public static void Apply(OdtPackage package, DocumentProperties properties)
    {
        XDocument document = package.GetXml(OdtPackage.MetaEntry) ?? package.AddXml(OdtPackage.MetaEntry, CreateMeta());
        XElement root = document.Root ?? throw new InvalidOdtPackageException("Invalid document: meta.xml has no root element.");
        XElement meta = root.Element(_meta) ?? AddElement(root, _meta);

        SetSingle(meta, OdfNames.Meta + "initial-creator", properties.Author);
        SetSingle(meta, OdfNames.Dc + "creator", properties.LastModifiedBy);
        SetSingle(meta, OdfNames.Dc + "title", properties.Title);
        SetSingle(meta, OdfNames.Dc + "subject", properties.Subject);
        SetSingle(meta, OdfNames.Dc + "description", properties.Description);
        SetSingle(meta, _keyword, properties.Keywords);

        if (properties.Category != null)
        {
            meta.Elements(_userDefined).Where(e => (string?)e.Attribute(_name) == "Category").Remove();
            meta.Add(new XElement(
                _userDefined,
                new XAttribute(_name, "Category"),
                new XAttribute(_valueType, "string"),
                properties.Category));
        }
    }

    private static void SetSingle(XElement meta, XName name, string? value)
    {
        if (value == null)
        {
            return;
        }

        List<XElement> existing = meta.Elements(name).ToList();
        if (existing.Count > 0)
        {
            existing[0].Value = value;
            existing.Skip(1).Remove();
        }
        else
        {
            meta.Add(new XElement(name, value));
        }
    }

    private static XElement AddElement(XElement parent, XName name)
    {
        XElement element = new XElement(name);
        parent.Add(element);
        return element;
    }

    private static XDocument CreateMeta() =>
        new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement(
                _documentMeta,
                new XAttribute(XNamespace.Xmlns + "office", OdfNames.Office.NamespaceName),
                new XAttribute(XNamespace.Xmlns + "meta", OdfNames.Meta.NamespaceName),
                new XAttribute(XNamespace.Xmlns + "dc", OdfNames.Dc.NamespaceName),
                new XAttribute(_version, "1.3"),
                new XElement(_meta)));
}
