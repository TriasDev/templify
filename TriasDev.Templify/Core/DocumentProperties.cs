// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace TriasDev.Templify.Core;

/// <summary>
/// Specifies document metadata properties to set on the output document.
/// Properties left as <c>null</c> preserve the original template value.
/// </summary>
public sealed class DocumentProperties
{
    /// <summary>
    /// Gets or initializes the document author.
    /// Maps to the OPC <c>Creator</c> property (shown as "Author" in Word).
    /// </summary>
    public string? Author { get; init; }

    /// <summary>
    /// Gets or initializes the document title.
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    /// Gets or initializes the document subject.
    /// </summary>
    public string? Subject { get; init; }

    /// <summary>
    /// Gets or initializes the document description.
    /// Maps to "Comments" in the Word document properties dialog.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Gets or initializes the document keywords.
    /// </summary>
    public string? Keywords { get; init; }

    /// <summary>
    /// Gets or initializes the document category.
    /// </summary>
    public string? Category { get; init; }

    /// <summary>
    /// Gets or initializes the last modified by value.
    /// </summary>
    public string? LastModifiedBy { get; init; }
}
