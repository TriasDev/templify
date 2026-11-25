// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using TriasDev.Templify.Builder.Model;

namespace TriasDev.Templify.Builder;

/// <summary>
/// Describes the content of one paragraph. Valid only inside the lambda it is passed to.
/// </summary>
public sealed class InlineComposer
{
    private readonly BuildContext _context;
    private readonly List<Inline> _inlines = new List<Inline>();
    private bool _isOpen = true;

    internal InlineComposer(BuildContext context)
    {
        _context = context;
    }

    /// <summary>Adds literal text.</summary>
    /// <param name="text">The text. Tabs and line breaks become the format's tab and line break.</param>
    public void Text(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        EnsureOpen();
        if (text.Length > 0)
        {
            _inlines.Add(new TextInline(text));
        }
    }

    /// <summary>Adds a value from the data: <c>{{path}}</c>.</summary>
    /// <param name="path">The data path of the value.</param>
    public void Value(TemplatePath path)
    {
        EnsureOpen();
        _inlines.Add(CreateValue(_context, path));
    }

    internal static ValueInline CreateValue(BuildContext context, TemplatePath path)
    {
        if (path.IsEmpty)
        {
            context.Error(
                DiagnosticCodes.EmptyValuePath,
                "A value placeholder was given the root of the data (template.Data or default(TemplatePath)); append a segment, for example template.Data[\"customer\"][\"name\"].");
        }

        return new ValueInline(path);
    }

    internal IReadOnlyList<Inline> Close()
    {
        _isOpen = false;
        return _inlines;
    }

    private void EnsureOpen()
    {
        if (!_isOpen)
        {
            throw new InvalidOperationException(
                "An inline composer was used outside the lambda it was passed to.");
        }
    }
}
