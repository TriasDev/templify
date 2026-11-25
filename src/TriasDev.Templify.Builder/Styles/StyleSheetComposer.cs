// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using TriasDev.Templify.Builder.Model;

namespace TriasDev.Templify.Builder;

/// <summary>
/// Defines the defaults and the styles of a <see cref="StyleSheet"/>. Valid only inside the lambda passed to
/// <see cref="StyleSheet.Create"/>.
/// </summary>
public sealed class StyleSheetComposer
{
    private bool _isOpen = true;

    internal StyleSheetComposer()
    {
    }

    internal TextProperties DefaultText { get; } = new TextProperties();

    internal List<ParagraphStyleDefinition> ParagraphStyles { get; } = new List<ParagraphStyleDefinition>();

    internal List<CharacterStyleDefinition> CharacterStyles { get; } = new List<CharacterStyleDefinition>();

    internal List<TableStyleDefinition> TableStyles { get; } = new List<TableStyleDefinition>();

    internal List<BoxStyleDefinition> BoxStyles { get; } = new List<BoxStyleDefinition>();

    internal List<ListStyleDefinition> ListStyles { get; } = new List<ListStyleDefinition>();

    /// <summary>Defines a bullet list style.</summary>
    /// <param name="key">The style's key.</param>
    /// <param name="style">Sets the levels and the item paragraph style.</param>
    public void List(ListStyleKey key, Action<ListStyleComposer> style)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(style);
        EnsureOpen();
        ListStyleDefinition definition = new ListStyleDefinition(key);
        style(new ListStyleComposer(this, definition));
        ListStyles.Add(definition);
    }

    /// <summary>Defines a box style.</summary>
    /// <param name="key">The style's key.</param>
    /// <param name="style">Sets the border, background and padding.</param>
    public void Box(BoxStyleKey key, Action<BoxStyleComposer> style)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(style);
        EnsureOpen();
        BoxStyleDefinition definition = new BoxStyleDefinition(key);
        style(new BoxStyleComposer(this, definition));
        BoxStyles.Add(definition);
    }

    /// <summary>Defines a table style.</summary>
    /// <param name="key">The style's key.</param>
    /// <param name="style">Sets the borders, rules, padding and header shading.</param>
    public void Table(TableStyleKey key, Action<TableStyleComposer> style)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(style);
        EnsureOpen();
        TableStyleDefinition definition = new TableStyleDefinition(key);
        style(new TableStyleComposer(this, definition));
        TableStyles.Add(definition);
    }

    /// <summary>Sets the default text formatting every style starts from.</summary>
    /// <param name="defaults">Sets font, size and colour.</param>
    public void Defaults(Action<DefaultStyleComposer> defaults)
    {
        ArgumentNullException.ThrowIfNull(defaults);
        EnsureOpen();
        DefaultStyleComposer composer = new DefaultStyleComposer(this, DefaultText);
        defaults(composer);
    }

    /// <summary>Defines a paragraph style.</summary>
    /// <param name="key">The style's key.</param>
    /// <param name="style">Sets the style's formatting.</param>
    public void Paragraph(ParagraphStyleKey key, Action<ParagraphStyleComposer> style)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(style);
        EnsureOpen();
        ParagraphStyleDefinition definition = new ParagraphStyleDefinition(key);
        style(new ParagraphStyleComposer(this, definition));
        ParagraphStyles.Add(definition);
    }

    /// <summary>Defines a character style.</summary>
    /// <param name="key">The style's key.</param>
    /// <param name="style">Sets the style's formatting.</param>
    public void Character(CharacterStyleKey key, Action<CharacterStyleComposer> style)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(style);
        EnsureOpen();
        CharacterStyleDefinition definition = new CharacterStyleDefinition(key);
        style(new CharacterStyleComposer(this, definition));
        CharacterStyles.Add(definition);
    }

    internal void Close() => _isOpen = false;

    internal void EnsureOpen()
    {
        if (!_isOpen)
        {
            throw new InvalidOperationException("A style sheet composer was used after StyleSheet.Create returned.");
        }
    }
}

/// <summary>Sets the default text formatting of a <see cref="StyleSheet"/>.</summary>
public sealed class DefaultStyleComposer
{
    private readonly TextProperties _text;

    private readonly StyleSheetComposer _owner;

    internal DefaultStyleComposer(StyleSheetComposer owner, TextProperties text)
    {
        _owner = owner;
        _text = text;
    }

    /// <summary>Sets the font family.</summary>
    /// <param name="font">The font.</param>
    /// <returns>This composer.</returns>
    public DefaultStyleComposer Font(FontFace font)
    {
        _owner.EnsureOpen();
        _text.Font = font ?? throw new ArgumentNullException(nameof(font));
        return this;
    }

    /// <summary>Sets the font size.</summary>
    /// <param name="size">The size; greater than zero.</param>
    /// <returns>This composer.</returns>
    public DefaultStyleComposer Size(Length size)
    {
        _owner.EnsureOpen();
        _text.Size = StyleRanges.FontSize(size);
        return this;
    }

    /// <summary>Sets the text colour.</summary>
    /// <param name="color">The colour.</param>
    /// <returns>This composer.</returns>
    public DefaultStyleComposer Color(Color color)
    {
        _owner.EnsureOpen();
        _text.Color = color;
        return this;
    }
}

/// <summary>Sets the formatting of a paragraph style. Unset properties are inherited from <c>BasedOn</c> or the defaults.</summary>
public sealed class ParagraphStyleComposer
{
    private readonly ParagraphStyleDefinition _style;

    private readonly StyleSheetComposer _owner;

    internal ParagraphStyleComposer(StyleSheetComposer owner, ParagraphStyleDefinition style)
    {
        _owner = owner;
        _style = style;
    }

    /// <summary>Inherits every unset property from another paragraph style.</summary>
    /// <param name="parent">The style to inherit from.</param>
    /// <returns>This composer.</returns>
    public ParagraphStyleComposer BasedOn(ParagraphStyleKey parent)
    {
        _owner.EnsureOpen();
        _style.BasedOn = parent ?? throw new ArgumentNullException(nameof(parent));
        return this;
    }

    /// <summary>Sets the style Word and LibreOffice switch to for the next paragraph when the user presses Enter.</summary>
    /// <param name="next">The next paragraph's style.</param>
    /// <returns>This composer.</returns>
    public ParagraphStyleComposer Next(ParagraphStyleKey next)
    {
        _owner.EnsureOpen();
        _style.Next = next ?? throw new ArgumentNullException(nameof(next));
        return this;
    }

    /// <summary>Sets the font family.</summary>
    /// <param name="font">The font.</param>
    /// <returns>This composer.</returns>
    public ParagraphStyleComposer Font(FontFace font)
    {
        _owner.EnsureOpen();
        _style.Text.Font = font ?? throw new ArgumentNullException(nameof(font));
        return this;
    }

    /// <summary>Sets the font size.</summary>
    /// <param name="size">The size; greater than zero.</param>
    /// <returns>This composer.</returns>
    public ParagraphStyleComposer Size(Length size)
    {
        _owner.EnsureOpen();
        _style.Text.Size = StyleRanges.FontSize(size);
        return this;
    }

    /// <summary>Sets the text colour.</summary>
    /// <param name="color">The colour.</param>
    /// <returns>This composer.</returns>
    public ParagraphStyleComposer Color(Color color)
    {
        _owner.EnsureOpen();
        _style.Text.Color = color;
        return this;
    }

    /// <summary>Sets bold text.</summary>
    /// <param name="value"><see langword="false"/> to switch off bold inherited from <c>BasedOn</c>.</param>
    /// <returns>This composer.</returns>
    public ParagraphStyleComposer Bold(bool value = true)
    {
        _owner.EnsureOpen();
        _style.Text.Bold = value;
        return this;
    }

    /// <summary>Sets italic text.</summary>
    /// <param name="value"><see langword="false"/> to switch off italic inherited from <c>BasedOn</c>.</param>
    /// <returns>This composer.</returns>
    public ParagraphStyleComposer Italic(bool value = true)
    {
        _owner.EnsureOpen();
        _style.Text.Italic = value;
        return this;
    }

    /// <summary>Sets the space above the paragraph.</summary>
    /// <param name="space">The space; not negative.</param>
    /// <returns>This composer.</returns>
    public ParagraphStyleComposer SpaceBefore(Length space)
    {
        _owner.EnsureOpen();
        _style.Paragraph.SpaceBefore = StyleRanges.NotNegative(space, nameof(space));
        return this;
    }

    /// <summary>Sets the space below the paragraph.</summary>
    /// <param name="space">The space; not negative.</param>
    /// <returns>This composer.</returns>
    public ParagraphStyleComposer SpaceAfter(Length space)
    {
        _owner.EnsureOpen();
        _style.Paragraph.SpaceAfter = StyleRanges.NotNegative(space, nameof(space));
        return this;
    }

    /// <summary>Sets an exact line height.</summary>
    /// <param name="height">The height of every line; greater than zero.</param>
    /// <returns>This composer.</returns>
    public ParagraphStyleComposer LineHeight(Length height)
    {
        _owner.EnsureOpen();
        _style.Paragraph.LineHeight = new Model.LineHeight(LineHeightRule.Exact, StyleRanges.Positive(height, nameof(height)), 0);
        return this;
    }

    /// <summary>Sets a minimum line height; lines grow with larger text.</summary>
    /// <param name="height">The minimum height of every line; greater than zero.</param>
    /// <returns>This composer.</returns>
    public ParagraphStyleComposer MinimumLineHeight(Length height)
    {
        _owner.EnsureOpen();
        _style.Paragraph.LineHeight = new Model.LineHeight(LineHeightRule.AtLeast, StyleRanges.Positive(height, nameof(height)), 0);
        return this;
    }

    /// <summary>Sets the line spacing as a multiple of single spacing, for example <c>1.15</c>.</summary>
    /// <param name="multiple">The multiple; between 0.25 and 10.</param>
    /// <returns>This composer.</returns>
    public ParagraphStyleComposer LineSpacing(double multiple)
    {
        _owner.EnsureOpen();
        if (double.IsNaN(multiple) || multiple < 0.25 || multiple > 10)
        {
            throw new ArgumentOutOfRangeException(nameof(multiple), multiple, "The line spacing must be between 0.25 and 10.");
        }

        _style.Paragraph.LineHeight = new Model.LineHeight(LineHeightRule.Proportional, Length.Zero, Math.Round(multiple, 3));
        return this;
    }

    /// <summary>Sets the alignment.</summary>
    /// <param name="alignment">The alignment.</param>
    /// <returns>This composer.</returns>
    public ParagraphStyleComposer Alignment(TextAlignment alignment)
    {
        _owner.EnsureOpen();
        if (!Enum.IsDefined(alignment))
        {
            throw new ArgumentOutOfRangeException(nameof(alignment), alignment, "Unknown alignment.");
        }

        _style.Paragraph.Alignment = alignment;
        return this;
    }

    /// <summary>Sets the indent at the start of the lines (left in left-to-right text).</summary>
    /// <param name="indent">The indent; not negative.</param>
    /// <returns>This composer.</returns>
    public ParagraphStyleComposer IndentStart(Length indent)
    {
        _owner.EnsureOpen();
        _style.Paragraph.IndentStart = StyleRanges.NotNegative(indent, nameof(indent));
        return this;
    }

    /// <summary>Sets the indent at the end of the lines (right in left-to-right text).</summary>
    /// <param name="indent">The indent; not negative.</param>
    /// <returns>This composer.</returns>
    public ParagraphStyleComposer IndentEnd(Length indent)
    {
        _owner.EnsureOpen();
        _style.Paragraph.IndentEnd = StyleRanges.NotNegative(indent, nameof(indent));
        return this;
    }

    /// <summary>Sets the indent of the first line relative to <see cref="IndentStart"/>; negative for a hanging indent.</summary>
    /// <param name="indent">The first-line indent.</param>
    /// <returns>This composer.</returns>
    public ParagraphStyleComposer FirstLineIndent(Length indent)
    {
        _owner.EnsureOpen();
        _style.Paragraph.FirstLineIndent = indent;
        return this;
    }

    /// <summary>Keeps the paragraph on the same page as the next one (for headings).</summary>
    /// <param name="value"><see langword="false"/> to switch off a setting inherited from <c>BasedOn</c>.</param>
    /// <returns>This composer.</returns>
    public ParagraphStyleComposer KeepWithNext(bool value = true)
    {
        _owner.EnsureOpen();
        _style.Paragraph.KeepWithNext = value;
        return this;
    }

    /// <summary>Keeps all lines of the paragraph on one page.</summary>
    /// <param name="value"><see langword="false"/> to switch off a setting inherited from <c>BasedOn</c>.</param>
    /// <returns>This composer.</returns>
    public ParagraphStyleComposer KeepLinesTogether(bool value = true)
    {
        _owner.EnsureOpen();
        _style.Paragraph.KeepLinesTogether = value;
        return this;
    }

    /// <summary>
    /// Makes the style a heading of the given outline level: it appears in the navigation pane and the table of
    /// contents, and ODT writes its paragraphs as headings (<c>text:h</c>).
    /// </summary>
    /// <param name="level">The level, 1 (top) to 9.</param>
    /// <returns>This composer.</returns>
    public ParagraphStyleComposer OutlineLevel(int level)
    {
        _owner.EnsureOpen();
        if (level is < 1 or > 9)
        {
            throw new ArgumentOutOfRangeException(nameof(level), level, "The outline level must be between 1 and 9.");
        }

        _style.Paragraph.OutlineLevel = level;
        return this;
    }
}

/// <summary>Sets the formatting of a character style. Unset properties are inherited.</summary>
public sealed class CharacterStyleComposer
{
    private readonly CharacterStyleDefinition _style;

    private readonly StyleSheetComposer _owner;

    internal CharacterStyleComposer(StyleSheetComposer owner, CharacterStyleDefinition style)
    {
        _owner = owner;
        _style = style;
    }

    /// <summary>Inherits every unset property from another character style.</summary>
    /// <param name="parent">The style to inherit from.</param>
    /// <returns>This composer.</returns>
    public CharacterStyleComposer BasedOn(CharacterStyleKey parent)
    {
        _owner.EnsureOpen();
        _style.BasedOn = parent ?? throw new ArgumentNullException(nameof(parent));
        return this;
    }

    /// <summary>Sets the font family.</summary>
    /// <param name="font">The font.</param>
    /// <returns>This composer.</returns>
    public CharacterStyleComposer Font(FontFace font)
    {
        _owner.EnsureOpen();
        _style.Text.Font = font ?? throw new ArgumentNullException(nameof(font));
        return this;
    }

    /// <summary>Sets the font size.</summary>
    /// <param name="size">The size; greater than zero.</param>
    /// <returns>This composer.</returns>
    public CharacterStyleComposer Size(Length size)
    {
        _owner.EnsureOpen();
        _style.Text.Size = StyleRanges.FontSize(size);
        return this;
    }

    /// <summary>Sets the text colour.</summary>
    /// <param name="color">The colour.</param>
    /// <returns>This composer.</returns>
    public CharacterStyleComposer Color(Color color)
    {
        _owner.EnsureOpen();
        _style.Text.Color = color;
        return this;
    }

    /// <summary>Sets bold text.</summary>
    /// <param name="value"><see langword="false"/> to switch off inherited bold.</param>
    /// <returns>This composer.</returns>
    public CharacterStyleComposer Bold(bool value = true)
    {
        _owner.EnsureOpen();
        _style.Text.Bold = value;
        return this;
    }

    /// <summary>Sets italic text.</summary>
    /// <param name="value"><see langword="false"/> to switch off inherited italic.</param>
    /// <returns>This composer.</returns>
    public CharacterStyleComposer Italic(bool value = true)
    {
        _owner.EnsureOpen();
        _style.Text.Italic = value;
        return this;
    }
}

/// <summary>Range checks for style values; they throw where the value is written.</summary>
internal static class StyleRanges
{
    public static Length FontSize(Length size)
    {
        if (size <= Length.Zero || size > 1638.Pt())
        {
            throw new ArgumentOutOfRangeException(nameof(size), size, "The font size must be greater than 0 and at most 1638 pt.");
        }

        return size;
    }

    public static Length NotNegative(Length value, string parameterName)
    {
        if (value < Length.Zero)
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "The value must not be negative.");
        }

        return value;
    }

    public static Length Positive(Length value, string parameterName)
    {
        if (value <= Length.Zero)
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "The value must be greater than zero.");
        }

        return value;
    }
}
