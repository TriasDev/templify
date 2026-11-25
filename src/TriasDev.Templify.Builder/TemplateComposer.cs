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
    private bool _isOpen = true;

    internal TemplateComposer(BuildContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Gets the root of the data the template is filled with. Append segments with the indexer:
    /// <c>template.Data["customer"]</c>.
    /// </summary>
    public TemplatePath Data => default;

    /// <summary>Describes the document body. Several calls append to the body in order.</summary>
    /// <param name="content">Describes the body's blocks.</param>
    public void Content(Action<BlockComposer> content)
    {
        ArgumentNullException.ThrowIfNull(content);
        EnsureOpen();
        BlockComposer composer = new BlockComposer(_context, _body);
        content(composer);
        composer.Close();
    }

    /// <summary>Ends composing and returns the body.</summary>
    internal IReadOnlyList<Block> Close()
    {
        _isOpen = false;
        return _body;
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
