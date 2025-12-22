// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using DocumentFormat.OpenXml;

namespace TriasDev.Templify.Conditionals;

/// <summary>
/// Represents a parsed conditional block in the document template.
/// Supports {{#if condition}}...{{#elseif condition}}...{{else}}...{{/if}} syntax.
/// </summary>
internal sealed class ConditionalBlock
{
    /// <summary>
    /// Gets all branches in this conditional block.
    /// The first branch is always the {{#if}} branch.
    /// Subsequent branches are {{#elseif}} branches.
    /// The last branch may be an {{else}} branch (with no condition).
    /// </summary>
    public IReadOnlyList<ConditionalBranch> Branches { get; }

    /// <summary>
    /// Gets the end marker element (contains {{/if}}).
    /// </summary>
    public OpenXmlElement EndMarker { get; }

    /// <summary>
    /// Gets whether this is a table row conditional.
    /// </summary>
    public bool IsTableRowConditional { get; }

    /// <summary>
    /// Gets the nesting level of this conditional block.
    /// Top-level conditionals have nesting level 0.
    /// Conditionals nested inside other conditionals have higher levels (1, 2, etc.).
    /// </summary>
    public int NestingLevel { get; }

    // Backward-compatible properties

    /// <summary>
    /// Gets the condition expression of the first (if) branch.
    /// </summary>
    public string ConditionExpression => Branches[0].ConditionExpression!;

    /// <summary>
    /// Gets the OpenXML elements that make up the IF branch content.
    /// These elements are kept if the condition is true.
    /// </summary>
    public IReadOnlyList<OpenXmlElement> IfContentElements => Branches[0].ContentElements;

    /// <summary>
    /// Gets the OpenXML elements that make up the ELSE branch content.
    /// These elements are kept if no conditions match.
    /// Returns empty list if there's no else branch.
    /// </summary>
    public IReadOnlyList<OpenXmlElement> ElseContentElements =>
        HasElseBranch ? Branches[^1].ContentElements : Array.Empty<OpenXmlElement>();

    /// <summary>
    /// Gets whether this conditional block has an else branch.
    /// </summary>
    public bool HasElseBranch => Branches.Count > 0 && Branches[^1].IsElseBranch;

    /// <summary>
    /// Gets the start marker element (contains {{#if expression}}).
    /// </summary>
    public OpenXmlElement StartMarker => Branches[0].Marker;

    /// <summary>
    /// Gets the optional else marker element (contains {{else}}).
    /// </summary>
    public OpenXmlElement? ElseMarker => HasElseBranch ? Branches[^1].Marker : null;

    /// <summary>
    /// Gets whether this conditional has elseif branches.
    /// </summary>
    public bool HasElseIfBranches => Branches.Count > 2 || (Branches.Count == 2 && !HasElseBranch);

    public ConditionalBlock(
        IReadOnlyList<ConditionalBranch> branches,
        OpenXmlElement endMarker,
        bool isTableRowConditional = false,
        int nestingLevel = 0)
    {
        if (branches == null || branches.Count == 0)
        {
            throw new ArgumentException("At least one branch is required.", nameof(branches));
        }

        if (branches[0].ConditionExpression == null)
        {
            throw new ArgumentException("First branch must have a condition (cannot be an else branch).", nameof(branches));
        }

        Branches = branches;
        EndMarker = endMarker ?? throw new ArgumentNullException(nameof(endMarker));
        IsTableRowConditional = isTableRowConditional;
        NestingLevel = nestingLevel;
    }

    /// <summary>
    /// Backward-compatible constructor for simple if/else conditionals.
    /// </summary>
    public ConditionalBlock(
        string conditionExpression,
        IReadOnlyList<OpenXmlElement> ifContentElements,
        IReadOnlyList<OpenXmlElement> elseContentElements,
        OpenXmlElement startMarker,
        OpenXmlElement? elseMarker,
        OpenXmlElement endMarker,
        bool isTableRowConditional = false,
        int nestingLevel = 0)
    {
        if (conditionExpression == null)
        {
            throw new ArgumentNullException(nameof(conditionExpression));
        }

        List<ConditionalBranch> branches = new List<ConditionalBranch>();

        // Add the if branch
        branches.Add(new ConditionalBranch(
            conditionExpression,
            ifContentElements ?? throw new ArgumentNullException(nameof(ifContentElements)),
            startMarker ?? throw new ArgumentNullException(nameof(startMarker))));

        // Add the else branch if it exists
        if (elseMarker != null && elseContentElements != null && elseContentElements.Count > 0)
        {
            branches.Add(new ConditionalBranch(
                null, // else has no condition
                elseContentElements,
                elseMarker));
        }
        else if (elseContentElements != null && elseContentElements.Count > 0)
        {
            // elseContentElements provided but no elseMarker - create dummy else branch
            // This shouldn't happen in normal usage but handle it gracefully
            branches.Add(new ConditionalBranch(
                null,
                elseContentElements,
                startMarker)); // Use startMarker as fallback
        }

        Branches = branches;
        EndMarker = endMarker ?? throw new ArgumentNullException(nameof(endMarker));
        IsTableRowConditional = isTableRowConditional;
        NestingLevel = nestingLevel;
    }
}
