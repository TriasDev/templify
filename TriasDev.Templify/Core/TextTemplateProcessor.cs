// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Collections;
using System.Text;
using System.Text.RegularExpressions;
using TriasDev.Templify.Conditionals;
using TriasDev.Templify.Loops;
using TriasDev.Templify.Placeholders;
using TriasDev.Templify.Replacements;

namespace TriasDev.Templify.Core;

/// <summary>
/// Processes text templates with placeholder replacement, conditionals, and loops.
/// Supports the same template syntax as DocumentTemplateProcessor but works with plain text instead of Word documents.
/// </summary>
/// <remarks>
/// This processor enables email generation and other text-based templating scenarios using the familiar
/// Templify template syntax: {{variables}}, {{(expressions)}}, {{#if condition}}...{{#elseif condition}}...{{#else}}...{{/if}},
/// {{#foreach collection}}...{{/foreach}} and {{#foreach item in collection}}...{{/foreach}}.
/// </remarks>
public sealed class TextTemplateProcessor
{
    private const RegexOptions MarkerOptions =
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline;

    // Markers are matched at a known "{{" position (\G), using the same patterns as the Word processor.
    // Singleline lets a condition span lines in plain text.
    private static readonly Regex _ifStart = new(@"\G" + ConditionalPatterns.IfStartPattern, MarkerOptions);
    private static readonly Regex _elseIf = new(@"\G" + ConditionalPatterns.ElseIfPattern, MarkerOptions);
    private static readonly Regex _else = new(@"\G" + ConditionalPatterns.ElsePattern, MarkerOptions);
    private static readonly Regex _ifEnd = new(@"\G" + ConditionalPatterns.IfEndPattern, MarkerOptions);
    private static readonly Regex _foreachEnd = new(@"\G" + LoopDetector.ForeachEndPattern, MarkerOptions);

    // Same iteration-variable grammar as the Word processor; the collection may be any property path
    // (e.g. Customer.Orders or Groups[0].Items), as text templates have always accepted.
    private static readonly Regex _foreachStart = new(
        @"\G\{\{#foreach\s+" + LoopDetector.IterationVariablePrefixPattern + @"([^\s{}]+)\s*\}\}",
        MarkerOptions);

    // A block marker keyword that did not match its full pattern (e.g. "{{#if X" without "}}").
    // A well-formed {{#elseif}} outside a block is not malformed; it is kept as literal text.
    private static readonly Regex _malformedMarker = new(
        @"\G\{\{(#if|#elseif|#foreach)\s",
        MarkerOptions);

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
    /// <returns>
    /// A <see cref="TextProcessingResult"/> containing the processed text and metadata. Template syntax errors
    /// (e.g. unmatched markers) and data errors (e.g. a loop over a non-collection) are reported as a failed result.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when any parameter is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown only when a variable is missing and <see cref="MissingVariableBehavior.ThrowException"/> is configured.
    /// </exception>
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
            List<Node> nodes = Parse(templateText);

            RenderState state = new RenderState(templateText);
            StringBuilder output = new StringBuilder(templateText.Length);
            Render(nodes, new GlobalEvaluationContext(data), parentLoop: null, output, state);

            return TextProcessingResult.Success(
                output.ToString(),
                state.ReplacementCount,
                state.MissingVariables.OrderBy(v => v).ToList(),
                state.Warnings.GetWarnings());
        }
        catch (MissingVariableException ex)
        {
            // Re-throw only missing variables with ThrowException behavior (same as DocumentTemplateProcessor),
            // as a plain InvalidOperationException with the same message as before.
            throw ex.ToPublicException();
        }
        catch (Exception ex)
        {
            // All other exceptions (including template syntax errors) are returned as failures
            return TextProcessingResult.Failure($"Processing failed: {ex.Message}");
        }
    }

    #region Parsing

    private abstract class Node
    {
    }

    /// <summary>A range of literal template text (may contain placeholders).</summary>
    private sealed class TextNode : Node
    {
        public TextNode(int start, int end)
        {
            Start = start;
            End = end;
        }

        public int Start { get; }

        public int End { get; }
    }

    /// <summary>An if/elseif/else block. A branch with a null condition is the else branch.</summary>
    private sealed class ConditionalNode : Node
    {
        public List<(string? Condition, List<Node> Children)> Branches { get; } = new();
    }

    private sealed class LoopNode : Node
    {
        public LoopNode(string collectionName, string? iterationVariableName)
        {
            CollectionName = collectionName;
            IterationVariableName = iterationVariableName;
        }

        public string CollectionName { get; }

        public string? IterationVariableName { get; }

        public List<Node> Children { get; } = new();
    }

    /// <summary>An open block while parsing.</summary>
    private sealed class Frame
    {
        public Frame(Node node, List<Node> children, int position)
        {
            Node = node;
            Children = children;
            Position = position;
        }

        public Node Node { get; }

        /// <summary>The list that receives nodes (the current branch for a conditional).</summary>
        public List<Node> Children { get; set; }

        /// <summary>Position of the start marker in the template text.</summary>
        public int Position { get; }

        public bool HasElse { get; set; }
    }

    /// <summary>
    /// Parses the template into a tree of text, conditional and loop nodes in a single left-to-right scan.
    /// Unmatched or misnested block markers are template syntax errors. Stray closing or branch markers
    /// outside any block are kept as literal text.
    /// </summary>
    private static List<Node> Parse(string text)
    {
        List<Node> root = new List<Node>();
        Stack<Frame> stack = new Stack<Frame>();
        List<Node> Current() => stack.Count > 0 ? stack.Peek().Children : root;

        int textStart = 0;
        int pos = text.IndexOf("{{", StringComparison.Ordinal);

        while (pos >= 0)
        {
            int markerEnd = -1;
            Match match;

            if ((match = _ifStart.Match(text, pos)).Success)
            {
                FlushText(Current(), textStart, pos);
                ConditionalNode node = new ConditionalNode();
                List<Node> branch = new List<Node>();
                node.Branches.Add((match.Groups[1].Value.Trim(), branch));
                stack.Push(new Frame(node, branch, pos));
                markerEnd = pos + match.Length;
            }
            else if ((match = _elseIf.Match(text, pos)).Success && stack.Count > 0 && stack.Peek().Node is ConditionalNode elseIfTarget)
            {
                Frame frame = stack.Peek();
                if (frame.HasElse)
                {
                    throw new TemplateSyntaxException(
                        ValidationErrorType.InvalidConditionalExpression,
                        "Invalid conditional structure: '{{#elseif}}' cannot appear after '{{#else}}'. " +
                        "The '{{#else}}' branch must be the last branch before '{{/if}}'.");
                }

                FlushText(frame.Children, textStart, pos);
                List<Node> branch = new List<Node>();
                elseIfTarget.Branches.Add((match.Groups[1].Value.Trim(), branch));
                frame.Children = branch;
                markerEnd = pos + match.Length;
            }
            else if ((match = _else.Match(text, pos)).Success && stack.Count > 0 && stack.Peek().Node is ConditionalNode elseTarget)
            {
                Frame frame = stack.Peek();
                FlushText(frame.Children, textStart, pos);
                if (frame.HasElse)
                {
                    // A second {{#else}} ends the else branch's content, as in the Word processor, where
                    // only the first {{#else}} of a block counts: keep the marker as literal text.
                    markerEnd = -1;
                    textStart = pos;
                }
                else
                {
                    List<Node> branch = new List<Node>();
                    elseTarget.Branches.Add((null, branch));
                    frame.Children = branch;
                    frame.HasElse = true;
                    markerEnd = pos + match.Length;
                }
            }
            else if ((match = _ifEnd.Match(text, pos)).Success && stack.Count > 0)
            {
                Frame frame = stack.Peek();
                if (frame.Node is not ConditionalNode)
                {
                    throw Unmatched(frame);
                }

                FlushText(frame.Children, textStart, pos);
                stack.Pop();
                Current().Add(frame.Node);
                markerEnd = pos + match.Length;
            }
            else if ((match = _foreachStart.Match(text, pos)).Success)
            {
                string collectionName = match.Groups[2].Value;
                string? iterationVariableName = match.Groups[1].Success ? match.Groups[1].Value : null;
                if (iterationVariableName != null)
                {
                    LoopDetector.ValidateIterationVariableName(iterationVariableName, collectionName);
                }

                FlushText(Current(), textStart, pos);
                LoopNode node = new LoopNode(collectionName, iterationVariableName);
                stack.Push(new Frame(node, node.Children, pos));
                markerEnd = pos + match.Length;
            }
            else if ((match = _foreachEnd.Match(text, pos)).Success && stack.Count > 0)
            {
                Frame frame = stack.Peek();
                if (frame.Node is not LoopNode)
                {
                    throw Unmatched(frame);
                }

                FlushText(frame.Children, textStart, pos);
                stack.Pop();
                Current().Add(frame.Node);
                markerEnd = pos + match.Length;
            }
            else if ((match = _malformedMarker.Match(text, pos)).Success && !_elseIf.Match(text, pos).Success)
            {
                string keyword = match.Groups[1].Value.ToLowerInvariant();
                throw new TemplateSyntaxException(
                    keyword == "#foreach" ? ValidationErrorType.InvalidPlaceholderSyntax : ValidationErrorType.InvalidConditionalExpression,
                    $"Malformed {{{{{keyword}}}}} tag at position {pos}");
            }

            if (markerEnd >= 0)
            {
                textStart = markerEnd;
                pos = text.IndexOf("{{", markerEnd, StringComparison.Ordinal);
            }
            else
            {
                // Not a block marker (a placeholder or literal text): continue after this "{{".
                pos = text.IndexOf("{{", pos + 2, StringComparison.Ordinal);
            }
        }

        if (stack.Count > 0)
        {
            // Report the outermost unclosed block.
            throw Unmatched(stack.Last());
        }

        FlushText(root, textStart, text.Length);
        return root;
    }

    private static TemplateSyntaxException Unmatched(Frame frame)
    {
        return frame.Node is LoopNode
            ? new TemplateSyntaxException(ValidationErrorType.UnmatchedLoopStart, $"Unmatched {{{{#foreach}}}} tag at position {frame.Position}")
            : new TemplateSyntaxException(ValidationErrorType.UnmatchedConditionalStart, $"Unmatched {{{{#if}}}} tag at position {frame.Position}");
    }

    private static void FlushText(List<Node> target, int start, int end)
    {
        if (end > start)
        {
            target.Add(new TextNode(start, end));
        }
    }

    #endregion

    #region Rendering

    private sealed class RenderState
    {
        public RenderState(string template)
        {
            Template = template;
        }

        public string Template { get; }

        public HashSet<string> MissingVariables { get; } = new HashSet<string>();

        public WarningCollector Warnings { get; } = new WarningCollector();

        public ConditionalEvaluator Evaluator { get; } = new ConditionalEvaluator();

        public PlaceholderFinder Finder { get; } = new PlaceholderFinder();

        public int ReplacementCount { get; set; }
    }

    /// <summary>
    /// Renders nodes in order. A conditional is evaluated before anything inside it (so loops and placeholders
    /// in a branch that is not taken are never evaluated), and content inside a loop is evaluated once per item
    /// with the loop's context: the same order as the Word processor.
    /// </summary>
    private void Render(List<Node> nodes, IEvaluationContext context, LoopContext? parentLoop, StringBuilder output, RenderState state)
    {
        foreach (Node node in nodes)
        {
            switch (node)
            {
                case TextNode textNode:
                    RenderText(textNode, context, output, state);
                    break;

                case ConditionalNode conditional:
                    foreach ((string? condition, List<Node> children) in conditional.Branches)
                    {
                        if (condition == null || state.Evaluator.Evaluate(condition, context, state.Warnings))
                        {
                            Render(children, context, parentLoop, output, state);
                            break;
                        }
                    }

                    break;

                case LoopNode loop:
                    RenderLoop(loop, context, parentLoop, output, state);
                    break;
            }
        }
    }

    private void RenderLoop(LoopNode loop, IEvaluationContext context, LoopContext? parentLoop, StringBuilder output, RenderState state)
    {
        if (!context.TryResolveVariable(loop.CollectionName, out object? collectionValue))
        {
            state.MissingVariables.Add(loop.CollectionName);
            state.Warnings.AddWarning(ProcessingWarning.MissingLoopCollection(loop.CollectionName));

            if (_options.MissingVariableBehavior == MissingVariableBehavior.ThrowException)
            {
                throw new TemplateDataException($"Collection not found: {loop.CollectionName}");
            }

            return;
        }

        if (collectionValue == null)
        {
            // Same as the Word processor: a null collection renders nothing and is reported as a warning.
            state.Warnings.AddWarning(ProcessingWarning.NullLoopCollection(loop.CollectionName));
            return;
        }

        // A string is IEnumerable<char>, but iterating its characters is never intended.
        if (collectionValue is string || collectionValue is not IEnumerable collection)
        {
            throw new TemplateDataException($"Variable '{loop.CollectionName}' is not a collection");
        }

        IReadOnlyList<LoopContext> contexts = LoopContext.CreateContexts(
            collection,
            loop.CollectionName,
            loop.IterationVariableName,
            parentLoop);

        foreach (LoopContext loopContext in contexts)
        {
            Render(loop.Children, new LoopEvaluationContext(loopContext, context), loopContext, output, state);
        }
    }

    private void RenderText(TextNode node, IEvaluationContext context, StringBuilder output, RenderState state)
    {
        string text = state.Template.Substring(node.Start, node.End - node.Start);
        int written = 0;

        foreach (PlaceholderMatch placeholder in state.Finder.FindPlaceholders(text))
        {
            output.Append(text, written, placeholder.StartIndex - written);
            written = placeholder.StartIndex + placeholder.Length;

            object? value;
            bool resolved = placeholder.IsExpression
                ? ExpressionPlaceholderEvaluator.TryEvaluate(placeholder.VariableName, context, state.Warnings, out value)
                : context.TryResolveVariable(placeholder.VariableName, out value);

            if (!resolved)
            {
                state.MissingVariables.Add(placeholder.VariableName);
                state.Warnings.AddWarning(ProcessingWarning.MissingVariable(placeholder.VariableName));

                switch (_options.MissingVariableBehavior)
                {
                    case MissingVariableBehavior.ReplaceWithEmpty:
                        state.ReplacementCount++;
                        break;

                    case MissingVariableBehavior.ThrowException:
                        throw new MissingVariableException(placeholder.VariableName, $"Missing variable: {placeholder.VariableName}");

                    case MissingVariableBehavior.LeaveUnchanged:
                    default:
                        output.Append(placeholder.FullMatch);
                        break;
                }

                continue;
            }

            // Text output has no markdown, so :raw only means "no format".
            string? format = string.Equals(placeholder.Format, "raw", StringComparison.OrdinalIgnoreCase)
                ? null
                : placeholder.Format;

            string replacementValue = ValueConverter.ConvertToString(
                value,
                _options.Culture,
                format,
                _options.BooleanFormatterRegistry);

            output.Append(TextReplacements.Apply(replacementValue, _options.TextReplacements));
            state.ReplacementCount++;
        }

        output.Append(text, written, text.Length - written);
    }

    #endregion
}
