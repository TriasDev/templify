# `:join` Format Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A `:join` format specifier that renders a collection as one text, e.g. `{{Assets:join:Name}}` → `A, B`.

**Architecture:** A new internal `Placeholders/JoinFormat` parses the specifier and joins the items. `ValueConverter.ConvertToString`
delegates to it when the format is a join and the value is a collection, so Word, OpenDocument and text templates get it
through the one conversion they already share. The three placeholder call sites additionally ask `JoinFormat` whether the
item property is missing on every item and add a `MissingVariable` warning.

**Tech Stack:** C# (net11.0/net10.0), xUnit v3, DocumentFormat.OpenXml (tests), existing `PropertyPathResolver`.

**Spec:** `docs/superpowers/specs/2026-11-25-join-format-design.md`

## Global Constraints

- No public API change: `PublicAPI.Shipped.txt` / `PublicAPI.Unshipped.txt` stay untouched; all new types are `internal`.
- Default separator `, ` (comma + space), not culture-dependent; `""` joins without separator.
- Separator only in double quotes; typographic quotes `“ ” „ ‟` accepted (`ConditionLexer.NormalizeQuotes`); `\"` and `\\` escape inside it. A separator cannot contain `}` (placeholder grammar).
- Unquoted argument = item property path (placeholder path syntax: `Name`, `Owner.Name`, `Tags[0]`).
- Collections: any `IEnumerable` except `string` and dictionaries (`IDictionary`, `IDictionary<,>`, `IReadOnlyDictionary<,>`, which covers `ExpandoObject`), plus `JsonElement` arrays. Other values: `:join` ignored, value renders as today.
- `null` items and items whose (property) value is `null` or converts to `""` are skipped.
- Items convert with `ValueConverter.ConvertToString(value, culture)` (no per-item format).
- Missing property: collection has ≥ 1 non-null item and the path resolves on none → one `MissingVariable` warning named `<VariableName>.<ItemPath>` (also added to `MissingVariables`). Processing only; `ValidateTemplate` unchanged.
- Builds with `-p:ContinuousIntegrationBuild=true` (warnings are errors); `dotnet format --verify-no-changes` clean; tests that assert numbers use an explicit culture.

## Review Focus

- Invalid join arguments (`join:"unterminated`, `join:Name:x`, `join:Na me`) → `TryParse` false → value renders exactly as without `:join` (no exception, no partial join).
- `{{Name:join}}` on a string value renders the string unchanged (strings are `IEnumerable<char>`).
- JSON input (`ProcessTemplate(..., jsonString)`): items arrive as converted dictionaries/lists → `:join:Name` works.
- Markdown in item values (`**A**`) is applied to the joined text; `:raw` is not combinable (`raw` is its own format).
- A join result inside a block loop uses the loop item's sub-collection (`{{x.Assets:join:Name}}` inside `{{#foreach x in Items}}`).

---

### Task 1: `JoinFormat` parser and joiner

**Files:**
- Create: `src/TriasDev.Templify/Placeholders/JoinFormat.cs`
- Test: `tests/TriasDev.Templify.Tests/Placeholders/JoinFormatTests.cs`

**Interfaces:**
- Produces:
  - `internal sealed class JoinFormat` with `string? ItemPath { get; }` (as written, e.g. `"Owner.Name"`) and `string Separator { get; }`
  - `static bool TryParse(string? format, [NotNullWhen(true)] out JoinFormat? join)`
  - `static bool IsCollection(object? value)`
  - `string Join(object collection, CultureInfo culture)` — caller guarantees `IsCollection(collection)`
  - `bool IsItemPathMissingOnAllItems(object collection)` — false when `ItemPath` is null or the collection has no non-null item
  - `static bool TryGetMissingItemVariable(string variableName, object? value, string? format, [NotNullWhen(true)] out string? missingVariable)` — combines the three above; `missingVariable` = `$"{variableName}.{ItemPath}"`

- [ ] **Step 1: Write the failing tests** (`[Theory]` where listed)

```csharp
// TryParse: format → (ItemPath, Separator)
[InlineData("join", null, ", ")]
[InlineData("JOIN", null, ", ")]
[InlineData("join:Name", "Name", ", ")]
[InlineData("join:Owner.Name", "Owner.Name", ", ")]
[InlineData("join:\" / \"", null, " / ")]
[InlineData("join:\"\"", null, "")]
[InlineData("join:Name:\"; \"", "Name", "; ")]
[InlineData("join:„ und “", null, " und ")]          // typographic quotes
[InlineData("join:\"a\\\"b\\\\\"", null, "a\"b\\")]   // escapes
TryParse_ValidFormat_ReturnsPathAndSeparator(string format, string? path, string separator)

// TryParse false
[InlineData(null)] [InlineData("")] [InlineData("joined")] [InlineData("number:N2")]
[InlineData("join:\"open")] [InlineData("join:Name:x")] [InlineData("join:Na me")] [InlineData("join:Name:\"a\":\"b\"")]
TryParse_InvalidFormat_ReturnsFalse(string? format)

// IsCollection
IsCollection: List<string> true; string[] true; JsonElement array true;
"abc" false; Dictionary<string,object> false; ExpandoObject false; JsonElement object false; 42 false; null false

// Join (CultureInfo.InvariantCulture unless stated)
Join_Strings_DefaultSeparator:                ["a","b","c"], "join"            → "a, b, c"
Join_EmptySeparator:                          ["A","B","C"], "join:\"\""       → "ABC"
Join_SkipsNullAndEmpty:                       ["a",null,"","b"], "join"        → "a, b"
Join_Property_FromDictionaries:               [{Name:A},{Name:B}], "join:Name" → "A, B"
Join_NestedProperty_FromPocos:                [new Asset(new Owner("X")), ...], "join:Owner.Name" → "X, Y"
Join_PropertyMissingOnSomeItems_SkipsThem:    [{Name:A},{Other:1},{Name:B}], "join:Name" → "A, B"
Join_Numbers_UseCulture:                      [1.5m, 2.25m], "join:\"; \"", de-DE → "1,5; 2,25"
Join_JsonElementArray:                        JsonDocument.Parse("[\"x\",\"y\"]").RootElement → "x, y"
Join_EmptyCollection:                         [] → ""

// IsItemPathMissingOnAllItems / TryGetMissingItemVariable
[{Nme:A}] with "join:Name" → true, missingVariable "Assets.Name" (variableName "Assets")
[{Name:A},{Other:1}] → false;  [] → false;  [null] → false;  "join" (no path) → false;  value "abc" → false
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/TriasDev.Templify.Tests/TriasDev.Templify.Tests.csproj -f net11.0 --filter "FullyQualifiedName~JoinFormatTests"`
Expected: build error (`JoinFormat` does not exist).

- [ ] **Step 3: Implement `JoinFormat`**

Parse after `ConditionLexer.NormalizeQuotes`: `join` (ordinal ignore case), then nothing, or `:` + either a quoted separator, or a path matching `^[\w.\[\]]+$` optionally followed by `:` + a quoted separator; nothing may follow the closing quote. Path resolution per item: `PropertyPath.TryParse` once in `TryParse` (invalid → false), then `PropertyPathResolver.TryResolvePath(item, path, out value)`. `JsonElement` arrays: enumerate via `JsonDataParser.ConvertJsonElementToObject` (returns a list). Items convert with `ValueConverter.ConvertToString(value, culture)`; join with `string.Join(Separator, parts)`.

- [ ] **Step 4: Run the tests to verify they pass**

Run: same as Step 2. Expected: all `JoinFormatTests` pass.

- [ ] **Step 5: Commit**

```bash
git add src/TriasDev.Templify/Placeholders/JoinFormat.cs tests/TriasDev.Templify.Tests/Placeholders/JoinFormatTests.cs
git commit -m "feat: add JoinFormat parser for the :join format specifier"
```

### Task 2: Wire `:join` into value conversion and the three placeholder processors

**Files:**
- Modify: `src/TriasDev.Templify/Placeholders/ValueConverter.cs` (start of `ConvertToString(object?, CultureInfo, string?, BooleanFormatterRegistry?)`)
- Modify: `src/TriasDev.Templify/Visitors/PlaceholderVisitor.cs` (resolved branch, before `ValueConverter.ConvertToString`)
- Modify: `src/TriasDev.Templify/OpenDocument/OdtPlaceholderProcessor.cs` (same place)
- Modify: `src/TriasDev.Templify/Core/TextTemplateProcessor.cs` (same place, ~line 495)
- Test: `tests/TriasDev.Templify.Tests/Placeholders/ValueConverterTests.cs`, `tests/TriasDev.Templify.Tests/Integration/JoinFormatIntegrationTests.cs`, `tests/TriasDev.Templify.Tests/Odt/OdtJoinFormatTests.cs`, `tests/TriasDev.Templify.Tests/Core/TextTemplateProcessorTests.cs`

**Interfaces:**
- Consumes: `JoinFormat.TryParse`, `JoinFormat.IsCollection`, `JoinFormat.Join`, `JoinFormat.TryGetMissingItemVariable` (Task 1).
- Produces: no new API; `ConvertToString` behavior: join when `TryParse(format)` and `IsCollection(value)`, else unchanged.

- [ ] **Step 1: Write the failing tests**

```csharp
// ValueConverterTests
ConvertToString_JoinFormatOnList_JoinsItems: (["a","b"], Invariant, "join") → "a, b"
ConvertToString_JoinFormatOnString_Unchanged: ("abc", Invariant, "join") → "abc"
ConvertToString_InvalidJoinFormat_RendersAsWithoutFormat: (List<string>{"a"}, Invariant, "join:\"open") == ConvertToString(same list, Invariant)

// Integration (Word, TemplateTestHarness.Process, invariant culture)
Join_InParagraph: "Rating for IT asset: {{Assets:join:Name}}" + Assets=[{Name:A},{Name:B}] → "Rating for IT asset: A, B"
Join_InsideBlockLoop: ["{{#foreach x in Items}}", "{{x.Title}}: {{x.Assets:join:Name}}", "{{/foreach}}"]
    Items=[{Title:T1, Assets:[{Name:A},{Name:B}]},{Title:T2, Assets:[]}] → ["T1: A, B", "T2: "]
Join_EmptySeparator: "{{Codes:join:\"\"}}" + ["A","B","C"] → "ABC"
Join_JsonData: ProcessJson("{\"Assets\":[{\"Name\":\"A\"},{\"Name\":\"B\"}]}") with "{{Assets:join:Name}}" → "A, B"
Join_MarkdownInItems: ["**A**","B"] → text "A, B", first run bold, the rest not (EnableMarkdown default)
Join_KeepsPlaceholderRunFormatting: bold placeholder run → whole "A, B" bold
Join_PropertyMissingOnAllItems_WarnsOnce: "{{Assets:join:Nmae}}" + [{Name:A},{Name:B}]
    → text "", Assert.Single(Warnings) MissingVariable with VariableName "Assets.Nmae", MissingVariables contains "Assets.Nmae"
Join_MissingCollection_UnchangedBehavior: "{{Nope:join}}" → "{{Nope:join}}" left, MissingVariable "Nope" (default LeaveUnchanged)

// ODT (OdtTestHelper.ProcessParagraphXml / Process): Join_InParagraph, Join_InsideBlockLoop, Join_PropertyMissingOnAllItems_WarnsOnce (same data/expectations)
// Text (TextTemplateProcessor): "Tags: {{Tags:join}}" → "Tags: a, b"; missing property warning as above
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/TriasDev.Templify.Tests/TriasDev.Templify.Tests.csproj -f net11.0 --filter "FullyQualifiedName~Join"`
Expected: FAIL (output is the CLR type name / no warning).

- [ ] **Step 3: Implement**

`ValueConverter.ConvertToString`: first statement — `if (JoinFormat.TryParse(format, out JoinFormat? join) && JoinFormat.IsCollection(value)) return join.Join(value!, culture);`.
Each of the three call sites, right after the value resolved (non-expression path is enough; expressions return scalars):
`if (JoinFormat.TryGetMissingItemVariable(placeholder.VariableName, value, placeholder.Format, out string? missing))` → add `missing` to the missing-variables set and `ProcessingWarning.MissingVariable(missing)` to the warnings (Word: `_missingVariables`/`_warningCollector`; ODT: `_missingVariables`/its collector; text: `state.MissingVariables`/`state.Warnings`). The placeholder is still replaced (by the empty join result).

- [ ] **Step 4: Run the tests to verify they pass**

Run: same as Step 2, then the whole project on both TFMs: `dotnet test tests/TriasDev.Templify.Tests/TriasDev.Templify.Tests.csproj`
Expected: all pass (3 known skips on net11.0).

- [ ] **Step 5: Commit**

```bash
git add src/TriasDev.Templify tests/TriasDev.Templify.Tests
git commit -m "feat: :join format renders collections as text in Word, ODT and text templates"
```

### Task 3: Documentation, tracking issue, pre-push checks

**Files:**
- Modify: `docs/for-template-authors/format-specifiers.md` (new "Lists: `:join`" section: default `, ` stated first, `""` example next to it, property, quoted separator, typographic quotes, skipping null/empty, single formatting for the whole list, not for per-item formatting → #258)
- Modify: `docs/for-template-authors/placeholders.md`, `docs/for-template-authors/template-syntax.md`, `README.md`, `src/TriasDev.Templify/README.md`, `CLAUDE.md` (format specifier lists: add `{{Tags:join}}`, `{{Assets:join:Name:"; "}}`)
- Modify: `docs/for-template-authors/loops.md` (in "Where to Put the Markers", the inline-loop bullet: "for a short list in a sentence use `{{Tags:join}}`" instead of "provide the joined text in your data")
- Modify: `docs/FAQ.md` (same advice where inline loops are mentioned)

- [ ] **Step 1: Update the docs listed above**
- [ ] **Step 2: Build docs** — Run: `python3 -m mkdocs build --strict -q -d <scratch>/site`. Expected: exit 0, no warnings.
- [ ] **Step 3: Documentation tests** — if `tests/TriasDev.Templify.Tests/Documentation/` mirrors the edited READMEs' samples, add one test per new sample (`{{Tags:join}}`, `{{Assets:join:Name}}`) there; run `--filter "FullyQualifiedName~Documentation"`. Expected: PASS.
- [ ] **Step 4: Pre-push checks** — `dotnet build templify.slnx -c Release -p:ContinuousIntegrationBuild=true` (0 warnings), all test projects pass, `dotnet format --verify-no-changes --no-restore` (exit 0). LibreOffice round trips: run `--filter "Category=LibreOffice"` (ODT placeholder processor touched): 15/15, 0 skipped.
- [ ] **Step 5: Tracking issue** — `gh issue create` "feat: :join format specifier for collections" (labels `enhancement`, `feature`), body: syntax summary from the spec, "inline loops stay in #258". Reference it in the PR.
- [ ] **Step 6: Commit**

```bash
git add docs README.md src/TriasDev.Templify/README.md CLAUDE.md tests/TriasDev.Templify.Tests/Documentation
git commit -m "docs: document the :join format specifier"
```
