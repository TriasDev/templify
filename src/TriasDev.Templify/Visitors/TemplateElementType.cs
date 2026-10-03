// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace TriasDev.Templify.Visitors;

/// <summary>
/// Represents the type of template element detected in the document.
/// </summary>
/// <remarks>
/// This enum is not used by any Templify API. It is obsolete and will be removed in 2.0.
/// </remarks>
[Obsolete("Not used by any Templify API; no replacement. Will be removed in 2.0.")]
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
