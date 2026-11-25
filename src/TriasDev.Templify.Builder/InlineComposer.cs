// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Text;
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

    /// <summary>Adds literal text in the paragraph's formatting.</summary>
    /// <param name="text">The text. Tabs and line breaks become the format's tab and line break.</param>
    public void Text(string text) => AddText(text, null);

    /// <summary>Adds literal text in a character style.</summary>
    /// <param name="text">The text. Tabs and line breaks become the format's tab and line break.</param>
    /// <param name="style">The character style; the style sheet must define it.</param>
    public void Text(string text, CharacterStyleKey style)
    {
        ArgumentNullException.ThrowIfNull(style);
        AddText(text, style);
    }

    /// <summary>Adds a value from the data: <c>{{path}}</c>.</summary>
    /// <param name="path">The data path of the value.</param>
    public void Value(TemplatePath path) => AddValue(path, null, null);

    /// <summary>Adds a formatted value from the data, for example <c>{{order.date:date:yyyy-MM-dd}}</c>.</summary>
    /// <param name="path">The data path of the value.</param>
    /// <param name="format">How Templify formats the value.</param>
    public void Value(TemplatePath path, ValueFormat format)
    {
        ArgumentNullException.ThrowIfNull(format);
        AddValue(path, format, null);
    }

    /// <summary>Adds a value from the data in a character style.</summary>
    /// <param name="path">The data path of the value.</param>
    /// <param name="style">The character style; the style sheet must define it.</param>
    public void Value(TemplatePath path, CharacterStyleKey style)
    {
        ArgumentNullException.ThrowIfNull(style);
        AddValue(path, null, style);
    }

    /// <summary>Adds a formatted value from the data in a character style.</summary>
    /// <param name="path">The data path of the value.</param>
    /// <param name="format">How Templify formats the value.</param>
    /// <param name="style">The character style; the style sheet must define it.</param>
    public void Value(TemplatePath path, ValueFormat format, CharacterStyleKey style)
    {
        ArgumentNullException.ThrowIfNull(format);
        ArgumentNullException.ThrowIfNull(style);
        AddValue(path, format, style);
    }

    /// <summary>Adds a tab.</summary>
    public void Tab() => AddText("\t", null);

    /// <summary>Adds a line break inside the paragraph.</summary>
    public void LineBreak() => AddText("\n", null);

    internal static ValueInline CreateValue(BuildContext context, TemplatePath path, ValueFormat? format = null, CharacterStyleKey? style = null)
    {
        if (path.IsEmpty)
        {
            context.Error(
                DiagnosticCodes.EmptyValuePath,
                "A value placeholder was given the root of the data (template.Data or default(TemplatePath)); append a segment, for example template.Data[\"customer\"][\"name\"].");
        }

        return new ValueInline(path, format, style);
    }

    /// <summary>
    /// Reports literal text that contains <c>{{</c>, also across adjacent pieces of text: Templify would read it as the
    /// start of a placeholder, and Templify has no escape for it.
    /// </summary>
    internal static void CheckLiteralText(BuildContext context, IReadOnlyList<Inline> inlines)
    {
        StringBuilder literal = new StringBuilder();
        foreach (Inline inline in inlines)
        {
            if (inline is TextInline text)
            {
                literal.Append(text.Text);
                continue;
            }

            Check(literal);
            literal.Clear();
        }

        Check(literal);

        void Check(StringBuilder text)
        {
            string value = text.ToString();
            if (value.Contains("{{", StringComparison.Ordinal))
            {
                context.Error(
                    DiagnosticCodes.LiteralOpenBraces,
                    $"Literal text contains '{{{{', which Templify reads as the start of a placeholder: \"{value}\". Write values with Value(path), not as text.");
            }
        }
    }

    internal IReadOnlyList<Inline> Close()
    {
        _isOpen = false;
        CheckLiteralText(_context, _inlines);
        return _inlines;
    }

    private void AddText(string text, CharacterStyleKey? style)
    {
        ArgumentNullException.ThrowIfNull(text);
        EnsureOpen();
        if (text.Length > 0)
        {
            _inlines.Add(new TextInline(text, style));
        }
    }

    private void AddValue(TemplatePath path, ValueFormat? format, CharacterStyleKey? style)
    {
        EnsureOpen();
        _inlines.Add(CreateValue(_context, path, format, style));
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
