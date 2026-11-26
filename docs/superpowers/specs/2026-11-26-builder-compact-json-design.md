# Builder: compact JSON definitions (design)

Status: draft for approval · 2026-11-26 · epic #261, follows phase 3 (#274)
Amends: `2026-11-25-builder-phase3-json-and-typed-data-design.md`, section 4.1 (shape). Everything else in that spec
(import pipeline, diagnostics, components, typed paths, determinism, round trip) stays as it is.

## 1. Intent

The phase 3 evaluation showed that the exported JSON is hard to read and expensive for agents: the sample is 717 lines
and ~18,000 characters, a table cell holding one value takes 12–14 lines, and `"style": "BodyCopy"` repeats on almost
every paragraph. The maintainer looked at the file and chose to make the format compact before going further
(proposal 1 of the evaluation). Translation keys (proposal 2) and a source generator (proposal 4) are separate work.

A throwaway spike (Python reformatter, `~/Desktop/templify-builder-phase3/proposal-1/`) measured the syntactic part:

| | lines | characters |
|---|---|---|
| phase 3 export | 717 | 18,461 |
| compact layout only | 425 | 13,500 |
| layout + short forms | 343 | 10,782 |

The semantic shortcuts below (placeholders in strings, default paragraph style, table text styles) are expected to cut
the sample further, to roughly 200 lines; the real number goes into the evaluation.

## 2. Goals, non-goals, anti-goals

Goals
- A definition that a person or an agent can read top to bottom: one line per simple block, the text of a sentence
  visible as one string.
- The same model as today: every JSON form maps to something the C# API can express, and C# → JSON → build stays
  byte-identical to building from C# (every builder test keeps round-tripping through JSON).
- The C# API gets the two style features too (default paragraph style, table text styles), so C# definitions shrink
  the same way.

Non-goals
- Backwards compatibility with the phase 3 shape. Nothing was released; `templifyDefinition` stays `1` and the reader
  accepts only the new shape.
- Conditions or loops inside strings. `{{#if}}`/`{{#foreach}}` stay structural objects; the JSON stays a tree, not a
  second template language.
- Translation keys, JSON Schema, YAML.

Anti-goals
- Two equally valid ways to write the same thing where one suffices: agents copy whatever they see, and every
  alternative doubles what the reader, the docs and the diagnostics must cover.
- A shortcut that makes the output depend on where a block sits in a way the file does not show (the style
  inheritance in 4.3 is the one exception, and its order is fixed and documented).

## 3. Approaches (decided in the conversation, 2026-11-26)

1. **Overall scope** — A: layout only; **B: layout + short forms (chosen)**; B + semantic shortcuts discussed one by
   one below. B because the format is unreleased and readability, not only size, was the problem.
2. **Values in text** — a: keep `{"value": …}` objects; **b: Templify placeholders `{{…}}` inside strings (chosen)**.
   The builder already rejects `{{` in literal text (`InlineComposer`), so `{{` in a JSON string can only be a value.
3. **Default paragraph style** — a: none (unstyled = Normal); **b: a named default in the style sheet (chosen)**;
   c: make Normal itself the body style. b keeps the named style, is opt-in and symmetric in C# and JSON.
4. **Table text** — **a: text styles on the table style, override on the row (chosen)**; b: on each table; c: both.
   a because text formatting is part of a table's design and is reused like its rules and background.

## 4. The format

### 4.1 Layout (writer)

- Two-space indentation. An object or array whose single-line form fits in **100 characters** (counting indentation and
  its key) is written on one line, `{"a": 1, "b": 2}` / `["x", "y"]` with a space after `,` and `:`; otherwise each
  member goes on its own line and the rule applies again one level down. Strings are never wrapped.
- Deterministic: the same model always gives the same text.
- Key order: options first, then the key holding the payload — `{"style": "SectionHeading", "paragraph": "Kunde"}`,
  `{"style": "LineGrid", "columns": […], "table": […]}`. Blocks whose kind key holds a head (`if`, `forEach`,
  `component`, `forEachRow`, `rowsIf`, `group`) keep the head first, as today.

### 4.2 Text with values

Wherever inline content is expected (paragraph content, list item content, inline `if` branches, cell content given as
a string), a string is **text with placeholders**:

```json
"Auftrag {{order.number}} · {{order.date:date:d. MMMM yyyy}}"
"Kostenloser Expressversand: {{(order.isExpress and order.total > 1000):yesno}}"
```

- `{{path}}`, `{{path:format}}` and `{{(condition)}}`, `{{(condition):format}}` — the syntax Templify itself reads,
  with the builder's own path, format and condition parsers (the builder uses only the core's public API).
- Inline content is a string or an array of pieces. A piece is a string (text with placeholders) or an object:
  - `{"text": "…", "style": "MutedText"}` — styled text; the string may contain placeholders, every resulting piece gets
    the character style (`{"text": "{{order.number}}", "style": "StrongText"}` is a styled value);
  - `{"if": …, "then": …, "else": …}` — inline condition; branches are inline content (string or array);
  - `{"tab": true}`, `{"tabRight": true}`, `{"lineBreak": true}`, `{"pageNumber": true}`, `{"pageCount": true}`,
    `{"image": …}` — unchanged.
- The `value` and `expression` objects are removed (anti-goal: one way per thing).
- Errors: a malformed placeholder (`{{` without `}}`, a bad path, an unknown format, a bad condition) is TB9200/TB9201
  at the string's pointer, the message quoting the placeholder; unknown paths and wrong kinds stay TB9203 per
  placeholder. `}}` without `{{` stays literal text, as in Templify.
- Export merges a run of same-styled text and value pieces into one string, **only where import splits it back into the
  same pieces**: two adjacent text inlines stay separate array entries, so the round trip stays byte-identical.

### 4.3 Paragraph styles by inheritance

New in the model and the C# API:

```csharp
s.Defaults(d => d.Font(...).Size(...).Paragraph(Styles.BodyCopy));
s.Table(Styles.LineGrid, t => t.RowRule(...).TextStyle(Styles.TableText).HeaderTextStyle(Styles.TableHeader));
t.Row(r => r.TextStyle(Styles.TableHeader).Cell(...));
```

```json
"defaults": {"font": "Liberation Sans", "size": "10pt", "color": "#1A1A1A", "paragraph": "BodyCopy"},
"table": {"LineGrid": {"rowRule": "0.5pt #D4D4D4", "textStyle": "TableText", "headerTextStyle": "TableHeader", …}},
{"textStyle": "TableHeader", "row": [{"span": 3, "content": "Gesamtbetrag"}, "{{order.total:currency}}"]}
```

A paragraph without its own style gets the first of:
1. inside a table cell: the row's `textStyle`; then the table style's `headerTextStyle` (header rows) or `textStyle`
   (other rows);
2. the style sheet's default paragraph style;
3. none (Normal), as today.

List items keep their list style's `itemStyle`; without one they inherit like any other paragraph (decided in the final review). The style is resolved at composition and stored in the model, so the
writers do not change and the document carries an explicit `pStyle` / `text:style-name`. Export omits a paragraph's
style when it equals the inherited one. Without `Paragraph(...)` in the defaults and without table text styles,
definitions build exactly as today.

### 4.4 Short forms

| Where | Form |
|---|---|
| block | a string = a paragraph in the inherited style: `"Zahlbar innerhalb von 14 Tagen."` |
| paragraph | `{"style": "SectionHeading", "paragraph": "Kunde"}`; `paragraph` holds inline content |
| box | `{"style": "SuccessBox", "box": [blocks]}` |
| list | `{"style": "Bullets", "list": [items]}`; an item is a string, `{"level": 1, "item": inline content}`, or `forEach`/`if` as today |
| table | `{"style": "LineGrid", "columns": […], "spaceAfter": "8pt", "table": [rows]}` |
| row | `{"header": true, "textStyle": …, "row": [cells]}` |
| cell | a string (one paragraph), an array of blocks, or `{"span": 3, "background": …, "align": …, "content": string or blocks}` when it has options |

`toc`, `rule`, `raw`, `pageBreak`, `component`, `group`, `forEach`, `if` keep their phase 3 shape.

### 4.5 The sample after the change (excerpt)

```json
{"style": "LineGrid", "columns": ["10mm", 4, "16mm", "30mm"], "spaceAfter": "8pt", "table": [
  {"header": true, "row": ["#", "Beschreibung", "Menge", "Betrag"]},
  {"forEachRow": "order.lines", "as": "line", "rows": [
    {"row": ["{{@number}}", [ … description and tags … ], "{{line.quantity}}", "{{line.amount:currency}}"]}
  ]},
  {"rowsIf": "order.discount > 0", "then": [{"row": [{"span": 3, "content": "Rabatt"}, "{{order.discount:currency}}"]}]},
  {"textStyle": "TableHeader", "row": [{"span": 3, "content": "Gesamtbetrag"}, "{{order.total:currency}}"]}
]}
```

(Shown hand-wrapped; the writer applies 4.1.)

## 5. Calls left to the builder (made here, check while reading)

- **Styled values** are `{"text": "{{…}}", "style": …}`, not a `value` object with a style (said differently in the
  conversation; this removes `value`/`expression` entirely). Cost if wrong: re-add one object form.
- **No way to force "no style"** inside a scope that inherits one. The sample's first-page logo paragraph will get
  `BodyCopy` (6 pt space after) unless the sample gives it its own style; the sample will define one if the layout
  changes visibly. Cost if wrong: add an explicit "Normal" marker later.
- **Line width 100.** Cost: a constant.
- The sample's C# (`OrderConfirmation.cs`, `TypedOrderConfirmation.cs`) switches to the default paragraph style and
  table text styles, so its documents are regenerated; the Desktop artefacts are refreshed.

## 6. Verification

- The existing JSON tests move to the new shape; `BuilderHarness.AssertJsonRoundTrip` keeps every builder test
  round-tripping byte-identically.
- New tests: placeholder strings (paths, formats with quotes like `join:" · "`, expressions with `)` and `:` inside
  quoted strings, malformed placeholders with pointers), merging/splitting on export, style inheritance order
  (paragraph > row > table header/body > default > none, lists unaffected, nested box/if inside a cell), short forms
  and their errors, writer layout (fits/does not fit at exactly 100).
- Phase 3 spec section 9 gets the new numbers (lines, characters, tokens) for the sample.
