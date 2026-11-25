// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Collections;
using System.Text.Json;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using TriasDev.Templify.Conditionals;
using TriasDev.Templify.Loops;
using TriasDev.Templify.Placeholders;
using TriasDev.Templify.Utilities;

namespace TriasDev.Templify.Core;

/// <summary>
/// Validates Word document templates for syntax errors and missing variables.
/// </summary>
internal sealed class TemplateValidator
{
    private readonly PlaceholderReplacementOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="TemplateValidator"/> class.
    /// </summary>
    /// <param name="options">Configuration options for validation. If null, default options are used.</param>
    public TemplateValidator(PlaceholderReplacementOptions? options = null)
    {
        _options = options ?? new PlaceholderReplacementOptions();
    }

    /// <summary>
    /// Validates a Word document template for syntax errors and optionally checks for missing variables.
    /// </summary>
    /// <param name="templateStream">Stream containing the template .docx file.</param>
    /// <param name="data">Optional dictionary for checking missing variables. If null, only syntax is validated.</param>
    /// <returns>A validation result with errors, placeholders, and missing variables.</returns>
    public ValidationResult Validate(Stream templateStream, IReadOnlyDictionary<string, object>? data)
    {
        List<ValidationError> errors = new List<ValidationError>();
        List<ValidationWarning> warnings = new List<ValidationWarning>();
        HashSet<string> allPlaceholders = new HashSet<string>();
        HashSet<string> missingVariables = new HashSet<string>();

        try
        {
            // Reset stream position if seekable
            if (templateStream.CanSeek)
            {
                templateStream.Position = 0;
            }

            // Open document read-only for validation
            using (WordprocessingDocument document = WordprocessingDocument.Open(templateStream, isEditable: false))
            {
                if (document.MainDocumentPart?.Document?.Body == null)
                {
                    errors.Add(ValidationError.Create(
                        ValidationErrorType.InvalidPlaceholderSyntax,
                        "Invalid document: MainDocumentPart or Body is missing."));

                    return ValidationResult.Failure(errors, Array.Empty<string>());
                }

                Body body = document.MainDocumentPart.Document.Body;
                List<OpenXmlElement> elements = body.Elements<OpenXmlElement>().ToList();
                WordTemplateLocator locator = new WordTemplateLocator(document);

                // 1. Structural lint (#255) over the containers processing visits: detector syntax errors, text next to
                //    block markers, multiple and orphan markers, unterminated tags; collects loop collection names
                TemplateLintWalker lint = new TemplateLintWalker(locator, errors, allPlaceholders);
                lint.ValidateBlocks(elements);

                // 2. Condition expressions (and the variables they reference) and inline expressions, in every paragraph
                ValidateConditionExpressions(elements, allPlaceholders, errors, warnings, data, locator);
                FindAllPlaceholders(body, allPlaceholders);
                ValidateInlineExpressions(elements, errors, locator);

                // 3. Headers, footers and notes
                foreach (List<OpenXmlElement> partElements in GetHeaderFooterElements(document))
                {
                    lint.ValidateBlocks(partElements);
                    ValidateConditionExpressions(partElements, allPlaceholders, errors, warnings, data, locator);
                    FindAllPlaceholdersInElements(partElements, allPlaceholders);
                    ValidateInlineExpressions(partElements, errors, locator);
                }

                // 4. Check for missing variables if data is provided
                if (data != null)
                {
                    ValidateMissingVariables(elements, data, allPlaceholders, missingVariables, warnings, errors, _options.WarnOnEmptyLoopCollections);

                    // Also validate missing variables in headers and footers
                    ValidateHeaderFooterMissingVariables(document, data, allPlaceholders, missingVariables, warnings, errors, _options.WarnOnEmptyLoopCollections);
                }
            }
        }
        catch (Exception ex)
        {
            // An unreadable or corrupted document ("Validation failed: File contains corrupted data.") would be
            // ValidationErrorType.InvalidDocument, as the OpenDocument validator reports it. This validator shipped
            // in 1.x with InvalidPlaceholderSyntax and consumers may switch on it, so the type changes only in 2.0
            // (issue #156). The same applies to the missing MainDocumentPart/Body error above.
            errors.Add(ValidationError.Create(
                ValidationErrorType.InvalidPlaceholderSyntax,
                $"Validation failed: {ex.Message}"));
        }

        // Return result
        List<string> allPlaceholdersList = allPlaceholders.OrderBy(p => p).ToList();
        List<string> missingVariablesList = missingVariables.OrderBy(v => v).ToList();
        List<ValidationWarning> warningsList = warnings.OrderBy(w => w.Message).ToList();

        return errors.Count == 0
            ? ValidationResult.Success(allPlaceholdersList, missingVariablesList, warningsList)
            : ValidationResult.Failure(errors, allPlaceholdersList, missingVariablesList, warningsList);
    }

    /// <summary>
    /// Finds all regular placeholders in the document body.
    /// </summary>
    private static void FindAllPlaceholders(Body body, HashSet<string> allPlaceholders)
    {
        FindAllPlaceholdersInElements(body.Elements<OpenXmlElement>().ToList(), allPlaceholders);
    }

    /// <summary>
    /// Validates missing variables using recursive loop scope analysis.
    /// </summary>
    /// <remarks>
    /// This method reuses the allPlaceholders set already populated by steps 1-4.
    /// It may add additional placeholders found during recursive loop validation.
    /// </remarks>
    private static void ValidateMissingVariables(
        List<OpenXmlElement> elements,
        IReadOnlyDictionary<string, object> data,
        HashSet<string> allPlaceholders,
        HashSet<string> missingVariables,
        List<ValidationWarning> warnings,
        List<ValidationError> errors,
        bool warnOnEmptyLoopCollections)
    {
        // Note: We reuse allPlaceholders from steps 1-4 (conditionals, loops, table loops, regular placeholders).
        // The scoped validation adds any additional placeholders found while descending into loops.
        new ScopedVariableValidator(data, allPlaceholders, missingVariables, warnings, errors, warnOnEmptyLoopCollections)
            .ValidateElements(elements);
    }

    /// <summary>
    /// Validates every <c>{{#if}}</c>/<c>{{#elseif}}</c> expression (block, inline and table-row conditionals)
    /// and collects the variables they reference from the parsed expression.
    /// </summary>
    /// <remarks>
    /// Variables are taken from the expression's AST, so operators and keywords (<c>in</c>, <c>contains</c>,
    /// <c>is empty</c>, ...), literals (<c>true</c>, <c>null</c>, numbers, strings) and list items
    /// (<c>("A", "B")</c>) are never reported as placeholders.
    /// </remarks>
    private static void ValidateConditionExpressions(
        List<OpenXmlElement> elements,
        HashSet<string> allPlaceholders,
        List<ValidationError> errors,
        List<ValidationWarning> warnings,
        IReadOnlyDictionary<string, object>? data,
        WordTemplateLocator locator)
    {
        ConditionalEvaluator evaluator = new ConditionalEvaluator();
        ValueResolver resolver = new ValueResolver();
        HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (Paragraph paragraph in EnumerateParagraphs(elements))
        {
            string text = paragraph.InnerText;
            if (text.IndexOf("{{#", StringComparison.Ordinal) < 0)
            {
                continue;
            }

            foreach (Match match in ConditionalPatterns.IfStart.Matches(text).Concat(ConditionalPatterns.ElseIf.Matches(text)))
            {
                string expression = match.Groups[1].Value.Trim();
                if (!seen.Add(expression))
                {
                    continue;
                }

                ConditionValidationResult validation = evaluator.Validate(expression);
                if (!validation.IsValid)
                {
                    string details = string.Join(" ", validation.Issues.Select(i => i.Message));
                    errors.Add(ValidationError.Create(
                        ValidationErrorType.InvalidConditionalExpression,
                        $"Invalid condition '{expression}': {details}",
                        locator.Describe(paragraph)));
                    continue;
                }

                Conditionals.Engine.ConditionNode node = ConditionalEvaluator.Parse(expression);
                foreach (Conditionals.Engine.VariableNode variable in ConditionalEvaluator.CollectVariables(node))
                {
                    allPlaceholders.Add(variable.Path);

                    if (variable.IsBareKeyword && data != null && resolver.TryResolveValue(data, variable.Path, out _))
                    {
                        warnings.Add(ValidationWarning.Create(
                            ValidationWarningType.ReservedWordAsVariable,
                            $"Condition '{expression}' uses '{variable.Path}', which is also a keyword, as a variable. " +
                            $"Write '[{variable.Path}]' to reference the variable unambiguously.",
                            locator.Describe(paragraph)));
                    }
                }
            }
        }
    }

    private static IEnumerable<Paragraph> EnumerateParagraphs(IEnumerable<OpenXmlElement> elements)
    {
        foreach (OpenXmlElement element in elements)
        {
            if (element is Paragraph paragraph)
            {
                yield return paragraph;
            }

            foreach (Paragraph nested in element.Descendants<Paragraph>())
            {
                yield return nested;
            }
        }
    }

    /// <summary>
    /// Gets all element lists from header, footer, footnote and endnote parts in the document.
    /// </summary>
    private static IEnumerable<List<OpenXmlElement>> GetHeaderFooterElements(WordprocessingDocument document)
    {
        if (document.MainDocumentPart == null)
        {
            yield break;
        }

        foreach (HeaderPart headerPart in document.MainDocumentPart.HeaderParts)
        {
            if (headerPart.Header != null)
            {
                yield return headerPart.Header.Elements<OpenXmlElement>().ToList();
            }
        }

        foreach (FooterPart footerPart in document.MainDocumentPart.FooterParts)
        {
            if (footerPart.Footer != null)
            {
                yield return footerPart.Footer.Elements<OpenXmlElement>().ToList();
            }
        }

        // Footnotes and endnotes are processed like headers and footers (separators excluded).
        IEnumerable<OpenXmlCompositeElement> notes =
            (document.MainDocumentPart.FootnotesPart?.Footnotes?.Elements<Footnote>()
                .Where(n => IsNormalNote(n.Type)) ?? Enumerable.Empty<Footnote>())
            .Cast<OpenXmlCompositeElement>()
            .Concat(document.MainDocumentPart.EndnotesPart?.Endnotes?.Elements<Endnote>()
                .Where(n => IsNormalNote(n.Type)) ?? Enumerable.Empty<Endnote>());

        foreach (OpenXmlCompositeElement note in notes)
        {
            yield return note.Elements<OpenXmlElement>().ToList();
        }
    }

    private static bool IsNormalNote(EnumValue<FootnoteEndnoteValues>? type) =>
        type?.Value is not FootnoteEndnoteValues value || value == FootnoteEndnoteValues.Normal;

    /// <summary>
    /// Validates missing variables in headers and footers.
    /// </summary>
    private static void ValidateHeaderFooterMissingVariables(
        WordprocessingDocument document,
        IReadOnlyDictionary<string, object> data,
        HashSet<string> allPlaceholders,
        HashSet<string> missingVariables,
        List<ValidationWarning> warnings,
        List<ValidationError> errors,
        bool warnOnEmptyLoopCollections)
    {
        foreach (List<OpenXmlElement> elements in GetHeaderFooterElements(document))
        {
            new ScopedVariableValidator(data, allPlaceholders, missingVariables, warnings, errors, warnOnEmptyLoopCollections)
                .ValidateElements(elements);
        }
    }

    /// <summary>
    /// Finds all regular placeholders in a list of elements.
    /// </summary>
    private static void FindAllPlaceholdersInElements(List<OpenXmlElement> elements, HashSet<string> allPlaceholders)
    {
        foreach (OpenXmlElement element in elements)
        {
            string text = element.InnerText;
            IEnumerable<string> foundPlaceholders = PlaceholderScanner.GetUniqueVariableNames(text);
            foreach (string placeholder in foundPlaceholders)
            {
                allPlaceholders.UnionWith(InlineExpressionValidation.GetPlaceholderNames(placeholder));
            }
        }
    }

    /// <summary>
    /// Reports the inline expression placeholders (<c>{{(...)}}</c>) that cannot be parsed.
    /// </summary>
    private static void ValidateInlineExpressions(List<OpenXmlElement> elements, List<ValidationError> errors, WordTemplateLocator locator)
    {
        foreach (Paragraph paragraph in EnumerateParagraphs(elements))
        {
            InlineExpressionValidation.CheckSyntax(ParagraphTextModel.GetText(paragraph), errors, () => locator.Describe(paragraph));
        }
    }
}
