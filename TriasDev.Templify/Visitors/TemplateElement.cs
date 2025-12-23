// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;
using TriasDev.Templify.Conditionals;
using TriasDev.Templify.Loops;
using TriasDev.Templify.Placeholders;

namespace TriasDev.Templify.Visitors;

/// <summary>
/// Represents the type of template element detected in the document.
/// </summary>
public enum TemplateElementType
{
    /// <summary>
    /// A conditional block ({{#if}}/{{#else}}/{{/if}}).
    /// </summary>
    Conditional,

    /// <summary>
    /// A loop block ({{#foreach}}/{{/foreach}}).
    /// </summary>
    Loop,

    /// <summary>
    /// A placeholder ({{VariableName}}).
    /// </summary>
    Placeholder,

    /// <summary>
    /// A regular paragraph with no template constructs.
    /// </summary>
    Paragraph,

    /// <summary>
    /// Unknown or unprocessable element.
    /// </summary>
    Unknown
}

/// <summary>
/// Represents a detected template element in the document.
/// Provides a unified view of different template constructs for the DocumentWalker.
/// </summary>
/// <remarks>
/// This is a discriminated union type that can represent any kind of template element.
/// Use the Type property to determine which specific property contains data.
/// Factory methods ensure type safety and prevent invalid states.
/// </remarks>
internal sealed class TemplateElement
{
    /// <summary>
    /// Gets the type of template element.
    /// </summary>
    public TemplateElementType Type { get; }

    /// <summary>
    /// Gets the OpenXML element (for paragraphs and unknown types).
    /// </summary>
    public OpenXmlElement? Element { get; private init; }

    /// <summary>
    /// Gets the conditional block (when Type is Conditional).
    /// </summary>
    public ConditionalBlock? Conditional { get; private init; }

    /// <summary>
    /// Gets the loop block (when Type is Loop).
    /// </summary>
    public LoopBlock? Loop { get; private init; }

    /// <summary>
    /// Gets the placeholder match (when Type is Placeholder).
    /// </summary>
    public PlaceholderMatch? Placeholder { get; private init; }

    /// <summary>
    /// Gets the paragraph (when Type is Placeholder or Paragraph).
    /// </summary>
    public Paragraph? Paragraph { get; private init; }

    private TemplateElement(TemplateElementType type)
    {
        Type = type;
    }

    /// <summary>
    /// Creates a template element representing a conditional block.
    /// </summary>
    public static TemplateElement FromConditional(ConditionalBlock conditional)
    {
        if (conditional == null)
        {
            throw new ArgumentNullException(nameof(conditional));
        }

        return new TemplateElement(TemplateElementType.Conditional)
        {
            Conditional = conditional
        };
    }

    /// <summary>
    /// Creates a template element representing a loop block.
    /// </summary>
    public static TemplateElement FromLoop(LoopBlock loop)
    {
        if (loop == null)
        {
            throw new ArgumentNullException(nameof(loop));
        }

        return new TemplateElement(TemplateElementType.Loop)
        {
            Loop = loop
        };
    }

    /// <summary>
    /// Creates a template element representing a placeholder.
    /// </summary>
    public static TemplateElement FromPlaceholder(PlaceholderMatch placeholder, Paragraph paragraph)
    {
        if (paragraph == null)
        {
            throw new ArgumentNullException(nameof(paragraph));
        }

        return new TemplateElement(TemplateElementType.Placeholder)
        {
            Placeholder = placeholder,
            Paragraph = paragraph
        };
    }

    /// <summary>
    /// Creates a template element representing a regular paragraph.
    /// </summary>
    public static TemplateElement FromParagraph(Paragraph paragraph)
    {
        if (paragraph == null)
        {
            throw new ArgumentNullException(nameof(paragraph));
        }

        return new TemplateElement(TemplateElementType.Paragraph)
        {
            Paragraph = paragraph,
            Element = paragraph
        };
    }

    /// <summary>
    /// Creates a template element representing an unknown element type.
    /// </summary>
    public static TemplateElement Unknown(OpenXmlElement element)
    {
        if (element == null)
        {
            throw new ArgumentNullException(nameof(element));
        }

        return new TemplateElement(TemplateElementType.Unknown)
        {
            Element = element
        };
    }
}
