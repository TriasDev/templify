// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using DocumentFormat.OpenXml;

namespace TriasDev.Templify.Conditionals;

/// <summary>
/// Represents a single branch in a conditional block (if, elseif, or else).
/// </summary>
internal sealed class ConditionalBranch
{
    /// <summary>
    /// Gets the condition expression to evaluate.
    /// Null for {{else}} branches which have no condition.
    /// </summary>
    public string? ConditionExpression { get; }

    /// <summary>
    /// Gets the OpenXML elements that make up the branch content.
    /// </summary>
    public IReadOnlyList<OpenXmlElement> ContentElements { get; }

    /// <summary>
    /// Gets the marker element (contains {{#if}}, {{#elseif}}, or {{else}}).
    /// </summary>
    public OpenXmlElement Marker { get; }

    /// <summary>
    /// Gets whether this is an else branch (no condition).
    /// </summary>
    public bool IsElseBranch => ConditionExpression == null;

    public ConditionalBranch(
        string? conditionExpression,
        IReadOnlyList<OpenXmlElement> contentElements,
        OpenXmlElement marker)
    {
        ConditionExpression = conditionExpression;
        ContentElements = contentElements ?? throw new ArgumentNullException(nameof(contentElements));
        Marker = marker ?? throw new ArgumentNullException(nameof(marker));
    }
}
