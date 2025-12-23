// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Text;
using TriasDev.Templify.Conditionals;
using TriasDev.Templify.Loops;
using TriasDev.Templify.Placeholders;

namespace TriasDev.Templify.Core;

/// <summary>
/// Processes text templates with placeholder replacement, conditionals, and loops.
/// Supports the same template syntax as DocumentTemplateProcessor but works with plain text instead of Word documents.
/// </summary>
/// <remarks>
/// This processor enables email generation and other text-based templating scenarios using the familiar
/// Templify template syntax: {{variables}}, {{#if condition}}...{{/if}}, {{#foreach collection}}...{{/foreach}}.
/// </remarks>
public sealed class TextTemplateProcessor
{
    private readonly PlaceholderReplacementOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="TextTemplateProcessor"/> class.
    /// </summary>
    /// <param name="options">Configuration options for placeholder replacement. If null, default options are used.</param>
    public TextTemplateProcessor(PlaceholderReplacementOptions? options = null)
    {
        _options = options ?? new PlaceholderReplacementOptions();
    }

    /// <summary>
    /// Processes a text template, replacing placeholders with values from the data dictionary.
    /// </summary>
    /// <param name="templateText">The template text containing placeholders, conditionals, and loops.</param>
    /// <param name="data">Dictionary containing variable names and their replacement values.</param>
    /// <returns>A <see cref="TextProcessingResult"/> containing the processed text and metadata.</returns>
    /// <exception cref="ArgumentNullException">Thrown when any parameter is null.</exception>
    public TextProcessingResult ProcessTemplate(string templateText, Dictionary<string, object> data)
    {
        if (templateText == null)
        {
            throw new ArgumentNullException(nameof(templateText));
        }

        if (data == null)
        {
            throw new ArgumentNullException(nameof(data));
        }

        try
        {
            // Track missing variables
            HashSet<string> missingVariables = new HashSet<string>();
            int replacementCount = 0;

            // Create global evaluation context
            GlobalEvaluationContext globalContext = new GlobalEvaluationContext(data);

            // Process the template text
            string processedText = ProcessTextInternal(
                templateText,
                globalContext,
                missingVariables,
                ref replacementCount);

            return TextProcessingResult.Success(
                processedText,
                replacementCount,
                missingVariables.OrderBy(v => v).ToList());
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Missing variable"))
        {
            // Re-throw only when it's a missing variable with ThrowException behavior
            // This allows the caller to handle it as an intentional validation error
            throw;
        }
        catch (Exception ex)
        {
            // All other exceptions (including template syntax errors) are returned as failures
            return TextProcessingResult.Failure($"Processing failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Internal method that processes the template text with the given context.
    /// </summary>
    private string ProcessTextInternal(
        string text,
        IEvaluationContext context,
        HashSet<string> missingVariables,
        ref int replacementCount)
    {
        // Step 1: Process loops first (innermost first)
        // This ensures conditionals inside loops are processed with the correct loop context
        text = ProcessLoops(text, context, missingVariables, ref replacementCount);

        // Step 2: Process conditionals (now that loops are expanded)
        text = ProcessConditionals(text, context);

        // Step 3: Process remaining placeholders
        text = ProcessPlaceholders(text, context, missingVariables, ref replacementCount);

        return text;
    }

    /// <summary>
    /// Processes conditional blocks in the text.
    /// </summary>
    private string ProcessConditionals(string text, IEvaluationContext context)
    {
        ConditionalEvaluator evaluator = new ConditionalEvaluator();

        // Process conditionals iteratively from outermost inward
        // Always processes the first {{#if ...}} found, handling nested conditionals in subsequent iterations
        int maxIterations = 100; // Prevent infinite loops
        int iteration = 0;

        while (iteration++ < maxIterations)
        {
            int ifStart = text.IndexOf("{{#if ", StringComparison.Ordinal);
            if (ifStart == -1)
            {
                break; // No more conditionals
            }

            // Find the matching {{/if}}
            int ifEnd = FindMatchingEndTag(text, ifStart, "{{#if ", "{{/if}}");
            if (ifEnd == -1)
            {
                throw new InvalidOperationException($"Unmatched {{{{#if}}}} tag at position {ifStart}");
            }

            // Parse the condition expression
            int conditionStart = ifStart + "{{#if ".Length;
            int conditionEnd = text.IndexOf("}}", conditionStart, StringComparison.Ordinal);
            if (conditionEnd == -1)
            {
                throw new InvalidOperationException($"Malformed {{{{#if}}}} tag at position {ifStart}");
            }

            string condition = text.Substring(conditionStart, conditionEnd - conditionStart);

            // Find the {{#else}} if it exists
            int elseTagStart = -1;
            int searchStart = conditionEnd + "}}".Length;
            int depth = 1;

            for (int i = searchStart; i < ifEnd; i++)
            {
                if (text.Substring(i).StartsWith("{{#if ", StringComparison.Ordinal))
                {
                    depth++;
                    i += "{{#if ".Length - 1;
                }
                else if (text.Substring(i).StartsWith("{{/if}}", StringComparison.Ordinal))
                {
                    depth--;
                    if (depth == 0)
                    {
                        break;
                    }
                    i += "{{/if}}".Length - 1;
                }
                else if (depth == 1 && text.Substring(i).StartsWith("{{#else}}", StringComparison.Ordinal))
                {
                    elseTagStart = i;
                    break;
                }
            }

            // Evaluate the condition
            bool conditionResult = evaluator.Evaluate(condition, context);

            // Extract the content to keep
            string contentToKeep;
            if (conditionResult)
            {
                // Keep the "if" branch
                int contentStart = conditionEnd + "}}".Length;
                int contentEnd = elseTagStart != -1 ? elseTagStart : ifEnd;
                contentToKeep = text.Substring(contentStart, contentEnd - contentStart);
            }
            else
            {
                // Keep the "else" branch (if it exists)
                if (elseTagStart != -1)
                {
                    int contentStart = elseTagStart + "{{#else}}".Length;
                    contentToKeep = text.Substring(contentStart, ifEnd - contentStart);
                }
                else
                {
                    contentToKeep = string.Empty;
                }
            }

            // Replace the entire conditional block with the kept content
            StringBuilder builder = new StringBuilder();
            builder.Append(text.Substring(0, ifStart));
            builder.Append(contentToKeep);
            builder.Append(text.Substring(ifEnd + "{{/if}}".Length));
            text = builder.ToString();
        }

        if (iteration >= maxIterations)
        {
            throw new InvalidOperationException("Maximum nesting depth exceeded for conditional blocks");
        }

        return text;
    }

    /// <summary>
    /// Processes loop blocks in the text.
    /// </summary>
    private string ProcessLoops(
        string text,
        IEvaluationContext context,
        HashSet<string> missingVariables,
        ref int replacementCount)
    {
        int maxIterations = 100; // Prevent infinite loops
        int iteration = 0;

        while (iteration++ < maxIterations)
        {
            int loopStart = text.IndexOf("{{#foreach ", StringComparison.Ordinal);
            if (loopStart == -1)
            {
                break; // No more loops
            }

            // Find the matching {{/foreach}}
            int loopEnd = FindMatchingEndTag(text, loopStart, "{{#foreach ", "{{/foreach}}");
            if (loopEnd == -1)
            {
                throw new InvalidOperationException($"Unmatched {{{{#foreach}}}} tag at position {loopStart}");
            }

            // Extract the collection name
            int collectionStart = loopStart + "{{#foreach ".Length;
            int collectionEnd = text.IndexOf("}}", collectionStart, StringComparison.Ordinal);
            if (collectionEnd == -1)
            {
                throw new InvalidOperationException($"Malformed {{{{#foreach}}}} tag at position {loopStart}");
            }

            string collectionName = text.Substring(collectionStart, collectionEnd - collectionStart).Trim();

            // Extract the loop content
            int contentStart = collectionEnd + "}}".Length;
            string loopContent = text.Substring(contentStart, loopEnd - contentStart);

            // Resolve the collection
            if (!context.TryResolveVariable(collectionName, out object? collectionValue))
            {
                missingVariables.Add(collectionName);

                if (_options.MissingVariableBehavior == MissingVariableBehavior.ThrowException)
                {
                    throw new InvalidOperationException($"Collection not found: {collectionName}");
                }

                // Remove the loop block
                text = text.Substring(0, loopStart) + text.Substring(loopEnd + "{{/foreach}}".Length);
                continue;
            }

            // Check if it's enumerable
            if (collectionValue is not System.Collections.IEnumerable enumerable)
            {
                throw new InvalidOperationException($"Variable '{collectionName}' is not a collection");
            }

            // Convert to list to get count
            List<object?> items = enumerable.Cast<object?>().ToList();

            // Build the expanded content
            StringBuilder expandedContent = new StringBuilder();
            int index = 0;

            foreach (object? item in items)
            {
                // Create loop context
                LoopContext loopContext = new LoopContext(
                    currentItem: item ?? new object(),
                    index: index,
                    count: items.Count,
                    collectionName: collectionName,
                    parent: null);

                LoopEvaluationContext loopEvalContext = new LoopEvaluationContext(loopContext, context);

                // Process the loop content recursively (to handle nested loops and conditionals)
                int loopReplacementCount = 0;
                string processedContent = ProcessTextInternal(
                    loopContent,
                    loopEvalContext,
                    missingVariables,
                    ref loopReplacementCount);

                replacementCount += loopReplacementCount;
                expandedContent.Append(processedContent);

                index++;
            }

            // Replace the entire loop block with the expanded content
            StringBuilder textBuilder = new StringBuilder();
            textBuilder.Append(text.Substring(0, loopStart));
            textBuilder.Append(expandedContent);
            textBuilder.Append(text.Substring(loopEnd + "{{/foreach}}".Length));
            text = textBuilder.ToString();
        }

        if (iteration >= maxIterations)
        {
            throw new InvalidOperationException("Maximum nesting depth exceeded for loop blocks");
        }

        return text;
    }

    /// <summary>
    /// Processes placeholders in the text.
    /// </summary>
    private string ProcessPlaceholders(
        string text,
        IEvaluationContext context,
        HashSet<string> missingVariables,
        ref int replacementCount)
    {
        PlaceholderFinder finder = new PlaceholderFinder();

        // Find all placeholders
        IReadOnlyList<PlaceholderMatch> placeholders = finder.FindPlaceholdersAsList(text);

        // Process in reverse order to maintain correct positions
        foreach (PlaceholderMatch placeholder in placeholders.Reverse())
        {
            // Try to resolve the variable
            if (!context.TryResolveVariable(placeholder.VariableName, out object? value))
            {
                missingVariables.Add(placeholder.VariableName);

                switch (_options.MissingVariableBehavior)
                {
                    case MissingVariableBehavior.ReplaceWithEmpty:
                        text = text.Remove(placeholder.StartIndex, placeholder.Length);
                        replacementCount++;
                        break;

                    case MissingVariableBehavior.ThrowException:
                        throw new InvalidOperationException($"Missing variable: {placeholder.VariableName}");

                    case MissingVariableBehavior.LeaveUnchanged:
                    default:
                        // Leave as-is
                        break;
                }
            }
            else
            {
                // Convert to string
                string replacementValue = ValueConverter.ConvertToString(
                    value,
                    _options.Culture,
                    placeholder.Format,
                    _options.BooleanFormatterRegistry);

                // Replace in text
                text = text.Remove(placeholder.StartIndex, placeholder.Length)
                          .Insert(placeholder.StartIndex, replacementValue);

                replacementCount++;
            }
        }

        return text;
    }

    /// <summary>
    /// Finds the matching end tag for a start tag, accounting for nesting.
    /// </summary>
    private int FindMatchingEndTag(string text, int startPos, string startTag, string endTag)
    {
        int depth = 1;
        int searchPos = startPos + startTag.Length;

        while (searchPos < text.Length && depth > 0)
        {
            int nextStart = text.IndexOf(startTag, searchPos, StringComparison.Ordinal);
            int nextEnd = text.IndexOf(endTag, searchPos, StringComparison.Ordinal);

            if (nextEnd == -1)
            {
                return -1; // No matching end tag
            }

            if (nextStart != -1 && nextStart < nextEnd)
            {
                // Found a nested start tag
                depth++;
                searchPos = nextStart + startTag.Length;
            }
            else
            {
                // Found an end tag
                depth--;
                if (depth == 0)
                {
                    return nextEnd;
                }
                searchPos = nextEnd + endTag.Length;
            }
        }

        return -1; // No matching end tag
    }
}
