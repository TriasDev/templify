// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

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
