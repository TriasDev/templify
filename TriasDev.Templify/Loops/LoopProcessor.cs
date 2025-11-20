// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;
using System.Collections;
using TriasDev.Templify.Conditionals;
using TriasDev.Templify.Core;
using TriasDev.Templify.Placeholders;
using TriasDev.Templify.Utilities;

namespace TriasDev.Templify.Loops;

/// <summary>
/// Processes loop blocks by cloning content for each item in the collection.
/// </summary>
internal sealed class LoopProcessor
{
    private readonly ValueResolver _valueResolver;
    private readonly PlaceholderReplacementOptions _options;
    private readonly PlaceholderFinder _finder;
    private readonly IEvaluationContext _globalContext;

    public LoopProcessor(PlaceholderReplacementOptions options, IEvaluationContext globalContext)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _globalContext = globalContext ?? throw new ArgumentNullException(nameof(globalContext));
        _valueResolver = new ValueResolver();
        _finder = new PlaceholderFinder();
    }

    /// <summary>
    /// Processes all loop blocks in the document.
    /// Returns the total number of replacements made.
    /// </summary>
    public int ProcessLoops(
        IReadOnlyList<LoopBlock> loops,
        Dictionary<string, object> data,
        List<string> missingVariables)
    {
        int totalReplacements = 0;

        foreach (LoopBlock loop in loops)
        {
            totalReplacements += ProcessLoop(loop, data, missingVariables);
        }

        return totalReplacements;
    }

    /// <summary>
    /// Processes a single loop block.
    /// </summary>
    private int ProcessLoop(
        LoopBlock loop,
        Dictionary<string, object> data,
        List<string> missingVariables)
    {
        // Resolve the collection
        if (!_valueResolver.TryResolveValue(data, loop.CollectionName, out object? collectionObj))
        {
            HandleMissingCollection(loop.CollectionName, missingVariables);
            RemoveLoopBlock(loop);
            return 0;
        }

        if (collectionObj is not IEnumerable collection)
        {
            throw new InvalidOperationException(
                $"Variable '{loop.CollectionName}' is not a collection. Cannot iterate.");
        }

        // Create loop contexts
        IReadOnlyList<LoopContext> contexts = LoopContext.CreateContexts(
            collection,
            loop.CollectionName,
            parent: null);

        // Handle empty collection
        if (contexts.Count == 0)
        {
            RemoveLoopBlock(loop);
            return 0;
        }

        // Process each iteration
        int totalReplacements = 0;
        OpenXmlElement? insertionPoint = loop.EndMarker;

        for (int i = contexts.Count - 1; i >= 0; i--)
        {
            LoopContext context = contexts[i];

            // Clone content elements
            List<OpenXmlElement> clonedElements = new List<OpenXmlElement>();
            foreach (OpenXmlElement contentElement in loop.ContentElements)
            {
                OpenXmlElement clonedElement = (OpenXmlElement)contentElement.CloneNode(true);
                clonedElements.Add(clonedElement);
            }

            // Process nested loops in the cloned content (within this loop's context)
            totalReplacements += ProcessNestedLoops(clonedElements, context, data, missingVariables);

            // Process conditionals in the cloned content with loop context (enables conditionals in loops)
            ProcessConditionalsInLoopIteration(clonedElements, context);

            // Process placeholders in the cloned elements
            foreach (OpenXmlElement clonedElement in clonedElements)
            {
                totalReplacements += ProcessPlaceholdersInElement(
                    clonedElement,
                    context,
                    data,
                    missingVariables);

                // Insert after the end marker
                insertionPoint.InsertAfterSelf(clonedElement);
            }
        }

        // Remove the loop block (markers and original content)
        RemoveLoopBlock(loop);

        return totalReplacements;
    }

    /// <summary>
    /// Processes placeholders in an element using the loop context.
    /// </summary>
    private int ProcessPlaceholdersInElement(
        OpenXmlElement element,
        LoopContext context,
        Dictionary<string, object> data,
        List<string> missingVariables)
    {
        int replacementCount = 0;

        if (element is Paragraph paragraph)
        {
            replacementCount += ProcessParagraph(paragraph, context, data, missingVariables);
        }
        else if (element is TableRow tableRow)
        {
            replacementCount += ProcessTableRow(tableRow, context, data, missingVariables);
        }
        else if (element is Table table)
        {
            replacementCount += ProcessTable(table, context, data, missingVariables);
        }

        return replacementCount;
    }

    /// <summary>
    /// Processes placeholders in a paragraph.
    /// </summary>
    private int ProcessParagraph(
        Paragraph paragraph,
        LoopContext context,
        Dictionary<string, object> data,
        List<string> missingVariables)
    {
        int replacementCount = 0;
        string originalText = paragraph.InnerText;
        IReadOnlyList<PlaceholderMatch> matches = _finder.FindPlaceholdersAsList(originalText);

        if (matches.Count == 0)
        {
            return 0;
        }

        // Build replacement text
        string replacedText = originalText;
        int offset = 0;

        foreach (PlaceholderMatch match in matches)
        {
            object? value = null;
            bool resolved = false;

            // Try to resolve from loop context first
            if (context.TryResolveVariable(match.VariableName, out value))
            {
                resolved = true;
            }
            // Try to resolve from root data
            else if (_valueResolver.TryResolveValue(data, match.VariableName, out value))
            {
                resolved = true;
            }

            if (resolved)
            {
                string replacementValue = ValueConverter.ConvertToString(value, _options.Culture);
                int adjustedIndex = match.StartIndex + offset;
                replacedText = replacedText.Remove(adjustedIndex, match.Length)
                                         .Insert(adjustedIndex, replacementValue);
                offset += replacementValue.Length - match.Length;
                replacementCount++;
            }
            else
            {
                HandleMissingVariable(match.VariableName, missingVariables);
            }
        }

        // Update paragraph text if changes were made
        if (replacementCount > 0)
        {
            UpdateParagraphText(paragraph, replacedText);
        }

        return replacementCount;
    }

    /// <summary>
    /// Processes placeholders in a table.
    /// </summary>
    private int ProcessTable(
        Table table,
        LoopContext context,
        Dictionary<string, object> data,
        List<string> missingVariables)
    {
        int replacementCount = 0;

        foreach (TableRow row in table.Elements<TableRow>())
        {
            replacementCount += ProcessTableRow(row, context, data, missingVariables);
        }

        return replacementCount;
    }

    /// <summary>
    /// Processes placeholders in a table row.
    /// </summary>
    private int ProcessTableRow(
        TableRow tableRow,
        LoopContext context,
        Dictionary<string, object> data,
        List<string> missingVariables)
    {
        int replacementCount = 0;

        foreach (TableCell cell in tableRow.Elements<TableCell>())
        {
            foreach (Paragraph paragraph in cell.Elements<Paragraph>())
            {
                replacementCount += ProcessParagraph(paragraph, context, data, missingVariables);
            }
        }

        return replacementCount;
    }

    /// <summary>
    /// Updates the text content of a paragraph.
    /// Preserves formatting (RunProperties) from the original runs.
    /// </summary>
    private void UpdateParagraphText(Paragraph paragraph, string newText)
    {
        // Extract and clone formatting from the original runs before removing them
        List<Run> runs = paragraph.Elements<Run>().ToList();
        RunProperties? clonedProperties = FormattingPreserver.ExtractAndCloneRunProperties(runs);

        // Remove all runs
        paragraph.RemoveAllChildren<Run>();

        // Create a new run with the updated text
        Run newRun = new Run();
        Text text = new Text(newText);
        text.Space = SpaceProcessingModeValues.Preserve;
        newRun.Append(text);

        // Apply the preserved formatting to the new run
        FormattingPreserver.ApplyRunProperties(newRun, clonedProperties);

        paragraph.Append(newRun);
    }

    /// <summary>
    /// Removes a loop block from the document (markers and content).
    /// </summary>
    private void RemoveLoopBlock(LoopBlock loop)
    {
        // Remove start marker
        loop.StartMarker.Remove();

        // Remove content elements
        foreach (OpenXmlElement element in loop.ContentElements)
        {
            element.Remove();
        }

        // Remove end marker
        loop.EndMarker.Remove();
    }

    private void HandleMissingVariable(string variableName, List<string> missingVariables)
    {
        if (!missingVariables.Contains(variableName))
        {
            missingVariables.Add(variableName);
        }

        if (_options.MissingVariableBehavior == MissingVariableBehavior.ThrowException)
        {
            throw new InvalidOperationException($"Variable '{variableName}' not found in data.");
        }
    }

    private void HandleMissingCollection(string collectionName, List<string> missingVariables)
    {
        if (!missingVariables.Contains(collectionName))
        {
            missingVariables.Add(collectionName);
        }

        if (_options.MissingVariableBehavior == MissingVariableBehavior.ThrowException)
        {
            throw new InvalidOperationException($"Collection '{collectionName}' not found in data.");
        }
    }

    /// <summary>
    /// Processes nested loops within cloned elements using the current loop context.
    /// This enables nested loop support by processing inner loops with the outer loop's current item.
    /// </summary>
    private int ProcessNestedLoops(
        List<OpenXmlElement> elements,
        LoopContext outerContext,
        Dictionary<string, object> rootData,
        List<string> missingVariables)
    {
        int totalReplacements = 0;

        // Detect nested loops in the cloned elements
        List<LoopBlock> nestedLoops = new List<LoopBlock>();
        int i = 0;

        while (i < elements.Count)
        {
            string? text = GetElementText(elements[i]);
            if (text != null && text.Contains("{{#foreach"))
            {
                // Try to find a loop starting at this element
                LoopBlock? nestedLoop = TryDetectLoop(elements, i);
                if (nestedLoop != null)
                {
                    nestedLoops.Add(nestedLoop);
                    i = elements.IndexOf(nestedLoop.EndMarker) + 1;
                    continue;
                }
            }
            i++;
        }

        // Process each nested loop
        foreach (LoopBlock nestedLoop in nestedLoops)
        {
            totalReplacements += ProcessNestedLoop(nestedLoop, outerContext, rootData, missingVariables, elements);
        }

        return totalReplacements;
    }

    /// <summary>
    /// Tries to detect a loop starting at the specified index in the elements list.
    /// </summary>
    private LoopBlock? TryDetectLoop(List<OpenXmlElement> elements, int startIndex)
    {
        string? startText = GetElementText(elements[startIndex]);
        if (startText == null)
        {
            return null;
        }

        // Check for {{#foreach CollectionName}}
        System.Text.RegularExpressions.Match match = System.Text.RegularExpressions.Regex.Match(
            startText,
            @"\{\{#foreach\s+(\w+)\}\}",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        if (!match.Success)
        {
            return null;
        }

        string collectionName = match.Groups[1].Value;

        // Find matching end marker
        int endIndex = FindMatchingEndInElements(elements, startIndex);
        if (endIndex == -1)
        {
            return null;
        }

        // Get content elements
        List<OpenXmlElement> contentElements = new List<OpenXmlElement>();
        for (int j = startIndex + 1; j < endIndex; j++)
        {
            contentElements.Add(elements[j]);
        }

        return new LoopBlock(
            collectionName,
            contentElements,
            elements[startIndex],
            elements[endIndex],
            isTableRowLoop: false,
            emptyBlock: null);
    }

    /// <summary>
    /// Finds the matching {{/foreach}} for a {{#foreach}} at the given index.
    /// </summary>
    private int FindMatchingEndInElements(List<OpenXmlElement> elements, int startIndex)
    {
        int depth = 1;

        for (int i = startIndex + 1; i < elements.Count; i++)
        {
            string? text = GetElementText(elements[i]);
            if (text == null)
            {
                continue;
            }

            if (System.Text.RegularExpressions.Regex.IsMatch(text, @"\{\{#foreach\s+\w+\}\}", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            {
                depth++;
            }
            else if (System.Text.RegularExpressions.Regex.IsMatch(text, @"\{\{/foreach\}\}", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            {
                depth--;
                if (depth == 0)
                {
                    return i;
                }
            }
        }

        return -1;
    }

    /// <summary>
    /// Processes a nested loop within the context of an outer loop.
    /// </summary>
    private int ProcessNestedLoop(
        LoopBlock loop,
        LoopContext outerContext,
        Dictionary<string, object> rootData,
        List<string> missingVariables,
        List<OpenXmlElement> parentElements)
    {
        // Try to resolve the collection from the outer loop context first
        object? collectionObj = null;
        bool resolved = false;

        if (outerContext.TryResolveVariable(loop.CollectionName, out collectionObj))
        {
            resolved = true;
        }
        else if (_valueResolver.TryResolveValue(rootData, loop.CollectionName, out collectionObj))
        {
            resolved = true;
        }

        if (!resolved || collectionObj is not IEnumerable collection)
        {
            HandleMissingCollection(loop.CollectionName, missingVariables);
            RemoveLoopBlockFromList(loop, parentElements);
            return 0;
        }

        // Create loop contexts for the nested loop
        IReadOnlyList<LoopContext> contexts = LoopContext.CreateContexts(
            collection,
            loop.CollectionName,
            parent: outerContext);

        if (contexts.Count == 0)
        {
            RemoveLoopBlockFromList(loop, parentElements);
            return 0;
        }

        // Process each iteration
        int totalReplacements = 0;
        int insertIndex = parentElements.IndexOf(loop.EndMarker);
        List<OpenXmlElement> allClonedElements = new List<OpenXmlElement>();

        for (int i = contexts.Count - 1; i >= 0; i--)
        {
            LoopContext context = contexts[i];

            // Clone content elements
            List<OpenXmlElement> clonedElements = new List<OpenXmlElement>();
            foreach (OpenXmlElement contentElement in loop.ContentElements)
            {
                OpenXmlElement clonedElement = (OpenXmlElement)contentElement.CloneNode(true);
                clonedElements.Add(clonedElement);
            }

            // Recursively process nested loops within this iteration
            totalReplacements += ProcessNestedLoops(clonedElements, context, rootData, missingVariables);

            // Process conditionals in the cloned content with loop context (enables conditionals in nested loops)
            ProcessConditionalsInLoopIteration(clonedElements, context);

            // Process placeholders in the cloned elements
            foreach (OpenXmlElement clonedElement in clonedElements)
            {
                totalReplacements += ProcessPlaceholdersInElement(
                    clonedElement,
                    context,
                    rootData,
                    missingVariables);

                allClonedElements.Add(clonedElement);
            }
        }

        // Insert all cloned elements at the appropriate position
        parentElements.InsertRange(insertIndex, allClonedElements);

        // Remove the loop block from the parent elements list
        RemoveLoopBlockFromList(loop, parentElements);

        return totalReplacements;
    }

    /// <summary>
    /// Removes a loop block from a list of elements.
    /// </summary>
    private void RemoveLoopBlockFromList(LoopBlock loop, List<OpenXmlElement> elements)
    {
        elements.Remove(loop.StartMarker);
        foreach (OpenXmlElement element in loop.ContentElements)
        {
            elements.Remove(element);
        }
        elements.Remove(loop.EndMarker);
    }

    /// <summary>
    /// Gets the text content of an element.
    /// </summary>
    private string? GetElementText(OpenXmlElement element)
    {
        if (element is Paragraph paragraph)
        {
            return paragraph.InnerText;
        }

        if (element is TableRow row)
        {
            return row.InnerText;
        }

        if (element is Table table)
        {
            return table.InnerText;
        }

        return null;
    }

    /// <summary>
    /// Processes conditionals in cloned loop content with loop context.
    /// This enables conditionals inside loops to access loop-scoped variables and metadata.
    /// </summary>
    /// <remarks>
    /// Since clonedElements are not yet inserted into the document, we can't use Remove().
    /// Instead, we process conditionals by modifying the clonedElements list directly.
    /// </remarks>
    private void ProcessConditionalsInLoopIteration(
        List<OpenXmlElement> clonedElements,
        LoopContext loopContext)
    {
        // Create loop evaluation context that chains to global context
        LoopEvaluationContext loopEvalContext = new LoopEvaluationContext(loopContext, _globalContext);

        // Detect conditionals in the cloned elements
        IReadOnlyList<ConditionalBlock> conditionals = ConditionalDetector.DetectConditionalsInElements(clonedElements);

        if (conditionals.Count == 0)
        {
            return;
        }

        // Create conditional evaluator
        ConditionalEvaluator evaluator = new ConditionalEvaluator();

        // Process conditionals from deepest to shallowest (same as top-level processing)
        List<ConditionalBlock> sortedConditionals = conditionals
            .OrderByDescending(c => c.NestingLevel)
            .ToList();

        // Collect elements to remove (can't modify list while iterating)
        HashSet<OpenXmlElement> elementsToRemove = new HashSet<OpenXmlElement>();

        foreach (ConditionalBlock conditional in sortedConditionals)
        {
            // Skip if markers already marked for removal by nested conditional
            if (elementsToRemove.Contains(conditional.StartMarker) ||
                elementsToRemove.Contains(conditional.EndMarker))
            {
                continue;
            }

            // Evaluate the condition with loop context
            bool conditionResult = evaluator.Evaluate(conditional.ConditionExpression, loopEvalContext);

            // Mark elements for removal based on condition result
            elementsToRemove.Add(conditional.StartMarker);  // Always remove start marker
            elementsToRemove.Add(conditional.EndMarker);    // Always remove end marker

            if (conditional.ElseMarker != null)
            {
                elementsToRemove.Add(conditional.ElseMarker);  // Always remove else marker
            }

            if (conditionResult)
            {
                // Condition TRUE: keep IF content, remove ELSE content
                foreach (OpenXmlElement element in conditional.ElseContentElements)
                {
                    elementsToRemove.Add(element);
                }
            }
            else
            {
                // Condition FALSE: remove IF content, keep ELSE content
                foreach (OpenXmlElement element in conditional.IfContentElements)
                {
                    elementsToRemove.Add(element);
                }
            }
        }

        // Remove marked elements from clonedElements list
        clonedElements.RemoveAll(e => elementsToRemove.Contains(e));
    }
}
