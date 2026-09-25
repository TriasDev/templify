# Headers & Footers

Templify processes headers and footers using the same template syntax as the document body. All placeholders, conditionals, and loops work identically in headers and footers.

## Supported Header/Footer Types

All Word header and footer types are supported:

| Type | Description |
|------|-------------|
| **Default** | Standard header/footer for most pages |
| **First Page** | Header/footer shown only on the first page |
| **Even Page** | Header/footer for even-numbered pages |

## No Additional Configuration Needed

`ProcessTemplate` automatically processes all headers and footers. There is no flag to enable or additional API call to make.

```csharp
var processor = new DocumentTemplateProcessor();
var result = processor.ProcessTemplate(templateStream, outputStream, data);
// Headers and footers are already processed!
```

## Where Templates Are Processed

Placeholders, conditionals and loops work in all of these places:

| Location | Notes |
|----------|-------|
| Document body | Paragraphs and tables (including nested tables) |
| Headers and footers | Default, first page and even page |
| Footnotes and endnotes | Separator notes are left untouched |
| Text boxes | VML and DrawingML text boxes; for DrawingML, both the modern shape and its compatibility fallback are processed |
| Content controls | Block-level (around paragraphs/tables), row-level, cell-level and inline content controls |

**Not processed:** comments. Comments are reviewer notes rather than document content, so placeholders
in comments stay as they are.

## Examples

### Placeholder in Header

Place `{{CompanyName}}` in your Word document header. It will be replaced just like any body placeholder.

**Template header:** `{{CompanyName}} - Confidential`

**JSON data:**
```json
{
  "CompanyName": "Acme Corp"
}
```

**Result:** `Acme Corp - Confidential`

### Conditional in Footer

Use conditionals to show different footer text based on data.

**Template footer:**
```
{{#if IsDraft}}DRAFT - For internal use only{{#else}}Final Version{{/if}}
```

**JSON data:**
```json
{
  "IsDraft": true
}
```

**Result:** `DRAFT - For internal use only`

### Loop in Header

Loops work in headers too - useful for listing authors, departments, etc.

**Template header:**
```
{{#foreach Authors}}{{Name}}{{#if @last}}{{#else}}, {{/if}}{{/foreach}}
```

**JSON data:**
```json
{
  "Authors": [
    { "Name": "Alice" },
    { "Name": "Bob" }
  ]
}
```

**Result:** `Alice, Bob`

## Formatting Preservation

Formatting in headers and footers is preserved during replacement, just as in the document body. If your placeholder is bold, the replacement text will also be bold.

## Tips

- Use **First Page** headers/footers for cover pages with different branding
- Combine conditionals with document-level flags (e.g., `IsDraft`, `IsConfidential`) to control header/footer content
- Loop metadata (`@index`, `@first`, `@last`, `@count`) works in headers and footers
