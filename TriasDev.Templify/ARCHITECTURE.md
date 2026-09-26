# Templify Architecture

This document describes how the `TriasDev.Templify` library processes templates. It is written for contributors;
for template syntax and API usage see the [documentation site](https://triasdev.github.io/templify/) and the
[library README](README.md).

Paths below are relative to `TriasDev.Templify/`. Everything not listed in `PublicAPI.Shipped.txt` /
`PublicAPI.Unshipped.txt` is `internal` and may change at any time.

## Design Principles

1. **Single responsibility** - small classes with one purpose (detectors, visitors, operators, resolvers)
2. **Composition over inheritance** - visitors and operators are composed, not subclassed
3. **Immutability** - options (`init` properties), results and parsed condition ASTs are immutable
4. **Explicit behavior** - template and data errors are reported, not guessed around
5. **Testability** - `InternalsVisibleTo` gives the test project access to internal components

## Overview

```
DocumentTemplateProcessor.ProcessTemplate(...)          Core/DocumentTemplateProcessor.cs
 ├─ validate streams, copy template → output stream
 ├─ TemplatePipeline.Create(options, missingVariables, warningCollector)
 │    ConditionalVisitor + LoopVisitor + PlaceholderVisitor in a CompositeVisitor, one DocumentWalker
 ├─ open the output with the Open XML SDK, create GlobalEvaluationContext(data)
 ├─ pipeline.Process(document, context)
 │    Walk (body) → WalkHeadersAndFooters → WalkFootnotesAndEndnotes
 ├─ DrawingIdAllocator.EnsureUniqueIds(document)          (loop clones duplicate drawing ids)
 ├─ UpdateFieldsOnOpen (Never / Always / Auto), DocumentProperties
 └─ ProcessingResult.Success(replacementCount, missingVariables, warnings) or Failure(message)
```

`TextTemplateProcessor` (plain text) and `TemplateValidator` (`ValidateTemplate`) reuse the same detectors, patterns,
condition engine, contexts and value conversion; see [Other Entry Points](#other-entry-points).

## Entry Point: DocumentTemplateProcessor

`Core/DocumentTemplateProcessor.cs` is the public entry point. `ProcessTemplate` has overloads for
`Stream` + `Stream` and `byte[]` + `out byte[]`, and `ProcessTemplateFile` works on file paths; each takes the data as
`Dictionary<string, object>`, `IReadOnlyDictionary<string, object?>` or a JSON string (parsed by
`Utilities/JsonDataParser.cs`). All overloads end in `ProcessTemplateCore`:

- The template stream must be readable; the output stream must be readable, writable and seekable. Unusable streams
  return a failed result before anything is written.
- The template is copied into the output stream and edited in place; the template itself is never modified.
  `ProcessTemplateFile` processes in memory and writes the output file only on success.
- `ValidateTemplate` delegates to `TemplateValidator` (syntax errors, placeholders, and missing variables when data is
  given).

### Error Model

| Situation | Result |
|-----------|--------|
| Template syntax error (unmatched `{{#if}}`/`{{#foreach}}`, `{{#elseif}}` after `{{#else}}`, invalid iteration variable, row markers sharing a row) | `ProcessingResult.Failure("Processing failed: ...")` |
| Data error (`{{#foreach}}` over a value that is not a collection) | `Failure` |
| Any other exception during processing (including Open XML errors) | `Failure` |
| Missing variable with `MissingVariableBehavior.ThrowException` | throws `InvalidOperationException` (the only exception) |
| Malformed `{{#if}}` / `{{#elseif}}` condition | condition is false, `ExpressionFailed` warning |
| Malformed inline `{{(...)}}` expression | treated as a missing variable, `ExpressionFailed` + `MissingVariable` warnings |
| Missing / null loop collection | loop removed, `MissingLoopCollection` / `NullLoopCollection` warning |
| Invalid JSON passed to a JSON overload | `JsonException` / `ArgumentException` from `JsonDataParser` (thrown before processing) |

Internally, `Core/TemplateExceptions.cs` defines `MissingVariableException`, `TemplateSyntaxException` (carrying a
`ValidationErrorType`, so `TemplateValidator` can map it) and `TemplateDataException`, all deriving from
`InvalidOperationException`. The processors classify by type: only `MissingVariableException` is rethrown, as a plain
`InvalidOperationException` with the same message.

Warnings are collected by `WarningCollector` (`Core/IWarningCollector.cs`) and returned as
`ProcessingResult.Warnings` (`Core/ProcessingWarning.cs`). `GetWarningReport()` / `GetWarningReportBytes()` render them
into a Word document through `Core/WarningReportGenerator.cs`, which processes an embedded template with Templify
itself.

## TemplatePipeline and Visitors

`Visitors/TemplatePipeline.cs` builds the visitor graph used by the processor:

```csharp
PlaceholderVisitor placeholderVisitor = new PlaceholderVisitor(options, missingVariables, warningCollector);
DocumentWalker walker = new DocumentWalker();
ConditionalVisitor conditionalVisitor = new ConditionalVisitor(warningCollector);

// LoopVisitor needs the final composite (which contains itself) to process nested constructs
CompositeVisitor tempComposite = new CompositeVisitor(conditionalVisitor, placeholderVisitor);
LoopVisitor loopVisitor = new LoopVisitor(walker, tempComposite, warningCollector);
CompositeVisitor composite = new CompositeVisitor(conditionalVisitor, loopVisitor, placeholderVisitor);
loopVisitor.SetNestedVisitor(composite);
```

All visitors implement `ITemplateElementVisitor` (`VisitConditional`, `VisitLoop`, `VisitPlaceholder`,
`VisitParagraph`); `CompositeVisitor` forwards each call to its children.

| Visitor | Responsibility |
|---------|----------------|
| `ConditionalVisitor` | Evaluates `{{#if}}` / `{{#elseif}}` / `{{#else}}` branches and removes the ones not taken. Block conditionals remove whole elements (paragraphs, tables, rows); inline conditionals inside one paragraph are parsed by `Conditionals/InlineConditionalParser.cs` and removed with `ParagraphTextRewriter`. Keeps table cells valid (a cell must end with a paragraph). |
| `LoopVisitor` | Resolves the collection, creates a `LoopContext` + `LoopEvaluationContext` per item, clones the loop content (`TemplateElementHelper.CloneElements`), inserts the clones and walks them with the nested composite, then removes the original block. |
| `PlaceholderVisitor` | Resolves a variable or inline expression, converts the value (`ValueConverter`, `TextReplacements`, `XmlCharacterSanitizer`) and rewrites the placeholder's text range. Counts replacements; records missing variables. |

## DocumentWalker

`Visitors/DocumentWalker.cs` traverses the document and dispatches to the visitor. `WalkElements` processes a list of
sibling elements in three steps:

1. **Conditionals** (`ConditionalDetector`), deepest first. Conditionals inside a loop block are skipped here and
   evaluated when the loop expands, with the item's context.
2. **Loops** (`LoopDetector`). Loop blocks whose markers were removed by a conditional are skipped.
3. **Placeholders** in the remaining paragraphs. Marker paragraphs are skipped; placeholders are found by
   `Placeholders/PlaceholderScanner.cs` in the paragraph's own text and visited from last to first so earlier offsets
   stay valid.

What is walked:

- **Body**, recursively into **tables** (rows and cells, nested tables).
- **Table rows** get row-aware detection: `{{#foreach}}` / `{{#if}}` markers that occupy their own row and are not
  closed in the same cell form row loops / row conditionals. Rows produced by a row loop are not re-processed. When
  processing removes every row of a table, the table is removed (Word rejects a table without rows), and an empty
  paragraph is added where a cell, header or footer would otherwise be left without one.
- **Content controls**: `SdtBlock` content, row-level `SdtRow` and cell-level `SdtCell`. A content control that
  contains a complete loop is not treated as a loop marker.
- **Text boxes** anchored in a paragraph (VML and DrawingML, both `mc:Choice` and `mc:Fallback`) are walked as separate
  block containers; their text does not count as the outer paragraph's text.
- **Headers and footers** (`WalkHeadersAndFooters`, all types) and **footnotes and endnotes**
  (`WalkFootnotesAndEndnotes`, separator notes skipped).
- **Comments are not processed** (they are reviewer notes, not document content).

Marker text for block detection comes from `Utilities/TemplateElementText.cs`; marker regexes are shared through
`Conditionals/ConditionalPatterns.cs` and `LoopDetector`.

## Paragraph Text Model and Rewriting

Word splits text into runs, so a placeholder can span several `w:t` elements with different formatting.

- `Utilities/ParagraphTextModel.cs` builds a paragraph's **own** text (not text boxes) as a list of text segments
  (`ParagraphTextSegment`: text element, owning run, offset) and non-text inline content (tabs, breaks, drawings, field
  characters, bookmarks) at their offsets.
- `Utilities/ParagraphTextRewriter.cs` replaces or removes character ranges in place: only the text elements that
  overlap a range change; runs outside the range, hyperlinks, fields, drawings and bookmarks stay untouched.
  Replacement text takes the formatting of the run holding the first replaced character.
- `Utilities/ReplacementContent.cs` describes the replacement as text pieces and line breaks: newlines become `w:br`
  when `EnableNewlineSupport` is on, and markdown (`Markdown/MarkdownParser.cs` → `MarkdownSegment`) becomes bold /
  italic / strikethrough pieces when `EnableMarkdown` is on and the placeholder is not `:raw`.
- `Utilities/FormattingPreserver.cs` clones run properties and merges markdown formatting into them in schema order.

## Condition Engine

`{{#if}}` / `{{#elseif}}`, inline `{{(...)}}` expressions, text templates and the public `ConditionEvaluator` share
one engine in `Conditionals/Engine/`:

```
expression string
  → ConditionLexer        tokens; ASCII/typographic quote normalization, string escapes (\" and \\),
                          [Name] bracket escape for keywords, numbers, true/false/null
  → ConditionParser       Pratt (precedence-climbing) parser → ConditionNode AST
                          (OperatorNode, VariableNode, LiteralNode, ListNode for "(a, b)")
  → ConditionEvaluatorCore walks the AST with a ConditionDialect
```

- **Operators** are classes implementing `IConditionOperator` (tokens, precedence, fixity, `Evaluate`), registered in
  `ConditionOperatorRegistry` - the single source of truth used by the lexer, parser and `Validate`. Operators live in
  `Engine/Operators/`: logical (`or`, `and`, `not`), comparison (`=`, `==`, `!=`, `>`, `<`, `>=`, `<=`), membership
  (`in`), string (`contains`, `startswith`, `endswith`; `contains` on a collection is a membership test) and postfix
  existence (`exists`, `is empty`, `is not empty`).
- **Precedence** (`OperatorPrecedence.cs`, loosest to tightest): `or` = 1, `and` = 2, `not` = 3, comparisons / `in` /
  string operators = 4, postfix = 5. So `not A = B` is `not (A = B)`.
- **Keywords in operand position**: the words added in 1.7.0 (`in`, `is`, `empty`, `exists`, `contains`,
  `startswith`, `endswith`) are read as variable names where an operand is expected; `[Name]` escapes any keyword.
- **Operand resolution**: comparison operands that do not resolve fall back to their own text (bareword literals, so
  `Status = Active` works); `in`, string and emptiness operators resolve strictly (missing = null); a bare missing
  variable is false.
- **Dialects** (`ConditionDialect.cs`) keep the two historical value policies:
  - `DefaultConditionDialect` (`{{#if}}`, text templates, `ConditionEvaluator`): rich truthiness (`"false"`, `"0"`,
    blank strings, numeric zero, NaN and empty collections are false), numeric comparison with invariant parsing of
    numeric strings.
  - `InlineConditionDialect` (`{{(...)}}`): only `true` is truthy; equality is numeric across numeric types, otherwise
    `object.Equals`; ordering via numbers or `IComparable`. The inline lexer also accepts single-quoted strings.
- `Utilities/NumericValue.cs` normalizes all CLR numeric types and `JsonElement` numbers, so `10 = 10.00m` and JSON
  `10.50 = 10.5`.
- `ConditionAstCache.cs` caches parsed ASTs process-wide (bounded, thread-safe); loops evaluate the same expression
  per item without re-parsing.
- `Conditionals/ConditionalEvaluator.cs` is the internal facade for `{{#if}}` (evaluation with warnings, parser-based
  `Validate` with typed `ConditionValidationIssue`s). `Placeholders/ExpressionPlaceholderEvaluator.cs` evaluates inline
  expressions. The public `ConditionEvaluator` / `ConditionContext` (`IConditionEvaluator`, `IConditionContext`) expose
  evaluation and validation without a document.

## Evaluation Contexts and Value Resolution

- `IEvaluationContext` (public) resolves variable paths. `GlobalEvaluationContext` wraps the root data;
  `Loops/LoopEvaluationContext.cs` first asks its `LoopContext`, then its parent context.
- `Loops/LoopContext.cs` holds the current item, index and count and resolves `@index`, `@number` (1-based), `@first`,
  `@last`, `@count`, `.` / `this`, the named iteration variable (`item`, `item.Name`) and the item's properties. A
  property that exists with a null value resolves as null and does not fall through to outer scopes; implicit names on a
  null item resolve from outer scopes.
- `Placeholders/ValueResolver.cs` looks up the root key (exact key first), then navigates with
  `PropertyPaths/PropertyPath.cs` / `PropertyPathResolver.cs`: properties and fields (case-insensitive), dictionary keys
  (dictionary comparer, keys win over dictionary members), `IReadOnlyDictionary`, `ExpandoObject`, list indexes, typed
  dictionary keys, and `JsonElement` objects/arrays (materialized as dictionaries/lists).
- `Placeholders/ValueConverter.cs` converts values to text with the configured culture: boolean formatters
  (`Formatting/BooleanFormatterRegistry.cs`, culture-aware `yesno`, `truefalse`, `onoff`, `enabled`, `active`, and
  `checkbox`, `checkmark`/`check`), `uppercase` / `lowercase`, `currency`, `number:FORMAT`, `date:FORMAT`.
- `Replacements/TextReplacements.cs` applies `PlaceholderReplacementOptions.TextReplacements` (e.g. the built-in
  `HtmlEntities` table); `Utilities/XmlCharacterSanitizer.cs` removes characters that are invalid in XML 1.0.

## Other Entry Points

- **`Core/TextTemplateProcessor.cs`** processes plain text with the same syntax: a single-pass block parser and
  renderer that reuses the marker patterns, `LoopDetector` validation, `ConditionalEvaluator`, loop contexts,
  `ExpressionPlaceholderEvaluator`, `ValueConverter` and `TextReplacements`. Conditionals are evaluated before their
  content; markdown and Word-only features (formatting, tables, fields, document properties) do not apply. Results are
  `TextProcessingResult` with the same error model and warnings.
- **`Core/TemplateValidator.cs`** (`ValidateTemplate`) walks body, headers/footers and notes: it reports unmatched
  markers and invalid conditions (`ValidationError`), collects `AllPlaceholders` (condition variables are taken from the
  AST), and with data reports `MissingVariables` plus warnings (`EmptyLoopCollection` when
  `WarnOnEmptyLoopCollections` is on, `ReservedWordAsVariable`). Missing variables are checked by
  `Core/ScopedVariableValidator.cs`, which traverses containers like the walker (body loops, table row loops per table
  and per loop row list, cells, nested tables, text boxes, content controls) and resolves names inside a loop against
  the items of that loop and its enclosing loops (implicit properties, named iteration variables, metadata).

## OpenDocument Text (.odt / .ott)

OpenDocument templates are processed by a separate internal engine in `OpenDocument/`. The Word pipeline above is not
touched. The format-agnostic components are reused unchanged: the condition engine, `InlineConditionalParser` and
`ConditionalPatterns`, `PlaceholderScanner` and `ExpressionPlaceholderEvaluator`, `ValueConverter`, `TextReplacements`,
the evaluation contexts, `ReplacementContent`/`MarkdownParser`, and results and warnings. The design and its decisions
are in `docs/superpowers/specs/2026-09-26-odt-support-design.md`.

```
OdtTemplateProcessor.ProcessTemplate(...)                 Core/OdtTemplateProcessor.cs
 ├─ OdtPackage.Open(template)       ZIP + XML parts; media type check; encrypted → failure
 ├─ OdtTemplateEngine.Process       content.xml body, then styles.xml master pages (headers/footers)
 │    per container: detect loops → conditionals (deepest first, not inside loops) → expand loops → placeholders
 │    paragraphs (text:p/text:h), tables/row groups, lists (items like rows), sections, text boxes, notes, indexes
 ├─ OdtUniqueNames                  frame/table/section names and note ids unique after cloning
 ├─ OdtDocumentProperties           DocumentProperties → meta.xml
 └─ package.Save(output)            mimetype first and stored; .ott → .odt; written only on success
```

- `OdtParagraphTextModel` / `OdtParagraphTextRewriter` are the ODT counterparts of the paragraph text model and
  rewriter. They treat `text:span`/`text:a` as inline containers and `text:s`, `text:tab` and `text:line-break` as
  atoms, and they encode replacements so that ODF whitespace collapsing renders them exactly. Markdown becomes
  automatic text styles (`OdtTextStyles`).
- `OdtConditionalDetector`, `OdtLoopDetector` and `OdtMarkerText` detect markers over `XElement` siblings with the same
  patterns and error messages as the Word detectors. `OdtTemplateValidator` implements `ValidateTemplate`.
- `Core/TemplateProcessor.cs` is the format-detecting facade. `TemplateFormatDetector` reads the ZIP: the ODF
  `mimetype` entry (or the manifest root media type) and the OOXML `[Content_Types].xml` WordprocessingML main part.
  The facade delegates to `DocumentTemplateProcessor` or `OdtTemplateProcessor`. A non-seekable template stream is
  buffered, and an unsupported format is a failed result.

## Post-Processing

- **`Utilities/DrawingIdAllocator.cs`** renumbers duplicate drawing (`wp:docPr`) and VML shape ids across all story
  parts after loop cloning; the first occurrence keeps its id.
- **Fields**: `UpdateFieldsOnOpen` (`Never`, `Always`, `Auto` = only when the document has dynamic fields such as TOC or
  PAGE) sets `w:updateFields` so Word refreshes fields on open.
- **Document properties**: non-null values of `PlaceholderReplacementOptions.DocumentProperties` overwrite the package
  properties.

## Source Layout

| Folder | Contents |
|--------|----------|
| `Core/` | Processors, options, results, warnings, validation, contexts, exceptions, warning report |
| `Visitors/` | `DocumentWalker`, `TemplatePipeline`, visitors, `TemplateElementHelper` |
| `Conditionals/` | Block/inline detection, `ConditionalEvaluator`, public `ConditionEvaluator` / `ConditionContext` |
| `Conditionals/Engine/` | Lexer, parser, AST, registry, operators, dialects, AST cache |
| `Loops/` | `LoopDetector`, `LoopBlock`, `LoopContext`, `LoopEvaluationContext` |
| `Placeholders/` | `PlaceholderScanner`, `ValueResolver`, `ValueConverter`, inline expression evaluation (`PlaceholderFinder` is obsolete) |
| `PropertyPaths/` | Path parsing and resolution |
| `Formatting/` | Boolean formatters |
| `Markdown/` | Markdown parsing |
| `Replacements/` | Text replacement tables |
| `Utilities/` | Paragraph text model/rewriter, formatting, JSON parsing, numeric values, sanitizing, drawing ids |
| `OpenDocument/` | OpenDocument Text engine: package, text model/rewriter, detectors, engine, styles, unique names, validator |

## Constraints and Trade-offs

- The whole document is loaded into memory (Open XML SDK DOM); very large documents need correspondingly more memory.
- Loop markers must be block-level (their own paragraph or table row); inline conditionals may share a paragraph with
  other text.
- When a placeholder spans runs with different formatting, the replacement takes the formatting of the first run.
- Processing is single-threaded per document; a `DocumentTemplateProcessor` instance holds only options and can be
  reused, and the shared caches (condition ASTs, default boolean formatters) are thread-safe.
