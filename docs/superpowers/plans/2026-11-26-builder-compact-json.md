# Builder: compact JSON definitions — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make exported/imported JSON definitions compact and readable — placeholders inside strings, inherited paragraph styles (document default, table text styles), short forms for blocks/rows/cells/items, and a width-based layout — while C# → JSON → build stays byte-identical.

**Architecture:** Style inheritance is a model feature: a post-compose pass in `TemplateBuilder.Compose` gives every unstyled paragraph its inherited style, so writers are untouched and the JSON writer can omit styles equal to the inherited one. The JSON reader/writer change shape in two steps (inline text, then blocks), each keeping the harness-wide round trip green; a final layout pass reformats the writer's compact output.

**Tech Stack:** C# / .NET 11+10, `System.Text.Json`, xUnit v3.

**Spec:** `docs/superpowers/specs/2026-11-26-builder-compact-json-design.md` (approved 2026-11-26), amending section 4.1 of `docs/superpowers/specs/2026-11-25-builder-phase3-json-and-typed-data-design.md`.

## Global Constraints

- Work on `feature/builder` only; one worktree per task: `git worktree add -b feat/cj-<n>-<name> ../templify-cj-<n> feature/builder`, then `dotnet restore templify.slnx`. Another agent uses the main checkout.
- Gate every merge with `$S/check.sh <worktree>` (`$S` = session scratchpad). Merge with `git -C ../templify-builder merge --ff-only <branch>`, `git push origin feature/builder`, remove the worktree and branch. Not `finish.sh`. No PRs, no NuGet.
- Conventional Commits, scope `builder`, reference `#274`; shared files (solution, CLAUDE.md, CI) in separate `build`/`docs` commits.
- Public API additions declared in `src/TriasDev.Templify.Builder/PublicAPI.Unshipped.txt`.
- `templifyDefinition` stays `1`; the reader accepts only the new shape (no phase 3 compatibility).
- Definitions without a default paragraph style and without table text styles build byte-identically to today.
- `BuilderHarness.AssertJsonRoundTrip` stays on for every builder test; it must pass after every task.
- Writer line width **100**; two-space indent; `", "` and `": "` separators on single lines; strings never wrapped.
- No `{{#if}}` / `{{#foreach}}` inside strings. `value` and `expression` inline objects are removed.
- Respond to the maintainer in Russian; code, docs, commits in English.

## Review Focus

1. **Hand-written placeholder strings at the edges** — `"{{"` unterminated, `"{{}}"`, `"{{a}}}"`, a lone `"}}"`, a format with colons (`{{order.date:date:HH:mm}}`): exactly one reading each (error with pointer, or the documented split at the first `:`). Tests in Task 2.
2. **Expressions whose condition contains `)`, `:` or `}` inside quoted strings** (`{{(order.status = "a):b"):yesno}}`) — paren matching honours quotes and escapes. Tests in Task 2.
3. **Nested scopes** — a table inside a table cell uses its own table style's text styles, a box inside a cell gets the document default, a paragraph explicitly styled with the inherited style round-trips and is exported without a style. Tests in Tasks 1 and 4.
4. **Undefined inherited styles with no paragraph using them** — the default paragraph style or a table text style that the sheet does not define is still TB2010. Test in Task 1.
5. **Inline sequences that must not merge** — adjacent text inlines, an empty text, a styled value with a format inside an inline `if`, an expression next to styled text: export never writes a string that import splits differently; import rejects an expression with a style / inside an inline `if` with a pointer. Tests in Task 3.

---

## File structure

New:
- `src/TriasDev.Templify.Builder/StyleInheritance.cs` — internal: the inheritance pass and `InheritedFor(...)` lookups shared by the pass and the JSON writer.
- `src/TriasDev.Templify.Builder/Json/TextTemplate.cs` — internal: string ↔ pieces (`{{path}}`, `{{path:format}}`, `{{(condition):format}}`).
- `src/TriasDev.Templify.Builder/Json/JsonLayout.cs` — internal: width-based reformatting.
- Tests: `StyleInheritanceTests.cs`, `TextTemplateTests.cs`, `JsonLayoutTests.cs`, `JsonShortFormTests.cs`.

Modified: `Styles/StyleSheet.cs`, `Styles/StyleSheetComposer.cs`, `Model/DocumentModel.cs` (`TableStyleDefinition`, `TableRowModel`), `Tables.cs`, `TemplateBuilder.cs`, `Json/ModelJsonWriter.cs`, `Json/JsonDefinition.cs`, existing JSON tests, the sample, `docs/for-developers/template-builder.md`, phase 3 spec section 9.

---

### Task 1: Inherited paragraph styles (model, C# API, JSON style properties)

**Files:**
- Create: `src/TriasDev.Templify.Builder/StyleInheritance.cs`, `tests/TriasDev.Templify.Builder.Tests/StyleInheritanceTests.cs`
- Modify: `Styles/StyleSheet.cs`, `Styles/StyleSheetComposer.cs`, `Model/DocumentModel.cs`, `Tables.cs`, `TemplateBuilder.cs`, `Json/ModelJsonWriter.cs` (styles section, row), `Json/JsonDefinition.cs` (styles section, row), `PublicAPI.Unshipped.txt`

**Interfaces:**
- Produces (public): `DefaultStyleComposer DefaultStyleComposer.Paragraph(ParagraphStyleKey style)`; `TableStyleComposer TableStyleComposer.TextStyle(ParagraphStyleKey style)`; `TableStyleComposer TableStyleComposer.HeaderTextStyle(ParagraphStyleKey style)`; `void RowComposer.TextStyle(ParagraphStyleKey style)`.
- Produces (internal): `ParagraphStyleKey? StyleSheet.DefaultParagraphStyle`; `TableStyleDefinition.TextStyle` / `.HeaderTextStyle` (`ParagraphStyleKey?`); `TableRowModel.TextStyle` (`ParagraphStyleKey?`).
- Produces (internal): `static class StyleInheritance` with `void Apply(List<Block> blocks, ParagraphStyleKey? inherited, StyleSheet styles, BuildContext context)` and `ParagraphStyleKey? ForCell(StyleSheet styles, TableBlock table, TableRowModel row)` = `row.TextStyle ?? (row.IsHeader ? def?.HeaderTextStyle : def?.TextStyle) ?? styles.DefaultParagraphStyle`, where `def = styles.Resolve(table)` (a box's `AsTableStyle()` has no text styles).
- JSON keys: `styles.defaults.paragraph`, table style `textStyle` / `headerTextStyle`, row `textStyle` (phase 3 shape otherwise; the writer keeps writing every paragraph's style explicitly in this task).

- [ ] **Step 1: Write failing tests** in `StyleInheritanceTests` (each builds DOCX and ODT through `BuilderHarness`, so the JSON round trip runs too):
  - `UnstyledParagraph_GetsTheDefault`: defaults `.Paragraph(Body)`; `c.Paragraph("x")` → snapshot line `Body | x`.
  - `ExplicitStyle_Wins`: `c.Paragraph(Heading, "x")` with default Body → `Heading | x`.
  - `NoDefault_StaysUnstyled`: no default → same `ContentHash` as before the change for an existing definition (use `WalkingSkeletonTests`' definition) — i.e. assert the paragraph has no `pStyle`.
  - `TableCells_UseTextStyles`: style `.TextStyle(Cell).HeaderTextStyle(Head)`; header row cell → `Head`, body row cell → `Cell`, row with `r.TextStyle(Total)` → `Total`, cell paragraph with explicit `Other` → `Other`.
  - `NestedTable_UsesItsOwnStyle` and `BoxInCell_GetsTheDocumentDefault`.
  - `ListItems_KeepTheirItemStyle`: default Body, list style with `ItemStyle(Item)` → items `Item`.
  - `HeaderAndFooter_GetTheDefault`.
  - `UndefinedDefault_IsTB2010` and `UndefinedTableTextStyle_IsTB2010` — with **no** unstyled paragraph in the definition.
  - `UndefinedDefault_ErrorHasJsonLocation`: imported JSON with `"defaults": {"paragraph": "Nope"}` → TB2010 (location may be the styles pointer or null; assert the code only).
- [ ] **Step 2: Run, expect compile failures.** `dotnet test tests/TriasDev.Templify.Builder.Tests --filter "FullyQualifiedName~StyleInheritanceTests"`
- [ ] **Step 3: Implement.** API setters; `StyleSheet` validation reports TB2010 for an undefined default paragraph style or table text style (message names the style and where it is used); `StyleInheritance.Apply` runs in `TemplateBuilder.Compose` right after `composer.Close()` over the body (inherited = default) and each header/footer part, recursing through conditionals, loops, components, list items (paragraphs with `List != null` are skipped), and table cells (inherited = `ForCell`). It replaces an unstyled `ParagraphBlock` in its list with `p with { Style = inherited }` and copies its `BuildContext.Locations` entry to the replacement. JSON writer/reader handle the three new keys.
- [ ] **Step 4: Run the whole builder suite.** `dotnet test tests/TriasDev.Templify.Builder.Tests` → all pass (existing ones unchanged).
- [ ] **Step 5: Declare API, `check.sh`, commit** `feat(builder): inherited paragraph styles — default, table text styles, row text style (#274)`, merge, push.

### Task 2: Placeholder text parser

**Files:**
- Create: `src/TriasDev.Templify.Builder/Json/TextTemplate.cs`, `tests/TriasDev.Templify.Builder.Tests/TextTemplateTests.cs`

**Interfaces:**
- Produces: `internal abstract record TextPiece`; `TextPiece.Literal(string Text)`, `TextPiece.Value(string Path, string? Format)`, `TextPiece.Expression(string Condition, string? Format)`.
- Produces: `internal static IReadOnlyList<TextPiece> TextTemplate.Parse(string text)` — throws `FormatException` (message quotes the offending placeholder); a string without `{{` is one `Literal` (also `""`); empty literals between placeholders are dropped.
- Produces: `internal static string TextTemplate.Write(IEnumerable<TextPiece> pieces)` — the inverse for pieces that came from `Parse`.
- Rules: `{{` starts a placeholder; `{{(` starts an expression: the condition runs to the matching `)` (nested parens counted; `"…"` and `'…'` with `\` escapes skipped), then optional `:format`, then `}}`. Otherwise path and format run to the first `}}`, split at the first `:`. Path/format/condition text is not validated here (the reader does that with its existing parsers).

- [ ] **Step 1: Write failing tests** (`[Theory]` tables):
  - `"Auftrag {{order.number}} · {{order.date:date:d. MMMM yyyy}}"` → Literal `"Auftrag "`, Value(`order.number`, null), Literal `" · "`, Value(`order.date`, `date:d. MMMM yyyy`).
  - `"{{a}}{{b}}"` → Value, Value. `"plain"` → Literal. `""` → Literal `""`. `"a }} b"` → Literal.
  - `"{{order.date:date:HH:mm}}"` → Value(`order.date`, `date:HH:mm`). `"{{line.tags:join:\" · \"}}"` → Value(`line.tags`, `join:" · "`).
  - `"{{(order.isExpress and order.total > 1000):yesno}}"` → Expression(`order.isExpress and order.total > 1000`, `yesno`); `"{{(a = \"x):y\"):yesno}}"` → Expression(`a = "x):y"`, `yesno`); `"{{((a or b) and c)}}"` → Expression(`(a or b) and c`, null).
  - Errors (`FormatException`): `"{{"`, `"x {{a"`, `"{{}}"`, `"{{(a}}"`, `"{{(a) junk}}"`.
  - `Write(Parse(s)) == s` for every valid row.
- [ ] **Step 2: Run, expect FAIL.** `dotnet test tests/TriasDev.Templify.Builder.Tests --filter "FullyQualifiedName~TextTemplateTests"`
- [ ] **Step 3: Implement** as a single left-to-right scanner.
- [ ] **Step 4: Run; PASS.**
- [ ] **Step 5: `check.sh`, commit** `feat(builder): placeholder text parser for JSON strings (#274)`, merge, push.

### Task 3: Inline text as strings with placeholders (reader + writer)

**Files:**
- Modify: `Json/JsonDefinition.cs` (`Inline`, `Inlines`, `TextInline(s)`, paragraph/item content strings), `Json/ModelJsonWriter.cs` (`Content`, `Inlines`, `IsPlainText`), JSON tests that use `text`/`value`/`expression` objects
- Test: `tests/TriasDev.Templify.Builder.Tests/JsonShortFormTests.cs`

**Interfaces:**
- Consumes: `TextTemplate.Parse/Write` (Task 2); existing `Resolve(text, at, kind)`, `Condition`, `ValueFormat.Custom`.
- Inline content = string or array; array pieces = string, `{"text": s, "style": c}`, `if`, `tab`, `tabRight`, `lineBreak`, `pageNumber`, `pageCount`, `image`. A string becomes `Text`/`Value`/`Expression` calls in order; styled text applies the style to each piece. `:join`/`join:` formats read a collection (as today).
- Errors (TB9200 at the string's pointer, message quoting the placeholder): malformed placeholder; an expression without a format (the C# API requires one); an expression in styled text; an expression inside an inline `if` branch; a value with both format and style inside an inline `if` (no such overload). Unknown paths stay TB9203 per placeholder. The removed `value`/`expression` objects give the existing "Unknown inline" error, its list updated.
- Writer: a maximal run of `TextInline`/`ValueInline`/`ExpressionInline` (expression only unstyled) with the same character style becomes one string, except that two `TextInline`s are never joined and an empty `TextInline` is written as its own `""`; a run of one unstyled piece in a paragraph is written as the bare string instead of an array.

- [ ] **Step 1: Write failing tests** in `JsonShortFormTests`:
  - `Import_StringWithPlaceholders`: `{"paragraph": {"content": "Order {{order.number}} of {{order.date:date:yyyy}}"}}` (phase 3 block shape, still current) → filled DOCX text `Order 42 of 2026`.
  - `Import_StyledTextWithValue`: `{"text": "{{order.number}}", "style": "Strong"}` → value run has the character style.
  - `Import_Expression`: `"Free: {{(a and b):yesno}}"` fills `Free: Yes`/`No` (culture-invariant).
  - `Import_Errors` (Theory): each error above → `TemplateBuildException` with code and `Location` ending `/content` or `/content/<i>`.
  - `Import_UnknownPathInString_IsTB9203` with `Data` catalogue.
  - `Export_MergesValuesIntoText`: C# `x.Text("Order "); x.Value(p); x.Text(" of "); x.Value(d, ValueFormat.Date("yyyy"))` → JSON contains `"Order {{order.number}} of {{order.date:date:yyyy}}"`.
  - `Export_KeepsAdjacentTextsApart`: `x.Text("a"); x.Text("b")` → `["a", "b"]`; empty text kept; same-hash round trip (harness).
  - `Export_StyledRun`: `Text("x", s); Value(p, s)` → `{"text": "x{{p}}", "style": "S"}`.
- [ ] **Step 2: Run, expect FAIL.** `dotnet test tests/TriasDev.Templify.Builder.Tests --filter "FullyQualifiedName~JsonShortFormTests"`
- [ ] **Step 3: Implement reader and writer; migrate the existing JSON tests' inline objects to strings.**
- [ ] **Step 4: Run the whole builder suite; all pass (the harness round trip covers every inline test).**
- [ ] **Step 5: `check.sh`, commit** `feat(builder): JSON inline content as strings with placeholders (#274)`, merge, push.

### Task 4: Block short forms and inherited-style omission (reader + writer)

**Files:**
- Modify: `Json/JsonDefinition.cs` (`Block`, `Paragraph`, `ListItem(s)`, `Table`, `RowItem`, `Row`, box/list), `Json/ModelJsonWriter.cs` (`Block`, `ListItems`, `Table`, `Rows`), JSON tests with block literals
- Test: `JsonShortFormTests.cs`

**Interfaces:**
- Consumes: `StyleInheritance.ForCell` and `StyleSheet.DefaultParagraphStyle` (Task 1) for the inherited style while writing.
- Shapes (spec 4.4): block string; `{"style"?, "paragraph": content}`; `{"style", "box": [blocks]}`; `{"style", "list": [items]}` with item = string | `{"level"?, "item": content}` | `forEach`/`if` (`items`); `{"style"?, "columns"?, "spaceAfter"?, "table": [rows]}`; row `{"header"?, "textStyle"?, "allowSplit"?, "row": [cells]}`; cell = string | [blocks] | `{"span"?, "background"?, "align"?, "content": string | [blocks]}`. Keep every existing option key under its current name; only the nesting changes.
- Writer key order: options first, payload key last; `if`/`forEach`/`component`/`group`/`forEachRow`/`rowsIf` unchanged. A paragraph's `style` is omitted when it equals the inherited style of its scope. Bare-string forms: block/cell/item whose single paragraph has the inherited style (items: level 0) and whose content is one unstyled string. An unstyled paragraph in a scope with no inherited style writes no `style`.

- [ ] **Step 1: Write failing tests:**
  - `Import_ShortForms`: one definition using every form above → snapshot equals the same definition in C#.
  - `Export_ShortForms`: the C# definition exports to JSON containing `{"style": "SuccessBox", "box": ["Paid"]}`, `{"header": true, "row": ["#", "Item"]}`, `"Plain block"`, `{"textStyle": "Total", "row": [{"span": 2, "content": "Total"}, "{{total}}"]}` (substring checks after Task 5's layout are fragile — assert by parsing the JSON with `JsonDocument` and comparing elements).
  - `Export_OmitsInheritedStyle_KeepsOthers`: default Body; paragraphs `Body` and `Heading` → first has no `style`, second has.
  - `Export_ExplicitInheritedStyleInNestedCell_RoundTrips` (Review Focus 3).
  - `Import_OldShape_IsRejected`: `{"paragraph": {"style": "Body", "content": "x"}}` → TB9200 at `/content/0/paragraph` ("expected a string or an array").
  - `Import_CellOptionsWithoutContent_IsTB9200`.
- [ ] **Step 2: Run, expect FAIL.**
- [ ] **Step 3: Implement reader and writer; migrate the existing JSON tests and the `Phase3ReviewTests`/`ComponentCatalogTests`/`DataCatalogTests` literals.**
- [ ] **Step 4: Run the whole builder suite; all pass.**
- [ ] **Step 5: `check.sh`, commit** `feat(builder): JSON block short forms, inherited styles omitted (#274)`, merge, push.

### Task 5: Compact layout, sample, docs, evaluation

**Files:**
- Create: `src/TriasDev.Templify.Builder/Json/JsonLayout.cs`, `tests/TriasDev.Templify.Builder.Tests/JsonLayoutTests.cs`
- Modify: `Json/ModelJsonWriter.cs` (write unindented, then `JsonLayout.Format`), `samples/TriasDev.Templify.Builder.Sample/OrderConfirmation.cs`, `TypedOrderConfirmation.cs`, `docs/for-developers/template-builder.md` (JSON section), phase 3 spec section 9 (new numbers), `SampleTests.cs` if its asserted strings change

**Interfaces:**
- Produces: `internal static string JsonLayout.Format(string json, int width = 100)` — input: valid JSON; primitives copied with `GetRawText()` (keeps the writer's escaping); object/array on one line when `indent + key + single-line form <= width`, else one member per line, recursively; empty `{}`/`[]` always inline; output ends with `\n`.

- [ ] **Step 1: Write failing tests** in `JsonLayoutTests`:
  - fits at exactly 100 (incl. indent and key) → one line; 101 → broken;
  - nested: outer breaks, inner fits → inner on one line;
  - long string never wrapped; escaping preserved (`"\""`, `"ü"` unescaped as the writer emits);
  - `Format(Format(x)) == Format(x)`; same input → same output.
- [ ] **Step 2: Run, expect FAIL.**
- [ ] **Step 3: Implement; switch the writer; update the sample:** style sheet gets `.Paragraph(Styles.BodyCopy)` in defaults and `LineGrid` gets `.TextStyle(TableText).HeaderTextStyle(TableHeader)`; drop now-redundant explicit styles in both sample definitions (typed and untyped must keep producing the same bytes as each other); the totals row uses `r.TextStyle(Styles.TableHeader)`. If the first-page logo paragraph's new `BodyCopy` spacing visibly moves the header (compare the PDF via `LibreOfficeConverter`), give it its own style in the sample.
- [ ] **Step 4: Run the whole builder suite and the sample** (`dotnet run --project samples/TriasDev.Templify.Builder.Sample -- phase3 ~/Desktop/templify-builder-phase3`), refresh `~/Desktop/templify-builder-phase3/` (JSON, assets, docx/odt, the sample's C#), record lines/characters/tokens for both languages in spec section 9 and in the developer docs' JSON example.
- [ ] **Step 5: `check.sh`, commit** `feat(builder): compact JSON layout; sample uses inherited styles (#274)` and `docs(builder): compact JSON format and evaluation (#274)`, merge, push.
