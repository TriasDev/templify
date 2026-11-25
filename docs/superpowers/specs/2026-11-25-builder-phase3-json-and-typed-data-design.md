# Template builder, phase 3: JSON definitions and typed data paths

- **Status:** Approved by the maintainer (2026-11-25). Builder check done.
- **Branch:** `feature/builder`. Nothing is released and nothing is merged to `main` in this phase.
- **Parent:** builder design spec `2026-11-25-template-builder-design.md`, epic #261, idea issue #274.

## 1. Intent

Phases 1 and 2 built the fluent C# builder. Phase 3 is exploratory. It answers two questions by building the core of
each and trying it on the real sample (`samples/TriasDev.Templify.Builder.Sample`):

1. **JSON definitions.** Can a template written with the fluent C# API be exported to a JSON file, and can that file
   be imported back into the same in-memory model, from which DOCX and ODT are built? What do the files look like,
   and how large are they?
2. **Typed data paths.** Can data paths come from a C# data type (`o => o.Order.Number`) instead of strings
   (`order["number"]`), and how does that feel in a real definition?

The maintainer wants to **see the resulting files and the typed sample** and then decide how to change them. So the
output of this phase includes artefacts to look at, not only code.

Why these two: in the longer term (#274), customers and their agents should change a stored definition instead of a
`.docx` copy. A file format that captures a definition exactly, and a catalogue of the data that exists, are the
foundations for that. The agent side (chat, MCP, patches, previews) is not part of this phase.

## 2. Goals, non-goals, anti-goals

**Goals**

- Export: `ITemplateDefinition` + build options → JSON text (+ binary assets).
- Import: JSON text (+ assets) → an `ITemplateDefinition` that builds through the normal `TemplateBuilder.Build`.
- **Exact round trip:** for the sample, C# → JSON → import → build gives **the same bytes** (DOCX and ODT) as building
  the C# definition directly. That is the acceptance test, and it is cheap because builds are deterministic.
- Hybrid component representation: registered components are written as references, others expanded (section 4.4).
- Import errors are the builder's normal diagnostics (`TB…`), plus a JSON-pointer location.
- Typed data paths (approach A, expression trees) and a data catalogue derived from a C# type, used to check the
  paths of an imported JSON definition.
- The sample: its exported JSON files and a typed version of the definition, placed where the maintainer can look at
  them, plus a short evaluation (sizes, readability, awkward spots).

**Non-goals (this phase)**

- Agents, in-app chat, MCP tools, JSON Patch, previews (#274 scenarios).
- YAML. JSON only (maintainer decision).
- Text keys and multi-language definitions. A JSON file holds the texts of the language it was exported in (section
  4.6). Keys come later.
- JSON Schema generation, schema versions with migrations. The file carries a format version number only.
- Overlays (a customer copy as a diff of the standard definition).
- A source generator for typed paths (approach B). Possible later on top of the same foundation.
- NuGet release, merge to `main`.

**Anti-goals** (would make it a failure even if it works)

- A second validation path: the importer must not check rules of its own that the composers also check, or
  duplicate them differently. Import goes **through the composers**.
- A format that is a dump of internal classes (type names, private fields): the file must read as a document
  description and be stable when internals are refactored.
- Breaking the fluent API or the output of existing definitions. Everything here is additive; existing tests and the
  sample's bytes stay unchanged.

## 3. Approaches considered

### 3.1 File format

| | |
|---|---|
| **JSON** (chosen) | `System.Text.Json` is in the BCL: no new dependency, a schema can follow, unambiguous. |
| YAML | Easier to read by hand, but a dependency (YamlDotNet) and ambiguities (implicit types). Dropped by the maintainer: only JSON. |

### 3.2 What the file contains

| | |
|---|---|
| A. Expanded model | What `Compose` produced: components expanded, texts resolved. Simplest, round trip nearly free; a stored file does not receive component changes. |
| B. Definition level | Components as references with parameters; needs a registry and serializable parameters. |
| **C. Hybrid** (chosen) | The model keeps a component's reference **and** its expansion. A registered component is written as a reference, an unregistered one expanded. Starts as A, grows into B without a format break. |

### 3.3 Typed data paths

| | |
|---|---|
| **A. Expression trees** (chosen) | `d.Path(o => o.Order.Number)`; no tooling; lambdas on every value. |
| B. Source generator | `d.Order.Number`; cleanest syntax; a Roslyn generator project, about twice the work. Deferred: B can be added later on the same foundation if A proves too noisy. |

Both share the foundation: a catalogue of paths built from a C# type by reflection.

## 4. JSON definitions

### 4.1 Shape

One JSON object per definition. Keys are camelCase; blocks are objects with **one discriminating key** (the block
kind), so a file reads like the C# that produced it. An excerpt of what the sample would export to (illustrative, the
exact names are the builder's call within these rules):

```json
{
  "templifyDefinition": 1,
  "identity": { "id": "order-confirmation", "version": 1 },
  "language": "en-GB",
  "properties": { "title": "Order confirmation", "author": "Example Trading" },
  "styles": {
    "defaults": { "font": "Liberation Sans", "size": "10pt", "color": "#1A1A1A" },
    "paragraph": {
      "SectionHeading": { "name": "Section Heading", "basedOn": "BodyCopy", "size": "13pt", "bold": true,
                          "color": "#1F4E79", "outlineLevel": 1, "keepWithNext": true }
    },
    "table": { "LineGrid": { "headerRule": "1pt #1F4E79", "rowRule": "0.5pt #D4D4D4", "cellPadding": "3pt 5pt" } }
  },
  "page": {
    "size": "A4", "margins": "25mm 20mm 22mm 20mm",
    "header": [ { "paragraph": { "style": "SmallPrint", "content": [
      { "text": "Order confirmation " }, { "value": "order.number" }, { "tabRight": true },
      { "text": "Page " }, { "pageNumber": true } ] } } ]
  },
  "content": [
    { "paragraph": { "style": "DocTitle", "content": "Order confirmation" } },
    { "component": "address", "params": { "address": "order.customer.address" } },
    { "if": "order.isExpress and order.total > 1000",
      "then": [ { "paragraph": "Express shipping – free of charge for you." } ],
      "elseIf": [ { "when": "order.isExpress", "then": [ { "paragraph": "Express shipping (plus €15.00)." } ] } ],
      "else": [ { "paragraph": "Standard shipping." } ] },
    { "forEach": "order.lines", "as": "line", "body": [
      { "paragraph": { "content": [ { "value": "line.description" }, { "text": " — " },
                                    { "value": "line.amount", "format": "currency" } ] } } ] }
  ]
}
```

Rules the builder follows:

- **Shorthands** where the C# API has them: a paragraph with only text may be a string; `content` may be a string.
  The writer always uses the shortest form, the reader accepts both.
- **Lengths** as strings with units (`"10pt"`, `"25mm"`), written in the unit the model stores (points, formatted
  invariantly) unless an exact millimetre value exists. **Colours** as `#RRGGBB`. **Page sizes** by name when they
  match a named size.
- **Paths** as Templify paths (`order.customer.name`; inside a loop `line.description`; loop metadata `line.@number`
  or the builder's chosen spelling). **Value formats** as their specifier (`currency`, `date:d MMMM yyyy`,
  `join:, `).
- **Conditions** as strings in Templify's condition syntax (`order.total > 1000 and order.isExpress`). The writer
  uses the existing condition emitter; the reader needs a parser for that grammar (truth, comparisons, `in`,
  `contains`/`startsWith`/`endsWith`, `exists`/`is empty`/`is not empty`, `not`, `and`/`or`, parentheses). This is
  the builder's call for readability: the alternative, conditions as JSON trees, needs no parser but is several times
  longer. The parser lives in the builder (D8: the builder uses core's public API only; core's parser is internal).
- **Styles:** the complete `StyleSheet` with resolved values (a theme record does not exist in JSON; its values do).
- **Images and embedded fonts** are not inlined. The file references them by content hash
  (`"image": "sha256:3f1a…"`), and the export returns them as an asset map. Import takes an asset resolver. (Base64
  would make files unreadable.)
- **Raw blocks** are exported (both XML strings) so the round trip is exact, but **import refuses them** unless an
  import option allows raw XML (default: refused, per #274's security note).
- **Determinism:** the writer emits keys in a fixed order and formats numbers invariantly. Export of the same
  definition and options gives the same JSON text.

### 4.2 API sketch (public, additive, namespace `TriasDev.Templify.Builder.Json`)

```csharp
// Export
TemplateJsonDocument doc = TemplateJson.Export(new OrderConfirmation(theme), buildOptions, catalog);
string json = doc.Json;                                   // the file
IReadOnlyDictionary<string, byte[]> assets = doc.Assets;  // "sha256:…" → bytes (images, fonts)

// Import: an ITemplateDefinition like any other
ITemplateDefinition imported = TemplateJson.Import(json, new TemplateJsonImportOptions
{
    Assets = assets,            // or a resolver
    Components = catalog,       // registered components
    Data = DataCatalog.From<OrderData>(),   // optional: check paths against a data type (section 5)
    AllowRawXml = false,
});
GeneratedTemplate template = TemplateBuilder.Build(imported, buildOptions);
```

Names are the builder's call. Export needs build options because `Compose` runs for one language (texts, section 4.6).

### 4.3 How import works

The importer is an `ITemplateDefinition` whose `Compose` walks the JSON and calls the **same composers** the fluent API
calls (`c.Paragraph(...)`, `c.If(...)`, `t.Table(...)`). So every rule (styles defined, loop scopes, cell counts,
lengths) is checked once, in the composers.

**When errors appear** (builder's call after the builder check):

- **At `Import`:** the JSON is parsed into an intermediate tree at once. Structural problems (malformed JSON, unknown
  keys or kinds, wrong value types, unknown components or assets, raw XML refused, paths outside the data catalogue)
  are collected and thrown together, with JSON pointers. Nothing language-dependent can be checked yet.
- **At `Build`:** `Compose` replays the tree through the composers, once per language and format, as for C#.
  - Composer rules that record diagnostics (`context.Error`, about 30) get the JSON pointer of the element being
    composed: the importer tracks the current pointer while it composes.
  - Composer rules that **throw** (about 200 argument checks: list levels, lengths, colours, path segments, style
    ids) are caught by the importer per element and turned into a diagnostic with a generic code (a new
    `TB9201` "invalid value", the exception's message and the pointer); composing continues with the next element.
    The fluent C# API keeps throwing at the call, unchanged. If a composer is left half-way inside a lambda, the
    importer abandons that element (and its children) only.
  - Checks that run on the finished model (undefined styles, `TableLayout`) find the pointer through an internal map
    from model blocks to JSON pointers that the importer fills; for C# definitions the map is empty and nothing
    changes.
  - All diagnostics of one build are reported together in one `TemplateBuildException`, like a C# build.
- `BuildDiagnostic` gets an optional location (additive public API, for example `string? Location`, a JSON pointer).

### 4.4 Components (hybrid)

- The model keeps, for each `ComponentBlock`, the component instance and its expansion (it already does).
- A **component catalog** maps a stable name to a component type: `catalog.Register<AddressBlock>("address")`.
  A registrable component has serializable parameters: its constructor parameters / properties are `TemplatePath`,
  `string`, numbers, `bool`, enums, style keys, or lists of those. Registration checks this and fails early
  otherwise (for example a component taking a `BrandTheme`).
- **Export:** a registered component → `{ "component": "address", "params": { … } }`. An unregistered one →
  its expansion as plain blocks (optionally tagged with the C# type name as a comment-like `"from"` key, the
  builder's call).
- **Import:** a reference → the catalog creates the component from its parameters and composes it (so it expands with
  the current component code). An unknown name → diagnostic.
- How the parameters are read from an instance (record properties, constructor parameters) is the builder's call; the
  sample's `AddressBlock` must become registrable as part of this phase.

### 4.5 Coverage

Everything in the semantic model is exported: paragraphs and inlines (text, value, tab, right tab, line break, page
fields, images, inline conditionals, expressions), page breaks, conditionals and switches (a switch is exported as the
conditional it produces, or as a `switch` if the model keeps it, builder's call), loops, tables (columns, header rows,
rows, row loops, row conditionals, cells with spans, backgrounds, alignment, nested blocks), boxes, rules, lists (items,
levels, loops and conditionals over items), images, table of contents, components, raw blocks, page setup with headers
and footers, document properties, identity, and the complete style sheet.

### 4.6 Texts and languages (deliberately minimal)

`template.Texts["key"]` returns a string today, so the model holds resolved text. In this phase a JSON file is
**per language**: it records the language it was exported in (`"language"`), and its texts are literal strings.
Building it in another language works but keeps the exported texts. Text keys in the model (`{ "text": { "key":
"title" } }`) are a later step, as agreed with the maintainer.

## 5. Typed data paths (approach A)

### 5.1 API sketch

```csharp
public sealed record OrderData(Order Order);
public sealed record Order(string Number, DateTime Date, decimal Total, bool IsExpress, Customer Customer, IReadOnlyList<Line> Lines);
public sealed record Line(string Description, int Quantity, decimal Amount);

DataModel<OrderData> d = template.Data<OrderData>();

c.Paragraph(x => x.Value(d.Path(o => o.Order.Number)));                        // order.number
c.If(d.Path(o => o.Order.IsExpress) & d.Path(o => o.Order.Total).Gt(1000), …);  // values are TemplatePaths, so conditions work unchanged
c.ForEach(d.Items(o => o.Order.Lines), "line", (line, b) =>                      // line: LoopItem<Line>
    b.Paragraph(x =>
    {
        x.Value(line.Number);                                                    // loop metadata as today
        x.Value(line.Path(l => l.Description));                                  // line.description
    }));
c.Component(new AddressBlock(d.Path(o => o.Order.Customer.Address)));            // TemplatePath<Address>

public sealed record AddressBlock(TemplatePath<Address> Address) : ITemplateComponent
{
    public void Compose(BlockComposer c) => c.Paragraph(x =>
    {
        x.Value(Address.Path(a => a.Street));                                    // order.customer.address.street
        x.LineBreak();
        x.Value(Address.Path(a => a.City));
    });
}
```

- **Types go all the way through** (maintainer decision): `Path` returns a `TemplatePath<T>`, where `T` is the
  property's type. `TemplatePath<T>` converts implicitly to `TemplatePath`, so everything that takes a path today
  (values, conditions, `Gt`/`Eq`, components with `TemplatePath` parameters, the contract, scope checks) accepts it
  unchanged. On an object it offers `Path(...)` for the next level, so a component can take a typed parameter
  (`TemplatePath<Address>`) and navigate inside it. Typed paths remain sugar that produces the same path values
  (spec D9).
- `Items` returns a typed collection that `ForEach` accepts; the loop item is a `LoopItem<T>` (a `LoopItem` with
  `Path`), and its paths are `TemplatePath<TProperty>` too.
- `TemplatePath<T>` is an allowed parameter type for registered components (section 4.4); in JSON it is a path
  string like any other, and with a data catalogue the importer also checks that the path's type fits (an object
  path for an object parameter).
- **Names:** C# property → JSON name with camelCase by default; `[JsonPropertyName]` wins; the policy can be set
  (`template.Data<OrderData>(JsonNamingPolicy.SnakeCaseLower)`), because the data is usually JSON.
- **Supported expressions:** member access chains; collection indexers with constant indices (`o.Lines[0]`) and
  dictionary indexers with constant keys. Anything else (method calls, computed values) is an `ArgumentException` at
  the call, naming the expression.
- Collections: arrays, `List<T>`, `IReadOnlyList<T>`, `IEnumerable<T>` (not `string`).

### 5.2 Data catalogue

`DataCatalog.From<OrderData>()` lists every path the type offers, with its kind (value, object, collection) and CLR
type, in the same canonical form as the data contract (`order.lines[].description`). It is used to:

- check an imported JSON definition: a path not in the catalogue is a diagnostic with the JSON pointer;
- compare with a template's data contract (`contract ⊆ catalogue`);
- later (#274), tell an agent what data exists.

Recursive types are cut at a depth limit (builder's call, with a test).

Rules (builder's calls after the builder check):

- The catalogue check is **opt-in** (`Data` in the import options). When a catalogue is given, a path it does not
  contain is an **error** with the JSON pointer: passing a type is asking for the check. Without one, nothing is
  checked.
- **Which paths are checked:** value, collection and component-parameter paths, and the paths in conditions. The
  writer always quotes condition literals (`status = "paid"`), and the reader requires that, so every bare word in a
  condition is a path and nothing is ambiguous. A bareword literal in a hand-written condition is a diagnostic that
  says to quote it.
- **Open shapes:** a property typed as a dictionary, `object`, `JsonElement` or `JsonNode` is listed as *open*;
  any path below it is accepted.

### 5.3 Typed sample

The sample gets typed data records for its sample JSON and a typed version of `OrderConfirmation`, including a typed
`AddressBlock` (alongside the string version, so both can be compared side by side). The typed version must produce **the same bytes** as the
string version.

## 6. Verification

- Round trip, both formats, both sample languages: `Build(C#) == Build(Import(Export(C#)))` byte for byte, and
  `Export(Import(Export(C#))) == Export(C#)` text for text.
- Round trip over the builder's existing test definitions where practical (a test that exports every definition the
  test suite builds would be ideal; the builder decides the mechanism).
- Import diagnostics: malformed JSON, unknown kinds and keys, wrong value types, unknown components, unknown assets,
  raw XML refused, composer errors with JSON pointers, all errors reported together.
- Typed paths: name policies, `[JsonPropertyName]`, indexers, unsupported expressions, loop items, nested loops,
  the typed sample's bytes equal the string sample's.
- Data catalogue: shape of the sample's catalogue, recursion limit, unknown paths in an imported definition.
- The usual invariants (test kit) on everything built from imported definitions.

## 7. Artefacts for the maintainer

- `~/Desktop/templify-builder-phase3/` (outside the repo): the sample's exported JSON for en-GB and de-DE, its assets,
  the DOCX/ODT built from the imported JSON, the typed definition source next to the string one.
- A short evaluation note in the spec: file sizes (bytes, lines, rough token count), what reads well, what is
  awkward, and what to change next.

## 8. Left to the builder

Exact JSON key names and shorthands within section 4.1's rules; the condition parser's implementation; how component
parameters are read; the public type names; the data catalogue's recursion limit; whether `switch` survives as its
own kind.

## 9. Phase 3 evaluation (2026-11-25)

Measured on the sample (`samples/TriasDev.Templify.Builder.Sample`, `dotnet run -- phase3 <dir>`), every builder
test and the new JSON/typed tests (519 builder tests after the review fixes, all green; every test that checks the invariants also
round-trips through JSON).

### What works

- **Exact round trip.** C# → JSON → import → build gives the same bytes for DOCX and ODT in both languages, for
  the sample and for every definition in the builder's test suite. Re-exporting an import gives the same JSON text.
- **One validation path.** The importer only replays the JSON through the composers; all builder rules apply
  unchanged, and diagnostics carry JSON pointers (`/content/2/body/0/paragraph/content/0/value`). Structural
  problems come at `Import`, composer problems at `Build`, each time all of them at once.
- **Hybrid components.** A registered component is three lines (`{ "component": "address", "params": { "address":
  "order.customer.address" } }`) and picks up changes to the component code on import; unregistered ones are
  expanded as a `group`.
- **Typed paths** build byte-identical templates (`TypedOrderConfirmation` vs `OrderConfirmation`), and the data
  catalogue of `OrderData` contains the sample's whole data contract; an import checks every path against it.

### Numbers

| | de-DE | en-GB |
|---|---|---|
| JSON (indented, as written) | 18,461 characters, 717 lines | 18,347 characters, 717 lines |
| approximate tokens (characters / 4) | ~4,600 | ~4,600 |
| same JSON without indentation | 7,620 characters | 7,506 characters |
| share: content / styles / page / properties | 71% / 13% / 7% / 1% | same |

| definition | non-blank code lines | characters |
|---|---|---|
| `OrderConfirmation` (string paths) | 159 | 4,854 |
| `TypedOrderConfirmation` (typed paths) | 163 | 5,370 (+11%) |

### What is awkward

1. **Depth and verbosity of tables.** A table cell holding one value is about 12 indented lines
   (`row → cells → content → paragraph → content → { "value" }`); content is 71% of the file. Indentation alone
   accounts for more than half of the characters.
2. **One file per language.** The two files differ only in texts; every text change has to be made twice. This is
   the known limit of section 4.6 (text keys come later).
3. **Raw XML is opaque in JSON**: escaped XML strings, readable only by copying them out. Acceptable for an escape
   hatch, and refused by default on import.
4. **Typed paths cost characters, not lines**: `order.Path(o => o.Number)` instead of `order["number"]`. The gain is
   the compiler (renames, typos), the catalogue check and typed components; the lambdas are noise in long value
   lists.

### Proposals for the next step

1. **Shorter forms for the common cases**, accepted by the reader and preferred by the writer:
   a paragraph or cell with one value (`{ "value": "line.quantity", "style": "TableText" }` as a cell), and a
   compact writer layout that puts objects with only scalar members on one line (roughly halves the lines without
   changing the format).
2. **Text keys** in the model (`{ "text": { "key": "title" } }`), so one JSON serves every language.
3. **JSON Schema** generated from the vocabulary, for editor completion and as the agent-facing contract (#274).
4. **Typed paths, approach B** (source generator) if the lambda noise matters in real definitions: same foundation
   (`TemplatePath<T>`, `DataCatalog`), shorter than the string version.
