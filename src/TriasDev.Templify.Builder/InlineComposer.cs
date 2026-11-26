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
    /// <returns>This composer.</returns>
    public InlineComposer Text(string text) => AddText(text, null);

    /// <summary>Adds literal text in a character style.</summary>
    /// <param name="style">The character style; the style sheet must define it.</param>
    /// <param name="text">The text. Tabs and line breaks become the format's tab and line break.</param>
    /// <returns>This composer.</returns>
    public InlineComposer Text(CharacterStyleKey style, string text)
    {
        ArgumentNullException.ThrowIfNull(style);
        AddText(text, style);
        return this;
    }

    /// <summary>Adds a value from the data: <c>{{path}}</c>.</summary>
    /// <param name="path">The data path of the value.</param>
    /// <returns>This composer.</returns>
    public InlineComposer Value(TemplatePath path) => AddValue(path, null, null);

    /// <summary>Adds a formatted value from the data, for example <c>{{order.date:date:yyyy-MM-dd}}</c>.</summary>
    /// <param name="path">The data path of the value.</param>
    /// <param name="format">How Templify formats the value.</param>
    /// <returns>This composer.</returns>
    public InlineComposer Value(TemplatePath path, ValueFormat format)
    {
        ArgumentNullException.ThrowIfNull(format);
        AddValue(path, format, null);
        return this;
    }

    /// <summary>Adds a value from the data in a character style.</summary>
    /// <param name="style">The character style; the style sheet must define it.</param>
    /// <param name="path">The data path of the value.</param>
    /// <returns>This composer.</returns>
    public InlineComposer Value(CharacterStyleKey style, TemplatePath path)
    {
        ArgumentNullException.ThrowIfNull(style);
        AddValue(path, null, style);
        return this;
    }

    /// <summary>Adds a formatted value from the data in a character style.</summary>
    /// <param name="style">The character style; the style sheet must define it.</param>
    /// <param name="path">The data path of the value.</param>
    /// <param name="format">How Templify formats the value.</param>
    /// <returns>This composer.</returns>
    public InlineComposer Value(CharacterStyleKey style, TemplatePath path, ValueFormat format)
    {
        ArgumentNullException.ThrowIfNull(format);
        ArgumentNullException.ThrowIfNull(style);
        AddValue(path, format, style);
        return this;
    }

    /// <summary>Adds a tab.</summary>
    /// <returns>This composer.</returns>
    public InlineComposer Tab() => AddText("\t", null);

    /// <summary>Adds a line break inside the paragraph.</summary>
    /// <returns>This composer.</returns>
    public InlineComposer LineBreak() => AddText("\n", null);

    /// <summary>
    /// Adds a tab to a right-aligned tab stop at the end of the text frame, for example to push an amount or a page
    /// number to the right margin.
    /// </summary>
    /// <returns>This composer.</returns>
    public InlineComposer TabRight()
    {
        EnsureOpen();
        _inlines.Add(new RightTabInline());
        return this;
    }

    /// <summary>
    /// Adds text shown only when a condition holds, inside this paragraph: <c>{{#if …}}…{{/if}}</c>. Continue with
    /// <see cref="InlineIfChain.ElseIf"/> and <see cref="InlineIfChain.Else"/>.
    /// </summary>
    /// <param name="condition">The condition.</param>
    /// <param name="then">Describes the text shown when it holds.</param>
    /// <returns>The chain, to add elseif and else parts.</returns>
    public InlineIfChain If(Condition condition, Action<TextComposer> then)
    {
        EnsureOpen();
        return InlineIfChain.Start(_context, _scope, _inlines, condition, then, () => _isOpen);
    }

    /// <summary>
    /// Adds the result of a condition, for example as Yes/No: <c>{{(condition):yesno}}</c>. Inline expressions use the
    /// engine's stricter dialect: only <c>true</c> is truthy.
    /// </summary>
    /// <param name="condition">The condition.</param>
    /// <param name="format">A boolean format such as <see cref="ValueFormat.YesNo"/> or <see cref="ValueFormat.Checkbox"/>.</param>
    /// <returns>This composer.</returns>
    public InlineComposer Expression(Condition condition, ValueFormat format)
    {
        ArgumentNullException.ThrowIfNull(condition);
        ArgumentNullException.ThrowIfNull(format);
        EnsureOpen();
        BlockComposer.CheckCondition(_context, _scope, condition);
        _inlines.Add(new ExpressionInline(condition, format));
        return this;
    }

    /// <summary>Adds an image at its natural size, inline with the text.</summary>
    /// <param name="image">The image.</param>
    /// <param name="altText">The text that describes the image (accessibility); required.</param>
    /// <returns>This composer.</returns>
    public InlineComposer Image(ImageSource image, string altText) => Image(image, altText, ImageFit.Natural);

    /// <summary>Adds an image inline with the text.</summary>
    /// <param name="image">The image.</param>
    /// <param name="altText">The text that describes the image (accessibility); required.</param>
    /// <param name="fit">How the image is sized; the aspect ratio is kept.</param>
    /// <returns>This composer.</returns>
    public InlineComposer Image(ImageSource image, string altText, ImageFit fit)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(fit);
        EnsureOpen();
        _inlines.Add(CreateImage(image, altText, fit));
        return this;
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
    /// <returns>This composer.</returns>
    public InlineComposer PageNumber() => AddField(PageField.Number, null);

    /// <summary>Adds the current page number in a character style.</summary>
    /// <param name="style">The character style; the style sheet must define it.</param>
    /// <returns>This composer.</returns>
    public InlineComposer PageNumber(CharacterStyleKey style)
    {
        ArgumentNullException.ThrowIfNull(style);
        AddField(PageField.Number, style);
        return this;
    }

    /// <summary>Adds the number of pages (a field Word and LibreOffice update).</summary>
    /// <returns>This composer.</returns>
    public InlineComposer PageCount() => AddField(PageField.Count, null);

    /// <summary>Adds the number of pages in a character style.</summary>
    /// <param name="style">The character style; the style sheet must define it.</param>
    /// <returns>This composer.</returns>
    public InlineComposer PageCount(CharacterStyleKey style)
    {
        ArgumentNullException.ThrowIfNull(style);
        AddField(PageField.Count, style);
        return this;
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

    private InlineComposer AddText(string text, CharacterStyleKey? style)
    {
        ArgumentNullException.ThrowIfNull(text);
        EnsureOpen();

        // A tab or a line break is an inline of its own, also inside a longer text: Text("a\tb") is Text("a"), Tab(),
        // Text("b"), so the JSON form writes both as "a\tb".
        int start = 0;
        for (int i = 0; i <= text.Length; i++)
        {
            if (i < text.Length && text[i] is not ('\t' or '\n'))
            {
                continue;
            }

            if (i > start)
            {
                _inlines.Add(new TextInline(text[start..i], style));
            }

            if (i < text.Length)
            {
                _inlines.Add(new TextInline(text[i].ToString(), style));
            }

            start = i + 1;
        }

        return this;
    }

    private InlineComposer AddField(PageField field, CharacterStyleKey? style)
    {
        EnsureOpen();
        _inlines.Add(new PageFieldInline(field, style));
        return this;
    }

    private InlineComposer AddValue(TemplatePath path, ValueFormat? format, CharacterStyleKey? style)
    {
        EnsureOpen();
        _inlines.Add(CreateValue(_context, _scope, path, format, style));
        return this;
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
