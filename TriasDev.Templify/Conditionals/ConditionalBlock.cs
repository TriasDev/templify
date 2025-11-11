using DocumentFormat.OpenXml;

namespace TriasDev.Templify.Conditionals;

/// <summary>
/// Represents a parsed conditional block in the document template.
/// Supports {{#if condition}}...{{else}}...{{/if}} syntax.
/// </summary>
internal sealed class ConditionalBlock
{
    /// <summary>
    /// Gets the condition expression to evaluate.
    /// </summary>
    public string ConditionExpression { get; }

    /// <summary>
    /// Gets the OpenXML elements that make up the IF branch content.
    /// These elements are kept if the condition is true.
    /// </summary>
    public IReadOnlyList<OpenXmlElement> IfContentElements { get; }

    /// <summary>
    /// Gets the OpenXML elements that make up the ELSE branch content.
    /// These elements are kept if the condition is false.
    /// May be empty if there's no else branch.
    /// </summary>
    public IReadOnlyList<OpenXmlElement> ElseContentElements { get; }

    /// <summary>
    /// Gets whether this conditional block has an else branch.
    /// </summary>
    public bool HasElseBranch => ElseContentElements.Count > 0;

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

    /// <summary>
    /// Gets the start marker element (contains {{#if expression}}).
    /// </summary>
    public OpenXmlElement StartMarker { get; }

    /// <summary>
    /// Gets the optional else marker element (contains {{else}}).
    /// </summary>
    public OpenXmlElement? ElseMarker { get; }

    /// <summary>
    /// Gets the end marker element (contains {{/if}}).
    /// </summary>
    public OpenXmlElement EndMarker { get; }

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
        ConditionExpression = conditionExpression ?? throw new ArgumentNullException(nameof(conditionExpression));
        IfContentElements = ifContentElements ?? throw new ArgumentNullException(nameof(ifContentElements));
        ElseContentElements = elseContentElements ?? throw new ArgumentNullException(nameof(elseContentElements));
        StartMarker = startMarker ?? throw new ArgumentNullException(nameof(startMarker));
        ElseMarker = elseMarker;
        EndMarker = endMarker ?? throw new ArgumentNullException(nameof(endMarker));
        IsTableRowConditional = isTableRowConditional;
        NestingLevel = nestingLevel;
    }
}
