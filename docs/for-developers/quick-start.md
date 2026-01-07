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

if (result.Success)
{
    Console.WriteLine("Template processed successfully!");
}
```

## Configuration Options

Customize template processing with `PlaceholderReplacementOptions`:

```csharp
var options = new PlaceholderReplacementOptions
{
    MissingVariableBehavior = MissingVariableBehavior.ReplaceWithEmpty,
    Culture = CultureInfo.GetCultureInfo("de-DE"),
    EnableNewlineSupport = true,
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
| `UpdateFieldsOnOpen` | enum | `Never` | When to prompt Word to update fields |
| `TextReplacements` | dictionary | `null` | Text replacement lookup table |

## Update Fields on Open (TOC Support)

When templates contain Table of Contents (TOC) or other dynamic fields, and content changes during processing (via conditionals or loops), page numbers become stale.

**Why this happens:** OpenXML SDK cannot calculate page numbers—only Word's layout engine can determine actual pagination.

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

> **Note:** When enabled, Word displays a prompt asking the user to confirm field updates. This is a security measure built into Word.

## Need Help?

- 🐛 [Report Issues](https://github.com/triasdev/templify/issues)
- 💬 [Discussions](https://github.com/triasdev/templify/discussions)
- 📖 [Current Documentation](../../TriasDev.Templify/README.md)
