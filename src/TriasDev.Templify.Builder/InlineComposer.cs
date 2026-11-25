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
    private readonly LoopScope? _scope;
    private readonly List<Inline> _inlines = new List<Inline>();
    private bool _isOpen = true;

    internal InlineComposer(BuildContext context, LoopScope? scope)
    {
        _context = context;
        _scope = scope;
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

    /// <summary>
    /// Adds a tab to a right-aligned tab stop at the end of the text frame, for example to push an amount or a page
    /// number to the right margin.
    /// </summary>
    public void TabRight()
    {
        EnsureOpen();
        _inlines.Add(new RightTabInline());
    }

    /// <summary>Adds an image at its natural size, inline with the text.</summary>
    /// <param name="image">The image.</param>
    /// <param name="altText">The text that describes the image (accessibility); required.</param>
    public void Image(ImageSource image, string altText) => Image(image, altText, ImageFit.Natural);

    /// <summary>Adds an image inline with the text.</summary>
    /// <param name="image">The image.</param>
    /// <param name="altText">The text that describes the image (accessibility); required.</param>
    /// <param name="fit">How the image is sized; the aspect ratio is kept.</param>
    public void Image(ImageSource image, string altText, ImageFit fit)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(fit);
        EnsureOpen();
        _inlines.Add(CreateImage(image, altText, fit));
    }

    internal static ImageInline CreateImage(ImageSource image, string altText, ImageFit fit)
    {
        ArgumentNullException.ThrowIfNull(altText);
        if (string.IsNullOrWhiteSpace(altText) || altText.Any(c => char.IsControl(c) && c is not ('\t' or '\n' or '\r')))
        {
            throw new ArgumentException("An image needs an alternative text without control characters.", nameof(altText));
        }

        (Length width, Length height) = fit.Resolve(image);
        return new ImageInline(image, altText, width, height);
    }

    /// <summary>Adds the current page number (a field Word and LibreOffice update).</summary>
    public void PageNumber() => AddField(PageField.Number, null);

    /// <summary>Adds the current page number in a character style.</summary>
    /// <param name="style">The character style; the style sheet must define it.</param>
    public void PageNumber(CharacterStyleKey style)
    {
        ArgumentNullException.ThrowIfNull(style);
        AddField(PageField.Number, style);
    }

    /// <summary>Adds the number of pages (a field Word and LibreOffice update).</summary>
    public void PageCount() => AddField(PageField.Count, null);

    /// <summary>Adds the number of pages in a character style.</summary>
    /// <param name="style">The character style; the style sheet must define it.</param>
    public void PageCount(CharacterStyleKey style)
    {
        ArgumentNullException.ThrowIfNull(style);
        AddField(PageField.Count, style);
    }

    internal static ValueInline CreateValue(BuildContext context, LoopScope? scope, TemplatePath path, ValueFormat? format = null, CharacterStyleKey? style = null)
    {
        LoopScope.CheckUse(context, scope, path);
        if (path.IsRoot)
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

    private void AddField(PageField field, CharacterStyleKey? style)
    {
        EnsureOpen();
        _inlines.Add(new PageFieldInline(field, style));
    }

    private void AddValue(TemplatePath path, ValueFormat? format, CharacterStyleKey? style)
    {
        EnsureOpen();
        _inlines.Add(CreateValue(_context, _scope, path, format, style));
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
