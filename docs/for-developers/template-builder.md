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
                x.Value(order["number"], Styles.Strong);
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
    s.Defaults(d => d.Font(t.Body).Size(10.Pt()).Color(t.Ink).Paragraph(Body));
    s.Paragraph(Body, p => p.SpaceAfter(6.Pt()).LineSpacing(1.15));
    s.Paragraph(Heading, p => p.BasedOn(Body).Size(13.Pt()).Bold().Color(t.Accent).KeepWithNext().OutlineLevel(1));
    s.Character(Strong, c => c.Bold());
    s.Table(Grid, g => g.HeaderRule(1.Pt(), t.Accent).RowRule(0.5.Pt(), t.Line).CellPadding(3.Pt(), 5.Pt())
        .TextStyle(TableText).HeaderTextStyle(TableHeader));
    s.Box(Success, b => b.Border(0.75.Pt(), t.SuccessBorder).Background(t.SuccessBackground).Padding(6.Pt(), 8.Pt()));
    s.List(Bullets, l => l.ItemStyle(ListItem).Level(0, "•", 6.Mm(), 4.Mm()));
});
```

A paragraph without a style of its own inherits one, so most paragraphs need none:

1. in a table cell, the row's `TextStyle` (for example a totals row: `r.TextStyle(TableHeader)`), then the table
   style's `HeaderTextStyle` (header rows) or `TextStyle` (other rows);
2. the style sheet's default paragraph style, `Defaults(d => d.Paragraph(Body))`;
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

## JSON definitions

`TemplateJson.Export(definition, options[, components])` writes a definition as JSON, and `TemplateJson.Import(json,
options)` reads it back as a definition that builds the same bytes. The format is compact: an object that fits in 100
characters is one line, a paragraph in the inherited style is a plain string, and values sit in the text as Templify
placeholders:

```json
{"style": "LineGrid", "columns": ["10mm", 4, "16mm", "30mm"], "table": [
  {"header": true, "row": ["#", "Beschreibung", "Menge", "Betrag"]},
  {"forEachRow": "order.lines", "as": "line", "rows": [
    {"row": ["{{@number}}", "{{line.description}}", "{{line.quantity}}", "{{line.amount:currency}}"]}
  ]},
  {"textStyle": "TableHeader", "row": [{"span": 3, "content": "Gesamtbetrag"}, "{{order.total:currency}}"]}
]}
```

(Hand-wrapped here; the writer applies the width rule.)

| Where | Form |
|---|---|
| Text | a string with `{{path}}`, `{{path:format}}`, `{{(condition):format}}`; styled: `{"text": "{{order.number}}", "style": "StrongText"}`; a list mixes strings, styled text, `{"if": …}`, `{"tab": true}`, `{"tabRight": true}`, `{"lineBreak": true}`, `{"pageNumber": true}`, `{"pageCount": true}`, `{"image": …}` |
| Block | a string (a paragraph in the inherited style), `{"style", "paragraph"}`, `{"style", "box": [blocks]}`, `{"style", "list": [items]}`, `{"style", "columns", "spaceAfter", "table": [rows]}`, `if`, `forEach`, `component`, `group`, `pageBreak`, `rule`, `toc`, `raw` |
| Row | `{"header", "allowSplit", "textStyle", "row": [cells]}`, `forEachRow`, `rowsIf` |
| Cell | a string (one paragraph), a list of blocks, or `{"span", "background", "align", "content"}` |
| List item | a string, `{"level", "item"}`, `forEach`, `if` |

**One JSON for every language.** Texts from `template.Texts["key"]` are exported as `[[key]]` references, wherever the
code put them (in text, in a date format such as `{{order.date:date:[[dateFormat]]}}`, in raw XML, in document
properties), so the exported JSON does not depend on the language. With
`TemplateJson.Export(definition, options, new TemplateJsonExportOptions { TextLanguages = [de, en] })` the
translations are written into a `texts` section and the file is self-contained:

```json
"texts": {
  "de": {"title": "Auftragsbestätigung", "items": "Positionen"},
  "en": {"title": "Order confirmation", "items": "Items"}
},
"content": [{"style": "DocTitle", "paragraph": "[[title]]"}, …]
```

At build time a key is translated from the JSON's `texts` for the build language (`de-DE` finds `de`), else from
`TemplateBuildOptions.Texts`; a key neither has is TB5001. A key is letters, digits, `_`, `.` or `-`; a literal `[[`
cannot be exported (TB9204). Texts are opaque to the export: code that changes a text (`Texts["x"].ToUpper()`) changes
the key, not the translation.

Conditions and loops stay objects; there is no `{{#if}}` inside strings. An expression whose condition is raw has no
text form and is written as `{"expression": {"raw": …, "uses": […]}, "format": …}`. Import reports every problem at
once, each with the JSON pointer of the element (`/content/7/table/1/row/3`).

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
  `UpdateFieldsOnOpen = UpdateFieldsOnOpenMode.Auto`). LibreOffice builds it neither from DOCX nor from ODT, also not
  when converting to PDF. The build reports warning TB9001.
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
