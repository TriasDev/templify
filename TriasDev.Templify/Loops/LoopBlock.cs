// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using DocumentFormat.OpenXml;

namespace TriasDev.Templify.Loops;

/// <summary>
/// Represents a parsed loop block in the document template.
/// Supports {{#foreach CollectionName}}...{{/foreach}} syntax.
/// </summary>
internal sealed class LoopBlock
{
    /// <summary>
    /// Gets the name of the collection to iterate over.
    /// </summary>
    public string CollectionName { get; }

    /// <summary>
    /// Gets the OpenXML elements that make up the loop content.
    /// These elements will be cloned for each item in the collection.
    /// </summary>
    public IReadOnlyList<OpenXmlElement> ContentElements { get; }

    /// <summary>
    /// Gets the optional empty block to show when collection is empty.
    /// </summary>
    public LoopBlock? EmptyBlock { get; }

    /// <summary>
    /// Gets whether this is a table row loop.
    /// </summary>
    public bool IsTableRowLoop { get; }

    /// <summary>
    /// Gets the start marker element (contains {{#foreach}}).
    /// </summary>
    public OpenXmlElement StartMarker { get; }

    /// <summary>
    /// Gets the end marker element (contains {{/foreach}}).
    /// </summary>
    public OpenXmlElement EndMarker { get; }

    public LoopBlock(
        string collectionName,
        IReadOnlyList<OpenXmlElement> contentElements,
        OpenXmlElement startMarker,
        OpenXmlElement endMarker,
        bool isTableRowLoop = false,
        LoopBlock? emptyBlock = null)
    {
        CollectionName = collectionName ?? throw new ArgumentNullException(nameof(collectionName));
        ContentElements = contentElements ?? throw new ArgumentNullException(nameof(contentElements));
        StartMarker = startMarker ?? throw new ArgumentNullException(nameof(startMarker));
        EndMarker = endMarker ?? throw new ArgumentNullException(nameof(endMarker));
        IsTableRowLoop = isTableRowLoop;
        EmptyBlock = emptyBlock;
    }
}
