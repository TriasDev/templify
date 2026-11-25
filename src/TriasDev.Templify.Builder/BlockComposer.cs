// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using TriasDev.Templify.Builder.Model;

namespace TriasDev.Templify.Builder;

/// <summary>
/// Describes block-level content: the document body and, in later versions, cells, boxes, headers and the bodies
/// of conditionals and loops. Valid only inside the lambda it is passed to.
/// </summary>
public sealed class BlockComposer
{
    private readonly BuildContext _context;
    private readonly List<Block> _blocks;
    private bool _isOpen = true;

    internal BlockComposer(BuildContext context, List<Block> blocks)
    {
        _context = context;
        _blocks = blocks;
    }

    /// <summary>Adds a paragraph with literal text.</summary>
    /// <param name="text">The text. Tabs and line breaks become the format's tab and line break.</param>
    public void Paragraph(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        EnsureOpen();
        _blocks.Add(new ParagraphBlock(text.Length == 0 ? Array.Empty<Inline>() : new Inline[] { new TextInline(text) }));
    }

    /// <summary>Adds a paragraph that holds one value from the data: <c>{{path}}</c>.</summary>
    /// <param name="value">The data path of the value.</param>
    public void Paragraph(TemplatePath value)
    {
        EnsureOpen();
        _blocks.Add(new ParagraphBlock(new Inline[] { InlineComposer.CreateValue(_context, value) }));
    }

    /// <summary>Adds a paragraph with mixed content: literal text and values.</summary>
    /// <param name="content">Describes the paragraph's content.</param>
    public void Paragraph(Action<InlineComposer> content)
    {
        ArgumentNullException.ThrowIfNull(content);
        EnsureOpen();
        InlineComposer composer = new InlineComposer(_context);
        content(composer);
        _blocks.Add(new ParagraphBlock(composer.Close()));
    }

    internal void Close() => _isOpen = false;

    private void EnsureOpen()
    {
        if (!_isOpen)
        {
            throw new InvalidOperationException(
                "A block composer was used outside the lambda it was passed to.");
        }
    }
}
