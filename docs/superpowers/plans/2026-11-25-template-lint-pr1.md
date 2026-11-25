# Template Lint PR 1 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** One lint walk per format that mirrors processing, gives every structural validation error a location, and adds rules R1 (unterminated tag), R3 (multiple block markers), R7 (detector errors in every container), R9 (orphan end/branch markers) and R5 (row-loop message).

**Architecture:** A locator per format turns any template element into the location string (`body, table 1, row 2, cell 3, paragraph 1: '…'`) by walking its ancestors. The Word lint walk replaces `BlockMarkerValidator` and the structural steps of `TemplateValidator` and visits exactly the containers `DocumentWalker` visits; the ODT `SyntaxWalker` gets the same rules. Detector exceptions carry their source element so R7 errors can be located.

**Tech Stack:** C# (net11.0/net10.0), DocumentFormat.OpenXml 3.5.1, System.Xml.Linq, xUnit v3.

**Spec:** `docs/superpowers/specs/2026-11-25-template-lint-design.md` (PR 1 of the rollout; R4 and R8 are later plans).

## Global Constraints

- Location: `<part>, <path>: '<text>'`; indexes 1-based within their parent, counted **per kind** (paragraph, table, row, cell, text box, content control, list, item); text whitespace-collapsed, cut at 100 chars with `…` (`BlockMarkerText.GetLocation`). Row-level problems: `…, row N: '<row text>'`. `Location` stays `null` when there is no element.
- Part names: Word `body`, `header (default|first|even)`, `footer (…)`, a second part of the same kind and type `header 2 (default)`, `footnote N` / `endnote N` (normal notes in document order); ODT `body`, `header (<master page name>)`, `header left (…)`, `header first (…)`, same for `footer`.
- Existing error and warning **messages stay word for word**; only `Location` changes (R5 extends one message, see Task 5).
- New public enum members (add to `PublicAPI.Unshipped.txt`): `ValidationErrorType.UnterminatedTag = 9`, `ValidationErrorType.MultipleBlockMarkers = 10`. No other public API change.
- Same detectors as processing, same containers as processing; each problem reported once; traversal linear (loop content walked once, skipped in the parent list).
- Content processing does not visit keeps today's condition/inline-expression syntax scans and `AllPlaceholders`; the new rules do not look there.
- Data checks (`ScopedVariableValidator`, ODT `ScopeWalker`, `MissingConditionVariableCheck`) are **not** changed in this PR.
- CI build (`-p:ContinuousIntegrationBuild=true`) 0 warnings; `dotnet format` clean; LibreOffice round trips 15/15, 0 skipped.

## Review Focus

- Lossless shapes must stay valid in both formats: whitespace/tab/NBSP/format characters around markers, markers split across runs, inline conditionals (also nested: `{{#if A}}{{#if B}}x{{/if}}{{/if}}`), clean row and cell blocks, `examples/templates/*`, the embedded `Resources/WarningReportTemplate.docx`.
- Literal braces that render as written (`{"a":{"b":1}}`, `}}`, `{{ ` followed by a space) must not trigger R1.
- An inner loop's `{{/foreach}}` inside loop content must not be reported as orphan at the parent level (R9 checks each list excluding its loop content).
- Deep nesting (20 levels loop > table > cell > loop) validates in well under a second.
- A problem inside loop content is reported once, at the template element (not per walk path).

---

### Task 1: Locators

**Files:**
- Create: `src/TriasDev.Templify/Core/TemplateLocation.cs`, `src/TriasDev.Templify/Core/WordTemplateLocator.cs`, `src/TriasDev.Templify/OpenDocument/OdtTemplateLocator.cs`
- Test: `tests/TriasDev.Templify.Tests/Core/WordTemplateLocatorTests.cs`, `tests/TriasDev.Templify.Tests/Odt/OdtTemplateLocatorTests.cs`

**Interfaces:**
- Produces:
  - `internal static class TemplateLocation { static string Format(string part, IEnumerable<string> path, string text); }` — joins `part, seg1, seg2: '<quoted text>'` with `BlockMarkerText.GetLocation(text)`.
  - `internal sealed class WordTemplateLocator { WordTemplateLocator(WordprocessingDocument document); string Describe(OpenXmlElement element); }` — part labels computed once in the constructor (header/footer types from the `HeaderReference`/`FooterReference` of the section properties; notes by order among normal notes); the path from the part root down to `element` (segments `paragraph N`, `table N`, `row N`, `cell N`, `text box N`, `content control N`), skipping other wrappers; `text` = the element's own text (`TemplateElementText.GetOwnText`).
  - `internal sealed class OdtTemplateLocator { OdtTemplateLocator(XElement contentRoot, XElement? stylesRoot); string Describe(XElement element); }` — segments `paragraph N` (`text:p`/`text:h`), `table N`, `row N`, `cell N`, `list N`, `item N`, `section N`, `frame N`; text = `OdtMarkerText.GetOwnText`.

- [ ] **Step 1: Write the failing tests** (Word via `DocumentBuilder`, ODT via `OdtDocumentBuilder`; describe the element, assert the exact string)

```
Word_BodyParagraph:                   3rd paragraph "Hello {{Na"           → "body, paragraph 3: 'Hello {{Na'"
Word_TableCellParagraph:              table 1 row 2 cell 3 para 1 "x"      → "body, table 1, row 2, cell 3, paragraph 1: 'x'"
Word_Row:                             row 1 cells "{{#foreach Items}}","H" → "body, table 1, row 1: '{{#foreach Items}}H'"
Word_NestedTable:                     table in cell → "body, table 1, row 1, cell 1, table 1, row 1, cell 1, paragraph 1: 'n'"
Word_HeaderDefault / Word_HeaderFirst: "header (default), paragraph 1: 'h'" / "header (first), paragraph 1: 'f'"
Word_SecondHeaderPartSameType:        → "header 2 (default), paragraph 1: '…'"
Word_Footnote:                        normal note id 1 (after separators)  → "footnote 1, paragraph 1: 'n'"
Word_TextBox:                         body para 2 text box para 1          → "body, paragraph 2, text box 1, paragraph 1: 't'"
Word_ContentControl:                  SdtBlock then its 2nd paragraph      → "body, content control 1, paragraph 2: 'c'"
Word_LongText_IsCut:                  150 'a' chars → text part ends with "…" after 100 chars
Odt_BodyParagraph / Odt_Heading:      "body, paragraph 2: 'x'" (text:h counts as paragraph)
Odt_TableCell:                        "body, table 1, row 2, cell 1, paragraph 1: 'x'"
Odt_ListItem:                         "body, list 1, item 3, paragraph 1: 'x'"
Odt_Header:                           master page "Standard" header         → "header (Standard), paragraph 1: 'h'"
```

- [ ] **Step 2: Run** `dotnet test tests/TriasDev.Templify.Tests/TriasDev.Templify.Tests.csproj -f net11.0 --filter "FullyQualifiedName~TemplateLocatorTests"` — Expected: build error (types missing).
- [ ] **Step 3: Implement** the three types per the Interfaces block.
- [ ] **Step 4: Run** the same command — Expected: PASS.
- [ ] **Step 5: Commit** `feat: template locators for validation locations (#255)`

### Task 2: Detector exceptions carry their source element; R5 row-loop message

**Files:**
- Modify: `src/TriasDev.Templify/Core/TemplateExceptions.cs` (`TemplateSyntaxException`), `Loops/LoopDetector.cs`, `Conditionals/ConditionalDetector.cs`, `OpenDocument/OdtLoopDetector.cs`, `OpenDocument/OdtConditionalDetector.cs` (all throw sites)
- Test: `tests/TriasDev.Templify.Tests/Loops/LoopDetectorTests.cs`, `Conditionals/ConditionalDetectorTests.cs` (append), `Odt/OdtLoopTests.cs` (append), `Integration/TableTests.cs` or the existing row-loop test class (append)

**Interfaces:**
- Produces: `internal object? SourceElement { get; init; }` on `TemplateSyntaxException` (an `OpenXmlElement` or `XElement`): the marker element the error is about (unmatched start → the start marker element; elseif after else → that elseif element; invalid iteration variable → the loop start element). `InlineConditionalParser` and `TextTemplateProcessor` throw sites leave it `null`.

- [ ] **Step 1: Write the failing tests**

```
Word: DetectLoopsInElements([P("{{#foreach Items}}"), P("x")]) throws; ex.SourceElement == the first paragraph
Word: DetectConditionalsInElements with elseif after else; ex.SourceElement == the elseif paragraph
ODT:  OdtLoopDetector.DetectLoops unmatched start; ex.SourceElement == the start text:p
R5 (Word and ODT, processing and ValidateTemplate): table row cells "{{#foreach Items}}" | "{{/foreach}}"
   → message == "Loop start marker '{{#foreach Items}}' has no matching '{{/foreach}}'. Loop markers in a table must be in rows of their own; a row with both '{{#foreach}}' and '{{/foreach}}' is not supported."
   (processing: ProcessingResult.ErrorMessage ends with that text; type UnmatchedLoopStart)
A start row WITHOUT an end in the same row keeps today's message exactly.
```

- [ ] **Step 2: Run** `--filter "FullyQualifiedName~Detector|FullyQualifiedName~RowLoop"` — Expected: FAIL (property missing / old message).
- [ ] **Step 3: Implement**: set `SourceElement` at every Word/ODT detector throw site; in `DetectTableRowLoops` / ODT `DetectUnitLoops`, when the unmatched start row's own text also matches the end pattern, append the R5 sentence.
- [ ] **Step 4: Run** the filter and the whole project — Expected: PASS (existing message tests unchanged).
- [ ] **Step 5: Commit** `feat: detector errors carry their element; explain same-row loop markers (#255)`

### Task 3: Word lint walk (R1, R2, R3, R7, R9) with locations

**Files:**
- Create: `src/TriasDev.Templify/Core/TemplateLintWalker.cs` (replaces `Core/BlockMarkerValidator.cs`, which is deleted)
- Create: `src/TriasDev.Templify/Utilities/TemplateTagText.cs` (format-agnostic text rules, shared with Task 4)
- Modify: `src/TriasDev.Templify/Core/TemplateValidator.cs` (remove steps 1–3 and 6 for body and parts; `ValidateConditionExpressions` and `ValidateInlineExpressions` keep their all-paragraph scan but set `Location = locator.Describe(paragraph)`), `Core/ValidationResult.cs` (enum members), `PublicAPI.Unshipped.txt`, `Core/InlineExpressionValidation.cs` (accept a location)
- Test: `tests/TriasDev.Templify.Tests/Integration/TemplateLintTests.cs`; update `ValidationInlineExpressionTests` (lines 83, 235) and `ValidationResultTests` (enum stability: 9, 10; length 11)

**Interfaces:**
- Consumes: `WordTemplateLocator` (Task 1), `TemplateSyntaxException.SourceElement` (Task 2), `BlockMarkerLoss.Find` (#253).
- Produces:
  - `internal static class TemplateTagText`:
    - `static string? FindUnterminatedTag(string text)` — R1: first match of `\{\{[\w@.#/(]` with no `}}` after it; returns the text from that `{{` (null if none).
    - `static IReadOnlyList<string> GetBlockMarkersOutsideInlineConditionals(string text)` — block markers (detector regexes, as `BlockMarkerText`) not inside a complete inline conditional (`InlineConditionalParser.Parse` ranges); used by R3.
    - `static IReadOnlyList<string> GetEndAndBranchMarkers(string text)` — `{{/if}}`, `{{/foreach}}`, `{{#else}}`, `{{#elseif …}}` matches; used by R9.
  - `internal sealed class TemplateLintWalker { TemplateLintWalker(WordTemplateLocator locator, List<ValidationError> errors); void ValidateBlocks(List<OpenXmlElement> elements); }` — containers as `BlockMarkerValidator` today (incl. `SdtRow`, text boxes, content controls, loop content once).

Rules per element list (rows: same with row lists):
- R7: detector exceptions → `ValidationError(ex.ErrorType, ex.Message, locator.Describe(ex.SourceElement))`; once per problem, keyed as the ODT `SyntaxWalker` does today (error type + the quoted start marker, else the message), so a row-level failure re-detected inside its cell is reported once, with the outermost level's message.
- R2: unchanged (`BlockMarkerLoss`), location from the locator.
- R3: every marker element of a non-inline block (inline loop excluded: that is R2) with `GetBlockMarkersOutsideInlineConditionals(text).Count > 1` → `MultipleBlockMarkers`, message `More than one block marker in one {unit}: 'm1', 'm2'. Put each block marker in a {unit} of its own.` (`unit` = paragraph / table row / content control).
- R9: every element of the list that is **not** inside this list's loop content, **not** a marker of a detected block, and whose text has `GetEndAndBranchMarkers` → per marker: `{{/if}}`/`{{#else}}`/`{{#elseif}}` → `UnmatchedConditionalEnd` with `Conditional marker '{m}' has no matching '{{#if}}'.`; `{{/foreach}}` → `UnmatchedLoopEnd` with `Loop end marker '{{/foreach}}' has no matching '{{#foreach}}'.`
- R1: every paragraph of the list (own text via `ParagraphTextModel.GetText`) with `FindUnterminatedTag` → `UnterminatedTag`, message `Unterminated tag '{tag}': a placeholder or marker must open and close in the same paragraph.`

- [ ] **Step 1: Write the failing tests** (`TemplateLintTests`; each `ValidateTemplate` with and without data; error Type, Message, Location exact)

```
R1_SplitPlaceholder:          ["Hello {{Na", "me}} world"]       → 1 error UnterminatedTag, Location "body, paragraph 1: 'Hello {{Na'"
R1_SplitBlockMarker:          ["{{#if", "Flag}}", "body", "{{/if}}"] → UnterminatedTag @ paragraph 1 AND UnmatchedConditionalEnd @ paragraph 4
R1_LiteralBraces_NotReported: ["{\"a\":{\"b\":1}}", "}}", "{{ not a tag"] → IsValid
R3_IfAndForeachInOneParagraph: ["{{#if Flag}}{{#foreach Items}}", "{{Name}}", "{{/foreach}}{{/if}}"] → MultipleBlockMarkers @ paragraph 1 and @ paragraph 3
R3_NestedInlineConditional_NotReported: ["{{#if A}}{{#if B}}x{{/if}}{{/if}}"] → IsValid
R7_ContentControlIfWithEndOutside (old bug b): SdtBlock(P("{{#if Flag}}"), P("x")), P("{{/if}}") → error (the one processing fails with), located
R7_NestedEndMarkersInLoop (old bug a): ["{{#foreach Items}}", "{{#if Flag}}", "{{Name}}", "{{/if}}{{/foreach}}"] → error, located; processing of the same template also fails
R7_UnmatchedInTableCell:      cell paragraphs ["{{#if A}}", "x"] → UnmatchedConditionalStart @ "body, table 1, row 1, cell 1, paragraph 1: '{{#if A}}'"
R9_OrphanEndIf:               ["x", "{{/if}}"]                    → UnmatchedConditionalEnd @ "body, paragraph 2: '{{/if}}'"
R9_OrphanElse / R9_OrphanEndForeach
R9_InnerLoopEnd_NotOrphan:    ["{{#foreach A}}", "{{#foreach B}}", "x", "{{/foreach}}", "{{/foreach}}"] → IsValid
R2_Location:                  #253 shape → BlockMarkerWithText Location "body, paragraph 1: 'Assets: {{#foreach Items}}'"
Header_Location:              header paragraphs ["{{#if A}}", "x"] → Location starts with "header (default), paragraph 1"
ConditionExpression_Location: ["{{#if Count >}}", "x", "{{/if}}"] → InvalidConditionalExpression Location "body, paragraph 1: '{{#if Count >}}'"
Lossless: whitespace/NBSP/U+200B around markers, markers split across runs, examples/templates/*.docx, Resources/WarningReportTemplate.docx → no errors
DeepNesting_IsLinear:         20 × (loop > table > cell) → < 1 s (Trait Performance)
```

- [ ] **Step 2: Run** `--filter "FullyQualifiedName~TemplateLintTests"` — Expected: FAIL (enum members missing / wrong locations).
- [ ] **Step 3: Implement** `TemplateTagText`, `TemplateLintWalker`, the enum members + `PublicAPI.Unshipped.txt`, and the `TemplateValidator` wiring; delete `BlockMarkerValidator`.
- [ ] **Step 4: Run** the filter, then the whole project on both TFMs; update the two existing tests that asserted the old `Location` values (`ValidationInlineExpressionTests`) and `ValidationResultTests` enum stability. Expected: PASS.
- [ ] **Step 5: Commit** `feat: lint walk for Word templates with locations, unterminated tags, multiple and orphan markers (#255)`

### Task 4: ODT lint walk (same rules) and parity

**Files:**
- Modify: `src/TriasDev.Templify/OpenDocument/OdtTemplateValidator.cs` (`SyntaxWalker`: locator, R1, R3, R9, R7 locations; `ValidateConditionExpressions` / `ValidateInlineExpressions` locations)
- Test: `tests/TriasDev.Templify.Tests/Odt/OdtTemplateLintTests.cs`; `Odt/OdtValidationParityTests.cs` (compare `Location`; "orphan end markers" now expects errors; add the R1/R3/R9 shapes); update `Odt/OdtValidationTests.cs` lines 72, 100 (new locations)

**Interfaces:**
- Consumes: `OdtTemplateLocator` (Task 1), `TemplateSyntaxException.SourceElement` (Task 2), `TemplateTagText` (Task 3), `OdtBlockMarkerLoss`.
- Produces: same errors (Type, Message, Location) as Word for body paragraphs and tables.

- [ ] **Step 1: Write the failing tests** — the Word list of Task 3 for ODT (no content controls/text boxes; plus `ListItemMarkerWithEndOutside`, header via `AddHeaderParagraph` → `header (Standard), paragraph 1: …`, `examples/templates/*.odt` lossless); parity: `Describe(errors)` now `"{Type}: {Message} @ {Location}"`.
- [ ] **Step 2: Run** `--filter "FullyQualifiedName~OdtTemplateLintTests|FullyQualifiedName~OdtValidationParityTests"` — Expected: FAIL.
- [ ] **Step 3: Implement** in `SyntaxWalker` (R9 excludes loop content of the current sequence; R3 on marker blocks; R1 on paragraphs of each sequence).
- [ ] **Step 4: Run** the filter, the whole project on both TFMs, and `--filter "Category=LibreOffice"` (15/15, 0 skipped). Expected: PASS.
- [ ] **Step 5: Commit** `feat: lint rules and locations for OpenDocument templates (#255)`

### Task 5: Docs and pre-push checks

**Files:**
- Modify: `docs/for-developers/quick-start.md` (error table: `UnterminatedTag`, `MultipleBlockMarkers`; `UnmatchedConditionalEnd`/`UnmatchedLoopEnd` now reported for orphan markers; a "Location" paragraph with the grammar and two examples), `docs/for-template-authors/loops.md` + `conditionals.md` (one marker per paragraph/row; same-row loop message), `src/TriasDev.Templify/ARCHITECTURE.md` (lint walk + locators replace `BlockMarkerValidator`), `CLAUDE.md` (Core components list)

- [ ] **Step 1: Update the docs above.**
- [ ] **Step 2: Checks** — `dotnet build templify.slnx -c Release -p:ContinuousIntegrationBuild=true` (0 warnings); all test projects pass; `dotnet format --verify-no-changes --no-restore` (exit 0); LibreOffice 15/15, 0 skipped; `python3 -m mkdocs build --strict` (exit 0).
- [ ] **Step 3: Commit** `docs: template lint rules and validation locations (#255)`
