# Template lint (`ValidateTemplate`) — design

Date: 2026-11-25. Issue: #255. Related: #253 (first lint rule, merged), #272 (`:join`), #258 (inline loops).

## Intent

`ValidateTemplate` becomes a lint template authors and CI pipelines can rely on: **if it reports no problems, the
template renders as written.** It matters most where templates are written by people who never see the engine, e.g.
customers uploading templates into an application built on Templify (a downstream document generator validates
uploads with it). Today several shapes pass validation and then render raw marker text, lose content or fail
processing.

## Goals

1. Every rule below in **Word and OpenDocument** (or the doc states why a rule does not apply to one format).
2. **Same detectors as processing**, run over **the same containers** processing visits (see Approach).
3. Every error and warning carries a human-readable `Location` (format below) — new and existing ones.
4. Severity: something that certainly renders wrongly is an **error**; something that may be intended is a
   **warning**; `IsValid` depends only on errors.
5. One test per rule per format, each proving the rule fails on a template that breaks it.

## Non-goals

- OOXML schema validity of the author's markup (that is `OpenXmlValidator`'s job; processing must not *introduce*
  schema errors — #254).
- Layout or design rules of a particular document family.
- A **structured** location (part, indexes as properties): decided against for now (maintainer). Interesting later
  for editing templates in a frontend (highlighting); the string format below is designed so a structured type can be
  added next to it without changing it.
- Inline loops (#258); `ValidateTemplate` keeps reporting them as `BlockMarkerWithText`.
- **Placeholders in field results** (the issue's finding 5). Dropped by the maintainer after the builder check:
  processing replaces placeholders in cached field results and in headings, so the issue's example (a TOC entry built
  from a heading with `{{…}}`) renders correctly after a TOC update. Values are lost only in fields Word recomputes
  from their code (`MERGEFIELD`, `DOCPROPERTY`, `DATE`, `PAGE`, …), and a block marker inside a field result could
  break the field — neither occurs in the example or known consumer templates. A separate issue if it ever matters.

## Anti-goals

- A rule that reports lossless templates (false positives) — every rule gets negative tests, including the
  shapes #253's review found lossless (whitespace/NBSP/format characters around markers, inline conditionals,
  markers split across runs, `examples/templates`, the embedded warning-report template).
- A traversal that is exponential in nesting (lesson from #253: one linear walk).

## Approach (decided)

Considered:
1. Add each rule to the existing walks (Word: `TemplateValidator` steps, `BlockMarkerValidator`,
   `ScopedVariableValidator`; ODT: `SyntaxWalker`, `ScopeWalker`). Cheap per rule, but locations would be computed in
   every walk and the walks keep drifting from processing — the drift is how two of the three pre-existing bugs below
   arose.
2. **One lint walk per format** that mirrors the processing walker's container structure (as `BlockMarkerValidator`
   in #253 already does, and the ODT `SyntaxWalker` nearly does), carries the location path, and runs **all
   structural rules** there, including the detectors' syntax errors, condition and inline-expression syntax. The data
   checks (`ScopedVariableValidator`, ODT `ScopeWalker`) stay separate but report locations. **Picked.**
3. Validation as a dry run of the real processing pipeline (expand every loop once, every branch). Strongest
   agreement, but changes the production engine for validation in two formats; highest cost and risk.

### Lint walk

- Word: replaces `BlockMarkerValidator` (#253) and takes over the structural steps of `TemplateValidator`
  (`ValidateConditionals`, `ValidateLoops`, `ValidateTableRowLoops`, `ValidateConditionExpressions`,
  `ValidateInlineExpressions`); `FindAllPlaceholders` / `AllPlaceholders` behavior stays as is. Containers exactly as
  `DocumentWalker`: body, headers/footers, normal footnotes/endnotes, table row lists (incl. `SdtRow`), cells of
  non-marker rows, `SdtBlock` content, text boxes of paragraphs, loop content (walked once, skipped in the parent
  list), inline-conditional paragraphs kept (their text boxes walked).
- ODT: the existing `SyntaxWalker` becomes the lint walk (it already mirrors `OdtTemplateEngine`); add locations and
  the new rules.
- Content processing never visits (Word block-level `w:customXml` and similar wrappers; ODT annotations and tracked
  deletions): the existing all-paragraph checks of condition and inline-expression syntax and the `AllPlaceholders`
  collection keep scanning it as today (no reported error disappears); the new rules do not look there.
- Detector `TemplateSyntaxException`s found in **any** container processing visits become errors (today the Word
  validator only detects at body level and in loop content, which is how the pre-existing bugs pass). Each problem is
  reported once (dedupe as the ODT `SyntaxWalker` does today: by marker / message).
- Messages of existing errors stay **word for word** (consumers may show them); only `Location` changes.

## Location format (decided: a string in the existing `Location`)

`<part>, <path>: '<text>'` — indexes 1-based within their parent, text whitespace-collapsed and cut at 100
characters with `…` (as `BlockMarkerText.GetLocation`).

```
body, paragraph 12: 'Hello {{Na'
body, table 1, row 2, cell 3, paragraph 1: 'Intro {{#if Flag}}'
body, table 1, row 4: '{{#foreach Items}} Header'                  (row-level problem: row text)
header (default), paragraph 1: '…'                                  (Word: header type default/first/even)
header 2 (default), paragraph 1: '…'                                (a second header part of the same type)
footnote 2, paragraph 1: '…'                                         (normal notes counted in document order)
body, paragraph 3, text box 1, paragraph 2: '…'
body, content control 1, paragraph 2: '…'                            (block content control)
header (Standard), paragraph 1: '…'                                  (ODT: master page name; "header left", "header first")
body, list 1, item 3, paragraph 1: '…'                               (ODT lists)
```

- Paragraph numbers count paragraphs (Word `w:p`; ODT `text:p`/`text:h`) among the parent's children; tables, text
  boxes, content controls, lists and sections are counted per kind.
- Problems inside loop content are located at the template element (one location per problem, not per iteration).
- For errors that have no element (a document that cannot be read), `Location` stays `null`.
- Parity: for the same paragraphs, Word and ODT produce the same `Type`, `Message` **and `Location`** in body
  paragraphs and tables (`OdtValidationParityTests` compares `Location` too).

## Rules

| # | Rule | Severity | Type (new members are additive public API) |
|---|------|----------|------|
| R1 | **Unterminated tag in a paragraph**: text that starts a tag — `{{` followed by a name character, `@`, `.`, `#`, `/` or `(` — with no `}}` after it in the same paragraph's text (a placeholder or block marker split across paragraphs, e.g. `Hello {{Na` · `me}} world`, `{{#if` · `Flag}}`). One error per paragraph, naming the text. A stray `}}` and `{{` not followed by a tag character (JSON or code samples such as `{"a":{"b":1}}`, which render as written) are **not** reported. | error | `ValidationErrorType.UnterminatedTag = 9` |
| R2 | **Block marker with text** (#253, unchanged) | error | `BlockMarkerWithText` (existing) |
| R3 | **More than one block marker in one marker paragraph/row**, outside an inline conditional (e.g. `{{#if Flag}}{{#foreach Items}}` … `{{/foreach}}{{/if}}`, `{{/if}}{{/foreach}}`). Processing removes the element once and loses the other block (raw `{{Name}}` in the output, or a processing failure). Exception: an inline loop is R2. | error | `ValidationErrorType.MultipleBlockMarkers = 10` |
| R4 | **Named loop variable used outside its loop**: a placeholder, condition variable or inline-expression variable whose root segment is the iteration variable of some `{{#foreach x in …}}` in the template, used where no enclosing loop declares `x`. **Without data:** only where no loop encloses the use at all (inside any loop the root may be a property of the current item, which `LoopContext` resolves). **With data:** whenever the root resolves neither globally nor as a property of an enclosing loop's items (the scope `ScopedVariableValidator` already computes); the error **replaces** today's `MissingVariable` error for that name (one, more specific error), and the name is still listed in `MissingVariables`. | error | `ValidationErrorType.LoopVariableOutOfScope = 11` |
| R5 | **Row loop with both markers in one row**: same error type as today (`UnmatchedLoopStart`), message extended: "… Loop markers in a table must be in rows of their own; a row with both `{{#foreach}}` and `{{/foreach}}` is not supported." Processing fails with the same, improved message (same detector). | error | `UnmatchedLoopStart` (existing) |
| R7 | **Detector syntax errors in every container** (unmatched markers, `{{#elseif}}` after `{{#else}}`, invalid iteration variable) — fixes the pre-existing gaps: a content control holding `{{#if Flag}}` plus another paragraph with `{{/if}}` outside it (Word); a nested `{{/if}}{{/foreach}}` marker paragraph inside a loop (Word; R3 also applies). | error | existing types |
| R9 | **Orphan end and branch markers**: `{{/if}}`, `{{/foreach}}`, `{{#else}}`, `{{#elseif}}` outside any block (issue #255 finding 2: "the orphaned `{{/if}}` is also unmatched"). Processing leaves them as text and, in Word, skips placeholder replacement in that paragraph. Uses the existing, so far unused, types; found by the lint walk (the detectors skip them), in the same containers. `OdtValidationParityTests` "orphan end markers" now expects these errors in both formats. | error | `UnmatchedConditionalEnd` / `UnmatchedLoopEnd` (existing) |
| R8 | **`:join` by an item property that no item has** (`{{Assets:join:Nmae}}`), with data, in scope (inside loops against the loop items' sub-collections). Same rule as processing (`JoinFormat.IsItemPathMissingOnAllItems`). | error | `MissingVariable` (existing), name `Assets.Nmae` |

The three pre-existing gaps from the #253 review map to R3 and R7: every one must have a regression test that fails
today.

## Rollout (separate PRs, in this order)

1. **Lint walk + locations + R1, R3, R7, R9** (Word and ODT), moving the structural steps into the walk; locations for
   all structural errors. Parity tests extended to `Location`.
2. **R4** loop variable scope.
3. **Locations for data checks + R8** (`ScopedVariableValidator`, ODT `ScopeWalker`: missing variables,
   `EmptyLoopCollection`, `ReservedWordAsVariable`, `MissingConditionVariable` get locations; `:join` property).
4. **R5** row-loop message (small; may ride along with PR 1 if trivial).

Each PR: public API impact stated, release-notes entry, LibreOffice round trips (ODT code is touched), docs.

## Behavior changes (release notes)

- Templates matching R1, R3, R4, R5, R7, R8, R9 no longer pass `ValidateTemplate` (`IsValid == false`).
- `Location` of existing errors and warnings now holds the location string (before: `null` or the marker text).
- R5 changes the text of one processing error message (`UnmatchedLoopStart` for same-row loops).
- New enum members: `UnterminatedTag`, `MultipleBlockMarkers`, `LoopVariableOutOfScope`.
- Downstream generator: its customer templates likely contain #253 shapes (and maybe R1/R3); warn that team before
  they upgrade.

## Left to the builder

- Class names and file layout of the lint walk and the location path (a small immutable path type pushed/popped
  while walking is suggested); how the ODT `SyntaxWalker` is renamed.
- How R1 scans text (a regex over the paragraph's own text from `ParagraphTextModel`/`OdtParagraphTextModel`).
- Exact wording of new messages (one sentence stating what is wrong and how to fix it, in the style of #253's).
- Test layout (one class per rule per format, plus parity entries).

## Calls made without the maintainer (check these)

- Location grammar above (part names, 1-based, per-kind counters, row-level text, `header 2 (default)` for repeated
  parts, ODT master page names).
- R1 flags only an unterminated tag start (`{{` + tag character); stray `}}` and other braces are not reported (builder
  check: JSON/code samples would otherwise be permanently invalid).
- R4: without data only outside all loops; with data it replaces the `MissingVariable` error for that name (builder
  check: a root can be an item property in any loop).
- R9 orphan markers are in scope, as errors with the existing unused types (issue #255 expects it; builder check).
- Content processing never visits keeps today's syntax checks; no new warning for it (builder check).
- R3 as its own error type rather than reusing `BlockMarkerWithText`.
- R4 with data: no error when the root resolves globally.
- R8 is an error of type `MissingVariable` (consistent with other missing variables in validation with data).
- Rollout order: R7 (pre-existing gaps) moves into PR 1 because the lint walk fixes them by construction.
