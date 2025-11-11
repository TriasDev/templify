using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using TriasDev.Templify.Core;

namespace TriasDev.Templify.Conditionals;

/// <summary>
/// Processes conditional blocks by evaluating conditions and removing false branches.
/// </summary>
internal sealed class ConditionalProcessor
{
    private readonly ConditionalEvaluator _evaluator;

    public ConditionalProcessor()
    {
        _evaluator = new ConditionalEvaluator();
    }

    /// <summary>
    /// Processes all conditional blocks in the document.
    /// Processes nested conditionals from deepest to shallowest to ensure inner blocks are evaluated first.
    /// </summary>
    public void ProcessConditionals(
        WordprocessingDocument document,
        IEvaluationContext context)
    {
        // Detect all conditionals (including nested ones)
        IReadOnlyList<ConditionalBlock> conditionals = ConditionalDetector.DetectConditionals(document);

        // Sort conditionals by nesting level (deepest first) to process inner blocks before outer blocks
        // This ensures that nested conditionals are evaluated and cleaned up before their parent blocks
        List<ConditionalBlock> sortedConditionals = conditionals
            .OrderByDescending(c => c.NestingLevel)
            .ThenBy(c => c.StartMarker.GetHashCode()) // Secondary sort for stable ordering
            .ToList();

        // Process each conditional block
        foreach (ConditionalBlock conditional in sortedConditionals)
        {
            // Skip if the conditional's markers have already been removed by a parent conditional
            if (conditional.StartMarker.Parent == null || conditional.EndMarker.Parent == null)
            {
                continue;
            }

            ProcessConditionalBlock(conditional, context);
        }
    }

    /// <summary>
    /// Processes a single conditional block.
    /// </summary>
    internal void ProcessConditionalBlock(ConditionalBlock conditional, IEvaluationContext context)
    {
        // Evaluate the condition
        bool conditionResult = _evaluator.Evaluate(conditional.ConditionExpression, context);

        if (conditionResult)
        {
            // Condition is TRUE: Keep IF branch, remove ELSE branch
            ProcessTrueBranch(conditional);
        }
        else
        {
            // Condition is FALSE: Remove IF branch, keep ELSE branch
            ProcessFalseBranch(conditional);
        }
    }

    /// <summary>
    /// Processes conditional when condition is TRUE.
    /// Keeps IF content, removes ELSE content and all markers.
    /// </summary>
    private void ProcessTrueBranch(ConditionalBlock conditional)
    {
        // Remove the start marker (if it still has a parent)
        if (conditional.StartMarker.Parent != null)
        {
            conditional.StartMarker.Remove();
        }

        // Remove ELSE content elements (if any)
        foreach (OpenXmlElement element in conditional.ElseContentElements)
        {
            // Skip if already removed by a nested conditional
            if (element.Parent != null)
            {
                element.Remove();
            }
        }

        // Remove the else marker (if any and if it still has a parent)
        if (conditional.ElseMarker?.Parent != null)
        {
            conditional.ElseMarker.Remove();
        }

        // Remove the end marker (if it still has a parent)
        if (conditional.EndMarker.Parent != null)
        {
            conditional.EndMarker.Remove();
        }

        // IF content elements remain in the document
    }

    /// <summary>
    /// Processes conditional when condition is FALSE.
    /// Removes IF content, keeps ELSE content (if any), removes all markers.
    /// </summary>
    private void ProcessFalseBranch(ConditionalBlock conditional)
    {
        // Remove the start marker (if it still has a parent)
        if (conditional.StartMarker.Parent != null)
        {
            conditional.StartMarker.Remove();
        }

        // Remove IF content elements
        foreach (OpenXmlElement element in conditional.IfContentElements)
        {
            // Skip if already removed by a nested conditional
            if (element.Parent != null)
            {
                element.Remove();
            }
        }

        // Remove the else marker (if any and if it still has a parent)
        if (conditional.ElseMarker?.Parent != null)
        {
            conditional.ElseMarker.Remove();
        }

        // Remove the end marker (if it still has a parent)
        if (conditional.EndMarker.Parent != null)
        {
            conditional.EndMarker.Remove();
        }

        // ELSE content elements (if any) remain in the document
        // If there's no else branch, nothing remains (which is correct)
    }
}
