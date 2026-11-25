// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace TriasDev.Templify.Builder.Model;

/// <summary>Text formatting that a style sets; unset properties are inherited.</summary>
internal sealed class TextProperties
{
    public FontFace? Font { get; set; }

    public Length? Size { get; set; }

    public Color? Color { get; set; }

    public bool? Bold { get; set; }

    public bool? Italic { get; set; }
}

/// <summary>How the height of a paragraph's lines is set.</summary>
internal enum LineHeightRule
{
    /// <summary>A multiple of the single line height (<see cref="LineHeight.Multiple"/>).</summary>
    Proportional,

    /// <summary>Exactly <see cref="LineHeight.Height"/>.</summary>
    Exact,

    /// <summary>At least <see cref="LineHeight.Height"/>.</summary>
    AtLeast,
}

/// <summary>The line height of a paragraph style.</summary>
internal sealed record LineHeight(LineHeightRule Rule, Length Height, double Multiple);

/// <summary>Paragraph formatting that a style sets; unset properties are inherited.</summary>
internal sealed class ParagraphProperties
{
    public Length? SpaceBefore { get; set; }

    public Length? SpaceAfter { get; set; }

    public LineHeight? LineHeight { get; set; }

    public TextAlignment? Alignment { get; set; }

    public Length? IndentStart { get; set; }

    public Length? IndentEnd { get; set; }

    public Length? FirstLineIndent { get; set; }

    public bool? KeepWithNext { get; set; }

    public bool? KeepLinesTogether { get; set; }

    public int? OutlineLevel { get; set; }
}

/// <summary>A paragraph style as defined in a <see cref="StyleSheet"/>.</summary>
internal sealed class ParagraphStyleDefinition(ParagraphStyleKey key)
{
    public ParagraphStyleKey Key { get; } = key;

    public ParagraphStyleKey? BasedOn { get; set; }

    public ParagraphStyleKey? Next { get; set; }

    public ParagraphProperties Paragraph { get; } = new ParagraphProperties();

    public TextProperties Text { get; } = new TextProperties();
}

/// <summary>A character style as defined in a <see cref="StyleSheet"/>.</summary>
internal sealed class CharacterStyleDefinition(CharacterStyleKey key)
{
    public CharacterStyleKey Key { get; } = key;

    public CharacterStyleKey? BasedOn { get; set; }

    public TextProperties Text { get; } = new TextProperties();
}

/// <summary>The standard document properties.</summary>
internal sealed record DocumentInfo(string? Title, string? Author, string? Subject)
{
    public static DocumentInfo Empty { get; } = new DocumentInfo(null, null, null);
}
