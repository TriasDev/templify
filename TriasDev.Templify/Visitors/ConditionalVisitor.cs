// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Text.RegularExpressions;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;
using TriasDev.Templify.Conditionals;
using TriasDev.Templify.Core;
using TriasDev.Templify.Loops;
using TriasDev.Templify.Placeholders;

namespace TriasDev.Templify.Visitors;

/// <summary>
/// Visitor that processes conditional blocks ({{#if}}/{{else}}/{{/if}}).
/// Evaluates conditions and removes false branches.
/// </summary>
/// <remarks>
/// This visitor wraps the logic from ConditionalProcessor into the visitor pattern.
/// Benefits:
/// - Works with DocumentWalker for unified traversal
/// - Context-aware (uses IEvaluationContext from Phase 1)
/// - Can be composed with other visitors
/// - Eliminates duplication between ConditionalProcessor and LoopProcessor
/// </remarks>
internal sealed class ConditionalVisitor : ITemplateElementVisitor
{
    private readonly ConditionalEvaluator _evaluator;

    private static readonly Regex _ifStartPattern = new Regex(
        @"\{\{#if\s+.+?\}\}",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex _elsePattern = new Regex(
        @"\{\{else\}\}",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex _ifEndPattern = new Regex(
        @"\{\{/if\}\}",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public ConditionalVisitor()
    {
        _evaluator = new ConditionalEvaluator();
    }

    /// <summary>
    /// Processes a conditional block by evaluating the condition and keeping the appropriate branch.
    /// </summary>
    /// <param name="conditional">The conditional block to process.</param>
    /// <param name="context">The evaluation context for resolving variables.</param>
    public void VisitConditional(ConditionalBlock conditional, IEvaluationContext context)
    {
        // Check if this is an inline conditional (start and end in same element)
        bool isInlineConditional = conditional.StartMarker == conditional.EndMarker
                                   && conditional.StartMarker is Paragraph;

        // Evaluate the condition using the provided context
        bool conditionResult = _evaluator.Evaluate(conditional.ConditionExpression, context);

        if (isInlineConditional)
        {
            // Process at Run level to preserve surrounding content
            ProcessInlineConditional(conditional, conditionResult);
        }
        else if (conditionResult)
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
    /// Not implemented - ConditionalVisitor only processes conditionals.
    /// </summary>
    public void VisitLoop(LoopBlock loop, IEvaluationContext context)
    {
        // ConditionalVisitor doesn't process loops
        // This method is no-op to satisfy the interface
    }

    /// <summary>
    /// Not implemented - ConditionalVisitor only processes conditionals.
    /// </summary>
    public void VisitPlaceholder(PlaceholderMatch placeholder, Paragraph paragraph, IEvaluationContext context)
    {
        // ConditionalVisitor doesn't process placeholders
        // This method is no-op to satisfy the interface
    }

    /// <summary>
    /// Not implemented - ConditionalVisitor only processes conditionals.
    /// </summary>
    public void VisitParagraph(Paragraph paragraph, IEvaluationContext context)
    {
        // ConditionalVisitor doesn't process regular paragraphs
        // This method is no-op to satisfy the interface
    }

    /// <summary>
    /// Processes conditional when condition is TRUE.
    /// Keeps IF content, removes ELSE content and all markers.
    /// </summary>
    /// <remarks>
    /// This method is extracted from ConditionalProcessor.ProcessTrueBranch()
    /// to eliminate duplication.
    /// </remarks>
    private void ProcessTrueBranch(ConditionalBlock conditional)
    {
        // Remove the start marker (if it still has a parent)
        TemplateElementHelper.SafeRemove(conditional.StartMarker);

        // Remove ELSE content elements (if any)
        TemplateElementHelper.SafeRemoveRange(conditional.ElseContentElements);

        // Remove the else marker (if any)
        if (conditional.ElseMarker != null)
        {
            TemplateElementHelper.SafeRemove(conditional.ElseMarker);
        }

        // Remove the end marker
        TemplateElementHelper.SafeRemove(conditional.EndMarker);

        // IF content elements remain in the document
    }

    /// <summary>
    /// Processes conditional when condition is FALSE.
    /// Removes IF content, keeps ELSE content (if any), removes all markers.
    /// </summary>
    /// <remarks>
    /// This method is extracted from ConditionalProcessor.ProcessFalseBranch()
    /// to eliminate duplication.
    /// </remarks>
    private void ProcessFalseBranch(ConditionalBlock conditional)
    {
        // Remove the start marker
        TemplateElementHelper.SafeRemove(conditional.StartMarker);

        // Remove IF content elements
        TemplateElementHelper.SafeRemoveRange(conditional.IfContentElements);

        // Remove the else marker (if any)
        if (conditional.ElseMarker != null)
        {
            TemplateElementHelper.SafeRemove(conditional.ElseMarker);
        }

        // Remove the end marker
        TemplateElementHelper.SafeRemove(conditional.EndMarker);

        // ELSE content elements (if any) remain in the document
        // If there's no else branch, nothing remains (which is correct)
    }

    /// <summary>
    /// Processes an inline conditional where {{#if}} and {{/if}} are in the same paragraph.
    /// Works at the Run level to preserve content before and after the conditional.
    /// </summary>
    /// <param name="conditional">The inline conditional block.</param>
    /// <param name="conditionResult">Whether the condition evaluated to true.</param>
    private void ProcessInlineConditional(ConditionalBlock conditional, bool conditionResult)
    {
        Paragraph paragraph = (Paragraph)conditional.StartMarker;
        List<Run> runs = paragraph.Elements<Run>().ToList();

        // Find the boundaries of the conditional within the runs
        int ifStartRunIndex = -1;
        int ifStartTextPosition = -1;
        int elseRunIndex = -1;
        int elseTextPosition = -1;
        int ifEndRunIndex = -1;
        int ifEndTextPosition = -1;

        // Scan runs to find the markers
        for (int i = 0; i < runs.Count; i++)
        {
            string? runText = runs[i].InnerText;
            if (string.IsNullOrEmpty(runText))
            {
                continue;
            }

            // Find {{#if ...}}
            if (ifStartRunIndex == -1)
            {
                Match ifMatch = _ifStartPattern.Match(runText);
                if (ifMatch.Success)
                {
                    ifStartRunIndex = i;
                    ifStartTextPosition = ifMatch.Index;
                }
            }

            // Find {{else}} (only look after we found {{#if}})
            if (ifStartRunIndex != -1 && elseRunIndex == -1 && ifEndRunIndex == -1)
            {
                Match elseMatch = _elsePattern.Match(runText);
                if (elseMatch.Success)
                {
                    elseRunIndex = i;
                    elseTextPosition = elseMatch.Index;
                }
            }

            // Find {{/if}} (only look after we found {{#if}})
            if (ifStartRunIndex != -1 && ifEndRunIndex == -1)
            {
                Match endMatch = _ifEndPattern.Match(runText);
                if (endMatch.Success)
                {
                    ifEndRunIndex = i;
                    ifEndTextPosition = endMatch.Index;
                }
            }
        }

        if (ifStartRunIndex == -1 || ifEndRunIndex == -1)
        {
            // Couldn't find markers - fall back to standard processing
            if (conditionResult)
            {
                ProcessTrueBranch(conditional);
            }
            else
            {
                ProcessFalseBranch(conditional);
            }
            return;
        }

        // Process the conditional at text level
        ProcessInlineConditionalText(
            paragraph,
            runs,
            ifStartRunIndex,
            ifStartTextPosition,
            elseRunIndex,
            elseTextPosition,
            ifEndRunIndex,
            ifEndTextPosition,
            conditionResult);
    }

    /// <summary>
    /// Processes inline conditional text, removing markers and unwanted content.
    /// </summary>
    private void ProcessInlineConditionalText(
        Paragraph paragraph,
        List<Run> runs,
        int ifStartRunIndex,
        int ifStartTextPosition,
        int elseRunIndex,
        int elseTextPosition,
        int ifEndRunIndex,
        int ifEndTextPosition,
        bool conditionResult)
    {
        // Strategy: Rebuild the paragraph text, keeping only the appropriate parts
        // 1. Keep everything before {{#if}}
        // 2. If condition true: keep IF content (between {{#if}} and {{else}}/{{/if}})
        // 3. If condition false and has else: keep ELSE content (between {{else}} and {{/if}})
        // 4. Keep everything after {{/if}}

        // Process each run, modifying text as needed
        for (int i = 0; i < runs.Count; i++)
        {
            Run run = runs[i];
            Text? textElement = run.GetFirstChild<Text>();
            if (textElement == null)
            {
                continue;
            }

            string text = textElement.Text ?? "";

            if (i < ifStartRunIndex)
            {
                // Before the conditional - keep as-is
                continue;
            }
            else if (i > ifEndRunIndex)
            {
                // After the conditional - keep as-is
                continue;
            }
            else if (i == ifStartRunIndex && i == ifEndRunIndex)
            {
                // The entire conditional is in one run
                text = ProcessSingleRunConditional(
                    text,
                    ifStartTextPosition,
                    elseRunIndex == i ? elseTextPosition : -1,
                    ifEndTextPosition,
                    conditionResult);
                textElement.Text = text;
            }
            else if (i == ifStartRunIndex)
            {
                // Start run - remove from {{#if}} onwards
                Match ifMatch = _ifStartPattern.Match(text);
                if (ifMatch.Success)
                {
                    string beforeIf = text.Substring(0, ifMatch.Index);
                    string afterIfMarker = text.Substring(ifMatch.Index + ifMatch.Length);

                    if (conditionResult)
                    {
                        // Keep content after the marker (IF content)
                        textElement.Text = beforeIf + afterIfMarker;
                    }
                    else
                    {
                        // Remove IF content - just keep text before marker
                        textElement.Text = beforeIf;
                    }
                }
            }
            else if (i == ifEndRunIndex)
            {
                // End run - remove up to and including {{/if}}
                Match endMatch = _ifEndPattern.Match(text);
                if (endMatch.Success)
                {
                    string afterEnd = text.Substring(endMatch.Index + endMatch.Length);

                    if (conditionResult)
                    {
                        // Keep content before the marker (IF content) + content after
                        string beforeEnd = text.Substring(0, endMatch.Index);
                        textElement.Text = beforeEnd + afterEnd;
                    }
                    else if (elseRunIndex == i)
                    {
                        // Else is in same run - extract ELSE content
                        Match elseMatch = _elsePattern.Match(text);
                        if (elseMatch.Success)
                        {
                            string elseContent = text.Substring(
                                elseMatch.Index + elseMatch.Length,
                                endMatch.Index - (elseMatch.Index + elseMatch.Length));
                            textElement.Text = elseContent + afterEnd;
                        }
                        else
                        {
                            textElement.Text = afterEnd;
                        }
                    }
                    else
                    {
                        // Just keep content after {{/if}}
                        textElement.Text = afterEnd;
                    }
                }
            }
            else if (elseRunIndex != -1 && i == elseRunIndex)
            {
                // Else run
                Match elseMatch = _elsePattern.Match(text);
                if (elseMatch.Success)
                {
                    if (conditionResult)
                    {
                        // Remove from {{else}} onwards
                        textElement.Text = text.Substring(0, elseMatch.Index);
                    }
                    else
                    {
                        // Keep content after {{else}}
                        textElement.Text = text.Substring(elseMatch.Index + elseMatch.Length);
                    }
                }
            }
            else
            {
                // Middle run - part of IF or ELSE content
                bool isInIfContent = elseRunIndex == -1 || i < elseRunIndex;
                bool isInElseContent = elseRunIndex != -1 && i > elseRunIndex;

                if (conditionResult && isInIfContent)
                {
                    // Keep IF content
                    continue;
                }
                else if (!conditionResult && isInElseContent)
                {
                    // Keep ELSE content
                    continue;
                }
                else
                {
                    // Remove this content
                    textElement.Text = "";
                }
            }
        }

        // Clean up empty runs
        foreach (Run run in runs)
        {
            Text? textElement = run.GetFirstChild<Text>();
            if (textElement != null && string.IsNullOrEmpty(textElement.Text))
            {
                // Check if run has other content (like breaks)
                if (!run.ChildElements.Any(c => !(c is Text) && !(c is RunProperties)))
                {
                    TemplateElementHelper.SafeRemove(run);
                }
            }
        }
    }

    /// <summary>
    /// Processes a conditional that's entirely within a single run.
    /// </summary>
    private string ProcessSingleRunConditional(
        string text,
        int ifStartPosition,
        int elsePosition,
        int ifEndPosition,
        bool conditionResult)
    {
        Match ifMatch = _ifStartPattern.Match(text);
        Match endMatch = _ifEndPattern.Match(text);

        if (!ifMatch.Success || !endMatch.Success)
        {
            return text;
        }

        string beforeIf = text.Substring(0, ifMatch.Index);
        string afterEnd = text.Substring(endMatch.Index + endMatch.Length);

        if (elsePosition >= 0)
        {
            Match elseMatch = _elsePattern.Match(text);
            if (elseMatch.Success)
            {
                string ifContent = text.Substring(
                    ifMatch.Index + ifMatch.Length,
                    elseMatch.Index - (ifMatch.Index + ifMatch.Length));
                string elseContent = text.Substring(
                    elseMatch.Index + elseMatch.Length,
                    endMatch.Index - (elseMatch.Index + elseMatch.Length));

                if (conditionResult)
                {
                    return beforeIf + ifContent + afterEnd;
                }
                else
                {
                    return beforeIf + elseContent + afterEnd;
                }
            }
        }

        // No else branch
        string content = text.Substring(
            ifMatch.Index + ifMatch.Length,
            endMatch.Index - (ifMatch.Index + ifMatch.Length));

        if (conditionResult)
        {
            return beforeIf + content + afterEnd;
        }
        else
        {
            return beforeIf + afterEnd;
        }
    }
}
