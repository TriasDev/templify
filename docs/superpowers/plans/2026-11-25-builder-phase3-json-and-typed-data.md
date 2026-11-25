# Builder phase 3: JSON definitions and typed data paths — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Export fluent C# template definitions to JSON and import them back into the builder's model with byte-identical DOCX/ODT output, and let definitions take data paths from C# types (`d.Path(o => o.Order.Number)`).

**Architecture:** A JSON writer walks the builder's semantic model (`DocumentModel` before writing) and emits a stable, readable JSON document; an importer is an `ITemplateDefinition` whose `Compose` replays the JSON through the existing composers, so every rule is checked once. Typed paths are a generic wrapper `TemplatePath<T>` built from expression trees that converts to the existing `TemplatePath`, so everything downstream is unchanged.

**Tech Stack:** C# / .NET 11+10, `System.Text.Json` (`Utf8JsonWriter`, `JsonDocument`), `System.Linq.Expressions`, xUnit v3.

**Spec:** `docs/superpowers/specs/2026-11-25-builder-phase3-json-and-typed-data-design.md` (approved 2026-11-25). Parent: `docs/superpowers/specs/2026-11-25-template-builder-design.md`.

## Global Constraints

- Work on `feature/builder` only; one worktree per task (`git worktree add -b feat/p3-<n>-<name> ../templify-p3-<n> feature/builder`, then `dotnet restore templify.slnx`). Another agent uses the main checkout.
- Gate every merge with `$S/check.sh <worktree>` (format, locked restore, CI-mode build, all tests). `$S` = the session scratchpad. Merge with `git -C ../templify-builder merge --ff-only <branch>` and `git push origin feature/builder`. Do **not** use `finish.sh` (it would tick #274 in the epic early). No PRs, no NuGet.
- Commits reference `#274`, Conventional Commits with scope `builder`; changes to shared files (solution, CLAUDE.md, CI) go in separate `build`/`docs` commits.
- No new package dependency. JSON only (no YAML).
- Public API changes are additive and declared in `src/TriasDev.Templify.Builder/PublicAPI.Unshipped.txt` (extract RS0016 symbols, as in phase 2).
- Existing behaviour is unchanged: every existing builder test and the sample's bytes stay as they are.
- Determinism: export of the same definition and options gives the same JSON text; numbers and lengths formatted with `CultureInfo.InvariantCulture`.
- Every new builder construct delivers DOCX **and** ODT and keeps the test kit's invariants (`BuilderHarness.AssertInvariants`).
- Respond to the maintainer in Russian; code, docs and commits in English.

## Review Focus

1. **A path in JSON that names an enclosing loop item vs. the data root** (`line.x` inside `forEach … as line`, `line.x` outside it) — import must resolve exactly like the fluent API (LoopItem vs root, TB4001/TB4003), not silently treat it as a root path. Test in Task 5.
2. **Hand-written JSON with legal-but-unusual forms** (shorthand string paragraph vs. object, lengths in `mm` vs `pt`, lower-case colour hex) — the reader accepts every form the writer could emit and the documented shorthands; re-export normalises them. Test in Task 4.
3. **Many errors in one file** (three bad blocks, one unknown component) — one exception listing all of them with distinct JSON pointers, not the first only. Test in Task 6.
4. **Typed expressions the walker cannot map** (`o => o.Lines.First().X`, `o => o.Name.ToUpper()`, a captured variable index `o.Lines[i]`) — `ArgumentException` naming the expression, never a wrong path. Test in Task 7.
5. **A data type whose JSON names differ from C#** (`[JsonPropertyName("cust")]`, snake_case policy) — typed paths and the data catalogue use the data's names, so the filled document is correct. Test in Tasks 7 and 9.

---

## File structure

New (library, namespace in brackets):

- `src/TriasDev.Templify.Builder/Json/TemplateJson.cs` — public entry: `Export`, `Import` (`…Builder.Json`).
- `src/TriasDev.Templify.Builder/Json/TemplateJsonDocument.cs`, `TemplateJsonImportOptions.cs` — public results/options.
- `src/TriasDev.Templify.Builder/Json/JsonValues.cs` — internal: lengths, colours, page sizes, formats, paths ↔ text.
- `src/TriasDev.Templify.Builder/Json/ConditionParser.cs` — internal: Templify condition text → `Condition`.
- `src/TriasDev.Templify.Builder/Json/ModelJsonWriter.cs` — internal: `DocumentModel` → JSON.
- `src/TriasDev.Templify.Builder/Json/JsonDefinition.cs` — internal `ITemplateDefinition` that composes from JSON.
- `src/TriasDev.Templify.Builder/Json/JsonDiagnostics.cs` — internal: pointer tracking, collected import errors.
- `src/TriasDev.Templify.Builder/Components/ComponentCatalog.cs` — public registry (`…Builder`).
- `src/TriasDev.Templify.Builder/Typed/TemplatePathOfT.cs`, `TemplateCollection.cs`, `LoopItemOfT.cs`, `PathExpression.cs` — typed paths (`…Builder`).
- `src/TriasDev.Templify.Builder/Typed/DataCatalog.cs` — public data catalogue (`…Builder`).

Modified: `Model/DocumentModel.cs` (ListBlock), `Lists.cs`, `BlockComposer.cs`, `Images.cs` (`ImageFit.Exact`), `BuildDiagnostic.cs` (Location), `BuildContext.cs`, `TemplateBuilder.cs`, `TemplateComposer.cs`, `Tables.cs`, `Lists.cs`, `DataContract.cs`.

Tests (flat, namespace `TriasDev.Templify.Builder.Tests`): `JsonValuesTests.cs`, `ConditionParserTests.cs`, `JsonExportTests.cs`, `JsonImportTests.cs`, `JsonDiagnosticsTests.cs`, `ComponentCatalogTests.cs`, `TypedPathTests.cs`, `DataCatalogTests.cs`; `BuilderHarness.cs` gains the round-trip invariant; `SampleTests.cs` extended.

Sample: `samples/TriasDev.Templify.Builder.Sample/` gains `OrderData.cs`, `TypedOrderConfirmation.cs`, and a `phase3` mode in `Program.cs`/`SampleRunner.cs`.

---

### Task 1: Model and API groundwork (no output change)

**Files:**
- Modify: `src/TriasDev.Templify.Builder/Model/DocumentModel.cs`, `Lists.cs`, `BlockComposer.cs` (List), `Images.cs`, `BuildDiagnostic.cs`, `BuildContext.cs`
- Test: `tests/TriasDev.Templify.Builder.Tests/ListTests.cs`, `ImageTests.cs`, `DefinitionErrorTests.cs`

**Interfaces:**
- Produces: `internal sealed record ListBlock(ListStyleKey Style, List<Block> Items) : Block` — the list's items (paragraphs with `ListItemInfo`, and `LoopBlock`/`ConditionalBlock` over items). `BlockTree.All`/`Children` and `BlockFlattener.Flatten` expand it to its items, so writers and the contract see exactly what they see today.
- Produces: `public static ImageFit ImageFit.Exact(Length width, Length height)` (both positive; the image is drawn at exactly that size).
- Produces: `public string? BuildDiagnostic.Location { get; init; }` (JSON pointer or null). Keep the positional record constructor `(Code, Message)`.
- Produces: `internal void BuildContext.Error(string code, string message, string? location)` overload; existing overload passes `null`.

- [ ] **Step 1: Write failing tests**
  - `ListTests.List_IsOneBlockInTheModel`: compose `c.List(bullets, l => { l.Item("a"); l.ForEach(path, "x", …); })` through an internal helper that returns the composed body (`BlockComposer` internal ctor); assert body is `[ListBlock]` with 2 items.
  - `ImageTests.Exact_DrawsAtThatSize` (both formats): `Image(png, "a", ImageFit.Exact(30.Pt(), 10.Pt()))` → DOCX `wp:extent cx=381000 cy=127000`, ODT `svg:width="30pt" svg:height="10pt"`; `ImageFit.Exact(0.Pt(), 1.Pt())` throws `ArgumentOutOfRangeException`.
  - `DefinitionErrorTests.Diagnostic_LocationIsNullForCSharp`: every error of an existing failing C# definition has `Location == null`.
- [ ] **Step 2: Run; expect compile failures / FAIL.** `dotnet test tests/TriasDev.Templify.Builder.Tests --filter "FullyQualifiedName~ListTests|FullyQualifiedName~ImageTests|FullyQualifiedName~DefinitionErrorTests"`
- [ ] **Step 3: Implement** `ListBlock` (ListComposer writes into it; `BlockComposer.List` adds one `ListBlock`), the flatten/tree expansion, `ImageFit.Exact`, `Location`.
- [ ] **Step 4: Run the whole builder suite; all pass, and `SampleTests` (byte expectations via snapshots) unchanged.** `dotnet test tests/TriasDev.Templify.Builder.Tests`
- [ ] **Step 5: Declare API, `check.sh`, commit** `refactor(builder): list container in the model, ImageFit.Exact, diagnostic locations (#274)`, merge, push.

### Task 2: JSON value encodings

**Files:**
- Create: `src/TriasDev.Templify.Builder/Json/JsonValues.cs`
- Test: `tests/TriasDev.Templify.Builder.Tests/JsonValuesTests.cs`

**Interfaces:**
- Produces (internal static class `JsonValues`):
  - `string Length(Length value)`; `bool TryLength(string text, out Length value)` — writer rule: whole or half points → `"10pt"`/`"10.5pt"`; else if a value with ≤ 2 decimals in mm round-trips to the same millipoints → `"25mm"`; else points with 3 decimals. Reader accepts `pt`, `mm`, `cm`, `in`.
  - `string Color(Color value)` → `"#1F4E79"`; `bool TryColor(string, out Color)` accepts upper/lower case.
  - `string? PageSizeName(PageSize)` (`A4`, `A5`, `Letter`, `Legal`) / `bool TryPageSize(string, out PageSize)`; non-named sizes are written as `{ "width": …, "height": … }`.
  - `string Border(BorderLine)` → `"0.5pt #D4D4D4"`; `bool TryBorder(string, out BorderLine)`.
  - `string Box(Length top, Length right, Length bottom, Length left)` → 1, 2 or 4 values CSS-style (`"3pt 5pt"`); `bool TryBox(...)`.

- [ ] **Step 1: Failing tests** (`[Theory]` tables): `10.Pt()`→`"10pt"`, `10.5.Pt()`→`"10.5pt"`, `25.Mm()`→`"25mm"`, `12.5.Mm()`→`"12.5mm"`, `Length.FromPoints(10.123)`→`"10.123pt"`; every written value parses back to the same `Length`; `"1in"`→72pt; `"#abcdef"` parses; `"12"`/`"pt"`/`"#12345"` fail; `PageSize.A4`→`"A4"`; box forms `"3pt"`, `"3pt 5pt"`, `"1pt 2pt 3pt 4pt"`. Run under tr-TR and ar-SA current culture (`[Fact]` that sets `CultureInfo.CurrentCulture`) — same strings.
- [ ] **Step 2: Run; FAIL.** `--filter FullyQualifiedName~JsonValuesTests`
- [ ] **Step 3: Implement `JsonValues`.**
- [ ] **Step 4: Run; PASS.**
- [ ] **Step 5: `check.sh`, commit** `feat(builder): JSON value encodings (#274)`, merge, push.

### Task 3: Condition parser

**Files:**
- Create: `src/TriasDev.Templify.Builder/Json/ConditionParser.cs`
- Test: `tests/TriasDev.Templify.Builder.Tests/ConditionParserTests.cs`

**Interfaces:**
- Consumes: `Condition` AST types in `Condition.cs` (`TruthCondition`, `ComparisonCondition`, `InCondition`, `PostfixCondition`, `NotCondition`, `LogicalCondition`), `ConditionText` emitter.
- Produces: `internal static Condition ConditionParser.Parse(string text, Func<string, TemplatePath> resolvePath)` — throws `FormatException` with the 0-based column on error. `resolvePath` maps a path's text (as written, including `[keyword]` escapes and `@index`) to a `TemplatePath` with the right loop scope (Task 5 supplies it).
- Grammar = what the emitter writes: `or` < `and` < `not` < comparison (`= != > >= < <=`, `in (…)`, `contains`, `startswith`, `endswith`) < postfix (`exists`, `is empty`, `is not empty`); parentheses; literals are double-quoted strings (with `\"`/`\\`) or invariant numbers. A bare word on the right of a comparison is an error: "quote the literal".

- [ ] **Step 1: Failing tests.**
  - Round trip: for a `[Theory]` of conditions built with the C# API (truth, each comparison, `In`, `Contains`, each postfix, `!`, nested `&`/`|` with parentheses, keyword-escaped segments, a decimal literal `0.5`, a string with `"` and `\`), `Parse(c.ToString(), Root).ToString() == c.ToString()`.
  - Errors: `"a >"` (column 3), `"a = b"` (message contains `quote`), `"(a"`, `"a and"`, `""` → `FormatException`.
  - `Condition.Raw` is **not** parsed: covered in Task 4 as its own JSON form.
- [ ] **Step 2: Run; FAIL.**
- [ ] **Step 3: Implement** a small recursive-descent / precedence-climbing parser over a tokenizer.
- [ ] **Step 4: Run; PASS.**
- [ ] **Step 5: `check.sh`, commit** `feat(builder): condition parser for JSON definitions (#274)`, merge, push.

### Task 4: Export (model → JSON) and the public entry points

**Files:**
- Create: `Json/ModelJsonWriter.cs`, `Json/TemplateJson.cs`, `Json/TemplateJsonDocument.cs`
- Modify: `TemplateBuilder.cs` (expose an internal `Compose(definition, options) → (DocumentModel, BuildContext)` used by both `Build` and `Export`)
- Test: `JsonExportTests.cs`

**Interfaces:**
- Produces (public, namespace `TriasDev.Templify.Builder.Json`):
  - `static class TemplateJson { static TemplateJsonDocument Export(ITemplateDefinition definition, TemplateBuildOptions options, ComponentCatalog? components = null); static ITemplateDefinition Import(string json, TemplateJsonImportOptions? options = null); }` (`Import` lands in Task 5; `components` is honoured from Task 8; until then it may be ignored).
  - `sealed class TemplateJsonDocument { string Json { get; } IReadOnlyDictionary<string, byte[]> Assets { get; } }` — asset keys `"sha256:<lower-hex>"`.
- Export runs `Compose` (same checks as `Build`; a definition that does not build throws `TemplateBuildException`), then writes. `options.Format` does not influence the JSON.
- JSON shape (spec §4.1), fixed key order, 2-space indented, `\n` line ends:
  - top: `templifyDefinition` (1), `identity` {id, version}, `language`, `properties`, `styles`, `page`, `content`.
  - `styles`: `defaults`, `paragraph`, `character`, `table`, `box`, `list` — each keyed by style id, values only for properties that are set; `name` = display name.
  - blocks: `paragraph` (string shorthand when it has no style, no list info and one plain `TextInline`), `pageBreak: true`, `if`/`then`/`elseIf[{when,then}]`/`else`, `forEach`/`as`/`body`, `table` {style, columns, spaceAfter, rows}, `box` {style, content}, `rule` {line, spaceAbove, spaceBelow}, `list` {style, items}, `image` (block form), `toc` {maxLevel}, `raw` {docx, odt, uses}, `component` (Task 8; until then expanded as `{ "group": [...] }` with `"from": "<CLR type name>"`).
  - rows: `{ "row": { "header": true?, "allowSplit": true?, "cells": [...] } }`, `{ "forEachRow": path, "as": name, "rows": [...] }`, `{ "rowsIf": cond, "then": [...], "elseIf": [...], "else": [...] }`; cells: `{ "span"?, "background"?, "align"?, "content": [blocks] }`.
  - list items: `{ "item": { "level"?: n, "content": … } }`, `forEach`/`if` with item bodies.
  - inlines (`content` string shorthand for one plain text): `{ "text", "style"? }`, `{ "value": path, "format"?, "style"? }`, `{ "tab": true }`, `{ "tabRight": true }`, `{ "lineBreak": true }`, `{ "pageNumber": true, "style"? }`, `{ "pageCount": true, "style"? }`, `{ "image": "sha256:…", "alt", "width", "height" }`, `{ "if": cond, "then": [inlines], "elseIf", "else" }`, `{ "expression": cond, "format"? }`.
  - conditions: the condition text; `Condition.Raw` → `{ "raw": expr, "uses": [paths] }`.
  - raw blocks: `{ "raw": { "docx": "<xml>", "odt": "<xml>", "uses": [...] } }` — the XML snapshot is taken by calling the factories with `RawContext` whose `Frame` is the page frame width (documented limit: an imported raw block does not see its real container width).
  - embedded fonts: `"font": { "family": "...", "embedded": "sha256:…" }`; plain fonts as the family string.
- [ ] **Step 1: Failing tests.**
  - `Export_WalkingSkeleton_IsThisJson`: a one-paragraph definition with a value → assert the exact JSON text (golden string in the test).
  - `Export_IsDeterministic` (both formats' options, tr-TR current culture): two exports → equal text; `Format` = DOCX vs ODT → equal text.
  - `Export_CoversEveryBlock`: one definition using every block and inline kind (reuse the sample's `OrderConfirmation`) → parse the JSON with `JsonDocument` and assert each kind's key occurs (`paragraph`, `if`, `forEach`, `table`, `forEachRow`, `rowsIf`, `box`, `rule`, `list`, `image`, `toc`, `raw`, `expression`, `tabRight`, `pageNumber`), and `Assets` holds the logo under `sha256:<hash>`.
  - `Export_FailingDefinition_Throws`: undefined style → `TemplateBuildException` with TB2010.
- [ ] **Step 2: Run; FAIL.**
- [ ] **Step 3: Implement** `ModelJsonWriter` with `Utf8JsonWriter` (`Indented = true`, `Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping` so umlauts and `€` stay readable) and `TemplateJson.Export`.
- [ ] **Step 4: Run; PASS.**
- [ ] **Step 5: Declare API, `check.sh`, commit** `feat(builder): export definitions to JSON (#274)`, merge, push.

### Task 5: Import (JSON → definition) with the round-trip invariant

**Files:**
- Create: `Json/JsonDefinition.cs`, `Json/TemplateJsonImportOptions.cs`
- Modify: `Json/TemplateJson.cs` (Import), `tests/.../BuilderHarness.cs`
- Test: `JsonImportTests.cs`

**Interfaces:**
- Consumes: `ConditionParser.Parse` (Task 3), `JsonValues` (Task 2), the JSON shape (Task 4), all composers.
- Produces (public): `sealed class TemplateJsonImportOptions { IReadOnlyDictionary<string, byte[]>? Assets { get; init; } ComponentCatalog? Components { get; init; } DataCatalog? Data { get; init; } bool AllowRawXml { get; init; } }` (`Components` used from Task 8, `Data` from Task 9).
- Produces: `TemplateJson.Import` returns an `ITemplateDefinition` (`JsonDefinition`) whose `Identity` comes from the JSON and whose `Compose` replays the JSON through the composers: `template.Styles(StyleSheet.Create(...))`, `Properties`, `Page`, `Content`. The reader accepts the writer's forms and the shorthands; re-export of an import is the original text.
- Path resolution while composing: a stack of `(itemName, LoopItem)` from enclosing `forEach`/`forEachRow`/list `forEach`; a path whose first segment equals an enclosing item name → `loopItem[rest]` (or `loopItem.Self` for the bare name); `@index`/`@number`/`@count`/`@first`/`@last` → the innermost loop item's metadata; else `template.Data[path]`. Feed the same resolver to `ConditionParser`.
- Values: `ValueFormat.Custom(specifier)`; images via `ImageSource.FromBytes(assets[key])` + `ImageFit.Exact(width, height)`; embedded fonts via `FontFace.Embedded(family, assets[key])`.
- Raw blocks: refused unless `AllowRawXml` (Task 6 makes this a diagnostic; here: `NotSupportedException` is acceptable as a placeholder only if Task 6 replaces it — prefer to emit the Task 6 diagnostic code `TB9202` directly).
- Harness: `BuilderHarness.AssertInvariants` additionally exports the definition, imports it (`AllowRawXml = true`, the export's assets), builds with the same options and asserts `ContentHash` equality and that re-export gives the same JSON text.

- [ ] **Step 1: Failing tests.**
  - `Import_RoundTripsTheSample` (DOCX, ODT × de-DE, en-GB): `Build(Import(Export(OrderConfirmation))).ContentHash == Build(OrderConfirmation).ContentHash`; `Export(Import(...)).Json == first.Json`.
  - `Import_AcceptsShorthandsAndAlternativeForms`: hand-written JSON with `"paragraph": "Hi"`, `{ "paragraph": { "content": "Hi" } }`, `"25mm"`, `"70.866pt"`, `"#1f4e79"` builds; re-export normalises (Review Focus 2).
  - `Import_ResolvesLoopItemsLikeTheFluentApi`: JSON `forEach order.lines as line` with `value: line.description` and `@number` fills `{"order":{"lines":[{"description":"A"}]}}` to `"1 A"`; the same `line.description` outside the loop is a **root** path `line.description` (contract entry `line.description`), matching what `template.Data["line"]["description"]` would do (Review Focus 1).
  - Harness change: the whole builder suite passes with the round-trip invariant on.
- [ ] **Step 2: Run; FAIL.**
- [ ] **Step 3: Implement `JsonDefinition` and `Import`; extend the harness.**
- [ ] **Step 4: Run the full builder suite; PASS.** Any test whose definition cannot round-trip is a bug in Task 4/5, not a reason to skip the invariant.
- [ ] **Step 5: Declare API, `check.sh`, commit** `feat(builder): import JSON definitions; every builder test round-trips (#274)`, merge, push.

### Task 6: Import diagnostics

**Files:**
- Create: `Json/JsonDiagnostics.cs`
- Modify: `Json/JsonDefinition.cs`, `Json/TemplateJson.cs`, `TemplateBuilder.cs` (post-compose checks look up block → pointer), `BuildContext.cs`
- Test: `JsonDiagnosticsTests.cs`

**Interfaces:**
- Produces codes (in `DiagnosticCodes`): `TB9200` malformed or unexpected JSON (structure, unknown key or kind, wrong value type, unknown asset, unsupported `templifyDefinition` version), `TB9201` invalid value (a composer threw `ArgumentException`), `TB9202` raw XML not allowed.
- `Import` parses the whole document first and throws one `TemplateBuildException` with every `TB9200`/`TB9202` (each with `Location` = JSON pointer, e.g. `/content/3/table/rows/1/row/cells/0`).
- During `Compose`, `JsonDefinition` keeps the current pointer in `BuildContext` (internal `string? CurrentLocation`); `BuildContext.Error` without an explicit location uses it. Per element: `try { compose } catch (ArgumentException e) { context.Error(TB9201, e.Message, pointer); }` and continue with the next sibling.
- Post-compose checks: `JsonDefinition` records `model block → pointer` in an internal `ConditionalWeakTable<Block, string>` on the context; `CheckStylesAreDefined` and `TableLayout.Check` pass the looked-up pointer as location.

- [ ] **Step 1: Failing tests.**
  - `Import_ReportsEveryStructuralProblemAtOnce`: JSON with an unknown block kind at `/content/1`, a wrong type at `/content/2/paragraph/style`, an unknown asset at `/content/4/image` and an unknown component → one `TemplateBuildException`, 4 errors, those pointers, codes TB9200 (Review Focus 3).
  - `Import_ComposerArgumentErrors_BecomeTB9201`: list item `level: 12` and `rule.line: "0pt #000000"` → at `Build`, two TB9201 errors with their pointers; the rest of the document still composed (assert a later undefined-style error TB2010 also reported, with pointer).
  - `Import_RawXml_RefusedByDefault`: TB9202 at the raw block's pointer; with `AllowRawXml = true` builds.
  - `Import_InvalidJsonText`: `"{"` → TB9200, `Location = ""`, message includes line/position.
  - `Import_UnknownVersion`: `templifyDefinition: 2` → TB9200.
- [ ] **Step 2: Run; FAIL.**
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run; PASS** (and the full suite).
- [ ] **Step 5: `check.sh`, commit** `feat(builder): import diagnostics with JSON pointers (#274)`, merge, push.

### Task 7: Typed data paths

**Files:**
- Create: `Typed/TemplatePathOfT.cs`, `Typed/TemplateCollection.cs`, `Typed/LoopItemOfT.cs`, `Typed/PathExpression.cs`
- Modify: `TemplateComposer.cs`, `BlockComposer.cs`, `Tables.cs` (`TableComposer`/`RowsComposer.ForEachRow`), `Lists.cs` (`ListComposer.ForEach`)
- Test: `TypedPathTests.cs`

**Interfaces:**
- Produces (public, namespace `TriasDev.Templify.Builder`):
  - `TemplateComposer.Data<T>(JsonNamingPolicy? naming = null) → TemplatePath<T>` (root; default naming camelCase; `[JsonPropertyName]` wins).
  - `readonly struct TemplatePath<T>`: `TemplatePath<TProp> Path<TProp>(Expression<Func<T, TProp>> member)`; `TemplateCollection<TItem> Items<TItem>(Expression<Func<T, IEnumerable<TItem>>> member)`; implicit conversions to `TemplatePath` and `Condition`; operators `!`, `&`, `|` (with `TemplatePath<U>`, `TemplatePath`, `Condition`); the condition helpers of `TemplatePath` forwarded (`Eq`, `NotEq`, `Gt`, `Ge`, `Lt`, `Le`, `In`, `Contains`, `StartsWith`, `EndsWith`, `Exists`, `IsEmpty`, `IsNotEmpty`); `ToString()` = the path text.
  - `readonly struct TemplateCollection<TItem>` (implicit to `TemplatePath`).
  - `sealed class LoopItem<T>`: `LoopItem Item { get; }`, `TemplatePath<T> Self`, `Path<TProp>(…)`, `Items<TItem>(…)`, `Index`, `Number`, `Count`, `IsFirst`, `IsLast` (forwarded).
  - Overloads: `BlockComposer.ForEach<T>(TemplateCollection<T> collection, string itemName, Action<LoopItem<T>, BlockComposer> body)`; same for `TableComposer.ForEachRow`, `RowsComposer.ForEachRow`, `ListComposer.ForEach`.
- `PathExpression.Segments(LambdaExpression, JsonNamingPolicy)` (internal): member chains, `Convert` nodes, constant `int` indexers (`List<T>` indexer / array index → `[n]`), constant `string` dictionary keys → a plain segment with the key text, exactly as `path["Theme"]` gives in the string API (Templify reads `settings.Theme` as a dictionary key); anything else → `ArgumentException("The expression 'o => …' cannot be turned into a data path: …")`.
- Typed paths produce the **same** `TemplatePath` values (same scope) as the string API, so the contract and output are identical.

- [ ] **Step 1: Failing tests.**
  - `Path_MapsMembersWithCamelCase`: `d.Path(o => o.Order.Customer.Name).ToString() == "order.customer.name"`.
  - `Path_HonoursJsonPropertyNameAndPolicy`: `[JsonPropertyName("cust")]` → `order.cust.name`; `JsonNamingPolicy.SnakeCaseLower` on `IsExpress` → `order.is_express`; filling with matching JSON renders the value (Review Focus 5).
  - `Path_ConstantIndexAndKey`: `o.Order.Lines[0].Description` → `order.lines[0].description`; dictionary `o.Settings["Theme"]` → `settings.Theme`, equal to `d["settings"]["Theme"]` in the string API.
  - `Path_UnsupportedExpressions_Throw` (Theory): `o => o.Order.Lines.First().Description`, `o => o.Order.Number.ToUpper()`, `o => o.Order.Lines[i].Description` with captured `i` → `ArgumentException` whose message contains the expression text (Review Focus 4).
  - `TypedLoop_ProducesTheSameBytesAsTheStringApi` (both formats): a definition with `ForEach(d.Items(o => o.Order.Lines), "line", (line, b) => b.Paragraph(x => { x.Value(line.Number); x.Value(line.Path(l => l.Description)); }))` vs. the string version → equal `ContentHash`.
  - `TypedComponentParameter`: a component taking `TemplatePath<Address>` composes `Address.Path(a => a.City)` → contract entry `order.customer.address.city`.
  - `TypedConditions`: `d.Path(o => o.Order.IsExpress) & d.Path(o => o.Order.Total).Gt(1000)` emits `order.isExpress and order.total > 1000`.
  - `TypedPaths_ScopeErrorsStillApply`: a captured `LoopItem<T>` used after its loop → TB4001.
- [ ] **Step 2: Run; FAIL.**
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run; PASS** (full suite).
- [ ] **Step 5: Declare API, `check.sh`, commit** `feat(builder): typed data paths from C# types (#274)`, merge, push.

### Task 8: Component catalog (hybrid export)

**Files:**
- Create: `Components/ComponentCatalog.cs`
- Modify: `Json/ModelJsonWriter.cs`, `Json/JsonDefinition.cs`
- Test: `ComponentCatalogTests.cs`

**Interfaces:**
- Consumes: `TemplatePath<T>` (Task 7), export/import (Tasks 4–6).
- Produces (public): `sealed class ComponentCatalog { ComponentCatalog Register<TComponent>(string name) where TComponent : ITemplateComponent; IReadOnlyCollection<string> Names { get; } }`.
- A registrable component type has one public constructor; every parameter has a same-named readable property (records satisfy this) of an allowed type: `TemplatePath`, `TemplatePath<T>`, `string`, `bool`, `int`, `long`, `double`, `decimal`, enums, `ParagraphStyleKey`/`CharacterStyleKey`/`BoxStyleKey`/`TableStyleKey`/`ListStyleKey` (written as id), `Length`, `Color`, nullable forms, and `IReadOnlyList<>` of those. `Register` throws `ArgumentException` naming the first offending parameter. Names: `[a-z][a-zA-Z0-9]*`, unique.
- Export: a `ComponentBlock` whose component's runtime type is registered → `{ "component": name, "params": { camelCaseParam: value } }`; otherwise `{ "group": [...], "from": "<type name>" }`. Paths in params are written relative to the component's call site exactly as the JSON paths elsewhere (loop item names apply).
- Import: `"component"` → `options.Components` must contain the name (else TB9200 at Import); construct via the constructor with parameters converted from JSON (paths through the loop-scope resolver; style keys looked up by id in the definition's own style sheet; else TB9200); then `c.Component(instance)`. `"group"` composes its blocks in place.

- [ ] **Step 1: Failing tests.**
  - `Register_RejectsUnserializableParameters`: a component with a `BrandTheme` parameter → `ArgumentException` naming it; a component without public ctor → `ArgumentException`.
  - `Export_RegisteredComponent_IsAReference`: sample `AddressBlock` (made registrable in Task 10; use a test component here) → `{ "component": "address", "params": { "address": "order.customer.address" } }`; unregistered → `group` with `from`.
  - `Import_ComponentReference_UsesCurrentComponentCode`: export with catalog, then import with a catalog whose `address` maps to a **changed** component type → the built document has the changed output (the hybrid's point).
  - `RoundTrip_WithCatalog_IsByteIdentical` (both formats) for a definition using registered components inside a loop (params with loop-item paths).
  - `Import_UnknownComponent`: TB9200 with pointer.
- [ ] **Step 2: Run; FAIL.**
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run; PASS.**
- [ ] **Step 5: Declare API, `check.sh`, commit** `feat(builder): component catalog; registered components export as references (#274)`, merge, push.

### Task 9: Data catalogue and import check

**Files:**
- Create: `Typed/DataCatalog.cs`
- Modify: `Json/JsonDefinition.cs` (Import-time path check)
- Test: `DataCatalogTests.cs`

**Interfaces:**
- Produces (public): `sealed class DataCatalog { static DataCatalog From<T>(JsonNamingPolicy? naming = null); static DataCatalog From(Type type, JsonNamingPolicy? naming = null); IReadOnlyList<DataCatalogEntry> Entries { get; } bool Contains(string canonicalPath); }`; `sealed record DataCatalogEntry(string Path, DataKind Kind, Type ClrType)`; `enum DataKind { Value, Object, Collection, Open }`.
- Canonical paths as in `DataContract` (`order.lines[].description`). Values: primitives, `string`, `decimal`, `DateTime`, `DateTimeOffset`, `DateOnly`, `TimeOnly`, `Guid`, enums and their nullables. Collections: `IEnumerable<T>` except `string`. `Open`: dictionaries, `object`, `JsonElement`, `JsonNode` — `Contains` accepts any path below. Recursion: a type already on the current branch is not expanded again (entry kept as `Object`), and depth is capped at 16.
- Import check (only when `options.Data` is set): every path the JSON uses (values, collections, conditions, component params, raw `uses`), canonicalised with the loop-scope stack, must be in the catalogue → else TB9203 "unknown data path" with pointer, at `Import` (with the other structural errors). Metadata paths (`@index` …) are exempt.

- [ ] **Step 1: Failing tests.**
  - `From_SampleData_ListsPaths`: `DataCatalog.From<OrderData>()` (test-local records mirroring the sample) contains `order.number` (Value), `order.lines` (Collection), `order.lines[].description` (Value), `order.customer.address.city`.
  - `From_HonoursNaming`: `[JsonPropertyName]` and snake_case (Review Focus 5).
  - `From_RecursiveType_Terminates`: `record Node(string Name, IReadOnlyList<Node> Children)` → contains `children[].name`, finishes.
  - `From_OpenShapes`: a `Dictionary<string, object>` property `extra` → `Contains("extra.anything.deep")`.
  - `Import_PathOutsideCatalog_IsTB9203`: JSON with `value: order.nummer` and a condition `order.totl > 1` → two TB9203 with pointers; with the correct names → builds.
  - `Contract_IsSubsetOfCatalog`: for the sample, every contract entry's path is in `DataCatalog.From<OrderData>()`.
- [ ] **Step 2: Run; FAIL.**
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run; PASS.**
- [ ] **Step 5: Declare API, `check.sh`, commit** `feat(builder): data catalogue from C# types; import checks paths against it (#274)`, merge, push.

### Task 10: Sample, artefacts and evaluation

**Files:**
- Create: `samples/TriasDev.Templify.Builder.Sample/OrderData.cs` (records matching `sample-data.json`), `TypedOrderConfirmation.cs` (same document via `template.Data<OrderData>()`, typed `TypedAddressBlock(TemplatePath<Address> Address)`)
- Modify: `OrderConfirmation.cs` (`AddressBlock` becomes a registrable record `AddressBlock(TemplatePath Address)`), `SampleRunner.cs` (`RunPhase3(string outputDirectory, TextWriter log)`), `Program.cs` (`phase3 <dir>` argument), `tests/.../SampleTests.cs`, design spec (evaluation section)

**Interfaces:**
- Consumes: everything above. `SampleRunner.Components` = `new ComponentCatalog().Register<AddressBlock>("address")`.
- `RunPhase3` writes: `order-confirmation.<lang>.json`, `assets/<hash>.png`, `order-confirmation.<lang>.from-json.{docx,odt}` (template built from the imported JSON) and `.filled.{docx,odt}`, for de-DE and en-GB; logs sizes (bytes, lines, approximate tokens = bytes / 4) and asserts by exception that from-JSON bytes equal the C# bytes.

- [ ] **Step 1: Failing tests** (`SampleTests`):
  - `Phase3_JsonRoundTripIsByteIdentical`: for both languages and formats, `RunPhase3` succeeds and the from-JSON templates equal the C# templates (hash).
  - `Typed_SampleProducesTheSameBytes`: `TypedOrderConfirmation` vs `OrderConfirmation`, both formats and languages → equal `ContentHash`; and `TemplateVerifier.VerifyAll` passes for the typed version with the sample data.
  - `Phase3_Catalogue`: importing the exported JSON with `Data = DataCatalog.From<OrderData>()` builds.
- [ ] **Step 2: Run; FAIL.**
- [ ] **Step 3: Implement the sample changes.**
- [ ] **Step 4: Run; PASS; run** `dotnet run --project samples/TriasDev.Templify.Builder.Sample -- phase3 ~/Desktop/templify-builder-phase3` **and copy `OrderConfirmation.cs`, `TypedOrderConfirmation.cs`, `OrderData.cs` next to the output.**
- [ ] **Step 5: Evaluation.** Add a section "Phase 3 evaluation" to the phase 3 design spec: JSON sizes per language (bytes, lines, approximate tokens), the share taken by styles vs. content, three things that read well and three that are awkward in the JSON; the typed vs. string definition (line count, character count, what the lambdas cost in readability), and proposals for the next step. LibreOffice round trip of the from-JSON documents: `dotnet test tests/TriasDev.Templify.Builder.Tests -c Release --filter "Category=LibreOffice"` passes with 0 skipped.
- [ ] **Step 6: `check.sh`, commit** `feat(builder): phase 3 sample — JSON round trip and typed definition (#274)` and `docs(builder): phase 3 evaluation (#274)`, merge, push. Report to the maintainer (Russian) with the Desktop folder and the evaluation's key numbers.
