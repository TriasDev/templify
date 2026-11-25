// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using TriasDev.Templify.Builder.Model;

namespace TriasDev.Templify.Builder;

/// <summary>
/// Describes a whole template. Passed to <see cref="ITemplateDefinition.Compose"/>; valid only during that call.
/// </summary>
public sealed class TemplateComposer
{
    private readonly BuildContext _context;
    private readonly List<Block> _body = new List<Block>();
    private readonly DocumentPropertiesComposer _properties = new DocumentPropertiesComposer();
    private StyleSheet? _styles;
    private bool _isOpen = true;

    internal TemplateComposer(BuildContext context, TemplateTexts texts)
    {
        _context = context;
        Texts = texts;
    }

    /// <summary>Gets the texts of the build's language: <c>template.Texts["invoiceTitle"]</c>.</summary>
    public TemplateTexts Texts { get; }

    /// <summary>
    /// Gets the root of the data the template is filled with. Append segments with the indexer:
    /// <c>template.Data["customer"]</c>.
    /// </summary>
    public TemplatePath Data => default;

    /// <summary>Sets the template's named styles. Call it once.</summary>
    /// <param name="styles">The style sheet, typically built from the product's theme.</param>
    public void Styles(StyleSheet styles)
    {
        ArgumentNullException.ThrowIfNull(styles);
        EnsureOpen();
        if (_styles != null)
        {
            _context.Error(DiagnosticCodes.StylesAlreadySet, "TemplateComposer.Styles was called more than once; a template has one style sheet.");
            return;
        }

        _styles = styles;
    }

    /// <summary>Sets the standard document properties: title, author, subject.</summary>
    /// <param name="properties">Sets the properties. Several calls combine; a later value replaces an earlier one.</param>
    public void Properties(Action<DocumentPropertiesComposer> properties)
    {
        ArgumentNullException.ThrowIfNull(properties);
        EnsureOpen();
        properties(_properties);
    }

    /// <summary>Describes the document body. Several calls append to the body in order.</summary>
    /// <param name="content">Describes the body's blocks.</param>
    public void Content(Action<BlockComposer> content)
    {
        ArgumentNullException.ThrowIfNull(content);
        EnsureOpen();
        BlockComposer composer = new BlockComposer(_context, _body, null);
        content(composer);
        composer.Close();
    }

    /// <summary>Ends composing and returns what was composed.</summary>
    internal (IReadOnlyList<Block> Body, StyleSheet Styles, DocumentInfo Info) Close()
    {
        _isOpen = false;
        _properties.Close();
        return (_body, _styles ?? StyleSheet.Empty, _properties.ToInfo());
    }

    private void EnsureOpen()
    {
        if (!_isOpen)
        {
            throw new InvalidOperationException(
                "The template composer was used after ITemplateDefinition.Compose returned. It is valid only during that call.");
        }
    }
}

/// <summary>Sets the standard document properties. Valid only during <see cref="ITemplateDefinition.Compose"/>.</summary>
public sealed class DocumentPropertiesComposer
{
    private string? _title;
    private string? _author;
    private string? _subject;
    private bool _isOpen = true;

    internal DocumentPropertiesComposer()
    {
    }

    /// <summary>Sets the document title.</summary>
    /// <param name="title">The title.</param>
    /// <returns>This composer.</returns>
    public DocumentPropertiesComposer Title(string title)
    {
        _title = Validate(title, nameof(title));
        return this;
    }

    /// <summary>Sets the author (creator).</summary>
    /// <param name="author">The author, for example the product or the company.</param>
    /// <returns>This composer.</returns>
    public DocumentPropertiesComposer Author(string author)
    {
        _author = Validate(author, nameof(author));
        return this;
    }

    /// <summary>Sets the subject.</summary>
    /// <param name="subject">The subject.</param>
    /// <returns>This composer.</returns>
    public DocumentPropertiesComposer Subject(string subject)
    {
        _subject = Validate(subject, nameof(subject));
        return this;
    }

    internal void Close() => _isOpen = false;

    internal DocumentInfo ToInfo() => new DocumentInfo(_title, _author, _subject);

    private string Validate(string value, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(value, parameterName);
        if (!_isOpen)
        {
            throw new InvalidOperationException("The document properties composer was used after ITemplateDefinition.Compose returned.");
        }

        if (value.Any(c => char.IsControl(c) && c is not ('\t' or '\n' or '\r')))
        {
            throw new ArgumentException("A document property must not contain control characters.", parameterName);
        }

        return value;
    }
}
