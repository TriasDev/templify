// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Text;
using System.Xml;
using System.Xml.Linq;
using TriasDev.Templify.Builder.Model;

namespace TriasDev.Templify.Builder.Writers;

/// <summary>
/// Writes a <see cref="DocumentModel"/> as an OpenDocument Text package (<c>.odt</c>, ODF 1.3).
/// </summary>
internal static class OdtWriter
{
    private const string MediaType = "application/vnd.oasis.opendocument.text";
    private const string OdfVersion = "1.3";

    private static readonly XNamespace _office = "urn:oasis:names:tc:opendocument:xmlns:office:1.0";
    private static readonly XNamespace _text = "urn:oasis:names:tc:opendocument:xmlns:text:1.0";
    private static readonly XNamespace _style = "urn:oasis:names:tc:opendocument:xmlns:style:1.0";
    private static readonly XNamespace _fo = "urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0";
    private static readonly XNamespace _meta = "urn:oasis:names:tc:opendocument:xmlns:meta:1.0";
    private static readonly XNamespace _dc = "http://purl.org/dc/elements/1.1/";
    private static readonly XNamespace _manifest = "urn:oasis:names:tc:opendocument:xmlns:manifest:1.0";

    public static byte[] Write(DocumentModel model)
    {
        return DeterministicZip.Create(new[]
        {
            // ODF 1.3 part 2, 3.3: the first entry, stored, without an extra field.
            new PackagePart("mimetype", Encoding.ASCII.GetBytes(MediaType), Stored: true),
            new PackagePart("content.xml", Serialize(Content(model))),
            new PackagePart("styles.xml", Serialize(Styles())),
            new PackagePart("meta.xml", Serialize(Meta(model))),
            new PackagePart("META-INF/manifest.xml", Serialize(Manifest())),
        });
    }

    private static XElement Content(DocumentModel model)
    {
        XElement text = new XElement(_office + "text");
        foreach (Block block in model.Body)
        {
            text.Add(WriteBlock(block));
        }

        return new XElement(
            _office + "document-content",
            Namespaces(_office, _text, _style, _fo),
            new XAttribute(_office + "version", OdfVersion),
            new XElement(_office + "automatic-styles"),
            new XElement(_office + "body", text));
    }

    private static XElement Styles() =>
        new XElement(
            _office + "document-styles",
            Namespaces(_office, _text, _style, _fo),
            new XAttribute(_office + "version", OdfVersion),
            new XElement(_office + "styles"),
            new XElement(_office + "automatic-styles"),
            new XElement(_office + "master-styles"));

    private static XElement Meta(DocumentModel model) =>
        new XElement(
            _office + "document-meta",
            Namespaces(_office, _meta, _dc),
            new XAttribute(_office + "version", OdfVersion),
            new XElement(_office + "meta", new XElement(_dc + "language", model.Language.Name)));

    private static XElement Manifest() =>
        new XElement(
            _manifest + "manifest",
            new XAttribute(XNamespace.Xmlns + "manifest", _manifest.NamespaceName),
            new XAttribute(_manifest + "version", OdfVersion),
            FileEntry("/", MediaType, version: true),
            FileEntry("content.xml", "text/xml"),
            FileEntry("styles.xml", "text/xml"),
            FileEntry("meta.xml", "text/xml"));

    private static XElement FileEntry(string path, string mediaType, bool version = false) =>
        new XElement(
            _manifest + "file-entry",
            new XAttribute(_manifest + "full-path", path),
            version ? new XAttribute(_manifest + "version", OdfVersion) : null,
            new XAttribute(_manifest + "media-type", mediaType));

    private static XElement WriteBlock(Block block) => block switch
    {
        ParagraphBlock paragraph => WriteParagraph(paragraph),
        _ => throw new NotSupportedException($"Block {block.GetType().Name} is not supported by the ODT writer."),
    };

    private static XElement WriteParagraph(ParagraphBlock block)
    {
        XElement paragraph = new XElement(_text + "p");
        WhitespaceEncoder encoder = new WhitespaceEncoder(paragraph);
        foreach (Inline inline in block.Inlines)
        {
            switch (inline)
            {
                case TextInline text:
                    foreach ((TextPieceKind kind, string piece) in TextPieces.Split(text.Text))
                    {
                        switch (kind)
                        {
                            case TextPieceKind.Tab:
                                encoder.Element(new XElement(_text + "tab"));
                                break;
                            case TextPieceKind.LineBreak:
                                encoder.Element(new XElement(_text + "line-break"));
                                break;
                            default:
                                encoder.Text(piece);
                                break;
                        }
                    }

                    break;

                case ValueInline value:
                    encoder.Text(TemplateSyntax.Placeholder(value.Path));
                    break;

                default:
                    throw new NotSupportedException($"Inline {inline.GetType().Name} is not supported by the ODT writer.");
            }
        }

        encoder.Finish();
        return paragraph;
    }

    private static IEnumerable<XAttribute> Namespaces(params XNamespace[] namespaces)
    {
        foreach (XNamespace ns in namespaces)
        {
            string prefix = ns == _office ? "office"
                : ns == _text ? "text"
                : ns == _style ? "style"
                : ns == _fo ? "fo"
                : ns == _meta ? "meta"
                : ns == _dc ? "dc"
                : throw new ArgumentOutOfRangeException(nameof(namespaces), ns.NamespaceName, "Unknown namespace.");
            yield return new XAttribute(XNamespace.Xmlns + prefix, ns.NamespaceName);
        }
    }

    private static byte[] Serialize(XElement root)
    {
        XmlWriterSettings settings = new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            Indent = false,
            NewLineHandling = NewLineHandling.Entitize,
        };

        using MemoryStream buffer = new MemoryStream();
        using (XmlWriter writer = XmlWriter.Create(buffer, settings))
        {
            new XDocument(new XDeclaration("1.0", "UTF-8", null), root).Save(writer);
        }

        return buffer.ToArray();
    }

    /// <summary>
    /// Writes text so that it renders exactly under ODF white-space rules: literal white space collapses and is
    /// dropped at the start of a paragraph, so every space that would be lost becomes <c>text:s</c>.
    /// </summary>
    /// <remarks>
    /// The rule is conservative: after the paragraph start, a tab or a line break, and after a literal space, every
    /// space is written as <c>text:s</c>, which always renders. A trailing literal space becomes <c>text:s</c> too.
    /// </remarks>
    private sealed class WhitespaceEncoder(XElement paragraph)
    {
        private readonly StringBuilder _pending = new StringBuilder();
        private bool _collapsible = true;

        public void Text(string text)
        {
            int i = 0;
            while (i < text.Length)
            {
                if (text[i] != ' ')
                {
                    _pending.Append(text[i]);
                    _collapsible = false;
                    i++;
                    continue;
                }

                int count = 0;
                while (i < text.Length && text[i] == ' ')
                {
                    count++;
                    i++;
                }

                if (!_collapsible)
                {
                    _pending.Append(' ');
                    count--;
                }

                if (count > 0)
                {
                    FlushText();
                    paragraph.Add(Spaces(count));
                }

                _collapsible = true;
            }
        }

        public void Element(XElement element)
        {
            FlushText();
            paragraph.Add(element);
            _collapsible = true;
        }

        public void Finish()
        {
            if (_pending.Length > 0 && _pending[^1] == ' ')
            {
                _pending.Length--;
                FlushText();
                paragraph.Add(Spaces(1));
                return;
            }

            FlushText();
        }

        private void FlushText()
        {
            if (_pending.Length > 0)
            {
                paragraph.Add(new XText(_pending.ToString()));
                _pending.Clear();
            }
        }

        private static XElement Spaces(int count) =>
            count == 1
                ? new XElement(_text + "s")
                : new XElement(_text + "s", new XAttribute(_text + "c", count));
    }
}
