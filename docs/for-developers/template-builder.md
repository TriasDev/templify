# Template Builder (preview)

`TriasDev.Templify.Builder` describes Templify templates in C# and generates them at runtime as Word (`.docx`) or
OpenDocument Text (`.odt`) files. Templify fills the generated templates like any other template. Users can download
one, change it in Word or LibreOffice and upload it again, and the builder checks the uploaded copy against the data
the template is allowed to read.

!!! warning "Preview"
    The builder is on its own `0.x` version line. Its API can change between minor versions until the first stable
    release.

```bash
dotnet add package TriasDev.Templify.Builder --prerelease
dotnet add package TriasDev.Templify.Builder.Testing --prerelease   # in your test project
```

The complete example on this page is the sample `samples/TriasDev.Templify.Builder.Sample`. It builds an order
confirmation in German and English, as DOCX and ODT, verifies it, fills it and writes the files:

```bash
dotnet run --project samples/TriasDev.Templify.Builder.Sample -- output
```

## Concepts

### Definition

A template is a class that implements `ITemplateDefinition`. `Identity` names it and carries its version, and
`Compose` describes it. `TemplateBuilder.Build` runs `Compose` for one format and one language:

```csharp
public sealed class OrderConfirmation(BrandTheme theme) : ITemplateDefinition
{
    public TemplateIdentity Identity { get; } = new("order-confirmation", 1);

    public void Compose(TemplateComposer template)
    {
        TemplateTexts t = template.Texts;
        TemplatePath order = template.Data["order"];

        template.Styles(Styles.Create(theme));
        template.Content(c =>
        {
            c.Paragraph(Styles.Title, t["title"]);
            c.Paragraph(Styles.Subtitle, x =>
            {
                x.Text(t["orderNo"] + " ");
                x.Value(Styles.Strong, order["number"]);
            });
        });
    }
}

GeneratedTemplate template = TemplateBuilder.Build(
    new OrderConfirmation(theme),
    new TemplateBuildOptions { Format = TemplateFormat.Docx, Language = CultureInfo.GetCultureInfo("de-DE"), Texts = texts });
```

`GeneratedTemplate` holds the file (`Content`, `OpenRead()`, `WriteTo`), the `FileExtension` and `MediaType` for a
download, the data `Contract`, the identity `Stamp` and build `Warnings`. Building is deterministic. The same
definition and options give the same bytes, so `ContentHash` can serve as a cache key.

`Compose` does not see the format: one definition produces both. Mistakes in a definition (an undefined style, a
loop variable used outside its loop, a missing text) fail the build with a `TemplateBuildException`. It lists every
problem with a code (`TB…`).

### Styles

Styles are named, and the names are stable: a user who edits the template sees "Section Heading", not direct
formatting. A `StyleSheet` defines paragraph, character, table, box and list styles. It is usually built from a theme
record, so one theme restyles every template of a product:

```csharp
public static StyleSheet Create(BrandTheme t) => StyleSheet.Create(s =>
{
    s.Defaults(d => d.Font(t.Body).Size(10.Pt()).Color(t.Ink).ParagraphStyle(Body));
    s.Paragraph(Body, p => p.SpaceAfter(6.Pt()).LineSpacing(1.15));
    s.Paragraph(Heading, p => p.BasedOn(Body).Size(13.Pt()).Bold().Color(t.Accent).KeepWithNext().OutlineLevel(1));
    s.Character(Strong, c => c.Bold());
    s.Table(Grid, g => g.HeaderRule(1.Pt(), t.Accent).RowRule(0.5.Pt(), t.Line).CellPadding(3.Pt(), 5.Pt())
        .ParagraphStyle(TableText).HeaderParagraphStyle(TableHeader));
    s.Box(Success, b => b.Border(0.75.Pt(), t.SuccessBorder).Background(t.SuccessBackground).Padding(6.Pt(), 8.Pt()));
    s.List(Bullets, l => l.ItemStyle(ListItem).Level(0, "•", 6.Mm(), 4.Mm()));
});
```

A paragraph without a style of its own inherits one, so most paragraphs need none:

1. in a table cell, the row's `ParagraphStyle` (for example a totals row: `r.ParagraphStyle(TableHeader)`), then the table
   style's `HeaderParagraphStyle` (header rows) or `ParagraphStyle` (other rows);
2. the style sheet's default paragraph style, `Defaults(d => d.ParagraphStyle(Body))`;
3. otherwise none (the application's "Normal" / "Standard").

List items take their list style's `ItemStyle`; a list style without one lets its items inherit like any paragraph. The inherited style is written into the document explicitly, so Word
and LibreOffice show it by name. Without a default and without table text styles nothing is inherited.

The marker paragraphs (`{{#if …}}`, `{{#foreach …}}`) use the style "Template Marker": small and grey, visible but
quiet.

#### Fonts

`new FontFace("Source Sans 3")` refers to an installed font by name. `FontFace.Embedded(File.ReadAllBytes("Brand.ttf"))`
embeds the file in the document, so a server-side PDF conversion and recipients without the font installed see it:

- TrueType files (`.ttf`) only; the family name is read from the file;
- the font's licence must allow installable or editable embedding (`fsType`); other fonts are refused;
- one file per family and weight: embed the bold weight as its own family ("Brand Bold"), because Word for Mac
  ignores embedded bold faces;
- the builder does not subset, so pass a subset file to keep documents small.

### Paths

`template.Data` is the root of the data. An indexer appends segments: `order["customer.address"]["city"]` is
`order.customer.address.city`. Values are written with `Value(path)`, with an optional `ValueFormat` (`Currency`,
`Date("d MMMM yyyy")`, `Number("N2")`, `YesNo`, `Checkmark`, `Join(separator: ", ")`, `Raw`, …) and a character
style.

A segment may read one item of a list by its index (0 to 999): `order["items[0].name"]`, typed
`order.Path(o => o.Items[0].Name)`, in JSON `{{order.items[0].name}}`. The data contract keeps the index, and its
witness holds enough items for the highest index used. A loop cannot run over an indexed path (`groups[1].lines`):
Templify's loop marker does not accept an index.

### Conditions

Conditions are built from paths and are written in Templify's syntax, with the correct quoting and precedence:

```csharp
c.If(order["isExpress"] & order["total"].Gt(1000), b => b.Paragraph(Body, t["expressFree"]))
 .ElseIf(order["isExpress"], b => b.Paragraph(Body, t["express"]))
 .Else(b => b.Paragraph(Body, t["standard"]));

c.Switch(order["status"], s =>
{
    s.Case("paid", b => b.Box(Styles.Success, x => x.Paragraph(Body, t["statusPaid"])));
    s.Case(new[] { "overdue", "reminded" }, b => b.Paragraph(Body, t["statusOverdue"]));
    s.Default(b => b.Paragraph(Body, t["statusOpen"]));
});
```

A path alone tests truthiness. `!`, `&` and `|` combine conditions. Comparisons are `Eq`, `NotEq`, `Gt`, `Ge`, `Lt`,
`Le`, `In`, `Contains`, `StartsWith` and `EndsWith`, and the existence tests are `Exists`, `IsEmpty` and
`IsNotEmpty`. Inside a paragraph, `x.If(…)` writes an inline conditional, and `x.Expression(condition, format)` an
inline expression such as `{{(order.isExpress and order.total > 1000):yesno}}`.

### Loops

Loops always have a named item, so nested loops can reach outer items:

```csharp
c.ForEach(order["lines"], "line", (line, b) => b.Paragraph(Body, x =>
{
    x.Value(line.Number);          // {{@number}}
    x.Text(". ");
    x.Value(line["description"]);  // {{line.description}}
}));
```

Tables loop over rows (`table.ForEachRow`, `RowsIf`), and lists loop over items (`list.ForEach`). A `LoopItem` that
escapes its loop, or loop metadata of an outer loop used in an inner one, is a build error.

### Components

An `ITemplateComponent` is a reusable block, for example an address, a signature block or a status box. Templates
share it through `c.Component(new AddressBlock(order["customer.address"]))`. A component receives paths, so the same
block can print the customer's address and the delivery address.

### Data contract

Every build lists the data the template reads in `template.Contract`: each path with its use (value, collection,
condition). Loop items are written as `[]` (`order.lines[].description`). The contract has a `Hash`, serialises to
JSON (`ToJson`, `FromJson`) and creates a *witness*, a minimal data object that touches every path. The contract does
not depend on the language or the format. Different contracts for German and English mean the definition branches on
something it should not.

### Stamp and upload check

The build writes an identity stamp into the document properties: template id, version, contract hash, language and
builder version (`Templify.*` custom properties). When a user uploads an edited copy, `TemplateInspector.CheckUpload`
reads the stamp, validates the syntax and checks that the copy reads only paths of the current contract:

```csharp
UploadCheckResult check = TemplateInspector.CheckUpload(upload, current.Contract, new TemplateIdentity("order-confirmation", 1));
if (!check.IsUsable)
{
    // check.UnknownPaths lists the paths as the copy writes them, check.Syntax the template errors.
}
```

`check.Origin` says whether the copy comes from this template with the current contract (`CurrentContract`), from this
template with another contract (`DifferentContract`), from another template (`OtherTemplate`) or has no stamp
(`Unknown`). The stamp stays in filled documents. `TemplateStampRemover.Remove(stream)` removes it before a
document leaves the organisation.

### Localisation

Texts come from an `ITextCatalog`: `TextCatalog.FromResourceManager(Resources.ResourceManager)` for `.resx` files,
`TextCatalog.FromDictionary(...)` for tests and small products, and `TextCatalog.Combine(product, customer)` for
overrides (a later catalog wins). `template.Texts["key"]` returns the text in the build's language (with the usual culture fallback). A
missing key is a build error (TB5001). Values (dates, numbers, currency) are formatted by Templify with the culture
of the fill (`PlaceholderReplacementOptions.Culture`), not by the builder.

### Page, headers and footers

```csharp
template.Page(page => page
    .Size(PageSize.A4)
    .Margins(25.Mm(), 20.Mm(), 22.Mm(), 20.Mm())
    .Header(h => h.Paragraph(Styles.Small, x =>
    {
        x.Value(order["number"]);
        x.TabRight();
        x.PageNumber();
        x.Text(" / ");
        x.PageCount();
    }))
    .Footer(f => f.Paragraph(Styles.Small, t["footer"]))
    .FirstPage(first => first.Header(h => h.Paragraph(x => x.Image(theme.Logo, "Logo", ImageFit.Height(12.Mm()))))));
```

### Content blocks

| Block | API |
|---|---|
| Paragraph | `Paragraph(style, text \| path \| x => …)`, with `Text`, `Value`, `Tab`, `TabRight`, `LineBreak`, `Image`, `PageNumber`, `PageCount`, inline `If`, `Expression` |
| Page break | `PageBreak()` |
| Table | `Table(t => …)`: `Style`, `Columns` (`Fixed`, `Relative`), `HeaderRow`, `Row`, `ForEachRow`, `RowsIf`; cells with `ColumnSpan`, `Background`, `VerticalAlignment` and any blocks |
| Box | `Box(boxStyle, b => …)`: a framed, shaded block (a one-cell table) |
| Rule | `Rule(thickness, color[, above, below])` |
| List | `List(listStyle, l => …)`: `Item(text \| path \| x => …)`, `Item(level, …)`, `ForEach`, `If` |
| Image | `Image(ImageSource.Png(bytes), altText, ImageFit.Within(w, h))`, PNG and JPEG |
| Table of contents | `TableOfContents(maxLevel)` over the styles with an outline level |
| Component | `Component(component)` |
| Raw XML | `Raw(new RawBlock { Docx = ctx => …, Odt = ctx => …, Uses = [...] })` (see below) |

Templify removes the marker paragraphs (`{{#if}}`, `{{/foreach}}`, …) when it fills a template. So that a table cell
still ends with a paragraph and a table never touches the next one afterwards, the builder adds an invisible 1 pt
paragraph in the style "Template Spacer" where that could happen (a cell ending with a conditional, a loop or a table; a
table followed by a marker). A style display name must not contain a comma (TB2006).

A style always comes first (`Paragraph(style, text)`, `Text(style, text)`, `Value(style, path[, format])`), like the
options before the content in JSON. The methods that add content return their composer, so short parts chain:
`x.Text("Dear ").Value(Styles.Strong, order["name"]).Text("!")`, `r.Cell("A").Cell("B")`.

### Raw blocks

For a construct the builder does not have, `Raw` writes format XML. Both formats are required, and the paths the XML
reads must be declared in `Uses`. `RawContext.Placeholder(path)` returns the placeholder text only for a declared
path, so the data contract stays complete:

```csharp
TemplatePath name = order["customer.name"];
c.Raw(new RawBlock
{
    Uses = [name],
    Docx = ctx => new XElement(w + "p",
        new XElement(w + "r", new XElement(w + "t", ctx.Placeholder(name))),
        new XElement(w + "fldSimple", new XAttribute(w + "instr", " DATE "), new XElement(w + "r", new XElement(w + "t", "date")))),
    Odt = ctx => new XElement(text + "p", ctx.Placeholder(name), " ", new XElement(text + "date", "date")),
});
```

The root must be a paragraph or a table (`w:p`, `w:tbl`; `text:p`, `text:h`, `text:list`, `table:table`). The XML
cannot add styles, numbering or relationships.

## Typed data paths

Instead of string paths (`order["customer.name"]`), a definition can take its paths from the C# classes of its data.
The package's source generator writes C# 14 extension properties for every type used in `template.DataAs<T>()` and every
type reachable from it, so the paths read like the data:

```csharp
TemplatePath<Order> order = template.DataAs<OrderData>().Order;
c.Paragraph(x => x.Value(order.Customer.Name));
table.ForEachRow(order.Lines, "line", (line, rows) => rows.Row(r =>
{
    r.Cell(line.Description);
    r.Cell(line.Quantity);
}));
c.If(order.Total.Gt(1000) & order.IsExpress, b => b.Paragraph("Free express shipping"));
```

The data classes need nothing (no attribute, no `partial`); they may live in another assembly. Names follow the JSON
rules of the data: `[JsonPropertyName]` wins, otherwise the naming policy given to `DataAs<T>(policy)` (camelCase by
default); `[JsonIgnore]` hides a property. A collection property gives a `TemplateCollection<T>` for loops, and the loop
item has the item type's properties. Every generated property is shorthand for the lambda form, which always works too:
`order.Path(o => o.Customer.Name)`.

- A property whose name the path or loop item type already uses (`Number`, `Count`, `Index` on a loop item; `Path`,
  `Items`, `Exists`, `IsEmpty` on a path) is not generated (information TBG001); use the lambda there.
- Types of .NET itself (`Uri`, `Version`, …) get a path but are not walked into, like the lambda API. Private,
  protected and `file` types, ref structs (`Span<T>`) and ref-returning properties are skipped. An `[Obsolete]` data
  property gives an `[Obsolete]` path property.
- Types not reached from `DataAs` are added with `[assembly: TemplateData(typeof(Address))]` (for example in a component
  library). A project that sees another assembly's internals (a test project) uses that assembly's generated paths.
- C# 14 or later is required (.NET 10 SDK); below, the generator reports TBG002 and generates nothing.

## JSON definitions

A definition can also be a JSON file: something an editor or an agent changes without a build, and that a host stores
per customer. `TemplateJson.Export` writes a C# definition as JSON; `TemplateJson.Import` reads JSON as a definition that
builds the same bytes. Every builder test checks that round trip, both ways.

```csharp
TemplateJsonDocument exported = TemplateJson.Export(new OrderConfirmation(theme), options, new TemplateJsonExportOptions
{
    Components = catalog,                 // registered components are written as references
    TextLanguages = [de, en],             // translations go into the file's texts section
    Indented = true,                      // false: one line without whitespace, for storage
});
ITemplateDefinition definition = TemplateJson.Import(exported.Json, new TemplateJsonImportOptions
{
    Assets = exported.Assets,             // the images and fonts the file names
    Components = catalog,                 // the components it may refer to
    Shared = [theme],                     // what template.Share(…) gives components in C#
    Data = DataCatalog.From<OrderData>(), // optional: every data path must exist (TB9203)
    Theme = customerColours,              // optional: token → value, replaces the file's theme
    ValueFormats = ["janein"],            // formats the host registered with Templify
    AllowRawXml = false,                  // default: raw XML is refused (TB9202)
});
```

### Layout

An object or array whose one-line form fits in 100 characters (counting its key, not its indentation) is written on
one line, otherwise one member per line. Options come before the content key (`{"style": …, "paragraph": …}`), so a
reader sees what a block is before what it holds. A paragraph in the inherited style is a plain string, values sit in
the text as Templify placeholders, and conditions are strings in Templify's condition syntax:

```json
{"style": "LineGrid", "columns": ["10mm", 4, "16mm", "30mm"], "table": [
  {"header": true, "row": ["#", "[[description]]", "[[quantity]]", "[[amount]]"]},
  {"forEachRow": "order.lines", "as": "line", "rows": [
    {"row": ["{{@number}}", "{{line.description}}", "{{line.quantity}}", "{{line.amount:currency}}"]}
  ]},
  {"rowsIf": "order.discount > 0", "then": [{"row": [{"span": 3, "content": "[[discount]]"}, "{{order.discount:currency}}"]}]},
  {"paragraphStyle": "TableHeader", "row": [{"span": 3, "content": "[[total]]"}, "{{order.total:currency}}"]}
]}
```

(Hand-wrapped here; the writer applies the width rule.)

### Root

| Key | Holds |
|---|---|
| `templifyDefinition` | the format version, `1` (required) |
| `identity` | `{"id", "version"}` (required) |
| `texts` | translations, key first: `"title": {"de": "…", "en": "…"}` |
| `assets` | images and fonts by name: `"logo": "sha256:…"`; the content comes from `TemplateJsonImportOptions.Assets` |
| `theme` | `{"colors": {"accent": "#1F4E79"}, "fonts": {"heading": "Source Serif 4"}}` |
| `properties` | document properties: `title`, `author`, `subject` |
| `styles` | `defaults`, `paragraph`, `character`, `table`, `box`, `list`, each a map from style id to its settings |
| `page` | `size` (`"A4"`, `"Letter"`, … or `{"width", "height"}`), `margins`, `headerFooterDistance`, `header`, `footer`, `firstPage` (`header`, `footer`; `"none"` for an empty one) |
| `content` | the body: a list of blocks |

`$schema` is allowed and ignored; `components` is reserved. Any other key is an error.

### Content

| Where | Form |
|---|---|
| Text | a string with `{{path}}`, `{{path:format}}`, `{{(condition):format}}`, `[[key]]`, a tab `\t` and a line break `\n`; styled: `{"text": "{{order.number}}", "style": "StrongText"}`; a list mixes strings, styled text, `{"if": …}`, `{"tabRight": true}`, `{"pageNumber": true}`, `{"pageCount": true}`, `{"image": …}` |
| Block | a string (a paragraph in the inherited style), `{"style", "paragraph"}`, `{"style", "box": [blocks]}`, `{"style", "list": [items]}`, `{"style", "columns", "spaceAfter", "table": [rows]}`, `if`, `forEach`, `{"section": "name", "blocks": [blocks]}`, `component`, `pageBreak`, `rule`, `toc`, `raw` |
| Row | `{"header", "allowSplit", "paragraphStyle", "row": [cells]}`, `forEachRow`, `rowsIf` |
| Cell | a string (one paragraph), `{"style": "Label", "content": "…"}` (one paragraph in its own style), or `{"span", "background", "verticalAlign", "content": [blocks]}` |
| List item | a string, `{"level", "item"}`, `forEach`, `if` |
| Image | `{"image": "logo", "alt": "…", "width": "40mm"}`; one size is enough, the other follows from the image's proportions |
| Component | `{"component": "address", "params": {"address": "order.customer.address"}}`; a component without parameters is `{"component": "terms"}` |

Every conditional has `then`, `elseIf` (a list of `{"if": …, "then": …}`) and `else`, in blocks, rows, list items and
inline. A loop is `{"forEach": "order.checks", "as": "check", "body": […]}` (rows: `"rows"`, list items: `"items"`). There
is no `{{#if}}` inside strings. An expression whose condition is raw has no text form and is written as
`{"expression": {"raw": …, "uses": […]}, "format": …}`.

### Values

- **Lengths** carry a unit: `"10mm"`, `"6pt"`, `"2cm"`, `"0.5in"`. A table column is a length (fixed) or a number
  (relative weight).
- **Colours** are `"#1F4E79"` or a theme token `"$accent"`. A **line** is a thickness and a colour: `"0.5pt $line"`.
- **Padding** (`padding`, `cellPadding`) is one value for all sides or two (vertical, horizontal): `"6pt 8pt"`.
- **Value formats** are Templify's: `date:…`, `number:…`, `currency`, `uppercase`, `lowercase`, `raw`, `join`, and the
  boolean formats (`yesno`, `checkmark`, …). A format the host registered with Templify (`ValueFormat.Custom("janein")`)
  is exported as written and listed in `TemplateJsonImportOptions.ValueFormats` on import; any other name is an error.

### Strictness

The import accepts only what it understands, so a mistake fails where it is made instead of building a different
template: an unknown key or block kind, an unknown format, padding with three values, a loop variable that shadows its
own collection's root (`{"forEach": "line.parts", "as": "line"}`), a text in `texts` without a language the others
have (a regional `de-CH` is covered by `de`), a duplicate section name. `==` is read as `=`. The import reports every problem at once, each with the JSON pointer of the
element (`/content/7/table/1/row/3`): TB9200 (structure), TB9201 (a value the builder rejects), TB9202 (raw XML not
allowed), TB9203 (a data path outside `Data`).

### Texts

**One JSON for every language.** Texts from `template.Texts["key"]` are exported as `[[key]]` references, wherever the
code put them (in text, in a date format such as `{{order.date:date:[[dateFormat]]}}`, in raw XML, in document
properties), so the exported JSON does not depend on the language. With `TextLanguages` the translations are written
into the `texts` section and the file is self-contained, including the texts a registered component reads. Every text
must then have every requested language; a missing one is TB9204 at `/texts/<key>`.

At build time a key is translated from the JSON's `texts` for the build language (`de-DE` finds `de`), else from
`TemplateBuildOptions.Texts`; a key neither has is TB5001. Components of an imported definition look their texts up the
same way. A key is letters, digits, `_`, `.` or `-`; a literal `[[` cannot be exported (TB9204). Texts are opaque to the
export: code that changes a text (`string.Format(Texts["pages"], n)`, `Texts["x"].ToUpper()`) changes the key, not the
translation. The export therefore checks itself: it imports the JSON it wrote, builds it for the options' language and
every `TextLanguages` entry, and fails with TB9204 (naming the first paragraph that differs) when the bytes differ from
the C# definition's. Re-exporting an imported definition without `TextLanguages` writes its `texts` section exactly as
it was read.

### Sections, components, assets, theme

A **section** (`c.Section("items", b => …)` in C#) names a part of the document without adding output: in JSON
`{"section": "items", "blocks": […]}`, the unit to read, replace or compare. Names are unique in a definition; a
component the export's catalog does not know is written as a section named after it (`AddressBlock`, `AddressBlock-2`).

**Components** see the build's texts (`c.Texts["title"]`) and objects the definition shares (`template.Share(theme)`,
`c.Shared<BrandTheme>()`; for JSON imports `TemplateJsonImportOptions.Shared`). A component then needs only data paths
as parameters, so it can be registered in a `ComponentCatalog` and written as a reference; the import builds it from the
current component code. A colour parameter that is a theme token is written as `"$accent"` and follows the host's theme.

Images and embedded fonts are **named assets**. In C# an image gets its name with `ImageSource.Png(bytes).WithName("logo")`;
unnamed images are exported as `image-1`, `image-2`, fonts as `font-<family>`.

**Theme tokens.** A colour or font that comes from the theme is a token: in C# `Color.Token("accent",
Color.FromHex("1F4E79"))` or `FontFace.Token("heading", new FontFace("Source Serif 4"))`, in JSON `"$accent"` wherever a
colour or font goes, with the values in the `theme` section:

```json
"theme": {"colors": {"accent": "#1F4E79", "line": "#D4D4D4"}, "fonts": {"heading": "Source Serif 4"}},
"styles": {"paragraph": {"SectionHeading": {"font": "$heading", "color": "$accent"}}}
```

A host replaces values on import (`TemplateJsonImportOptions.Theme`, for example a customer's accent colour), so a
customer's JSON keeps working when the brand changes. A colour token takes a colour (anything else is TB9200); a token the
definition does not have is not used, and the build warns (TB9205), so one palette can serve many definitions.

## Testing your definitions

The builder generates templates at runtime, so a definition is verified in your CI. Use the test kit
`TriasDev.Templify.Builder.Testing`, which works with any test framework:

```csharp
[Fact]
public void OrderConfirmation_IsValid() => TemplateVerifier.VerifyAll(
    () => new OrderConfirmation(theme),
    new TemplateVerificationOptions
    {
        Languages = [CultureInfo.GetCultureInfo("de-DE"), CultureInfo.GetCultureInfo("en-GB")],
        Texts = _ => SampleTexts.Catalog,
        SampleData = [File.ReadAllText("sample-data.json")],
    });
```

`VerifyAll` builds every language and format. It fails, listing every problem, when the build fails,
`ValidateTemplate` reports errors, the contract's witness does not fill cleanly, a sample does not fill without
missing variables and warnings, a DOCX fails the schema validation or an ODT the package checks, a second build
(also under the tr-TR and ar-SA cultures) gives different bytes, or the languages and formats have different
contracts.

`TemplateSnapshot.Create(template)` returns a text dump for golden tests. It shows paragraphs with their styles,
markers, tables, lists, headers and footers, in the same notation for DOCX and ODT. `LibreOfficeConverter` converts
templates and filled documents with a local LibreOffice.

## Support matrix

| Target | Supported |
|---|---|
| DOCX in Microsoft Word (Windows, Mac) | yes |
| DOCX in LibreOffice (including conversion to PDF) | yes |
| ODT in LibreOffice | yes |
| ODT in Word | best effort, not tested |

Known behaviour of the applications:

- **Table of contents.** Word builds a DOCX table of contents when it opens the document (fill with
  `UpdateFieldsOnOpen = UpdateFieldsOnOpenMode.Auto`). Converters that refresh indexes on load (for example
  Stirling-PDF) build it from DOCX and ODT; a plain `soffice --convert-to pdf` does not. The build reports warning
  TB9001.
- **Word's built-in style names.** LibreOffice maps a DOCX style named like a Word built-in (`Body Text`,
  `Heading 1`) to its own style when it opens the file. Choose your own display names ("Body Copy").
- **Embedded fonts.** LibreOffice uses embedded fonts from DOCX and ODT, also when converting to PDF. Word uses
  them from DOCX too (not tested automatically).
- **Cell padding.** With a 1 pt border, cell text in DOCX and ODT can be up to 2 pt apart in LibreOffice.

## Known limits

- **Comparison operands in uploaded copies.** A path that an edited copy uses only as a comparison operand
  (`{{#if order.priority = "high"}}`) is not reported as unknown by the upload check. The engine treats an unresolved
  operand as a string literal ([#271](https://github.com/TriasDev/templify/issues/271)).
- **Condition variables are read from a message.** The upload check reads the variable of a missing-condition-variable
  warning from its message text until the engine exposes it
  ([#275](https://github.com/TriasDev/templify/issues/275)).
- **Implicit names inside loops.** Inside a loop the engine also resolves names on the current item, so a root path
  such as `order.number` resolves to the item's `order` property if the item has one. The builder cannot see the data.
  The guards are the data contract and the item names a definition chooses. A root path whose first segment is the
  name of an enclosing loop's item is a build error (TB4003).
- **Raw blocks** are not checked beyond their root element. The DOCX schema validation in the test kit covers them.
