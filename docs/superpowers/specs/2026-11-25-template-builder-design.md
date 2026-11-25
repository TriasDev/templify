# Design: TriasDev.Templify.Builder — code-first template generation

- **Issues:** epic [#261](https://github.com/TriasDev/templify/issues/261), spec task [#270](https://github.com/TriasDev/templify/issues/270), analysis [#260](https://github.com/TriasDev/templify/issues/260)
- **Date:** 2026-11-25
- **Status:** Accepted (2026-11-25). Spikes done (section 9): the upload check works through core's validator once #271 is fixed; a box as a one-cell table works in both engines.

## 1. Goal and binding decisions

A new package, `TriasDev.Templify.Builder`, generates Templify templates from C# code, as `.docx` and as `.odt`. A
consuming application describes a template once: styles, blocks, placeholders, conditionals and loops. It then builds
that template at runtime in the language, format and customer theme it needs. Templify fills the result as it fills any
template today.

The decisions below were taken in #260 and in the maintainer's answers to the review in #261. This spec does not reopen
them:

| # | Decision |
|---|---|
| D1 | Templates are generated **at runtime**, in memory. Nothing is generated ahead of time and stored. The result is also available as bytes or a stream, so a user can download it. |
| D2 | Customer customisation is **data passed to the definition** (theme slots: colours, logo, fonts, a few texts). It is never done by editing a generated file. |
| D3 | A customer may download a generated template, edit it in Word or LibreOffice and upload it. From then on that copy **lives on its own**: no merging, no regeneration. Going back to the standard template is always possible. |
| D4 | Every generated template carries an **identity stamp**, and an uploaded copy can be checked against the current data contract. |
| D5 | **DOCX and ODT from the first task.** Every task delivers both formats. |
| D6 | The builder is a **project in this repository**, under the same rules: public API tracking, tests, LibreOffice round trips. |
| D7 | **Its own version line.** A separate release-please component; versions stay `0.x` until the API has carried one real multi-section document. |
| D8 | The builder uses **core's public API only**. No `InternalsVisibleTo` from core to the builder. |
| D9 | **Model paths are values** (`TemplatePath`, `LoopItem`). Typed paths (`m => m.Customer.Name`) come after the MVP, as sugar that produces the same values. |
| D10 | **No font embedding in the MVP.** Choosing one family per weight, which avoids Word for Mac ignoring an embedded bold face, is a token decision and needs no embedding. *Phase 2 (#288) adds `FontFace.Embedded` (section 3.3).* |

Out of scope:

- importing a letterhead or any other customer-supplied document;
- PDF output, and any layout engine of our own;
- reading a model back from a document;
- merging a customer's edits with a new version.

## 2. Principles

### 2.1 QuestPDF's ergonomics, not its container model

QuestPDF can chain decorators (`.Padding().Border().Background()`) because it owns the layout. Word and LibreOffice
lay out flow documents themselves, from named styles. A decorator chain has no faithful mapping there, and it would push
users toward direct formatting, which makes a downloaded copy hard to edit (D3).

The builder therefore takes from QuestPDF only the parts that carry over:

- nested lambda descriptors;
- a definition class;
- reusable components.

The public surface is a **closed vocabulary of semantic blocks** plus Templify constructs. The document model behind it is
`internal`.

### 2.2 Invalid template syntax cannot be written

Users never type marker text. Every Templify construct exists only where the engine supports it, and the writer emits it
in the shape the engine needs (section 3.6). If the engine has a limitation, **we fix the engine** and do not add a builder
workaround. The builder's conformance tests double as engine regression tests.

### 2.3 Editability is an output requirement

A downloaded template must be pleasant to adapt by hand. That means:

- paragraph and character formatting comes from **named styles**, never direct formatting;
- markers are **visible**, in a dedicated style;
- loop variables have **readable names**;
- lists are **real lists**;
- there are no invisible spacer or keeper hacks where the format has a native option.

### 2.4 Admission rule for constructs

A construct joins the vocabulary only when all of these hold:

1. it has a defined mapping in **both** formats;
2. it has a LibreOffice round-trip test;
3. a real template needs it.

A construct that maps differently between the formats is listed in the support matrix (section 6). The builder reports
it as a **build warning**; it never lets the formats diverge silently.

### 2.5 Determinism

The same definition, options and builder version give **the same bytes**, under any current culture. As a consequence:

- `GeneratedTemplate.ContentHash` works as a cache key or ETag;
- snapshot tests are possible, in this repository and in consumers.

## 3. Public API

Namespace `TriasDev.Templify.Builder`. The listings show shape, not final signatures; task 1 freezes names in
`PublicAPI.Unshipped.txt`.

### 3.1 API shape decision: composer classes with internal constructors

The composers are **classes with internal constructors**, not interfaces:

- users can neither implement nor derive from them;
- adding a member is non-breaking under RS0016/RS0017 and package validation.

`CellComposer` and `BoxComposer` derive from `BlockComposer`, so every block container shares the same verbs.

Users implement only three interfaces, each with a single member that will not grow: `ITemplateDefinition`,
`ITemplateComponent` and `ITextCatalog`.

**Naming** (maintainer decision): the suffix is `…Composer`, as in `TemplateComposer`, `BlockComposer`,
`InlineComposer`, `TableComposer`, `RowComposer` and `ListComposer`. It matches `Compose`, and it does not clash with
the static `TemplateBuilder` entry point, the package name or core's types. Rejected:

- `…Descriptor` (QuestPDF): odd for objects that add content;
- `…Builder`: a builder inside `TemplateBuilder`;
- `…Container`: suggests QuestPDF's decorator chain.

### 3.2 Entry point and output

```csharp
public interface ITemplateDefinition
{
    TemplateIdentity Identity { get; }
    void Compose(TemplateComposer template);   // runs on every Build; must not have side effects
}

public sealed record TemplateIdentity(string Id, int Version);

public sealed record TemplateBuildOptions
{
    public required TemplateFormat Format { get; init; }      // core enum: Docx | Odt
    public required CultureInfo Language { get; init; }
    public ITextCatalog Texts { get; init; } = TextCatalog.Empty;
}

public static class TemplateBuilder
{
    public static GeneratedTemplate Build(ITemplateDefinition definition, TemplateBuildOptions options);
}

public sealed class GeneratedTemplate
{
    public TemplateFormat Format { get; }
    public string FileExtension { get; }        // ".docx" | ".odt"
    public string MediaType { get; }
    public ReadOnlyMemory<byte> Content { get; }
    public string ContentHash { get; }          // SHA-256, lower-case hex
    public DataContract Contract { get; }
    public TemplateStamp Stamp { get; }
    public IReadOnlyList<BuildDiagnostic> Warnings { get; }
    public void WriteTo(Stream destination);
    public Stream OpenRead();
}

public sealed class TemplateBuildException : Exception
{
    public IReadOnlyList<BuildDiagnostic> Errors { get; }  // all errors, not the first one
}
```

- **An invalid definition is a programming error, so `Build` throws.** It collects every error first (missing
  translations, undefined style keys, an empty path used as a value, scope violations) and throws once. Core returns a
  `ProcessingResult` because its errors come from users' templates and data; here they come from code.
  - **Path syntax is checked where the path is written.** An invalid segment throws `ArgumentException` from the
    `TemplatePath` indexer, so the stack trace points at the line in the definition.
  - **Composers are valid only inside their lambda.** Using one afterwards throws `InvalidOperationException`.
- **`Compose` does not see the format.** Definitions cannot branch on it, except through `Raw` (section 3.9).
- **`TemplateFormat` is core's enum.** The builder depends on core anyway, so it does not define a second one.
- **Thread safety.** `Build` is a pure function. Concurrent builds share nothing mutable.

### 3.3 Styles

The builder ships value types and a stylesheet, but **no theme type**. A token vocabulary is product design, so the
product defines its theme as its own record and passes it to the definition's constructor. The customer's theme slots
arrive the same way.

```csharp
public readonly record struct Length;          // integer 1/1000 pt internally
public static class Units                      // 12.Pt(), 25.Mm()
{
    public static Length Pt(this double value);
    public static Length Mm(this double value);
}
public readonly record struct Color;            // Color.FromHex("1F4E79")
public sealed record FontFace(string Family);       // referenced by name; no fallback (no ODF equivalent; additive later)
// #288: FontFace.Embedded(byte[] ttf) / Embedded(string family, byte[] ttf), IsEmbedded

public sealed record ParagraphStyleKey(string Id, string DisplayName);   // DisplayName is what Word / LibreOffice show
public sealed record CharacterStyleKey(string Id, string DisplayName);

public sealed class StyleSheet
{
    public static StyleSheet Create(Action<StyleSheetComposer> styles);
}
// usage (product code)
StyleSheet sheet = StyleSheet.Create(s =>
{
    s.Defaults(d => d.Font(theme.Body).Size(10.5.Pt()).Color(theme.Ink));
    s.Paragraph(Styles.Body, p => p.LineHeight(15.Pt()).SpaceAfter(8.Pt()));
    s.Paragraph(Styles.Heading1, p => p.BasedOn(Styles.Body).Font(theme.HeadingFont).Size(16.Pt())
                                       .SpaceBefore(20.Pt()).KeepWithNext().KeepLinesTogether().OutlineLevel(1));
    s.Character(Styles.Emphasis, c => c.Font(theme.MediumFont).Color(theme.Accent));
});
```

- **A key the stylesheet does not define is a build error.**
- **Paragraph and character styles become real named styles in both formats.**
- **The builder owns one well-known key, `BuilderStyles.TemplateMarker`** (display name "Template Marker").
  - By default it is visible and subtle: Courier New, 8 pt, grey `#737373`, no spacing, kept with the next
    paragraph. It is written only when the template has markers. A style sheet that defines the key replaces the
    default entirely.
  - A product's stylesheet can change how it looks, and so can a customer, through the style in Word or LibreOffice.
  - **There is no hidden-marker mode** (maintainer decision). A customer adapting a copy has to see the template's logic
    to delete a block together with its markers (D3).
- **Table, box and list styles come later.** They are builder-level concepts (tasks 10, 14 and 15) that each writer
  resolves. Word table styles have no ODF counterpart, so they are not mapped one to one.

### 3.4 Content

```csharp
public class BlockComposer          // body, header, footer, cell, box, and the bodies of If and ForEach
{
    public void Paragraph(ParagraphStyleKey style, string text);
    public void Paragraph(ParagraphStyleKey style, TemplatePath value, ValueFormat? format = null);
    public void Paragraph(ParagraphStyleKey style, Action<InlineComposer> content);
    public void PageBreak();
    public void Component(ITemplateComponent component);

    public IfChain If(Condition condition, Action<BlockComposer> then);
    public void Switch(TemplatePath value, Action<SwitchComposer> cases);
    public void ForEach(TemplatePath collection, string itemName, Action<LoopItem, BlockComposer> body);
    // later tasks: Table, List, Box, Rule, Image, TableOfContents, Raw
}

public sealed class IfChain
{
    public IfChain ElseIf(Condition condition, Action<BlockComposer> then);
    public void Else(Action<BlockComposer> otherwise);   // void: nothing can follow an else
}

public sealed class SwitchComposer
{
    public void Case(string value, Action<BlockComposer> then);
    public void Case(IEnumerable<string> values, Action<BlockComposer> then);   // emitted as `in (...)`
    public void Default(Action<BlockComposer> otherwise);                       // mandatory: a missing Default is a build error
}

public sealed class InlineComposer
{
    public void Text(string text, CharacterStyleKey? style = null);         // literal "{{" is a build error (TB1002): Templify has no escape for it
    public void Value(TemplatePath path, ValueFormat? format = null, CharacterStyleKey? style = null);
    public void Tab();
    public void TabRight();                                                 // right tab stop at the frame's edge — task 9 (needs page setup)
    public void LineBreak();
    // later tasks: If (inline, text only), Expression, Image, PageNumber, PageCount
}

public sealed class ValueFormat      // closed set: the engine's format specifiers
{
    public static ValueFormat Date(string pattern);
    public static ValueFormat Number(string pattern);
    public static ValueFormat Currency { get; }
    public static ValueFormat Uppercase { get; }
    public static ValueFormat Lowercase { get; }
    public static ValueFormat Raw { get; }
    public static ValueFormat YesNo { get; }
    public static ValueFormat Checkbox { get; }
    // further boolean formats as in core: TrueFalse, OnOff, Enabled, Active, Checkmark
    public static ValueFormat Join(string? itemPath = null, string? separator = null);   // :join, :join:Name:"; "
    public static ValueFormat Custom(string specifier);   // escape hatch, e.g. a registered boolean formatter
}
```

A `Switch` without any case writes its default unconditionally, with no markers. `Switch` exists because the engine cannot make a property depend on data: a fill colour per status, for example. The
answer is an if/elseif/else chain with one variant per value. A mandatory `Default` prevents the failure seen in practice,
where an unmapped value printed nothing.

### 3.5 Paths, conditions, loops

```csharp
public readonly struct TemplatePath
{
    public static TemplatePath Root(string path);       // also: template.Data["order"]
    public TemplatePath this[string relative] { get; }  // "customer.address.city": dotted, each segment validated
    public TemplatePath this[int index] { get; }        // items[0]
    public TemplatePath Key(string key);                // one dictionary-key segment, validated against the engine's grammar

    public Condition Eq(string value);
    public Condition Eq(double value);
    public Condition NotEq(string value);
    public Condition Gt(double value);                  // also Ge, Lt, Le
    public Condition In(params string[] values);
    public Condition Contains(string value);            // also StartsWith, EndsWith
    public Condition Exists { get; }
    public Condition IsEmpty { get; }
    public Condition IsNotEmpty { get; }
    public static implicit operator Condition(TemplatePath path);   // truthiness
    public static Condition operator !(TemplatePath path);          // C# does not apply the implicit conversion to find
    public static Condition operator &(TemplatePath a, TemplatePath b);   // operators when both operands are paths,
    public static Condition operator |(TemplatePath a, TemplatePath b);   // so TemplatePath declares them itself (#265)
}

public abstract class Condition      // a small AST
{
    public static Condition operator !(Condition operand);
    public static Condition operator &(Condition left, Condition right);
    public static Condition operator |(Condition left, Condition right);
    public static Condition Raw(string expression, params TemplatePath[] uses);   // checked with ConditionEvaluator.Validate
}

public sealed class LoopItem        // only exists inside a ForEach lambda
{
    public TemplatePath Self { get; }                     // {{item}}
    public TemplatePath this[string relative] { get; }    // {{item.relative}}
    public Condition IsFirst { get; }
    public Condition IsLast { get; }
    public TemplatePath Index { get; }
    public TemplatePath Number { get; }
    public TemplatePath Count { get; }
}
```

- **One internal emitter** turns the AST into `{{#if}}`-dialect text. It owns:
  - precedence, so `!a.Eq("x")` becomes `not (a = "x")`;
  - quoting and escapes;
  - escaping keyword-named segments as `[in]`;
  - invariant number formatting.

  String operands are always quoted, so the engine's bareword rule never applies to generated templates. Numbers are
  written in decimal notation (`100000000000000000000`, never `1E+20`): the condition lexer reads a number as one word
  of digits, `.` and a leading `-`. Strings with `{{`, `}}`, control characters or typographic double quotes (which
  the engine normalises to `"`) are rejected where they are written.
- **`ForEach` always emits a named iteration variable.** The names make the contract exact and the markers readable for
  a customer editing the file.
- **Scope rules** (#266). A path remembers the loop whose variable it starts from.
  - **Item names.** Letters, digits and `_`, starting with a letter or `_`. Not a Templify keyword (`in`, `this`,
    `and`, `empty`, …), not the name of an enclosing loop's variable. Checked where written (`ArgumentException`).
  - **TB4001.** A loop variable's path used outside its loop, for example a captured `LoopItem` after the loop.
  - **TB4002.** `@index`/`@number`/`@count`/`@first`/`@last` of an outer loop used inside a nested loop: the
    engine's metadata always refers to the innermost loop. An outer `LoopItem`'s *paths* stay usable in inner loops.
  - **TB4003.** A path from the data's root whose first segment is the name of an enclosing loop variable: the engine
    would resolve it to the item.
  - **Known limit.** Inside a loop the engine also resolves names implicitly on the current item. A root path such as
    `order.number` resolves to the item's `order` property if the item has one. The builder cannot see the data, so
    the data contract (#267) and the item names chosen by the definition are the guard.
- **Product helpers.** For dictionary-keyed payloads, products write plain C# helpers over `TemplatePath`. The builder
  needs no feature for that:

```csharp
static Condition IsYes(TemplatePath scope, string question) =>
    scope["answers.byKey"].Key(question)["options.byKey"].Key("yes")["selected"];
```

### 3.6 How the writers place markers

| Engine rule | Guaranteed by |
|---|---|
| A block marker sits alone in its paragraph, as one run | `If`, `Switch` and `ForEach` exist only on block, row and list composers. The writer emits a one-run marker paragraph in `TemplateMarker`. |
| A table-row loop has `{{#foreach}}` and `{{/foreach}}` each in its own row, with the table's grid width | `ForEachRow` (task 11) emits full-grid marker rows. |
| `{{#elseif}}` comes before `{{#else}}`; `{{#else}}` comes last | `IfChain`; `Else` returns `void` |
| No loop inside a paragraph | `InlineComposer` has no `ForEach` |
| Loop metadata exists only inside a loop; iteration variable names are valid | `LoopItem`; names are validated at build |
| Every table cell ends with a paragraph | the writer (core also guarantees this after processing) |
| Placeholder and format syntax is valid | `TemplatePath` segment validation; the closed `ValueFormat` set |

### 3.7 Components and sharing

```csharp
public interface ITemplateComponent { void Compose(BlockComposer content); }
```

- **Simple blocks need no interface.** An extension method on `BlockComposer` is enough.
- **The model keeps a component as a unit** (`ComponentBlock`: the component and its content). This is the hook for
  exporting definitions with component references (#274). A component composes in its caller's loop scope.
- **Implement `ITemplateComponent`** for a block with state, or one that is built through dependency injection.
- **Sharing sections between documents** means using the same component or method in several definitions.

### 3.8 Localisation, contract, identity

```csharp
public interface ITextCatalog { bool TryGet(string key, CultureInfo language, out string text); }
public static class TextCatalog
{
    public static ITextCatalog Empty { get; }
    public static ITextCatalog FromResourceManager(ResourceManager resources);   // no parent-culture fallback by default
    public static ITextCatalog FromDictionary(IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> texts);
    public static ITextCatalog Combine(params ITextCatalog[] catalogs);          // later catalogs override earlier ones
}
// in Compose: string title = template.Texts["invoiceTitle"];   (TemplateTexts, also .Language)
// every missing (key, language) pair is collected and reported in the single TemplateBuildException (TB5001)
// Lookup (#269): specific → neutral culture (de-AT → de), never the invariant culture — a missing German text fails
// the build instead of printing the default language. FromResourceManager(rm, neutralLanguage): the neutral .resx
// answers only for the declared neutral language (and its specific cultures).

public sealed class DataContract
{
    public IReadOnlyList<ContractEntry> Entries { get; }    // canonical paths: "order.items[].name"
    public string Hash { get; }                             // over the sorted entries; independent of language, format and theme
    public string ToJson();
    public static DataContract FromJson(string json);
    public IReadOnlyDictionary<string, object?> CreateWitness();   // every path present, every collection with one item
}
public sealed record ContractEntry(string Path, ContractUsage Usage);   // Value | Collection | Condition

public sealed record TemplateStamp(string TemplateId, int Version, string ContractHash, string Language,
                                   string BuilderVersion, string MinimumEngineVersion);

public static class TemplateInspector
{
    public static TemplateInspection Inspect(Stream template);    // format, Stamp (null = unknown origin)
    public static UploadCheckResult CheckUpload(Stream template, DataContract current, TemplateIdentity? expected = null);
}
public static class TemplateStampRemover
{
    public static bool Remove(Stream document);         // removes the stamp from a filled document, in place; DOCX and ODT
}
public sealed class UploadCheckResult
{
    public bool IsUsable { get; }                       // no syntax errors and no unknown paths
    public TemplateFormat Format { get; }
    public TemplateStamp? Stamp { get; }
    public TemplateOrigin Origin { get; }               // CurrentContract | DifferentContract | OtherTemplate | Unknown
    public ValidationResult Syntax { get; }             // core type
    public IReadOnlyList<string> UnknownPaths { get; }
}
```

- **The stamp says where a copy came from; it does not decide validity.** Other editors may drop custom properties, so a
  missing stamp means "unknown origin", never "reject". A different contract hash is not an error either: an additive
  payload change alters the hash while old copies stay valid.
- **Validity means the copy uses only known paths.** `CheckUpload` builds `current.CreateWitness()` and calls core's
  `TemplateProcessor.ValidateTemplate(stream, witness)`. The missing variables and missing-condition-variable warnings
  are the unknown paths. Core already resolves loop scopes, so this works on hand-edited copies with renamed aliases,
  as spike 1 confirmed (section 9).
  - `IsUsable` means: no `MissingVariable` errors, no `MissingConditionVariable` warnings, and no syntax errors.
  - `UnknownPaths` lists the paths **as the copy writes them** (`l.discount`, with the customer's alias), because the
    customer has to find them in that copy. Mapping them to canonical contract paths is not attempted.
  - Unknown paths that are comparison operands (`{{#if order.priority = "high"}}`) are only found once #271 is fixed in
    core. Task 7 depends on #271.
- **Stamp properties** (#268): `Templify.TemplateId`, `Templify.TemplateVersion`, `Templify.ContractHash`,
  `Templify.Language`, `Templify.BuilderVersion`, `Templify.MinimumEngineVersion`, all strings. The versions are the
  assemblies' informational versions without build metadata. `TemplateStampRemover` removes exactly the properties
  with the `Templify.` prefix and keeps all others; in DOCX it deletes the custom-properties part if nothing else is
  left.
- **Reading condition variables.** The engine does not yet expose the variable of a `MissingConditionVariable`
  warning, so `CheckUpload` reads it from the message until #275 lands; a test pins the wording.
- **Where the stamp is stored.** DOCX: `docProps/custom.xml`. ODT: `meta:user-defined` in `meta.xml`. Core's
  `DocumentProperties` option overwrites only the standard properties, so the stamp survives into filled documents.
- **The stamp stays in filled documents by default** (maintainer decision). Support can tell from a customer's document
  which template and version produced it. A backend that sends documents to external recipients, and does not want
  internal ids in their metadata, calls `TemplateStampRemover.Remove(stream)` after filling. Core does not change for
  this. The helper removes only the builder's own properties, which use a builder-specific name prefix, and leaves every
  other custom property alone.
- **Contract details** (#267).
  - **Collected from the model:** values, every path of every condition (including `Switch` values and the declared
    `uses` of `Condition.Raw`), loop collections, and the content of components. Loop metadata (`@index`, …) is not
    data and is left out.
  - **Canonical paths.** Loop variables are resolved: `line.description` inside `foreach line in order.lines`
    becomes `order.lines[].description`, and an item printed as a whole becomes `order.lines[]`.
  - **Hash.** SHA-256 over the sorted lines `"<usage> <path>\n"` (usage in lower case), as lower-case hex.
  - **JSON.** `{ "version": 1, "hash": …, "entries": [ { "path": …, "usage": "value|collection|condition" } ] }`.
    `FromJson` recomputes the hash.
  - **Witness.** Nested dictionaries, a one-item list per collection, `"x"` for every leaf. Filling a template with
    its own witness raises no missing variable and no warning; a test asserts this for both formats.
- **The contract is the language parity check.** Its hash is equal across languages and formats; a test asserts this. That
  replaces a hand-written EN/DE parity test.

### 3.9 Escape hatch (task 17)

```csharp
public sealed record RawBlock
{
    public required Func<RawContext, XElement> Docx { get; init; }    // both are required: parity by signature
    public required Func<RawContext, XElement> Odt { get; init; }
    public IReadOnlyList<TemplatePath> Uses { get; init; } = [];       // declared for the contract
}
```

It takes `XElement`, not OpenXML SDK types. SDK types never appear in the builder's public API, so the two packages
cannot become version-coupled through it.

Font embedding as built (#288), in section 3.3's terms:

- `FontFace.Embedded(ttf)` reads the family (name id 1, Windows English first) and `Embedded(family, ttf)` accepts a
  family the file declares (id 1 or 16). The bytes are copied; equality is family plus file hash.
- TrueType outlines only: Word embeds TrueType, so `OTTO` (CFF), `ttcf` and WOFF are refused with a hint.
- `fsType`: installable (0) or editable (bit 3) only. A template is edited after download, so preview-and-print
  (bit 2), restricted (bit 1) and bitmap-only (bit 9) are refused at `Embedded`.
- One file per family (TB2011 for two different files); one face (`embedRegular`) per family, matching the
  one-family-per-weight rule. No subsetting: pass pre-subset files.
- Measured with LibreOffice 26.8: a family installed nowhere renders from the document in the PDF conversion, from
  DOCX and from ODT (test `LibreOffice_RendersWithTheEmbeddedFont`, checked with `pdffonts`). Word is not tested on
  CI; the DOCX passes the schema validation.

As built (#285):

- `c.Raw(RawBlock)` is on every `BlockComposer` (body, cells, boxes, headers, footers, loop and branch bodies).
  `RawContext` gives the container's `Frame` width and `Placeholder(path, format?)`, which returns the placeholder
  text only for a path declared in `Uses` (otherwise TB9102), so the contract cannot miss what the XML reads. `Uses`
  paths are scope-checked like any other path (TB4001–4003) and recorded as values.
- Allowed roots: DOCX `w:p`, `w:tbl`; ODT `text:p`, `text:h`, `text:list`, `table:table`. Anything else, including
  an element of the other format, is TB9101. The DOCX element becomes a typed `Paragraph`/`Table`, so the writer's
  own rules apply to it (a paragraph between touching tables, the closing paragraph).
- The XML is written as given. It cannot add styles, numbering, relationships or ODT automatic styles; it may refer
  to styles of the sheet. A construct that needs those belongs in the vocabulary, not in `Raw`.

### 3.10 Example

```csharp
public sealed class InvoiceTemplate(BrandTheme brand) : ITemplateDefinition
{
    public TemplateIdentity Identity { get; } = new("invoice", Version: 3);

    public void Compose(TemplateComposer template)
    {
        var T = template.Texts;
        TemplatePath invoice = template.Data["invoice"];

        template.Styles(BrandStyles.Create(brand));
        template.Properties(p => p.Title(T["invoiceTitle"]).Author(brand.CompanyName));

        template.Content(c =>
        {
            c.Paragraph(BrandStyles.Title, T["invoiceTitle"]);
            c.Paragraph(BrandStyles.Body, x =>
            {
                x.Text(T["customer"] + ": ");
                x.Value(invoice["customer.name"], style: BrandStyles.Strong);
            });

            c.ForEach(invoice["lines"], "line", (line, b) =>
            {
                b.If(!line.IsFirst, r => r.Paragraph(BrandStyles.Separator, ""));
                b.Paragraph(BrandStyles.Body, x =>
                {
                    x.Value(line["description"]); x.TabRight();
                    x.Value(line["amount"], ValueFormat.Currency);
                });
            });

            c.Switch(invoice["status"], s =>
            {
                s.Case(["paid", "settled"], b => b.Paragraph(BrandStyles.NoticeSuccess, T["paid"]));
                s.Case("overdue", b => b.Paragraph(BrandStyles.NoticeDanger, T["overdue"]));
                s.Default(b => b.Paragraph(BrandStyles.Notice, T["open"]));
            });
        });
    }
}

// at runtime, in the backend
GeneratedTemplate tpl = TemplateBuilder.Build(new InvoiceTemplate(theme with { Accent = tenant.Accent }),
    new TemplateBuildOptions { Format = TemplateFormat.Odt, Language = CultureInfo.GetCultureInfo("de"), Texts = InvoiceTexts.Catalog });
return Results.File(tpl.OpenRead(), tpl.MediaType, "invoice" + tpl.FileExtension);   // download, or pass to TemplateProcessor
```

## 4. Internal architecture

```
ITemplateDefinition.Compose ──▶ composers ──▶ semantic model (internal, immutable)
                                                │
                                                ▼
                     analysis: scopes, style keys, frame widths, literal "{{", contract, capability per format
                                                │   (errors → TemplateBuildException, warnings → GeneratedTemplate)
                                                ▼
                     TemplateSyntax: the one emitter of marker and placeholder text
                                    ┌───────────┴───────────┐
                               DocxWriter               OdtWriter
                                    └──────▶ DeterministicZip ◀──────┘
```

- **Semantic model.** Blocks (`Paragraph`, `PageBreak`, `If`, `Switch`, `ForEach`, `ComponentScope`, and later
  `Table`, `List`, `Box`, `Rule`, `Image`, `Toc`, `Raw`) and inlines (`Text`, `Value`, `Tab`, `Break`, and later
  `InlineIf`, `Expression`, `Image`, `Field`). `If` and `ForEach` stay semantic until the writer, which chooses their
  carrier: a marker paragraph, a full-grid row, or an ODT list item, which core treats like a row.
- **DocxWriter.** Builds the XML parts with the OpenXML SDK's typed elements, which keep the schema's child order. It
  writes `document.xml`, `docProps/core.xml`, and later `styles.xml`, `settings.xml`, `docProps/custom.xml`,
  `numbering.xml` and the header, footer and media parts. Ids come from counters: `docPr`, bookmarks, numbering. It
  writes no rsids. The SDK stays behind the writer boundary.

  The writer assembles **the package itself** rather than through the SDK's packaging, because that packaging is not
  deterministic. Measured with SDK 3.5.1 while building task 1, it gives every build:
  - random relationship ids (`R2a7031b3f8ae41e2`);
  - a random core-properties part name (`package/services/metadata/core-properties/<guid>.psmdcp`);
  - the current time on every entry.

  The writer therefore uses fixed ids (`rId1`, …), the standard part names (`docProps/core.xml`) and a content-types
  part of its own.
- **OdtWriter.** Uses `System.Xml.Linq` and `System.IO.Compression`, the same stack as core's ODT engine. It writes
  `content.xml`, `styles.xml`, `meta.xml` and `META-INF/manifest.xml`, with `mimetype` first and stored. Automatic styles
  get a builder prefix (`B…`), so they cannot collide with core's markdown `T{n}` styles.
- **DeterministicZip.** Writes both formats with a fixed entry order, a fixed timestamp (2000-01-01) and zeroed
  external attributes.
  - The bytes are reproducible on the same runtime and operating system family.
  - They are not byte-identical between Windows and Unix: `ZipArchive` records the host platform in the "version made
    by" field. That is acceptable, because `ContentHash` serves as a cache key within a deployment.

  Determinism also requires:
  - no GUIDs;
  - counter-based ids;
  - invariant formatting of every number;
  - integer units, rounded once per target unit;
  - no creation or modification time unless the options set one.

### 4.1 Format mapping (MVP part)

| Concept | DOCX | ODT |
|---|---|---|
| Paragraph style | `w:style w:type="paragraph"`, `w:basedOn`, `w:next`, `w:outlineLvl` | `style:style style:family="paragraph"` in `office:styles`, `style:parent-style-name`, `style:next-style-name`, `style:default-outline-level`; a paragraph with an outline level is written as `text:h` |
| Character style | `w:style w:type="character"`, `w:rStyle` | `style:style style:family="text"`, `text:span text:style-name` |
| Defaults | `w:docDefaults` (font, size, colour, `w:lang`), widow control on | `style:default-style` per family; `fo:language` and `fo:country` |
| Spacing and keeping together | `w:spacing`, `w:keepNext`, `w:keepLines` | `fo:margin-top`/`fo:margin-bottom`, `fo:line-height` / `style:line-height-at-least`, `fo:keep-with-next`, `fo:keep-together` |
| Tab, right tab | `w:tab`; `w:tabs` with a right stop at the frame width | `text:tab`; `style:tab-stops` |
| Line break | `w:br` | `text:line-break` |
| Page break | a paragraph of its own: `w:p/w:r/w:br w:type="page"` | an empty paragraph of its own in the automatic style `TemplifyPageBreak` (`fo:break-after="page"`, so no empty line on the new page) |
| Character style on text | `w:rPr/w:rStyle` on the run | `text:span text:style-name` |
| Page (#277) | `w:sectPr`: `pgSz`, `pgMar` (top/right/bottom/left/header/footer), `titlePg`; always written (default A4, 25 mm margins, 12.5 mm header/footer distance) | `style:page-layout` "TemplifyPageLayout" + master page "Standard" |
| Header / footer | `header1.xml`, `footer1.xml` (+ `header2.xml`/`footer2.xml` for a distinct first page, empty for "none"), `headerReference`/`footerReference` | `style:header`/`style:footer` (+ ODF 1.3 `style:header-first`/`style:footer-first`); ODF measures the top margin to the header, so page `fo:margin-top` = header distance and the header's `fo:min-height` = top margin − header distance: the body starts where Word starts it |
| Page number / count | complex fields `PAGE` / `NUMPAGES` with a cached `1` | `text:page-number text:select-page="current"` / `text:page-count` |
| Table (#278) | `w:tbl`: `tblW`, `tblBorders` (outer + insideH/insideV from the table style), `tblLayout fixed`, `tblCellMar`; `tblGrid` from the resolved widths; cell `tcW` = the sum of its rounded grid columns (so `tcW` and `tblGrid` agree), `gridSpan`, `shd`, `vAlign`, header-rule as `tcBorders/bottom`; rows `cantSplit` (unless `AllowSplit`), header rows `tblHeader`; `SpaceAfter` as a 1 pt paragraph; a paragraph between two touching tables (Word would merge them) | `table:table` with automatic table, column, row (`keep-together`) and cell styles; `border-model="collapsing"` and per-cell borders (ODF has no table-level borders); header rows in `table:table-header-rows`; spans with `table:covered-table-cell`; `SpaceAfter` as the table's `fo:margin-bottom` |
| Row loops / row conditionals (#279) | marker rows: one `w:tc` with `gridSpan` = all columns and `tcW` = the table width, holding the marker paragraph | marker rows: one cell with `table:number-columns-spanned` = all columns plus `table:covered-table-cell`s |
| Inline conditional / expression (#281) | markers as plain runs inside the paragraph around the branch runs (`{{#if …}}`, `{{#elseif …}}`, `{{#else}}`, `{{/if}}`, nesting allowed); `{{(condition):format}}` as one run. Branch content is text and values only (`TextComposer`), no tabs, breaks or images. Expressions use the engine's inline dialect (only `true` is truthy) | the same text inside `text:p`, white-space encoded |
| Image (#280) | `word/media/imageN.{png,jpeg}`, each distinct image (by SHA-256) once; relationships per part (`document.xml.rels`, `header1.xml.rels`, …); inline DrawingML with a document-wide `docPr` counter and the alt text as `descr`; EMU = pt × 12 700 | `Pictures/<hash>.{png,jpeg}` + manifest entries; `draw:frame` (`text:anchor-type="as-char"`, unique `draw:name` per part prefix) with `draw:image` and `svg:title`/`svg:desc` (alt text) |
| Box / Rule (#282) | a box is a one-cell `w:tbl` over the container's width: the box style's border as `tblBorders`, padding as `tblCellMar`, background as the cell's `shd`; rows may split. A rule is an empty paragraph (exact 1 pt line) with a top `pBdr` and spacing | the same one-cell `table:table` with per-cell border, padding and background; a rule is an empty paragraph in an automatic style (`fo:border-top`, `fo:line-height` 1 pt) |
| Bullet list (#283) | items are paragraphs with `numPr` (`ilvl`, one `numId` per list style used) after `pStyle` (the list's item style); `numbering.xml` with an `abstractNum` of bullet levels (`lvlText`, `ind left/hanging`); loops and conditionals over items are block markers between item paragraphs | `text:list-style` (`text:list-level-style-bullet`, label alignment: tab stop at the indent, `fo:text-indent` = −hanging); each item is a `text:list` of its own, nested to its level — consecutive lists of one style read as one list, and loop/conditional markers stay paragraphs between them. LibreOffice's *text* export draws its own bullets per level; the rendered document shows the style's |
| Table of contents (#284) | a paragraph with a complex field `TOC \o "1-N" \h \z \u`, `w:dirty="true"`, no cached result | `text:table-of-content` (`text:outline-level` N, an entry template per level with link, text, right tab with dot leader, page number), empty `text:index-body` |
| End of a part | the body, headers and footers always end with a paragraph (Word keeps one after a final table; LibreOffice 26.8's text export loops forever on a DOCX whose body ends with a table — found in #279) | — |
| Column widths | fixed widths as given; relative weights share the container frame (page frame, or a cell's width minus its horizontal padding for nested tables) | same resolution |
| Right tab (`TabRight`) | `w:tabs/w:tab w:val="right"` at the frame width, measured from the frame's start | an automatic paragraph style with `style:tab-stop style:type="right"`, positioned at frame width − the style's start indent (ODF measures from the indent) |
| Marker | one-run paragraph in `TemplateMarker` | `text:p` in `TemplateMarker` with the marker as its only text |
| Language | `w:lang` in the defaults, `dc:language` | `fo:language` and `fo:country`, `dc:language` |
| Properties | `docProps/core.xml` (title, creator, subject), `docProps/custom.xml` (stamp) | `meta.xml` (`dc:title`, `meta:initial-creator`, `dc:subject`, `meta:user-defined`) |
| Embedded font (#288) | `word/fonts/fontN.odttf`, obfuscated (the first 32 bytes XOR the font key's 16 bytes in reverse order), related from `word/_rels/fontTable.xml.rels`; `word/fontTable.xml` `w:font` with `w:embedRegular r:id w:fontKey`; `word/settings.xml` `w:embedTrueTypeFonts`; the key is a GUID from the file's SHA-256 (deterministic); named relationship ids (`rIdSettings`, `rIdFontTable`), so numbered ids do not shift; nothing is written without embedded fonts | `Fonts/<sha256[..16]>.ttf` + manifest (`application/x-font-ttf`); `style:font-face` with `svg:font-face-src/svg:font-face-uri` (`svg:font-face-format svg:string="truetype"`) |
| Raw block (#285) | the given `w:p` / `w:tbl`, as typed elements | the given `text:p` / `text:h` / `text:list` / `table:table` |

Tables, boxes, lists, page setup, headers, footers, fields, images and the table of contents are mapped in the design
section of their tasks. The review's first draft is in #261.

## 5. Verification

Every builder test asserts these invariants for both formats, through a shared helper:

1. **Structure.** `TemplateProcessor.ValidateTemplate` returns no errors.
2. **Contract.** `ValidateTemplate(witness)` reports no missing variables. The contract hash is equal across languages and formats.
3. **Render.** Processing sample data through `TemplateProcessor` with `MissingVariableBehavior.ThrowException` succeeds without warnings.
4. **Schema.** DOCX passes `OpenXmlValidator`; ODT passes core's `OdtDocumentVerifier` package checks.
5. **Determinism.** Two builds give equal bytes, also under tr-TR and ar-SA.
6. **LibreOffice.** In `[Trait("Category", "LibreOffice")]` tests, mandatory locally as for core: the built template and the filled document both open and convert.

The consumer-facing test kit, `TriasDev.Templify.Builder.Testing` (task 18), packages the same checks plus a canonical
text dump for snapshot tests. A consumer builds at runtime, so its CI is where its definitions get verified.

As built (#286), test-framework neutral:

- `TemplateVerifier.VerifyAll(definitionFactory, options)` builds every language × format (`TemplateVerificationOptions`:
  `Languages`, `Formats`, `Texts` per language, `SampleData` as JSON, `RequireEqualContracts`, `FailOnBuildWarnings`,
  `DeterminismCultures` = tr-TR, ar-SA) and throws `TemplateVerificationException` listing every failure; `Verify`
  returns the `TemplateVerificationReport` instead. Checks (`VerificationCheck`): build, structure (`ValidateTemplate`),
  contract (`ValidateTemplate(witness)` reports no missing variable, and the witness fills without warnings), fill
  (sample data, the target language as fill culture), package (template and every filled document), determinism
  (a fresh definition from the factory under each culture), contract parity. Single checks: `CheckTemplate`,
  `CheckFill`, `CheckPackage`.
- The ODT package check is the kit's own (no test-project code ships): `mimetype` first, stored, no data descriptor,
  no extra field, ODT media type; manifest root type and listed files present; well-formed XML.
- `TemplateSnapshot.Create` dumps templates or filled documents in one notation for both formats (style per paragraph,
  `→` tab, `↵` break, `[image]`, `{FIELD}`, `[list n]`, tables as indented rows/cells, then headers and footers). The
  DOCX and ODT dumps of the same definition are equal unless DOCX needs layout paragraphs (after a table that ends a
  part, between touching tables); a test asserts the equality.
- `LibreOfficeConverter` (`IsAvailable`, `Convert`, `ToPdf`, `ToText`) with a private profile per conversion.
- Dogfooding: the builder tests' `AssertInvariants`/`AssertPackage` are `CheckTemplate`/`CheckPackage`, so every builder
  test now also fills the contract's witness.

## 6. Support matrix

| Target | Supported |
|---|---|
| DOCX in Microsoft Word (Windows, Mac) | yes |
| DOCX in LibreOffice (including the PDF conversion path) | yes |
| ODT in LibreOffice | yes |
| ODT in Word | best effort, not tested |

Known behaviour of the converters, documented rather than worked around:

- **Word's built-in style names.** LibreOffice maps a DOCX style named like a built-in Word style (`Body Text`,
  `Heading 1`, `Title`) to its own style (`Text body`, `Heading 1`, …) when it opens the file. A display name of
  your own (`Body Copy`) survives unchanged. Measured in task 2 (#263).

- **Cell and box padding vs. border.** LibreOffice starts cell text after the padding plus (part of) the border
  width; the two formats differ by under 2 pt for a 1 pt border (DOCX measures padding from the border's inner
  edge, ODT centres collapsed borders on the edge). Measured in #282; accepted.

The user-facing version of this matrix, the concepts and the known limits are on the documentation page
`docs/for-developers/template-builder.md` (#287). The sample `samples/TriasDev.Templify.Builder.Sample` uses every
construct, verifies itself with the test kit and runs as a builder test (`SampleTests`), so CI keeps it working.

Known differences become build warnings when the construct is used. The first entries arrive with their tasks:

- **table of contents (#284, measured):** a DOCX TOC field (dirty, `\o "1-N" \h \z \u`, no `\t`) is built by
  Word on open (fill with `UpdateFieldsOnOpen = Auto`, which writes `w:updateFields`). LibreOffice does **not**
  build a TOC on load or on PDF conversion, neither from DOCX nor from ODT (an empty `text:index-body`); it stays
  empty until a user updates it. The build warns (TB9001, `GeneratedTemplate.Warnings`) in both formats. For a
  server-side PDF with a TOC, the converter has to update the indexes (for example a LibreOffice macro);
  out of scope for the MVP;
- paragraph-border "between" rules have no ODF equivalent, which is why boxes are tables (task 14).

## 7. Packaging and release

- **Projects:**
  - `src/TriasDev.Templify.Builder`: TFMs as the library, its own `PublicAPI.Shipped.txt`/`PublicAPI.Unshipped.txt`
    and `packages.lock.json`, and a `ProjectReference` to core, which becomes a NuGet dependency on the core version of
    the build;
  - `src/TriasDev.Templify.Builder.Testing` (#286): the consumer test kit, same TFMs, own `PublicAPI` files, released
    with the builder on its version line;
  - `tests/TriasDev.Templify.Builder.Tests`.

  All are added to `templify.slnx` under the existing solution folders.
- **Release.**
  - **Since task 1:** the root release-please package has `exclude-paths` for `src/TriasDev.Templify.Builder`,
    `src/TriasDev.Templify.Builder.Testing` and `tests/TriasDev.Templify.Builder.Tests`. A commit whose files all lie there does not bump core. A commit that also
    touches shared files (`templify.slnx`, docs, CI) still counts for core; its release-notes entry is edited by hand,
    as today.
  - **Before the first preview release:** a second release-please package, component `templify-builder`, with tags
    `builder-v0.x.y` and `bump-minor-pre-major`. The publish job packs and pushes the builder too. Until then nothing
    publishes the builder, so it cannot reach NuGet by accident.
  - **As built (#289):** package `src/TriasDev.Templify.Builder`, component `builder` (so the tags read
    `builder-v0.x.y`), `release-type: simple` with `version.txt` as the single version source of the builder and the
    test kit (both csproj files read it), `additional-paths` for the test kit, the builder tests and the sample,
    `bump-minor-pre-major`, the first release pinned with `release-as: 0.1.0` (manifest `0.0.0` = never released).
    The release workflow has a separate `publish-builder` job on the per-path outputs
    (`src/TriasDev.Templify.Builder--release_created`); the core `publish` job still keys on the root outputs. It runs
    the builder tests, packs both packages and pushes them with Trusted Publishing (`nuget` environment).
  - **Core first.** The builder package depends on the core version in `TriasDev.Templify.csproj`, and the builder
    already needs core changes released after 1.10.0 (`:join` #273, `updateFields` position #256, …).
    `publish-builder` refuses to publish unless that core version is on nuget.org and the core source is unchanged
    since its tag. So the first preview follows the next core release (1.11.0).
- **Package validation.** Off until the first release. During `0.x` the baseline points at the previous `0.x` release.
  Breaking changes are allowed there but must be deliberate: entries move in `PublicAPI.Shipped.txt`, and the package
  validation difference is recorded in a compatibility suppression file.
- **The LibreOffice helper.** Core's test helper for finding and running `soffice` is shared with the builder tests by
  linking its source file. There is no shared test project.

## 8. Phase plan

Phase 1 ends with a usable letter- or notice-type template with a data contract and an identity stamp:

| Task | Issue |
|---|---|
| 0 Design spec, spikes | #270 |
| 1 Walking skeleton | #262 |
| 2 StyleSheet | #263 |
| 3 Inline content | #264 |
| 4 Block conditionals, `Switch` | #265 |
| 5 Paragraph loops, components | #266 |
| 6 Data contract | #267 |
| 7 Identity stamp, upload check | #268 |
| 8 Localisation | #269 |

Phase 2 (tasks 9–19: page furniture, tables, row loops, images, inline conditionals, box and rule, lists, table of
contents, raw, test kit, sample and docs) is listed in #261. Its issues are written after phase 1.

## 9. Spikes

Both spikes ran on 2026-11-25 against `main` (da01316) on net11.0, with hand-built DOCX and ODT templates fed to the
public `TemplateProcessor`. The spike code was temporary and is not committed. Its findings carry over into the tests of
tasks 7 and 14.

### 9.1 Spike 1: the upload check

**Setup.** One template, as DOCX and as ODT, containing:

- a value placeholder and a nested value placeholder;
- a dictionary-key condition (`order.flags.byKey.express.selected`);
- a named loop with a nested named loop that reads the outer item and the root;
- `{{@number}}`, and a condition on `@last`;
- a format specifier;
- an if/else on a comparison.

The witness was the payload a `DataContract` of that template would produce: every path present, every collection with one
item.

**Result.** The approach works. DOCX and ODT give identical results.

| Variant | Reported | Verdict |
|---|---|---|
| Unchanged | nothing | correct: no false positives from loop aliases, nested scopes, outer-item access, `@number`, `@last`, a dictionary-key path or a format specifier |
| Two sections deleted by hand | nothing | correct |
| Loop alias renamed by hand (`line` → `l`) | nothing | correct |
| New unknown paths: a root value, a property in a loop, a property in a nested loop, a collection, a root truthiness condition | `order.endDate`, `line.discount`, `tag.color`, `order.attachments` (all `MissingVariable` errors); `order.archived` (`MissingConditionVariable` warning) | correct |
| Unknown property in a renamed loop | `l.discount` | correct; the name is the one the copy uses |
| Loop without an alias, implicit `{{discount}}` | `discount` | correct |
| Unknown paths in conditions: truthiness, `not`, inside a loop | `MissingConditionVariable` warnings | correct |
| Unknown paths in an inline expression `{{(a and b):yesno}}` | `MissingVariable` errors | correct |
| Unknown paths as comparison operands: `=`, `>`, `in (…)`, `is empty`, also inside a loop | **nothing** | gap. The bareword rule treats an unresolved operand as a string literal. Filed as **#271** (core validator); task 7 depends on it. |

The "stamp removed" variant needs no engine check: the stamp is not part of validation. Task 7 covers it in
`TemplateInspector`.

**Consequences for the design** (section 3.8):

- `CheckUpload` combines `MissingVariables` with the `MissingConditionVariable` warnings.
- `UnknownPaths` keeps the copy's own names.
- No second path extractor is needed in the builder.

### 9.2 Spike 2: a box as a one-cell table

**Setup.** A body loop `{{#foreach box in boxes}}` around a one-cell table. The cell holds:

- a paragraph with `{{box.title}}` and `{{@number}}`;
- a block `{{#if box.show}}`;
- a paragraph loop `{{#foreach item in box.items}}`;
- a nested two-column table with a header row, a table-row loop `{{#foreach row in box.rows}}` (marker rows with an empty
  second cell) and a row reading the outer `box.title`;
- a trailing paragraph.

A second one-cell table holds only a conditional that is false. The data has two boxes: one with everything shown and
three rows, one with `show = false` and empty collections.

**Result.** Both engines processed everything correctly:

- The box repeats once per item.
- The `If` removes or keeps its content.
- The paragraph loop and the nested row loop expand and read the outer variable.
- Empty collections leave only the inner header row.
- The cell whose only content was removed **keeps an empty paragraph** in both formats.
- ODT: the cloned tables get unique names (`Box`, `Box_2`, `Inner`, `Inner_2`).
- `ValidateTemplate` reported no errors for either template.
- LibreOffice 26.8 converted both filled documents to PDF, with the same text in the same order.

**Consequence.** A box is a one-cell table, as section 6 assumes. No engine work is needed before task 14.

## 10. Maintainer decisions on the open questions (2026-11-25)

1. **Marker look.** Markers are visible and subtle by default, and they can be restyled through the `Template Marker`
   style. There is no hidden-marker mode (section 3.3).
2. **Stamp in filled documents.** It is kept by default. `TemplateStampRemover.Remove` strips it for documents that go
   to external recipients (section 3.8).
3. **Naming.** Composer types use the suffix `…Composer` (section 3.1).
