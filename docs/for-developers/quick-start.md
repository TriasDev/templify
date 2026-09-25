# Quick Start for Developers

Get started with Templify in your .NET application.

## Prerequisites

- .NET 8.0 SDK or later ([Download](https://dotnet.microsoft.com/download)). The library targets `net8.0`, `net9.0` and `net10.0`.
- A Word template (`.docx`). Microsoft Word is only needed to author templates, not to process them.

## Installation

```bash
dotnet add package TriasDev.Templify
```

Or in Visual Studio's Package Manager Console:

```powershell
Install-Package TriasDev.Templify
```

## Your First Template

### Step 1: Create a Word Template

Create a Word document with this content and save it as `template.docx`:

```
Hello {{Name}}!

This is your invoice #{{InvoiceNumber}} dated {{Date}}.

Total Amount: {{Amount}} EUR
```

Placeholders are written as `{{VariableName}}`, without spaces inside the braces (`{{ Name }}` is not a
placeholder and stays as text). Names consist of letters, digits and underscores; use dots and brackets for
nested data (`{{Customer.Address.City}}`, `{{Items[0].Name}}`).

### Step 2: Write the Code

```csharp
using TriasDev.Templify.Core;

var data = new Dictionary<string, object>
{
    ["Name"] = "John Doe",
    ["InvoiceNumber"] = "INV-2025-001",
    ["Date"] = DateTime.Now.ToString("yyyy-MM-dd"),
    ["Amount"] = 1250.50m
};

var processor = new DocumentTemplateProcessor();
using var templateStream = File.OpenRead("template.docx");
using var outputStream = File.Create("output.docx");

ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

if (result.IsSuccess)
{
    Console.WriteLine($"Done: {result.ReplacementCount} placeholders replaced.");
}
else
{
    Console.WriteLine($"Failed: {result.ErrorMessage}");
}
```

All public types live in namespaces below `TriasDev.Templify`: `TriasDev.Templify.Core` (processors, options,
results), `TriasDev.Templify.Conditionals` (standalone condition evaluation), `TriasDev.Templify.Formatting`
(boolean formatters), `TriasDev.Templify.Replacements` (text replacement tables) and
`TriasDev.Templify.Utilities` (`JsonDataParser`).

The output stream must be **readable, writable and seekable** (a `MemoryStream`, or a `FileStream` opened
with `FileAccess.ReadWrite` such as `File.Create`; `File.OpenWrite` is write-only and does not work), because the
document is edited in place after the template has been copied into it. An unusable stream is reported as a
failed result (`IsSuccess == false` with an `ErrorMessage`) before anything is written to it.

### Step 3: Run It

Run the program and open `output.docx`:

```
Hello John Doe!

This is your invoice #INV-2025-001 dated 2025-01-15.

Total Amount: 1250.50 EUR
```

Numbers and dates are formatted with `PlaceholderReplacementOptions.Culture` (the current culture by default), so
`1250.50` may appear as `1250,50` on a German system. See [Configuration Options](#configuration-options).

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

Every overload (stream, `byte[]` and file) also accepts a JSON string instead of a dictionary (see
[Using JSON Data](#using-json-data)). `TextTemplateProcessor.ProcessTemplate` and
`DocumentTemplateProcessor.ValidateTemplate` accept `IReadOnlyDictionary<string, object?>` data as well.

## Data

### Nested Data and Objects

Values can be dictionaries, objects (POCOs, anonymous types, records), lists and arrays, in any combination:

```csharp
var data = new Dictionary<string, object>
{
    ["Company"] = new Dictionary<string, object>
    {
        ["Name"] = "Acme Corp",
        ["Address"] = new { Street = "123 Main St", City = "Springfield" }
    },
    ["Items"] = new List<string> { "Item 1", "Item 2" }
};
```

```
Company: {{Company.Name}}
Address: {{Company.Address.Street}}, {{Company.Address.City}}
First item: {{Items[0]}}
```

Dictionary keys are looked up with the dictionary's own comparer (ordinal, case-sensitive for a plain
`Dictionary<string, object>`); object properties are matched case-insensitively.

### Using JSON Data

Pass the JSON text directly; nested objects and arrays become dictionaries and lists:

```csharp
string json = File.ReadAllText("data.json");
ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, json);
```

Or parse it once and reuse the dictionary:

```csharp
using TriasDev.Templify.Utilities;

Dictionary<string, object> data = JsonDataParser.ParseJsonToDataDictionary(json);
```

The JSON root must be an object. Invalid JSON is not a failed result: `JsonException` (or `ArgumentException` for
an empty string) is thrown to the caller.

!!! warning "Avoid `JsonSerializer.Deserialize<Dictionary<string, object>>`"
    It leaves every value as a `JsonElement`. Since 1.8.0 nested paths through `JsonElement` objects and arrays
    resolve (`{{Customer.Name}}`, `{{Items[0].Name}}`), but a top-level JSON array used directly in
    `{{#foreach Items}}` fails with "is not a collection", and a JSON `false` is truthy in `{{#if Flag}}`. Use the
    JSON overload or `JsonDataParser` instead.

## Error Handling

```csharp
ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

if (!result.IsSuccess)
{
    // Template syntax errors (e.g. an {{#if}} without {{/if}}) and data errors
    // (e.g. {{#foreach}} over a value that is not a collection)
    Console.WriteLine(result.ErrorMessage);   // "Processing failed: ..."
    return;
}

foreach (string name in result.MissingVariables)
{
    Console.WriteLine($"Missing: {name}");
}

foreach (ProcessingWarning warning in result.Warnings)
{
    Console.WriteLine(warning);   // e.g. "MissingVariable [placeholder]: Variable 'X' was not found in the data."
}
```

`ProcessingResult` has `IsSuccess`, `ErrorMessage`, `ReplacementCount`, `MissingVariables`, `Warnings`,
`HasWarnings`, and `GetWarningReport()` / `GetWarningReportBytes()`, which render the warnings as a Word
document. See [Processing Warnings](processing-warnings.md).

Only two things throw instead of returning a failed result: invalid arguments (`ArgumentNullException`, invalid
JSON as above, unreadable files for `ProcessTemplateFile`), and a missing variable when
`MissingVariableBehavior.ThrowException` is configured, which throws `InvalidOperationException`. A malformed
condition such as `{{#if Count > 2 && IsActive}}` (`&&` is not an operator; use `and`) does not fail the
document: it evaluates to false and adds an `ExpressionFailed` warning.

### Validating a Template

`ValidateTemplate` checks a template without producing output: unmatched markers, invalid conditions and, when
data is passed, missing variables.

```csharp
using var template = File.OpenRead("template.docx");
ValidationResult validation = processor.ValidateTemplate(template, data);

if (!validation.IsValid)
{
    foreach (ValidationError error in validation.Errors)
    {
        Console.WriteLine($"{error.Type}: {error.Message}");
    }
}

Console.WriteLine(string.Join(", ", validation.AllPlaceholders));
Console.WriteLine(string.Join(", ", validation.MissingVariables));
```

Each missing variable or loop collection is reported as an error of type `MissingVariable` (so `IsValid` is
`false` for that data) and listed in `MissingVariables`; call `ValidateTemplate(template)` without data to check
the syntax only. `validation.Warnings` reports loops over empty collections (`EmptyLoopCollection`, controlled by
`WarnOnEmptyLoopCollections`) and condition keywords used as variable names (`ReservedWordAsVariable`).

## Configuration Options

Customize template processing with `PlaceholderReplacementOptions`:

```csharp
using System.Globalization;
using TriasDev.Templify.Core;

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
| `MissingVariableBehavior` | enum | `LeaveUnchanged` | `LeaveUnchanged` keeps `{{Name}}`, `ReplaceWithEmpty` removes it, `ThrowException` throws `InvalidOperationException` |
| `Culture` | `CultureInfo` | `CurrentCulture` | Culture for formatting numbers, dates and localized boolean formats |
| `BooleanFormatterRegistry` | `BooleanFormatterRegistry?` | `null` | Custom boolean formatters; `null` uses the built-in formatters for `Culture` |
| `EnableNewlineSupport` | `bool` | `true` | Convert `\n` to Word line breaks |
| `EnableMarkdown` | `bool` | `true` | Render markdown in values (`**bold**`, `*italic*`, `_italic_`, `~~strike~~`) as Word formatting. Set to `false` to insert values as plain text; newline handling is unaffected. Single placeholders can opt out with `{{Name:raw}}` |
| `WarnOnEmptyLoopCollections` | `bool` | `true` | `ValidateTemplate` warns about loops over empty collections (does not affect `ProcessTemplate`) |
| `UpdateFieldsOnOpen` | enum | `Never` | When to prompt Word to update fields |
| `TextReplacements` | dictionary | `null` | Text replacement lookup table applied to replaced values (e.g. `TextReplacements.HtmlEntities`) |
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
using TriasDev.Templify.Formatting;

var registry = new BooleanFormatterRegistry(CultureInfo.GetCultureInfo("de-DE"));
registry.Register("status", new BooleanFormatter("Aktiv", "Inaktiv"));   // register first ...

var options = new PlaceholderReplacementOptions
{
    Culture = CultureInfo.GetCultureInfo("de-DE"),
    BooleanFormatterRegistry = registry                                     // ... then share
};
```

## Where Templates Are Processed

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

Auto mode looks at field codes in the body, headers and footers:

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

## Next Steps

- [Template Syntax](../for-template-authors/template-syntax.md) - placeholders, conditionals, loops
- [Format Specifiers](../for-template-authors/format-specifiers.md) and [Boolean Expressions](../for-template-authors/boolean-expressions.md)
- [Text Template Processing](text-templates.md) - the same syntax for plain-text output (emails etc.)
- [Condition Evaluation](condition-evaluation.md) - evaluate conditions without a document
- [Tutorials](../tutorials/index.md) and [FAQ](../FAQ.md)

## Need Help?

- 🐛 [Report Issues](https://github.com/TriasDev/templify/issues)
- 💬 [Discussions](https://github.com/TriasDev/templify/discussions)
- 📖 [Library README](https://github.com/TriasDev/templify/blob/main/TriasDev.Templify/README.md)
