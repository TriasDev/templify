// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Collections;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using TriasDev.Templify.Conditionals;
using TriasDev.Templify.Core;
using TriasDev.Templify.Placeholders;
using TriasDev.Templify.PropertyPaths;
using TriasDev.Templify.Utilities;

namespace TriasDev.Templify.OpenDocument;

/// <summary>
/// Validates OpenDocument Text templates for syntax errors and missing variables. The counterpart of
/// <c>TemplateValidator</c> and <c>ScopedVariableValidator</c>, with the same rules and messages.
/// </summary>
/// <remarks>
/// The body and the headers and footers are traversed like <see cref="OdtTemplateEngine"/> traverses them,
/// so syntax errors are reported where processing would fail and each placeholder is checked in the loop
/// scope in which processing resolves it.
/// </remarks>
internal sealed class OdtTemplateValidator
{
    private const string UnreadableStreamMessage = "Invalid template stream: the template stream must be readable.";

    private readonly PlaceholderReplacementOptions _options;

    public OdtTemplateValidator(PlaceholderReplacementOptions options)
    {
        _options = options;
    }

    /// <summary>
    /// Validates a template; <paramref name="data"/> (optional) enables the missing-variable checks.
    /// </summary>
    public ValidationResult Validate(Stream templateStream, IReadOnlyDictionary<string, object>? data)
    {
        List<ValidationError> errors = new List<ValidationError>();
        List<ValidationWarning> warnings = new List<ValidationWarning>();
        HashSet<string> allPlaceholders = new HashSet<string>();
        HashSet<string> missingVariables = new HashSet<string>();

        if (!templateStream.CanRead)
        {
            return ValidationResult.Failure(
                new[] { ValidationError.Create(ValidationErrorType.InvalidDocument, UnreadableStreamMessage) },
                Array.Empty<string>());
        }

        try
        {
            if (templateStream.CanSeek)
            {
                templateStream.Position = 0;
            }

            OdtPackage package = OdtPackage.Open(templateStream);
            List<XElement> containers = GetContainers(package);

            OdtTemplateLocator locator = new OdtTemplateLocator(
                package.GetXml(OdtPackage.ContentEntry)!.Root!, package.GetXml(OdtPackage.StylesEntry)?.Root);
            SyntaxWalker syntax = new SyntaxWalker(allPlaceholders, errors, locator);
            foreach (XElement container in containers)
            {
                syntax.WalkBlocks(container.Elements().ToList());
                ValidateConditionExpressions(container, allPlaceholders, errors, warnings, data, locator);
                ValidateInlineExpressions(container, errors, locator);
                FindAllPlaceholders(container, allPlaceholders);
            }

            if (data != null)
            {
                foreach (XElement container in containers)
                {
                    new ScopeWalker(data, allPlaceholders, missingVariables, warnings, errors, _options.WarnOnEmptyLoopCollections)
                        .ValidateBlocks(container.Elements().ToList());
                }
            }
        }
        catch (InvalidOdtPackageException ex)
        {
            errors.Add(ValidationError.Create(ValidationErrorType.InvalidDocument, ex.Message));
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or XmlException)
        {
            // The stream could not be read (truncated, or failing while it is copied or inflated).
            errors.Add(ValidationError.Create(ValidationErrorType.InvalidDocument, $"Validation failed: {ex.Message}"));
        }
        catch (Exception ex)
        {
            errors.Add(ValidationError.Create(ValidationErrorType.InvalidPlaceholderSyntax, $"Validation failed: {ex.Message}"));
        }

        List<string> placeholders = allPlaceholders.OrderBy(p => p).ToList();
        List<string> missing = missingVariables.OrderBy(v => v).ToList();
        List<ValidationWarning> sortedWarnings = warnings.OrderBy(w => w.Message).ToList();

        return errors.Count == 0
            ? ValidationResult.Success(placeholders, missing, sortedWarnings)
            : ValidationResult.Failure(errors, placeholders, missing, sortedWarnings);
    }

    private static List<XElement> GetContainers(OdtPackage package)
    {
        XElement body = package.GetXml(OdtPackage.ContentEntry)!.Root?.Element(OdfNames.OfficeBody)?.Element(OdfNames.OfficeText)
            ?? throw new InvalidOdtPackageException("Invalid document: content.xml has no text body (office:text).");

        List<XElement> containers = new List<XElement> { body };
        XElement? stylesRoot = package.GetXml(OdtPackage.StylesEntry)?.Root;
        if (stylesRoot != null)
        {
            containers.AddRange(OdtTemplateEngine.GetHeadersAndFooters(stylesRoot));
        }

        return containers;
    }

    /// <summary>Gets the paragraphs of a container, excluding annotations and tracked deletions.</summary>
    private static IEnumerable<XElement> GetParagraphs(XElement container) =>
        container.Descendants()
            .Where(OdfNames.IsParagraph)
            .Where(p => !p.Ancestors().Any(a => a.Name == OdfNames.OfficeAnnotation || a.Name == OdfNames.Text + "tracked-changes"));

    private static void FindAllPlaceholders(XElement container, HashSet<string> allPlaceholders)
    {
        foreach (XElement paragraph in GetParagraphs(container))
        {
            allPlaceholders.UnionWith(PlaceholderScanner.GetUniqueVariableNames(OdtParagraphTextModel.GetText(paragraph))
                .SelectMany(InlineExpressionValidation.GetPlaceholderNames));
        }
    }

    /// <summary>Reports the inline expression placeholders (<c>{{(...)}}</c>) that cannot be parsed.</summary>
    private static void ValidateInlineExpressions(XElement container, List<ValidationError> errors, OdtTemplateLocator locator)
    {
        foreach (XElement paragraph in GetParagraphs(container))
        {
            InlineExpressionValidation.CheckSyntax(OdtParagraphTextModel.GetText(paragraph), errors, () => locator.Describe(paragraph));
        }
    }

    /// <summary>
    /// Validates every <c>{{#if}}</c>/<c>{{#elseif}}</c> expression and collects the variables it references.
    /// </summary>
    private static void ValidateConditionExpressions(
        XElement container,
        HashSet<string> allPlaceholders,
        List<ValidationError> errors,
        List<ValidationWarning> warnings,
        IReadOnlyDictionary<string, object>? data,
        OdtTemplateLocator locator)
    {
        ConditionalEvaluator evaluator = new ConditionalEvaluator();
        ValueResolver resolver = new ValueResolver();
        HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (XElement paragraph in GetParagraphs(container))
        {
            string text = OdtParagraphTextModel.GetText(paragraph);
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

    private static bool IsListItem(XElement element) =>
        element.Name == OdfNames.ListItem || element.Name == OdfNames.ListHeader;

    private static bool IsRowGroup(XElement element) =>
        element.Name == OdfNames.TableHeaderRows || element.Name == OdfNames.TableRows || element.Name == OdfNames.TableRowGroup;

    /// <summary>
    /// Enumerates the child block sequences of a block (cells of a table, items of a list, …) in the way
    /// the engine descends into them. Row and item sequences are returned as sequences of rows / items.
    /// </summary>
    private static IEnumerable<List<XElement>> GetChildSequences(XElement block)
    {
        if (OdfNames.IsParagraph(block) || block.Name.Namespace == OdfNames.Draw)
        {
            if (block.Name.Namespace == OdfNames.Draw && block.Elements().Any(OdfNames.IsParagraph))
            {
                yield return block.Elements().ToList();
                yield break;
            }

            foreach (XElement nested in OdtTemplateEngine.FindNestedContainers(block))
            {
                yield return nested.Elements().ToList();
            }
        }
        else if (block.Name == OdfNames.TableElement || IsRowGroup(block))
        {
            List<XElement> rows = block.Elements(OdfNames.TableRow).ToList();
            if (rows.Count > 0)
            {
                yield return rows;
            }

            foreach (XElement group in block.Elements().Where(IsRowGroup))
            {
                foreach (List<XElement> sequence in GetChildSequences(group))
                {
                    yield return sequence;
                }
            }
        }
        else if (block.Name == OdfNames.TableRow)
        {
            foreach (XElement cell in OdtMarkerText.GetRowCells(block))
            {
                yield return cell.Elements().ToList();
            }
        }
        else if (block.Name == OdfNames.List)
        {
            List<XElement> items = block.Elements().Where(IsListItem).ToList();
            if (items.Count > 0)
            {
                yield return items;
            }
        }
        else if (IsListItem(block)
                 || block.Name == OdfNames.Section
                 || block.Name == OdfNames.NumberedParagraph
                 || block.Name == OdfNames.IndexBody
                 || block.Name == OdfNames.IndexTitle
                 || OdfNames.IsHeaderFooterRegion(block))
        {
            yield return block.Elements().ToList();
        }
        else if (OdfNames.Indexes.Contains(block.Name))
        {
            foreach (XElement indexBody in block.Elements(OdfNames.IndexBody))
            {
                yield return indexBody.Elements().ToList();
            }
        }
    }

    /// <summary>
    /// Runs the loop and conditional detectors over every block sequence and reports their syntax errors
    /// (each message once); collects loop collection names as placeholders.
    /// </summary>
    private sealed class SyntaxWalker
    {
        private readonly HashSet<string> _allPlaceholders;
        private readonly List<ValidationError> _errors;
        private const int MaxQuotedTagLength = 40;

        private readonly HashSet<(XElement? Element, ValidationErrorType Type, string Message)> _reported =
            new HashSet<(XElement?, ValidationErrorType, string)>();

        /// <summary>Elements a detector error was reported for; problems inside them are follow-ups and not reported.</summary>
        private readonly HashSet<XElement> _failedElements = new HashSet<XElement>();
        private readonly HashSet<(XElement Element, string Rule)> _reportedElements = new HashSet<(XElement, string)>();
        private readonly OdtTemplateLocator _locator;

        public SyntaxWalker(HashSet<string> allPlaceholders, List<ValidationError> errors, OdtTemplateLocator locator)
        {
            _allPlaceholders = allPlaceholders;
            _errors = errors;
            _locator = locator;
        }

        public void WalkBlocks(IReadOnlyList<XElement> blocks)
        {
            if (blocks.Count == 0)
            {
                return;
            }

            IReadOnlyList<OdtLoopBlock> loops;
            IReadOnlyList<OdtConditionalBlock> conditionals;
            bool loopsFailed;
            bool conditionalsFailed;
            bool isBlockSequence = false;
            if (blocks.All(OdtMarkerText.IsRow))
            {
                (loops, loopsFailed) = Detect(() => OdtLoopDetector.DetectTableRowLoops(blocks));
                (conditionals, conditionalsFailed) = Detect(() => OdtConditionalDetector.DetectTableRowConditionals(blocks));
            }
            else if (blocks.All(IsListItem))
            {
                (loops, loopsFailed) = Detect(() => OdtLoopDetector.DetectListItemLoops(blocks));
                (conditionals, conditionalsFailed) = Detect(() => OdtConditionalDetector.DetectListItemConditionals(blocks));
            }
            else
            {
                isBlockSequence = true;
                (loops, loopsFailed) = Detect(() => OdtLoopDetector.DetectLoops(blocks));
                (conditionals, conditionalsFailed) = Detect(() => OdtConditionalDetector.DetectConditionals(blocks));
            }

            ReportLostText(loops, conditionals);
            ReportMultipleMarkers(loops, conditionals);

            // Marker blocks are removed by processing. A marker row or list item holds its marker in a paragraph of
            // its own, which must not be detected again as an unmatched marker inside the cell or item.
            // An inline conditional's paragraph stays (only its markers are removed): its frames are walked.
            HashSet<XElement> markers = new HashSet<XElement>(
                loops.SelectMany(l => new[] { l.StartMarker, l.EndMarker })
                    .Concat(conditionals
                        .Where(c => !(c.StartMarker == c.EndMarker && OdfNames.IsParagraph(c.StartMarker)))
                        .SelectMany(c => c.Branches.Select(b => b.Marker).Append(c.EndMarker))));

            // Loop content is walked as a sequence of its own; skipping it below keeps the walk linear in nested
            // loop > table > cell > loop structures.
            HashSet<XElement> loopContent = new HashSet<XElement>();
            foreach (OdtLoopBlock loop in loops)
            {
                _allPlaceholders.Add(loop.CollectionName);
                loopContent.UnionWith(loop.ContentElements);
            }

            // Paragraph rules on this sequence (row and item sequences hold no paragraphs of their own); paragraphs in
            // loop content are checked when the content is walked.
            if (isBlockSequence)
            {
                HashSet<XElement> allMarkers = new HashSet<XElement>(
                    loops.SelectMany(l => new[] { l.StartMarker, l.EndMarker })
                        .Concat(conditionals.SelectMany(c => c.Branches.Select(b => b.Marker).Append(c.EndMarker))));
                foreach (XElement paragraph in blocks.Where(b => OdfNames.IsParagraph(b) && !loopContent.Contains(b)))
                {
                    ReportUnterminatedTag(paragraph);
                    if (!allMarkers.Contains(paragraph) && !IsInsideFailedElement(paragraph))
                    {
                        ReportOrphanMarkers(paragraph, checkConditionals: !conditionalsFailed, checkLoops: !loopsFailed);
                    }
                }
            }

            foreach (OdtLoopBlock loop in loops)
            {
                // Nested loops in the content are detected when the content is processed per iteration.
                WalkBlocks(loop.ContentElements);
            }

            foreach (XElement block in blocks)
            {
                if (markers.Contains(block) || loopContent.Contains(block))
                {
                    continue;
                }

                foreach (List<XElement> sequence in GetChildSequences(block))
                {
                    WalkBlocks(sequence);
                }
            }
        }

        /// <summary>
        /// Reports block markers whose element holds other text, which processing removes with the marker (#253);
        /// each marker element once (loop content is walked again for nested loops).
        /// </summary>
        private void ReportLostText(IReadOnlyList<OdtLoopBlock> loops, IReadOnlyList<OdtConditionalBlock> conditionals)
        {
            IEnumerable<(XElement Marker, string Message)> losses =
                loops.SelectMany(OdtBlockMarkerLoss.Find).Concat(conditionals.SelectMany(OdtBlockMarkerLoss.Find));
            foreach ((XElement marker, string message) in losses)
            {
                Report(marker, "R2", ValidationErrorType.BlockMarkerWithText, message);
            }
        }

        /// <summary>R3: a removed marker element (paragraph, row, list item) holding more than one block marker.</summary>
        private void ReportMultipleMarkers(IReadOnlyList<OdtLoopBlock> loops, IReadOnlyList<OdtConditionalBlock> conditionals)
        {
            IEnumerable<XElement> removed = loops.Where(l => l.StartMarker != l.EndMarker).SelectMany(l => new[] { l.StartMarker, l.EndMarker })
                .Concat(conditionals
                    .Where(c => !(c.StartMarker == c.EndMarker && OdfNames.IsParagraph(c.StartMarker)))
                    .SelectMany(c => c.Branches.Select(b => b.Marker).Append(c.EndMarker)))
                .Distinct();
            foreach (XElement marker in removed)
            {
                IReadOnlyList<string> markers = TemplateTagText.GetBlockMarkersOutsideInlineConditionals(OdtBlockMarkerLoss.GetText(marker));
                if (markers.Count > 1)
                {
                    string unit = OdtBlockMarkerLoss.GetUnit(marker);
                    Report(marker, "R3", ValidationErrorType.MultipleBlockMarkers,
                        $"More than one block marker in one {unit}: {string.Join(", ", markers.Select(m => $"'{m}'"))}. " +
                        $"Put each block marker in a {unit} of its own.");
                }
            }
        }

        private void ReportUnterminatedTag(XElement paragraph)
        {
            string? tag = TemplateTagText.FindUnterminatedTag(OdtParagraphTextModel.GetText(paragraph));
            if (tag != null)
            {
                string quoted = tag.Length <= MaxQuotedTagLength ? tag : tag.Substring(0, MaxQuotedTagLength) + "…";
                Report(paragraph, "R1", ValidationErrorType.UnterminatedTag,
                    $"Unterminated tag '{quoted}': a placeholder or marker must open and close in the same paragraph.");
            }
        }

        private void ReportOrphanMarkers(XElement paragraph, bool checkConditionals, bool checkLoops)
        {
            foreach (string marker in TemplateTagText.GetEndAndBranchMarkers(OdtParagraphTextModel.GetText(paragraph)))
            {
                bool isLoopEnd = Loops.LoopDetector.ForeachEnd.IsMatch(marker);
                if (isLoopEnd && checkLoops)
                {
                    Report(paragraph, "R9:" + marker, ValidationErrorType.UnmatchedLoopEnd,
                        "Loop end marker '{{/foreach}}' has no matching '{{#foreach}}'.");
                }
                else if (!isLoopEnd && checkConditionals)
                {
                    Report(paragraph, "R9:" + marker, ValidationErrorType.UnmatchedConditionalEnd,
                        $"Conditional marker '{marker}' has no matching '{{{{#if}}}}'.");
                }
            }
        }

        private void Report(XElement element, string rule, ValidationErrorType type, string message)
        {
            if (_reportedElements.Add((element, rule)))
            {
                _errors.Add(ValidationError.Create(type, message, _locator.Describe(element)));
            }
        }

        private (IReadOnlyList<T> Blocks, bool Failed) Detect<T>(Func<IReadOnlyList<T>> detect)
        {
            try
            {
                return (detect(), false);
            }
            catch (TemplateSyntaxException ex)
            {
                // Processing stops at the outermost error: a row or list item that fails at its level is detected again
                // inside its cells or paragraphs (and loop content again per walk); those follow-ups are not reported.
                // The same problem at another place (body and header) is reported at each place.
                XElement? element = ex.SourceElement as XElement;
                if (element != null && IsInsideFailedElement(element))
                {
                    return (Array.Empty<T>(), true);
                }

                if (_reported.Add((element, ex.ErrorType, ex.Message)))
                {
                    _errors.Add(ValidationError.Create(ex.ErrorType, ex.Message, element != null ? _locator.Describe(element) : null));
                    if (element != null)
                    {
                        _failedElements.Add(element);
                    }
                }

                return (Array.Empty<T>(), true);
            }
        }

        private bool IsInsideFailedElement(XElement element) =>
            _failedElements.Count > 0 && element.Ancestors().Any(_failedElements.Contains);
    }

    /// <summary>
    /// Checks placeholders against the data, scoping variables inside loops to the loop items.
    /// The counterpart of <c>ScopedVariableValidator</c>.
    /// </summary>
    private sealed class ScopeWalker
    {
        private readonly IReadOnlyDictionary<string, object> _data;
        private readonly HashSet<string> _allPlaceholders;
        private readonly HashSet<string> _missingVariables;
        private readonly List<ValidationWarning> _warnings;
        private readonly List<ValidationError> _errors;
        private readonly bool _warnOnEmptyLoopCollections;
        private readonly ValueResolver _resolver = new ValueResolver();
        private readonly Stack<LoopScope> _loopStack = new Stack<LoopScope>();

        public ScopeWalker(
            IReadOnlyDictionary<string, object> data,
            HashSet<string> allPlaceholders,
            HashSet<string> missingVariables,
            List<ValidationWarning> warnings,
            List<ValidationError> errors,
            bool warnOnEmptyLoopCollections)
        {
            _data = data;
            _allPlaceholders = allPlaceholders;
            _missingVariables = missingVariables;
            _warnings = warnings;
            _errors = errors;
            _warnOnEmptyLoopCollections = warnOnEmptyLoopCollections;
        }

        public void ValidateBlocks(IReadOnlyList<XElement> blocks)
        {
            if (blocks.Count == 0)
            {
                return;
            }

            IReadOnlyList<OdtLoopBlock> loops = DetectLoops(blocks);
            foreach (OdtLoopBlock loop in loops)
            {
                ValidateLoop(loop);
            }

            HashSet<XElement> loopElements = new HashSet<XElement>(
                loops.SelectMany(l => l.ContentElements.Append(l.StartMarker).Append(l.EndMarker)));

            foreach (XElement block in blocks.Where(b => !loopElements.Contains(b)))
            {
                if (OdfNames.IsParagraph(block))
                {
                    ValidateText(OdtParagraphTextModel.GetText(block));
                }

                foreach (List<XElement> sequence in GetChildSequences(block))
                {
                    ValidateBlocks(sequence);
                }
            }
        }

        private static IReadOnlyList<OdtLoopBlock> DetectLoops(IReadOnlyList<XElement> blocks)
        {
            try
            {
                if (blocks.All(OdtMarkerText.IsRow))
                {
                    return OdtLoopDetector.DetectTableRowLoops(blocks);
                }

                return blocks.All(IsListItem)
                    ? OdtLoopDetector.DetectListItemLoops(blocks)
                    : OdtLoopDetector.DetectLoops(blocks);
            }
            catch (TemplateSyntaxException)
            {
                // Loop syntax errors are reported by the syntax validation.
                return Array.Empty<OdtLoopBlock>();
            }
        }

        private void ValidateLoop(OdtLoopBlock loop)
        {
            _allPlaceholders.Add(loop.CollectionName);

            if (!TryResolveCollectionItems(loop.CollectionName, out List<object?> items))
            {
                ReportMissing(
                    loop.CollectionName,
                    $"Collection '{loop.CollectionName}' is referenced in a loop but not provided in the data.");
                return;
            }

            HashSet<string> properties = AggregateProperties(items);
            if (properties.Count == 0)
            {
                if (_warnOnEmptyLoopCollections)
                {
                    _warnings.Add(ValidationWarning.Create(
                        ValidationWarningType.EmptyLoopCollection,
                        $"Collection '{loop.CollectionName}' is empty. Variables inside this loop could not be validated."));
                }

                return;
            }

            _loopStack.Push(new LoopScope(loop.IterationVariableName, properties, items));
            ValidateBlocks(loop.ContentElements);
            _loopStack.Pop();
        }

        private void ValidateText(string text)
        {
            foreach (string placeholder in PlaceholderScanner.GetUniqueVariableNames(text))
            {
                _allPlaceholders.UnionWith(InlineExpressionValidation.GetPlaceholderNames(placeholder));

                if (placeholder.StartsWith('('))
                {
                    foreach (string variable in InlineExpressionValidation.GetMissingVariables(placeholder, CanResolveInScope))
                    {
                        ReportMissing(
                            variable,
                            $"Variable '{variable}' is referenced in expression '{placeholder}' but not provided in the data.");
                    }

                    continue;
                }

                if (placeholder.StartsWith('@') || placeholder.StartsWith('.') || placeholder == "this")
                {
                    continue;
                }

                if (!CanResolveInScope(placeholder))
                {
                    ReportMissing(
                        placeholder,
                        $"Variable '{placeholder}' is referenced in the template but not provided in the data.");
                }
            }

            MissingConditionVariableCheck.Check(text, CanResolveInScope, _warnings);
        }

        private void ReportMissing(string variable, string message)
        {
            _missingVariables.Add(variable);
            _errors.Add(ValidationError.Create(ValidationErrorType.MissingVariable, message));
        }

        private bool TryResolveCollectionItems(string collectionName, out List<object?> items)
        {
            items = new List<object?>();

            foreach (LoopScope scope in _loopStack)
            {
                string? relativePath = GetPathRelativeToScope(collectionName, scope);
                if (relativePath == null)
                {
                    continue;
                }

                if (PropertyPath.TryParse(relativePath, out PropertyPath? path) && path != null)
                {
                    foreach (object? item in scope.Items)
                    {
                        if (PropertyPathResolver.TryResolvePath(item, path, out object? value))
                        {
                            AddItems(value, items);
                        }
                    }
                }

                return true;
            }

            if (!_resolver.TryResolveValue(_data, collectionName, out object? collection) || collection == null)
            {
                return false;
            }

            AddItems(collection, items);
            return true;
        }

        private static string? GetPathRelativeToScope(string name, LoopScope scope)
        {
            if (scope.IterationVariableName != null)
            {
                string prefix = scope.IterationVariableName + ".";
                if (name.StartsWith(prefix, StringComparison.Ordinal))
                {
                    string propertyPath = name.Substring(prefix.Length);
                    return scope.Properties.Contains(GetRootSegment(propertyPath)) ? propertyPath : null;
                }
            }

            return scope.Properties.Contains(GetRootSegment(name)) ? name : null;
        }

        private static string GetRootSegment(string path)
        {
            int end = path.IndexOfAny(new[] { '.', '[' });
            return end > 0 ? path.Substring(0, end) : path;
        }

        private static void AddItems(object? collection, List<object?> items)
        {
            if (collection is JsonElement { ValueKind: JsonValueKind.Array } jsonArray)
            {
                foreach (JsonElement item in jsonArray.EnumerateArray())
                {
                    items.Add(item);
                }

                return;
            }

            if (collection is string || collection is not IEnumerable enumerable)
            {
                return;
            }

            foreach (object? item in enumerable)
            {
                items.Add(item);
            }
        }

        private static HashSet<string> AggregateProperties(List<object?> items)
        {
            HashSet<string> properties = new HashSet<string>();
            Dictionary<Type, System.Reflection.PropertyInfo[]> cache = new Dictionary<Type, System.Reflection.PropertyInfo[]>();

            foreach (object? item in items)
            {
                if (item == null)
                {
                    continue;
                }

                if (item is IDictionary<string, object> dictionary)
                {
                    properties.UnionWith(dictionary.Keys);
                }
                else if (item is JsonElement { ValueKind: JsonValueKind.Object } json)
                {
                    properties.UnionWith(json.EnumerateObject().Select(p => p.Name));
                }
                else
                {
                    Type type = item.GetType();
                    if (!cache.TryGetValue(type, out System.Reflection.PropertyInfo[]? typeProperties))
                    {
                        typeProperties = type.GetProperties();
                        cache[type] = typeProperties;
                    }

                    properties.UnionWith(typeProperties.Select(p => p.Name));
                }
            }

            return properties;
        }

        private bool CanResolveInScope(string placeholder)
        {
            foreach (LoopScope scope in _loopStack)
            {
                if (placeholder == scope.IterationVariableName || GetPathRelativeToScope(placeholder, scope) != null)
                {
                    return true;
                }
            }

            return _resolver.TryResolveValue(_data, placeholder, out _);
        }

        private sealed record LoopScope(string? IterationVariableName, HashSet<string> Properties, List<object?> Items);
    }
}
