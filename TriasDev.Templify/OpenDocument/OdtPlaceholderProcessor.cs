// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Xml.Linq;
using TriasDev.Templify.Core;
using TriasDev.Templify.Placeholders;
using TriasDev.Templify.Replacements;
using TriasDev.Templify.Utilities;

namespace TriasDev.Templify.OpenDocument;

/// <summary>
/// Resolves placeholders (<c>{{VariableName}}</c>, <c>{{Value:format}}</c>, <c>{{(expression)}}</c>)
/// and replaces them in OpenDocument paragraphs. The OpenDocument counterpart of
/// <c>PlaceholderVisitor</c>, with the same resolution, formatting and missing-variable handling.
/// </summary>
internal sealed class OdtPlaceholderProcessor
{
    private readonly PlaceholderReplacementOptions _options;
    private readonly HashSet<string> _missingVariables;
    private readonly IWarningCollector _warningCollector;

    public OdtPlaceholderProcessor(
        PlaceholderReplacementOptions options,
        HashSet<string> missingVariables,
        IWarningCollector warningCollector)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _missingVariables = missingVariables ?? throw new ArgumentNullException(nameof(missingVariables));
        _warningCollector = warningCollector ?? throw new ArgumentNullException(nameof(warningCollector));
    }

    /// <summary>Gets the number of placeholders replaced so far.</summary>
    public int ReplacementCount { get; private set; }

    /// <summary>
    /// Replaces all placeholders in the paragraph's own text, right to left so earlier offsets stay valid.
    /// </summary>
    public void ProcessParagraph(XElement paragraph, IEvaluationContext context)
    {
        string text = OdtParagraphTextModel.GetText(paragraph);
        IReadOnlyList<PlaceholderToken> placeholders = PlaceholderScanner.FindPlaceholdersAsList(text);

        foreach (PlaceholderToken placeholder in placeholders.OrderByDescending(p => p.StartIndex))
        {
            ProcessPlaceholder(placeholder, paragraph, context);
        }
    }

    private void ProcessPlaceholder(PlaceholderToken placeholder, XElement paragraph, IEvaluationContext context)
    {
        object? value;
        bool resolved = placeholder.IsExpression
            ? ExpressionPlaceholderEvaluator.TryEvaluate(placeholder.VariableName, context, _warningCollector, out value)
            : context.TryResolveVariable(placeholder.VariableName, out value);

        if (!resolved)
        {
            _missingVariables.Add(placeholder.VariableName);
            _warningCollector.AddWarning(ProcessingWarning.MissingVariable(placeholder.VariableName));

            switch (_options.MissingVariableBehavior)
            {
                case MissingVariableBehavior.ReplaceWithEmpty:
                    Replace(paragraph, placeholder, ReplacementContent.Empty);
                    break;

                case MissingVariableBehavior.ThrowException:
                    throw new MissingVariableException(
                        placeholder.VariableName,
                        $"Missing variable or invalid expression: {placeholder.VariableName}");

                case MissingVariableBehavior.LeaveUnchanged:
                default:
                    break;
            }

            return;
        }

        // The :raw specifier only disables markdown; the value itself uses default conversion.
        bool isRaw = string.Equals(placeholder.Format, "raw", StringComparison.OrdinalIgnoreCase);
        string replacementValue = ValueConverter.ConvertToString(
            value,
            _options.Culture,
            isRaw ? null : placeholder.Format,
            _options.BooleanFormatterRegistry);

        replacementValue = TextReplacements.Apply(replacementValue, _options.TextReplacements)!;

        // Characters invalid in XML 1.0 would make the part unserializable.
        replacementValue = XmlCharacterSanitizer.Sanitize(replacementValue)!;

        // Markdown rendering via automatic styles is not supported yet: values are inserted as text.
        ReplacementContent content = ReplacementContent.FromValue(
            replacementValue,
            _options.EnableNewlineSupport,
            parseMarkdown: false);

        Replace(paragraph, placeholder, content);
    }

    private void Replace(XElement paragraph, PlaceholderToken placeholder, ReplacementContent content)
    {
        OdtParagraphTextRewriter.Replace(
            paragraph,
            placeholder.StartIndex,
            placeholder.StartIndex + placeholder.Length,
            content);
        ReplacementCount++;
    }
}
