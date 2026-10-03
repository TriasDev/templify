// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Xml.Linq;

namespace TriasDev.Templify.OpenDocument;

/// <summary>
/// A branch of an OpenDocument conditional block: its condition (null for <c>{{#else}}</c>), its marker
/// element and the elements between its marker and the next marker.
/// </summary>
internal sealed record OdtConditionalBranch(string? ConditionExpression, IReadOnlyList<XElement> ContentElements, XElement Marker)
{
    /// <summary>Gets whether this is the <c>{{#else}}</c> branch.</summary>
    public bool IsElseBranch => ConditionExpression == null;
}

/// <summary>
/// A conditional block (<c>{{#if}}</c> … <c>{{/if}}</c>) over sibling OpenDocument elements
/// (paragraphs, or table rows for a table-row conditional). The counterpart of <c>ConditionalBlock</c>.
/// </summary>
internal sealed class OdtConditionalBlock
{
    public OdtConditionalBlock(
        IReadOnlyList<OdtConditionalBranch> branches,
        XElement endMarker,
        bool isTableRowConditional,
        int nestingLevel)
    {
        Branches = branches;
        EndMarker = endMarker;
        IsTableRowConditional = isTableRowConditional;
        NestingLevel = nestingLevel;
    }

    /// <summary>Gets the branches in order (if, elseif…, else).</summary>
    public IReadOnlyList<OdtConditionalBranch> Branches { get; }

    /// <summary>Gets the element holding <c>{{#if}}</c>.</summary>
    public XElement StartMarker => Branches[0].Marker;

    /// <summary>Gets the element holding <c>{{/if}}</c>.</summary>
    public XElement EndMarker { get; }

    /// <summary>Gets whether the markers are table rows.</summary>
    public bool IsTableRowConditional { get; }

    /// <summary>Gets the nesting level (0 for top level).</summary>
    public int NestingLevel { get; }
}
