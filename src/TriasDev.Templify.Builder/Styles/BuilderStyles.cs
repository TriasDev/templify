// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using TriasDev.Templify.Builder.Model;

namespace TriasDev.Templify.Builder;

/// <summary>Styles the builder owns.</summary>
public static class BuilderStyles
{
    /// <summary>
    /// The paragraph style of the Templify markers (<c>{{#if …}}</c>, <c>{{/if}}</c>, …). Markers are visible on purpose:
    /// whoever adapts a downloaded template has to see its logic to delete a block together with its markers. Templify
    /// removes the marker paragraphs when it fills the template.
    /// </summary>
    /// <remarks>
    /// By default it is small, muted and monospace (Courier New, 8 pt, grey, no spacing, kept with the next paragraph).
    /// A style sheet may define this key to change how markers look; it then replaces the default entirely.
    /// </remarks>
    public static ParagraphStyleKey TemplateMarker { get; } = ParagraphStyleKey.BuiltIn("TemplifyMarker", "Template Marker");

    /// <summary>The paragraph style of spacer paragraphs that keep cells and tables apart once markers are removed (1 pt, invisible).</summary>
    internal static ParagraphStyleKey TemplateSpacer { get; } = ParagraphStyleKey.BuiltIn("TemplifySpacer", "Template Spacer");

    /// <summary>The definition of <see cref="TemplateSpacer"/>.</summary>
    internal static ParagraphStyleDefinition SpacerDefinition()
    {
        ParagraphStyleDefinition definition = new ParagraphStyleDefinition(TemplateSpacer);
        definition.Text.Size = 1.Pt();
        definition.Paragraph.SpaceBefore = Length.Zero;
        definition.Paragraph.SpaceAfter = Length.Zero;
        definition.Paragraph.LineHeight = new LineHeight(LineHeightRule.Exact, 1.Pt(), 0);
        return definition;
    }

    /// <summary>The default definition of <see cref="TemplateMarker"/>.</summary>
    internal static ParagraphStyleDefinition DefaultMarkerDefinition()
    {
        ParagraphStyleDefinition definition = new ParagraphStyleDefinition(TemplateMarker);
        definition.Text.Font = new FontFace("Courier New");
        definition.Text.Size = 8.Pt();
        definition.Text.Color = Color.FromHex("737373");
        definition.Paragraph.SpaceBefore = Length.Zero;
        definition.Paragraph.SpaceAfter = Length.Zero;
        definition.Paragraph.KeepWithNext = true;
        return definition;
    }
}
