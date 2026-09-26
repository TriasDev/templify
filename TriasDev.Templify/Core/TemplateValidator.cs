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

                // 1. Validate conditionals, their expressions, and collect variables from conditions
                _ = ValidateConditionals(elements, allPlaceholders, errors);
                ValidateConditionExpressions(elements, allPlaceholders, errors, warnings, data);

                // 2. Validate loops and collect collection names
                ValidateLoops(elements, allPlaceholders, errors);

                // 3. Validate table row loops and collect collection names
                ValidateTableRowLoops(body, allPlaceholders, errors);

                // 4. Find all regular placeholders in the document
                FindAllPlaceholders(body, allPlaceholders);

                // 5. Validate headers and footers
                ValidateHeadersAndFooters(document, allPlaceholders, errors, warnings, data);

                // 6. Check for missing variables if data is provided
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
    /// Validates conditional blocks and collects variables from condition expressions.
    /// </summary>
    private static IReadOnlyList<ConditionalBlock> ValidateConditionals(
        List<OpenXmlElement> elements,
        HashSet<string> allPlaceholders,
        List<ValidationError> errors)
    {
        IReadOnlyList<ConditionalBlock> conditionalBlocks = new List<ConditionalBlock>();
        try
        {
            // Variables referenced by the conditions are collected by ValidateConditionExpressions.
            conditionalBlocks = ConditionalDetector.DetectConditionalsInElements(elements);
        }
        catch (TemplateSyntaxException ex)
        {
            errors.Add(ValidationError.Create(ex.ErrorType, ex.Message));
        }

        return conditionalBlocks;
    }

    /// <summary>
    /// Validates loop blocks and collects collection names.
    /// </summary>
    private static void ValidateLoops(
        List<OpenXmlElement> elements,
        HashSet<string> allPlaceholders,
        List<ValidationError> errors)
    {
        try
        {
            IReadOnlyList<LoopBlock> loopBlocks = LoopDetector.DetectLoopsInElements(elements);

            // Extract collection names from loops, including loops nested in loop content (those are otherwise
            // only listed when data is passed and the outer collection has items).
            foreach (LoopBlock block in loopBlocks)
            {
                allPlaceholders.Add(block.CollectionName);
                ValidateLoops(block.ContentElements.ToList(), allPlaceholders, errors);
            }
        }
        catch (TemplateSyntaxException ex)
        {
            errors.Add(ValidationError.Create(ex.ErrorType, ex.Message));
        }
    }

    /// <summary>
    /// Validates table row loops and collects collection names.
    /// </summary>
    private static void ValidateTableRowLoops(
        Body body,
        HashSet<string> allPlaceholders,
        List<ValidationError> errors)
    {
        ValidateTableRowLoopsInElements(body.Elements<OpenXmlElement>().ToList(), allPlaceholders, errors);
    }

    /// <summary>
    /// Validates table row loops in a list of elements (used for headers/footers), including the
    /// loops of tables nested in cells, loops or content controls.
    /// </summary>
    private static void ValidateTableRowLoopsInElements(
        List<OpenXmlElement> elements,
        HashSet<string> allPlaceholders,
        List<ValidationError> errors)
    {
        IEnumerable<Table> tables = elements.SelectMany(e => e is Table table
            ? table.Descendants<Table>().Prepend(table)
            : e.Descendants<Table>());

        foreach (Table table in tables)
        {
            try
            {
                IReadOnlyList<LoopBlock> tableLoops = LoopDetector.DetectTableRowLoops(table);
                foreach (LoopBlock block in tableLoops)
                {
                    allPlaceholders.Add(block.CollectionName);
                }
            }
            catch (TemplateSyntaxException ex)
            {
                errors.Add(ValidationError.Create(ex.ErrorType, ex.Message));
            }

            // Table row conditionals (markers in rows of their own) fail processing when unmatched, so they must
            // fail validation too.
            try
            {
                _ = ConditionalDetector.DetectTableRowConditionals(table);
            }
            catch (TemplateSyntaxException ex)
            {
                errors.Add(ValidationError.Create(ex.ErrorType, ex.Message));
            }
        }
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
        IReadOnlyDictionary<string, object>? data)
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
                        match.Value));
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
                            match.Value));
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
    /// Validates template elements in all headers and footers.
    /// </summary>
    private static void ValidateHeadersAndFooters(
        WordprocessingDocument document,
        HashSet<string> allPlaceholders,
        List<ValidationError> errors,
        List<ValidationWarning> warnings,
        IReadOnlyDictionary<string, object>? data)
    {
        foreach (List<OpenXmlElement> elements in GetHeaderFooterElements(document))
        {
            _ = ValidateConditionals(elements, allPlaceholders, errors);
            ValidateConditionExpressions(elements, allPlaceholders, errors, warnings, data);
            ValidateLoops(elements, allPlaceholders, errors);
            ValidateTableRowLoopsInElements(elements, allPlaceholders, errors);
            FindAllPlaceholdersInElements(elements, allPlaceholders);
        }
    }

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
                allPlaceholders.Add(placeholder);
            }
        }
    }
}
