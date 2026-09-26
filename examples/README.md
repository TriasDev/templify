# Templify Examples

This folder contains example Word (and one LibreOffice / OpenDocument) templates and the documents Templify produced from them. They are generated
by the [DocumentGenerator](../TriasDev.Templify.DocumentGenerator/README.md) tool, so they always match the
current library.

## Available Examples

| Example | Template | Output | Shows |
|---------|----------|--------|-------|
| Hello World | [templates/hello-world-template.docx](templates/hello-world-template.docx) | [outputs/hello-world-output.docx](outputs/hello-world-output.docx) | Simple placeholders |
| Invoice | [templates/invoice-template.docx](templates/invoice-template.docx) | [outputs/invoice-output.docx](outputs/invoice-output.docx) | Nested properties, table row loops, number formatting |
| Conditionals | [templates/conditionals-template.docx](templates/conditionals-template.docx) | [outputs/conditionals-output.docx](outputs/conditionals-output.docx) | If/else blocks, boolean flags, status-based content |
| Advanced Conditionals | [templates/advanced-conditionals-template.docx](templates/advanced-conditionals-template.docx) | [outputs/advanced-conditionals-output.docx](outputs/advanced-conditionals-output.docx) | `elseif` chains, `in`/`contains`/`startswith`, `exists`/`is empty`, grouping with parentheses |
| Warning Report | [templates/warning-report-template.docx](templates/warning-report-template.docx) | [outputs/warning-report-output.docx](outputs/warning-report-output.docx) | The template behind `ProcessingResult.GetWarningReport()` |
| LibreOffice Letter | [templates/libreoffice-letter-template.odt](templates/libreoffice-letter-template.odt) | [outputs/libreoffice-letter-output.odt](outputs/libreoffice-letter-output.odt) | OpenDocument (`.odt`) template for LibreOffice Writer: conditionals, list-item and table-row loops, markdown, footer |

The sample data for each example is defined in code in
[`TriasDev.Templify.DocumentGenerator/Generators/`](../TriasDev.Templify.DocumentGenerator/Generators/).

## How to Use These Examples

### 1. Open a Template

Open a file from `templates/` in Word (or the `.odt` file in LibreOffice Writer) to see the placeholders, conditionals and loops, and compare it with the
matching file in `outputs/`.

### 2. Process It With Your Own Data

Write a JSON file with the values the template uses, then use one of these options.

**Option A: Templify GUI**

```bash
dotnet run --project TriasDev.Templify.Gui/TriasDev.Templify.Gui.csproj
```

Select the template (`.docx`), the JSON data file and the output file, then click **Process Template**.

**Option B: Demo console application**

```bash
dotnet run --project TriasDev.Templify.Demo -- --template examples/templates/hello-world-template.docx --data my-data.json --output my-output.docx
```

There is no general-purpose `templify` command-line tool; `TriasDev.Templify.Converter` only migrates
OpenXMLTemplates documents (`analyze`, `convert`, `validate`, `clean`).

**Option C: Code (C#)**

```csharp
using TriasDev.Templify.Core;

string json = File.ReadAllText("my-data.json");
var processor = new TemplateProcessor();   // .docx, .odt and .ott

ProcessingResult result = processor.ProcessTemplateFile(
    "examples/templates/hello-world-template.docx",
    "my-output.docx",
    json);

Console.WriteLine(result.IsSuccess ? "Done" : result.ErrorMessage);
```

To reuse the parsed data, `TriasDev.Templify.Utilities.JsonDataParser.ParseJsonToDataDictionary(json)` turns the
JSON into a `Dictionary<string, object>`.

### 3. Experiment

- **Change the data** - change values, add items to arrays, etc.
- **Edit the template** - add new placeholders, change formatting
- **Try different conditional values**

## Regenerating the Examples

```bash
dotnet run --project TriasDev.Templify.DocumentGenerator -- --skip-images
```

See the [DocumentGenerator README](../TriasDev.Templify.DocumentGenerator/README.md) for options (including the
preview images in `docs/images/examples/`).

## Need Help?

- **[Template Author Documentation](../docs/for-template-authors/getting-started.md)** - Complete guide
- **[Examples Gallery](../docs/for-template-authors/examples-gallery.md)** - Visual examples
- **[FAQ](../docs/FAQ.md)** - Common questions
- **[GitHub Issues](https://github.com/TriasDev/templify/issues)** - Report problems
