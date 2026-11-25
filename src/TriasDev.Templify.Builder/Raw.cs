// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Xml.Linq;

namespace TriasDev.Templify.Builder;

/// <summary>
/// A block written as format XML, for a construct the builder's vocabulary does not have (a field, vertical text, a
/// special table). Both formats are required, so a template never silently lacks content in one of them.
/// </summary>
/// <remarks>
/// <para>
/// DOCX: a <c>w:p</c> or <c>w:tbl</c> element (WordprocessingML namespace). ODT: a <c>text:p</c>, <c>text:h</c>,
/// <c>table:table</c> or <c>text:list</c> element. The XML is written as given; the built template is still
/// validated by the builder's tests and by <c>TemplateProcessor.ValidateTemplate</c>.
/// </para>
/// <para>
/// Paths the XML reads through placeholders are declared in <see cref="Uses"/> so the data contract stays complete.
/// Raw blocks are not available to declarative definitions.
/// </para>
/// </remarks>
public sealed record RawBlock
{
    /// <summary>Gets the DOCX element.</summary>
    public required Func<RawContext, XElement> Docx { get; init; }

    /// <summary>Gets the ODT element.</summary>
    public required Func<RawContext, XElement> Odt { get; init; }

    /// <summary>Gets the data paths the XML's placeholders read (recorded as values in the contract).</summary>
    public IReadOnlyList<TemplatePath> Uses { get; init; } = [];
}

/// <summary>What a raw block's XML may depend on.</summary>
public sealed class RawContext
{
    private readonly IReadOnlyList<TemplatePath> _uses;

    internal RawContext(RawBlock raw, Length frame)
    {
        _uses = raw.Uses;
        Frame = frame;
    }

    /// <summary>Gets the width of the container the block sits in (page frame, cell or box content).</summary>
    public Length Frame { get; }

    /// <summary>
    /// Returns the placeholder text for a path declared in <see cref="RawBlock.Uses"/>, for example
    /// <c>{{customer.name}}</c>; put it into one text run (<c>w:t</c>) or one text node so the engine finds it whole.
    /// </summary>
    /// <param name="path">A path from <see cref="RawBlock.Uses"/>.</param>
    /// <param name="format">The format, or null.</param>
    /// <exception cref="TemplateBuildException">The path is not declared in <see cref="RawBlock.Uses"/>.</exception>
    public string Placeholder(TemplatePath path, ValueFormat? format = null)
    {
        if (path.IsRoot || !_uses.Contains(path))
        {
            throw new TemplateBuildException([new BuildDiagnostic(
                DiagnosticCodes.UndeclaredRawPath,
                $"The raw block reads '{path}', which is not declared in its Uses; the data contract would miss it.")]);
        }

        return TemplateSyntax.Placeholder(path, format);
    }

    /// <summary>The root elements a DOCX raw block may have.</summary>
    internal static readonly XName[] DocxRoots =
    {
        XName.Get("p", "http://schemas.openxmlformats.org/wordprocessingml/2006/main"),
        XName.Get("tbl", "http://schemas.openxmlformats.org/wordprocessingml/2006/main"),
    };

    /// <summary>The root elements an ODT raw block may have.</summary>
    internal static readonly XName[] OdtRoots =
    {
        XName.Get("p", "urn:oasis:names:tc:opendocument:xmlns:text:1.0"),
        XName.Get("h", "urn:oasis:names:tc:opendocument:xmlns:text:1.0"),
        XName.Get("list", "urn:oasis:names:tc:opendocument:xmlns:text:1.0"),
        XName.Get("table", "urn:oasis:names:tc:opendocument:xmlns:table:1.0"),
    };

    /// <summary>Checks a raw element's root and returns it; a wrong root is a definition error.</summary>
    internal static XElement Check(XElement? element, XName[] allowed, string format)
    {
        if (element is null || !allowed.Contains(element.Name))
        {
            throw new TemplateBuildException([new BuildDiagnostic(
                DiagnosticCodes.InvalidRawBlock,
                $"A raw {format} block must be one of {string.Join(", ", allowed.Select(n => n.LocalName))} in its namespace, not '{element?.Name}'.")]);
        }

        return element;
    }
}
