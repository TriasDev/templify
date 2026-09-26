# Design: OpenDocument Text (.odt / .ott) support

- **Issue:** [#138](https://github.com/TriasDev/templify/issues/138)
- **Date:** 2026-09-26
- **Status:** Accepted. The maintainer decisions from 2026-09-26 (issue comment) are binding. This spec records how they are implemented.

## 1. Goal and binding decisions

Templify processes `.odt` templates (LibreOffice, Collabora, OpenOffice) with the same template syntax it supports for `.docx`: placeholders with format specifiers, block and inline conditionals (including the 1.7.0 operator engine), loops, table-row loops, headers and footers, and markdown.

Decisions from the maintainer:

- **API.** A dedicated public `OdtTemplateProcessor` with the same method shapes as `DocumentTemplateProcessor`: `Stream`/`Stream` with `Dictionary`, `IReadOnlyDictionary` and JSON overloads, `byte[]` with `out byte[]`, `ProcessTemplateFile`, and `ValidateTemplate`. It uses the same `PlaceholderReplacementOptions`, `ProcessingResult` and warnings. A universal facade that detects the format from the package `mimetype` follows later as a separate new class. `DocumentTemplateProcessor` keeps its behavior. All changes are additive and ship in 1.9.0.
- **Scope.** Full parity with DOCX, delivered in small sequential PRs (section 7).
- **Formats.** `.odt` and `.ott`. The output is always `.odt`. `.fodt` (flat XML) is out of scope.
- **Dependencies.** No new NuGet dependency. The package is handled with `System.IO.Compression` and the XML with `System.Xml.Linq`.
- **Verification.** Tests check the ZIP, the manifest and the `mimetype` of every generated document. Documents are also opened with LibreOffice headless, locally.

## 2. ODF structure (what the implementation relies on)

An ODT document is a ZIP package:

| Entry | Content | Used for |
|---|---|---|
| `mimetype` | `application/vnd.oasis.opendocument.text` (`…-text-template` for `.ott`) | format detection. It must be the **first** entry, **stored** (uncompressed), with no extra field. |
| `content.xml` | `office:document-content`, with `office:automatic-styles` and `office:body/office:text` | the body |
| `styles.xml` | `office:styles`, its own `office:automatic-styles`, and `office:master-styles/style:master-page` | headers and footers (`style:header`, `style:footer`, `style:header-left`, `style:footer-left`, `style:header-first`, `style:footer-first`) |
| `meta.xml` | `office:meta` (`dc:title`, `meta:initial-creator`, …) | document properties (PR 5) |
| `META-INF/manifest.xml` | `manifest:file-entry` per entry. The entry with `full-path="/"` carries the package media type. | kept consistent with `mimetype` |
| `settings.xml`, `manifest.rdf`, `Pictures/*`, `Thumbnails/*`, `Configurations2/` | other entries | copied unchanged |

### 2.1 Element mapping

| Concept | DOCX (OOXML) | ODT (ODF) | Handling in Templify |
|---|---|---|---|
| paragraph | `w:p` | `text:p`, `text:h` (heading, `text:outline-level`) | a paragraph. `text:h` is handled exactly like `text:p`. |
| run and text | `w:r` / `w:t` | `text:span` (nestable, formatting via `text:style-name`), text nodes directly in the paragraph | a span is an inline container, and its text nodes are text segments |
| spaces | `xml:space="preserve"` | `text:s text:c="n"` (the 2nd and later spaces of a sequence) | an *atom* that contributes `n` spaces to the paragraph text |
| tab | `w:tab` | `text:tab` | an atom that contributes `\t` |
| line break | `w:br` | `text:line-break` | an atom that contributes `\n`. Newlines in values become `text:line-break`. |
| page-break hint | `w:lastRenderedPageBreak` | `text:soft-page-break` | a kept anchor (zero length) |
| bookmark | `w:bookmarkStart/End` | `text:bookmark`, `text:bookmark-start`, `text:bookmark-end` | a kept anchor |
| hyperlink | `w:hyperlink` | `text:a` | an inline container |
| fields | `w:fldSimple`, complex fields | `text:date`, `text:page-number`, `text:variable-get`, `text:user-defined`, … (the element holds the displayed value) | an opaque anchor with no text of its own. It is removed when it lies strictly inside a replaced range. |
| footnote/endnote | separate part | `text:note` inline in the paragraph (`text:note-citation`, `text:note-body`) | an opaque anchor in the paragraph. `text:note-body` is walked as a block container. |
| comment | separate part | `office:annotation` / `office:annotation-end` | a kept anchor. The content is not processed, as for DOCX. |
| image / text box / shape | `w:drawing`, VML, `wps:txbx` | `draw:frame` (`draw:image`, `draw:text-box`), `draw:custom-shape`, … | an opaque anchor. A `draw:text-box` or a shape with paragraphs is walked as a block container. |
| table | `w:tbl`/`w:tr`/`w:tc` | `table:table` / `table:table-row` / `table:table-cell` | rows can be grouped in `table:table-header-rows`, `table:table-rows`, `table:table-row-group`. `table:covered-table-cell` is the covered part of a merged cell. |
| repeated rows/cells | none | `table:number-rows-repeated`, `table:number-columns-repeated` | a repeated row stands for identical rows, so it is cloned or removed as a unit, keeping the attribute; no expansion is needed (decided in PR 3). |
| list | numbering properties on `w:p` | `text:list` / `text:list-item` / `text:list-header` | a list item is a block container. A list is a sequence of items, like table rows (PR 4). |
| section | `w:sectPr` | `text:section` | a block container |
| table of contents and other indexes | field + result paragraphs | `text:table-of-content` etc. with `text:index-body` | the index body is walked like DOCX result paragraphs |
| tracked changes | `w:ins`/`w:del` | `text:tracked-changes` (deleted content) + `text:change-start`/`text:change-end` marks | deleted content is never walked. The marks are kept anchors. |
| run formatting | inline `w:rPr` | `text:span text:style-name` → `office:automatic-styles` | markdown creates or reuses automatic text styles (PR 5) |
| core properties | `docProps/core.xml` | `meta.xml` | PR 5 |

### 2.2 Whitespace

ODF collapses white space like HTML (ODF 1.2 part 1, §6.1.2), and LibreOffice applies this across span boundaries. We checked this with LibreOffice 25 headless:

| XML | Rendered |
|---|---|
| `<text:p>  lead</text:p>` | `lead` (leading literal spaces are dropped) |
| `a <text:span>  b</text:span>` | `a b` (the sequence continues across spans) |
| `a<text:s/> b` | `a  b` (a literal space after `text:s` is kept) |
| `x<text:tab/> y`, `x<text:line-break/> y` | the space is kept |
| `nl\n   z` | `nl z` |

The paragraph text model keeps XML text 1:1. Offsets map directly to text node characters, and tabs, CR and LF inside text nodes are seen as spaces. The model does not collapse. When the writer inserts replacement text, it encodes the text so that it renders exactly. A space becomes `text:s` when the previous rendered character is a literal white space, or when it is at the start of the paragraph. `\t` becomes `text:tab`. A trailing space before a literal space becomes `text:s`. Removing text (a missing variable with `ReplaceWithEmpty`, inline conditional markers) can bring two literal spaces together, and they then render as one. DOCX would show two spaces in that case. This small difference is accepted and documented.

## 3. Architecture: a second, ODT-specific set of internals

### 3.1 Options considered

1. **A generic document abstraction over both formats.** Examples are an `IDocumentTree` or an `IParagraph` interface, with the current DOCX walker, visitors and detectors rewritten against it. This is the cleanest end state, but it rewrites `DocumentWalker`, all visitors, `ConditionalDetector`, `LoopDetector`, `ParagraphTextModel` and `ParagraphTextRewriter`. Those classes have been hardened by about 1,200 tests and a long list of fixed edge cases (#140, #178, text boxes, content controls, fields). Every DOCX edge case would be at risk.
2. **A second, OpenDocument-specific set of internal classes.** These classes reuse every format-agnostic component unchanged. The DOCX code is not touched.

**Chosen: option 2.** It is the only option that guarantees the DOCX path does not change behavior. The format-agnostic engine is already separate and is reused as is:

- condition engine (`ConditionalEvaluator`, `Conditionals/Engine/*`), `InlineConditionalParser`, `ConditionalPatterns`
- `PlaceholderScanner`, `ExpressionPlaceholderEvaluator`, `ValueConverter`, `BooleanFormatterRegistry`, `TextReplacements`, `XmlCharacterSanitizer`
- `IEvaluationContext`, `GlobalEvaluationContext`, `LoopContext`, `LoopEvaluationContext`, `PropertyPathResolver`, `ValueResolver`
- `ReplacementContent` (text pieces, line breaks, markdown flags) and `MarkdownParser`
- `ProcessingResult`, `ProcessingWarning`, `WarningCollector`, `MissingVariableException` and the other template exceptions, `JsonDataParser`

The ODT code duplicates only the thin structural layer: marker detection over `XElement` siblings and the paragraph text model. Once both implementations exist and are proven, a later refactoring can unify the detectors behind a generic `IReadOnlyList<T>` plus a text-function scanner. That refactoring is optional and not part of #138.

### 3.2 No visitor indirection

The DOCX pipeline uses visitors with a composite and a circular reference, for historical reasons. The ODT engine keeps the **same processing order and semantics**, but as one internal class, `OdtTemplateEngine`, that calls its steps directly:

```
ProcessContainer(blocks, context):
  1. detect loops (to know which blocks are inside loops)
  2. detect conditionals, evaluate deepest first, skip those inside loops      (PR 2)
  3. expand loops: clone content per item, ProcessContainer(clones, loopCtx)    (PR 3)
  4. for each remaining block:
       paragraph → walk its nested containers (text boxes, notes), then its placeholders
                   (skipped for marker paragraphs, as in DOCX)
       table     → table-row conditionals/loops, then cells → ProcessContainer
       list      → items as rows (PR 4), each item → ProcessContainer
       section, index body, numbered paragraph, page-anchored frame → ProcessContainer
```

This mirrors `DocumentWalker.WalkElements`/`WalkRows`, including the #140 rule: rows produced by a loop are processed only with the loop context and are never walked again with the outer context.

### 3.3 Components (all `internal`, namespace `TriasDev.Templify.OpenDocument`)

| Class | Responsibility | PR |
|---|---|---|
| `OdfNames` | namespaces and `XName` constants | 1 |
| `OdtPackage` | reads the ZIP (any readable stream) and validates the media type (`mimetype`, with a fallback to the manifest root entry). It loads and saves XML parts (`LoadOptions.PreserveWhitespace`, DTDs prohibited, no resolver; written as UTF-8 without BOM, no indentation, newlines entitized). It writes the package with `mimetype` first and stored, followed by the other entries in their original order, and saves only the parts that changed. For `.ott` it rewrites `mimetype` and the manifest root media type to `.odt`. It rejects encrypted packages with a clear message and removes `META-INF/documentsignatures.xml`, because a processed document can no longer match its signature. | 1 |
| `OdtParagraphTextModel` | a snapshot of a paragraph's own text. It holds *pieces* (text nodes; atoms for `text:s`, `text:tab` and `text:line-break`) and *anchors* (zero-length elements such as bookmarks, fields, frames, notes and annotations). Inline containers (`text:span`, `text:a`, `text:meta`) are entered. | 1 |
| `OdtParagraphTextRewriter` | `Replace(paragraph, start, end, ReplacementContent)` and `Remove(paragraph, ranges)`. The replacement is inserted where the first replaced character was, inside the same spans, so it takes that formatting ("the run where the placeholder starts"). Anchors strictly inside the range are removed if they are content (fields, frames, notes, tabs) and kept if they are markup (bookmarks, annotations, change marks, soft page breaks). Spans and links left empty are pruned. A `text:s` that is partly covered keeps the rest of its count. | 1 |
| `OdtPlaceholderProcessor` | resolves and replaces one placeholder. This is the ODT counterpart of `PlaceholderVisitor`, with the same resolution, formatting, `TextReplacements`, sanitizing, `MissingVariableBehavior` and warnings. | 1 |
| `OdtTemplateEngine` | the walker and step orchestration (3.2) for `content.xml` (`office:body/office:text`) and `styles.xml` (master-page headers and footers) | 1, extended by 2–4 |
| `OdtMarkerText` | the marker text of a block (paragraph, list item, row, cell), excluding nested text boxes and notes. This is the counterpart of `TemplateElementText`. | 2 |
| `OdtConditionalDetector`, `OdtConditionalBlock` | block and table-row conditionals over `XElement` siblings. They reuse `ConditionalPatterns` and have the same errors and messages as `ConditionalDetector`. | 2 |
| `OdtLoopDetector`, `OdtLoopBlock` | body, table-row and list-item loops. They reuse the `LoopDetector` patterns and name validation. | 3 |
| `OdtLoopDetector`, `OdtLoopBlock` (row loops) | a repeated row is cloned and removed as a unit, and covered cells are cloned with their row (no separate component needed) | 3 |
| `OdtUniqueNames` | makes `draw:name` of frames and shapes, `table:name`, the `text:name` of sections and the `text:id` of notes unique after cloning, and drops duplicate `xml:id`s (the counterpart of `DrawingIdAllocator`, #178). Duplicate names make LibreOffice rename objects on load and break references. | 4 |
| `OdtTextStyles` | the automatic-style registry per part (`content.xml` and `styles.xml` each have their own). It creates or reuses `T…` text styles for bold, italic, strikethrough and their combinations. | 5 |
| `OdtTemplateValidator` | the ODT counterpart of `TemplateValidator` / `ScopedVariableValidator` | 5 |

### 3.4 Public API

`TriasDev.Templify.Core.OdtTemplateProcessor` (sealed), declared in `PublicAPI.Unshipped.txt`:

- `OdtTemplateProcessor(PlaceholderReplacementOptions? options = null)`
- `ProcessTemplate(Stream, Stream, Dictionary<string, object>)`, `(Stream, Stream, IReadOnlyDictionary<string, object?>)` and `(Stream, Stream, string json)`
- `ProcessTemplate(byte[], Dictionary<string, object>, out byte[])`, and the same with `IReadOnlyDictionary` and with JSON
- `ProcessTemplateFile(string, string, …)` × 3
- `ValidateTemplate(Stream)`, `(Stream, Dictionary)`, `(Stream, IReadOnlyDictionary)`, from PR 5

Contract differences from `DocumentTemplateProcessor`, all of them relaxations:

- The output stream only has to be **writable**. The document is built in memory and written in one go when processing succeeds. The DOCX processor edits in place and needs a readable, writable and seekable stream.
- On failure nothing is written to the output stream.

Options that do not apply to ODT:

- `UpdateFieldsOnOpen`: LibreOffice recalculates page and date fields on load. Indexes are not updated automatically, and ODF has no portable "update on open" flag. The option is ignored, and its documentation says so.
- `DocumentProperties` is applied to `meta.xml` in PR 5.

## 4. Placeholder replacement (PR 1)

- Placeholders are found in the paragraph's own text (`PlaceholderScanner`) and replaced right to left, so earlier offsets stay valid.
- **Split placeholders** (`{{Na` + `<text:span>me}}</text:span>`, or across `text:a`) are handled by the text model. The replacement goes into the container of the first character.
- `text:s` inside a placeholder cannot happen: placeholder names contain no spaces. `text:s` inside expression placeholders (`{{(A  and B)}}`) and in conditions works, because `text:s` contributes its spaces to the model text.
- Values are converted with `ValueConverter` (`Culture`, format specifiers, boolean formatters). `TextReplacements` are applied, then `XmlCharacterSanitizer`. With `EnableNewlineSupport`, `\r\n`, `\r` and `\n` become `text:line-break`.
- `EnableMarkdown` and `:raw`: until PR 5, markdown syntax in values is inserted as literal text. From PR 5, it is rendered with automatic styles.
- Conditional and loop markers are left in the text unchanged until PRs 2 and 3.
- Headers and footers (`styles.xml`), footnotes and endnotes, text boxes, lists, sections and index bodies are walked for placeholders from PR 1, because the generic container walk covers them for free. Block constructs inside them follow in PRs 2–4.

## 5. Markdown via automatic styles (PR 5)

- A formatted piece becomes `<text:span text:style-name="T{n}">…</text:span>`, nested **inside** the span that holds the placeholder. ODF nested spans combine their properties, so a red template span plus markdown bold gives red bold text. This matches the DOCX "merge, not replace" rule.
- Styles are `style:style style:family="text"` with `style:text-properties`: `fo:font-weight="bold"` (plus `style:font-weight-asian` and `-complex`), `fo:font-style="italic"` (plus the asian and complex variants), and `style:text-line-through-style="solid"`.
- The registry scans the part's `office:automatic-styles` for an existing text style with exactly these properties and reuses it. Otherwise it creates a new name `T{n}` that is not used in the part. Headers and footers in `styles.xml` get their styles in `styles.xml`.

## 6. Test strategy

- **Builder and verifier** (`TriasDev.Templify.Tests/Helpers/OdtDocumentBuilder`, `OdtDocumentVerifier`). The builder writes a minimal, valid ODT: content, styles with a master page, meta and manifest, optionally as `.ott`. It has a fluent API for paragraphs, headings, spans, tables, headers and footers, and takes raw XML fragments for special cases. The verifier reads the rendered paragraph text (it applies the whitespace rules of 2.2, `text:s`, tabs and line breaks) and the XML parts, and it checks the package structure.
- **Structural tests on every output.** `mimetype` is the first entry, stored, with no extra field and no data descriptor, and has the right content. The manifest root media type matches. `content.xml` and `styles.xml` are well-formed. `.ott` produces `.odt`. Other entries are copied byte for byte.
- **Behavior tests** in `TriasDev.Templify.Tests/Odt/`, one class per feature and PR, mirroring the DOCX integration tests.
- **LibreOffice round trip** (`[Trait("Category", "LibreOffice")]`). Each test converts the output with `soffice --headless --convert-to txt` (and pdf) and compares the text. It runs only when `soffice` is found (the macOS default path, `PATH`, or the `TEMPLIFY_SOFFICE` variable), and is skipped otherwise with `Assert.Skip`. Each conversion uses its own `-env:UserInstallation` profile, so the three TFM test processes can run concurrently.
- **CI decision:** no LibreOffice job for now. Installing `libreoffice-writer` on ubuntu adds about 1–2 minutes and about 300 MB per run. Headless startup is occasionally slow or hangs on shared runners, which would make CI flaky. The structural tests catch the package-level errors that would make LibreOffice reject a file. If round trips prove stable locally over PRs 1–5, a dedicated follow-up PR can add an opt-in job (`workflow_dispatch` or nightly) that installs `libreoffice-writer` and runs `--filter Category=LibreOffice`.
- The DOCX suite must stay green, unchanged.

## 7. Phase plan

The maintainer's list names "internal document abstraction" as step 1. Because of the decision in 3.1, that step is the ODT-internal model (package, text model, rewriter) and ships together with placeholders in PR 1.

| PR | Content |
|---|---|
| 1 | This spec. `OdtPackage`, `OdtParagraphTextModel`, `OdtParagraphTextRewriter`, `OdtTemplateEngine` with placeholders everywhere (body, headings, tables, lists, sections, notes, text boxes, headers/footers), and the public `OdtTemplateProcessor` (process overloads). |
| 2 | Conditionals: block (paragraph-level, across any block containers), inline (same paragraph, including elseif/else and nesting), table-row, and **list-item** (see 8.1), with the same warnings and errors. Cells, notes, text boxes and headers/footers that end up empty get an empty `text:p`. A list item left without content, and a list, table or header-row group left without items or rows, is removed. |
| 3 | Loops: body loops with implicit and named iteration variables, metadata (`@index`, `@number`, `@first`, `@last`, `@count`), nested loops, null items and missing or null collections with warnings, and `WarnOnEmptyLoopCollections`. Table-row loops and conditionals, with repeated rows expanded and covered cells. |
| 4 | Container integrity: loops and conditionals in headers/footers, notes, text boxes, sections and nested lists, and empty-container fixes. List-item loops and conditionals shipped earlier, in PRs 2 and 3. **Name uniqueness after cloning** (`OdtUniqueNames`) covers `draw:name` of frames and shapes, `table:name`, the `text:name` of sections and the `text:id` of notes. The first occurrence keeps its name, and duplicates get `_2`, `_3`, … Duplicate `xml:id`s are removed from the later copies. Body and headers/footers share one name space. This was moved forward from PR 5. |
| 5 | Markdown via automatic styles, `EnableMarkdown` and `:raw`. `ValidateTemplate` for ODT. `DocumentProperties` → `meta.xml`. |
| later (lead) | The universal facade, docs and examples, converter and GUI support, and an optional LibreOffice CI job. |

## 8. Known limitations and open questions (conservative defaults chosen)

### 8.1 Lists (decided in PR 2)

In Word a list item is just a numbered paragraph, so markers can be list paragraphs. In ODF, list items are nested containers (`text:list/text:list-item/text:p`). The rules:

- Within a list, markers in their own items work like table rows. The marker items are removed, and the items between them are kept or removed as a whole. A conditional confined to one item is processed inside the item.
- LibreOffice stores a lone bullet between normal paragraphs as a list of its own. A list with a **single item** therefore carries marker text at the enclosing level, so `{{#if}}` as a one-item list, followed by paragraphs and `{{/if}}`, works as in Word.
- A marker inside a longer list must be matched **within that list**. Otherwise processing fails with the usual "has no matching `{{/if}}`" error. This is the conservative choice: an explicit error rather than guessing which items to remove.

- Collapsing whitespace after a removal (2.2) is accepted.
- A digital signature (`META-INF/documentsignatures.xml`) is removed, because the output content no longer matches it. Macro signatures are kept.
- An encrypted (password-protected) package fails with a clear error.
- The thumbnail (`Thumbnails/thumbnail.png`) is copied unchanged. LibreOffice regenerates it on save.
- Indexes (table of contents) are not regenerated. Placeholders in their cached body are replaced like normal text.
- `.fodt` is not supported (a failed result with a clear message).
- Bookmark and annotation names cloned by loops are not renamed. They come in start/end pairs that would have to be renamed consistently. LibreOffice tolerates duplicates by renaming them on load. The same applies to DOCX, where loop-cloned bookmarks are not renamed either.
