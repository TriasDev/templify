# Quick Start for Developers

Get started with Templify in your .NET application.

## Installation

```bash
dotnet add package TriasDev.Templify
```

## Basic Usage

```csharp
using TriasDev.Templify;

var data = new Dictionary<string, object>
{
    ["CustomerName"] = "John Doe",
    ["OrderDate"] = DateTime.Now.ToString("yyyy-MM-dd")
};

var processor = new DocumentTemplateProcessor();
using var templateStream = File.OpenRead("template.docx");
using var outputStream = File.Create("output.docx");

var result = processor.ProcessTemplate(templateStream, outputStream, data);

if (result.IsSuccess)
{
    Console.WriteLine("Template processed successfully!");
}
```

The output stream must be **readable, writable and seekable** (a `MemoryStream`, or a `FileStream` opened
with `FileAccess.ReadWrite` such as `File.Create`), because the document is edited in place after the template
has been copied into it. An unusable stream is reported as a failed result (`IsSuccess == false` with an
`ErrorMessage`) before anything is written to it.

### Other Input and Output Shapes

```csharp
// File to file: the output file is written only when processing succeeds.
ProcessingResult fileResult = processor.ProcessTemplateFile("template.docx", "output.docx", data);

// Bytes to bytes: `output` is empty when processing fails.
byte[] template = File.ReadAllBytes("template.docx");
ProcessingResult bytesResult = processor.ProcessTemplate(template, data, out byte[] output);

// Read-only data (IReadOnlyDictionary<string, object?>), e.g. an ImmutableDictionary or ReadOnlyDictionary.
// The dictionary is not copied; lookups use its own key comparer.
IReadOnlyDictionary<string, object?> readOnlyData = new Dictionary<string, object?> { ["CustomerName"] = null };
ProcessingResult readOnlyResult = processor.ProcessTemplate(templateStream, outputStream, readOnlyData);
```

Every overload also accepts a JSON string instead of a dictionary. `TextTemplateProcessor.ProcessTemplate` and
`DocumentTemplateProcessor.ValidateTemplate` accept `IReadOnlyDictionary<string, object?>` data as well.

## Configuration Options

Customize template processing with `PlaceholderReplacementOptions`:

```csharp
var options = new PlaceholderReplacementOptions
{
    MissingVariableBehavior = MissingVariableBehavior.ReplaceWithEmpty,
    Culture = CultureInfo.GetCultureInfo("de-DE"),
    EnableNewlineSupport = true,
    EnableMarkdown = true,
    UpdateFieldsOnOpen = UpdateFieldsOnOpenMode.Auto
};

var processor = new DocumentTemplateProcessor(options);
```

### Available Options

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `MissingVariableBehavior` | enum | `LeaveUnchanged` | How to handle missing variables |
| `Culture` | `CultureInfo` | `CurrentCulture` | Culture for formatting numbers and dates |
| `EnableNewlineSupport` | `bool` | `true` | Convert `\n` to Word line breaks |
| `EnableMarkdown` | `bool` | `true` | Render markdown in values (`**bold**`, `*italic*`, `_italic_`, `~~strike~~`) as Word formatting. Set to `false` to insert values as plain text; newline handling is unaffected. Single placeholders can opt out with `{{Name:raw}}` |
| `UpdateFieldsOnOpen` | enum | `Never` | When to prompt Word to update fields |
| `TextReplacements` | dictionary | `null` | Text replacement lookup table |
| `DocumentProperties` | `DocumentProperties?` | `null` | Metadata properties to set on output document |

!!! warning "Set `Culture` explicitly on servers"
    `Culture` defaults to `CultureInfo.CurrentCulture` at the time the options are created, i.e. the locale of
    the server process or request thread. The same template and data can then produce different numbers, dates
    and localized boolean formats on differently configured machines. Set `Culture` explicitly (for example
    `CultureInfo.GetCultureInfo("en-US")` or `CultureInfo.InvariantCulture`) for reproducible output.

### Sharing Options and Processors

A `DocumentTemplateProcessor` or `TextTemplateProcessor` and its options can be shared by concurrent calls.
`BooleanFormatterRegistry.Register` is not synchronized: register all custom boolean formatters first, then
assign the registry to `PlaceholderReplacementOptions.BooleanFormatterRegistry`, and do not modify it afterwards.

```csharp
var registry = new BooleanFormatterRegistry(CultureInfo.GetCultureInfo("de-DE"));
registry.Register("status", new BooleanFormatter("Aktiv", "Inaktiv"));   // register first ...

var options = new PlaceholderReplacementOptions
{
    Culture = CultureInfo.GetCultureInfo("de-DE"),
    BooleanFormatterRegistry = registry                                     // ... then share
};
```

## Headers and Footers

`ProcessTemplate` automatically processes all headers and footers in the document - no additional API calls or configuration needed. The same visitor pipeline (placeholders, conditionals, loops) is applied to every header and footer part (Default, First Page, Even Page).

The same applies to footnotes and endnotes (separator notes are skipped), text boxes (VML and DrawingML, both the `mc:Choice` and the `mc:Fallback` rendering) and content controls (block, row, cell and inline). Comments are not processed.

## Update Fields on Open (TOC Support)

When templates contain Table of Contents (TOC) or other dynamic fields, and content changes during processing (via conditionals or loops), page numbers become stale.

**Why this happens:** The OpenXML SDK cannot calculate page numbers—only Word's layout engine can determine actual pagination.

### UpdateFieldsOnOpenMode Options

| Mode | Description |
|------|-------------|
| `Never` | Never prompt to update fields (default) |
| `Always` | Always prompt to update fields |
| `Auto` | **Recommended** - Only prompt if document contains fields (TOC, PAGE, etc.) |

### Usage Examples

**For applications processing user-uploaded templates (recommended):**

```csharp
// Auto-detect: only prompts if document has TOC, PAGE, etc.
var options = new PlaceholderReplacementOptions
{
    UpdateFieldsOnOpen = UpdateFieldsOnOpenMode.Auto
};
```

**For templates known to have TOC:**

```csharp
var options = new PlaceholderReplacementOptions
{
    UpdateFieldsOnOpen = UpdateFieldsOnOpenMode.Always
};
```

### Fields Detected in Auto Mode

- `TOC` - Table of Contents
- `PAGE` - Current page number
- `NUMPAGES` - Total page count
- `PAGEREF` - Page references
- `DATE` - Current date
- `TIME` - Current time
- `FILENAME` - Document filename
- `REF` - Cross-references
- `NOTEREF` - Footnote/endnote references
- `SECTIONPAGES` - Pages in current section

> **Note:** When enabled, Word displays a prompt asking the user to confirm field updates. This is a security measure built into Word.

## Document Properties

Set metadata on the output document (Author, Title, Subject, etc.). Properties left as `null` preserve the original template value.

```csharp
var options = new PlaceholderReplacementOptions
{
    DocumentProperties = new DocumentProperties
    {
        Author = "My Application",
        Title = "Generated Report"
    }
};
```

### Available Properties

| Property | Maps to in Word |
|----------|----------------|
| `Author` | Author (Creator) |
| `Title` | Title |
| `Subject` | Subject |
| `Description` | Comments |
| `Keywords` | Keywords |
| `Category` | Category |
| `LastModifiedBy` | Last Modified By |

> **Note:** When `DocumentProperties` is `null` (default), all original template metadata is preserved. When set, only non-null properties are applied.

## Need Help?

- 🐛 [Report Issues](https://github.com/triasdev/templify/issues)
- 💬 [Discussions](https://github.com/triasdev/templify/discussions)
- 📖 [Current Documentation](../../TriasDev.Templify/README.md)
