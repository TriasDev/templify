<p align="center">
  <img src="https://raw.githubusercontent.com/TriasDev/templify/main/assets/icon-128.png" alt="Templify" width="128" />
</p>

# Templify

[![NuGet](https://img.shields.io/nuget/v/TriasDev.Templify.svg)](https://www.nuget.org/packages/TriasDev.Templify/)
[![Build Status](https://img.shields.io/github/actions/workflow/status/TriasDev/templify/ci.yml?branch=main)](https://github.com/TriasDev/templify/actions)
[![Documentation](https://img.shields.io/badge/docs-online-blue)](https://triasdev.github.io/templify/)
[![codecov](https://codecov.io/gh/TriasDev/templify/branch/main/graph/badge.svg)](https://codecov.io/gh/TriasDev/templify)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-8.0%20%7C%209.0%20%7C%2010.0-purple)](https://dotnet.microsoft.com/download)
[![Changelog](https://img.shields.io/badge/changelog-Keep%20a%20Changelog-orange)](CHANGELOG.md)

> A modern .NET library for processing Word document templates without Microsoft Word

---

## Overview

Templify is a focused .NET library built on the OpenXML SDK that enables dynamic Word document generation and text template processing through simple placeholder replacement, conditionals, and loops. Unlike complex templating systems, Templify provides an intuitive API for the most common use cases: replacing `{{placeholders}}` in Word templates with actual data, and generating dynamic text content for emails and notifications.

**Key Features:**
- 📝 Simple placeholder syntax: `{{variableName}}`, nested paths `{{Customer.Address.City}}` and indexing `{{Items[0].Name}}`
- 🔀 Conditional blocks: `{{#if}}...{{#elseif}}...{{#else}}...{{/if}}`, with `and`/`or`/`not`, comparisons, `in`, `contains`, `exists`, `is empty` and more
- 🔁 Loops: `{{#foreach Items}}...{{/foreach}}` or `{{#foreach item in Items}}...{{/foreach}}`, including table row loops and loop metadata (`@index`, `@number`, `@first`, `@last`, `@count`)
- 🎛️ Format specifiers: `{{Amount:currency}}`, `{{Date:date:yyyy-MM-dd}}`, `{{IsActive:checkbox}}`, `{{(Age >= 18):yesno}}`
- ✨ Markdown formatting in variable values: `**bold**`, `*italic*`, `~~strikethrough~~`
- ↩️ Line breaks in variable values: `"Line 1\nLine 2"` renders as separate lines
- 🎨 Automatic formatting preservation (bold, italic, fonts, colors)
- 📄 Processes the body, tables, headers and footers, footnotes and endnotes, text boxes and content controls
- ✅ Template validation and processing warnings, including a Word warning report
- 📧 Text template processing for emails and notifications
- 🚀 No Microsoft Word required (pure OpenXML processing)

---

## Why Templify?

Generating Word documents programmatically usually means manual OpenXML manipulation: many lines of code, easy to corrupt documents, and templates that business users cannot maintain. With Templify you:

1. **Create templates in Word** - Use familiar tools, not code
2. **Add simple placeholders** - Just `{{Name}}` and `{{#if}}...{{/if}}`
3. **Process with a few lines of code** - Clean, simple API
4. **Let business users maintain templates** - No developer needed

<details>
<summary><b>Manual OpenXML vs. Templify</b></summary>

```csharp
// Manual OpenXML: find and replace text yourself, then handle
// placeholders split across runs, tables, loops and conditionals...
using (var doc = WordprocessingDocument.Open(stream, true))
{
    foreach (var text in doc.MainDocumentPart!.Document!.Body!.Descendants<Text>())
    {
        text.Text = text.Text.Replace("{{Name}}", customerName);
    }
    // ... many more lines for tables, loops and conditionals
}
```

```csharp
// Templify
var data = new Dictionary<string, object>
{
    ["Name"] = customerName,
    ["Items"] = orderItems,
    ["IsActive"] = true
};

var processor = new DocumentTemplateProcessor();
processor.ProcessTemplate(templateStream, outputStream, data);
```
</details>

---

## Quick Start

### Installation

```bash
dotnet add package TriasDev.Templify
```

### Your First Document

1. **Create a Word template** with placeholders:
   ```
   Hello {{Name}}!
   Your order #{{OrderId}} has been confirmed.
   ```

2. **Process it**:
   ```csharp
   using TriasDev.Templify.Core;

   var data = new Dictionary<string, object>
   {
       ["Name"] = "John Doe",
       ["OrderId"] = "12345"
   };

   var processor = new DocumentTemplateProcessor();
   using var templateStream = File.OpenRead("template.docx");
   using var outputStream = File.Create("output.docx");

   ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

   if (!result.IsSuccess)
   {
       Console.WriteLine($"Processing failed: {result.ErrorMessage}");
   }
   ```

3. **Done!** Open `output.docx` and see the result.

The output stream must be readable, writable and seekable (`File.Create` or a `MemoryStream`). There are also overloads for files (`ProcessTemplateFile`), byte arrays, `IReadOnlyDictionary<string, object?>` data and JSON strings; see the [library documentation](TriasDev.Templify/README.md#api-reference).

### Markdown Formatting

Variable values can include markdown syntax for text formatting:

```csharp
var data = new Dictionary<string, object>
{
    ["Message"] = "My name is **Alice**" // **bold**
};
```

**Supported markdown:**
- `**text**` or `__text__` → Bold
- `*text*` or `_text_` → Italic
- `~~text~~` → Strikethrough
- `***text***` → Bold + Italic

The markdown formatting is automatically merged with any existing template formatting (e.g., red text + markdown bold = red bold text).

Because any `*`, `_` or `~` can be read as markdown, ordinary data such as `my_report_final.docx` or `2*3*4` may be reformatted. To insert such values literally:

```csharp
// Disable markdown for all placeholders
var options = new PlaceholderReplacementOptions { EnableMarkdown = false };
```

or opt out for a single placeholder with the `:raw` format specifier: `{{FileName:raw}}` (both since 1.8.0).

### Line Breaks in Variable Values

Newline characters in variable values are automatically converted to line breaks in Word:

```csharp
var data = new Dictionary<string, object>
{
    ["Address"] = "123 Main Street\nApartment 4B\nNew York, NY 10001"
};
```

All newline formats are supported: `\n` (Unix), `\r\n` (Windows), `\r` (old Mac). Newlines work together with markdown: `"**Bold line**\n*Italic line*"` renders as two lines with proper formatting.

To disable:
```csharp
var options = new PlaceholderReplacementOptions { EnableNewlineSupport = false };
```

### Format Specifiers

Control how values are displayed using format specifiers (outputs shown for `en-US`):

```
{{Name:uppercase}}              → ALICE JOHNSON
{{Code:lowercase}}              → abc-123
{{Amount:currency}}             → $1,234.57 (en-US) or 1.234,57 € (de-DE)
{{Value:number:N2}}             → 1,234.57
{{Percentage:number:P2}}        → 12.34%
{{OrderDate:date:yyyy-MM-dd}}   → 2024-01-15
{{OrderDate:date:MMMM d, yyyy}} → January 15, 2024
{{IsActive:checkbox}}           → ☑ or ☐
{{IsActive:yesno}}              → Yes or No
{{FileName:raw}}                → my_report_final.docx (no markdown interpretation)
```

Format specifiers use `PlaceholderReplacementOptions.Culture`, which defaults to the current culture. Set it explicitly for reproducible output:

```csharp
var options = new PlaceholderReplacementOptions
{
    Culture = new CultureInfo("de-DE")  // Affects currency, numbers, dates, and localized boolean words
};
var processor = new DocumentTemplateProcessor(options);
```

### Standalone Condition Evaluation

Use Templify's condition engine without processing Word documents:

```csharp
using TriasDev.Templify.Conditionals;

var evaluator = new ConditionEvaluator();
var data = new Dictionary<string, object>
{
    ["IsActive"] = true,
    ["Count"] = 5,
    ["Status"] = "Active"
};

// Single evaluations
bool result = evaluator.Evaluate("IsActive and Count > 0", data);

// Batch evaluation (more efficient for multiple conditions)
var context = evaluator.CreateConditionContext(data);
bool r1 = context.Evaluate("IsActive");
bool r2 = context.Evaluate("IsActive = true");
bool r3 = context.Evaluate("Count > 3");
bool r4 = context.Evaluate("Status in (\"Active\", \"Pending\")");
```

**Supported operators:** `=` (or `==`), `!=`, `>`, `<`, `>=`, `<=`, `and`, `or`, `not`, `in`, `contains`, `startswith`, `endswith`, `exists`, `is empty`, `is not empty`, and parentheses for grouping. Precedence from lowest to highest: `or`, `and`, `not`, comparisons (including `in` and the text operators), `exists`/`is empty`. So `not Status = "Active"` means `not (Status = "Active")`. `&&` and `||` are not supported.

📖 **[Condition Evaluation Guide](https://triasdev.github.io/templify/for-developers/condition-evaluation/)** | **[Conditionals for template authors](https://triasdev.github.io/templify/for-template-authors/conditionals/)**

---

## Documentation

🌐 **[Full documentation online →](https://triasdev.github.io/templify/)** (sources in [`docs/`](docs/index.md))

- **For template authors:** [Getting Started](https://triasdev.github.io/templify/for-template-authors/getting-started/), [Template Syntax](https://triasdev.github.io/templify/for-template-authors/template-syntax/), [Format Specifiers](https://triasdev.github.io/templify/for-template-authors/format-specifiers/)
- **For developers:** [Quick Start](https://triasdev.github.io/templify/for-developers/quick-start/), [Text Templates](https://triasdev.github.io/templify/for-developers/text-templates/), [Processing Warnings](https://triasdev.github.io/templify/for-developers/processing-warnings/)
- **[Tutorials](https://triasdev.github.io/templify/tutorials/)** and **[FAQ](https://triasdev.github.io/templify/FAQ/)**
- **[Library README](TriasDev.Templify/README.md)** - Feature and API reference (also shown on NuGet)
- **[Examples.md](TriasDev.Templify/Examples.md)** - Extensive code samples and use cases
- **[ARCHITECTURE.md](TriasDev.Templify/ARCHITECTURE.md)** - Design and implementation
- **[PERFORMANCE.md](TriasDev.Templify/PERFORMANCE.md)** - Benchmarks
- **[CHANGELOG.md](CHANGELOG.md)** - Release history

---

## Repository Structure

```
templify/
├── TriasDev.Templify/                  # Core library (net8.0, net9.0, net10.0), published to NuGet
├── TriasDev.Templify.Tests/            # xUnit tests for the core library (run on all library TFMs)
├── TriasDev.Templify.Converter/        # CLI tool: migrate OpenXMLTemplates documents to Templify
├── TriasDev.Templify.Converter.Tests/  # Tests for the converter
├── TriasDev.Templify.Gui/              # Cross-platform desktop app for trying templates (Avalonia)
├── TriasDev.Templify.Demo/             # Demo console application
├── TriasDev.Templify.DocumentGenerator/# Generates the example templates and outputs used in the docs
├── TriasDev.Templify.Tools.Tests/      # Tests for the GUI and the document generator
├── TriasDev.Templify.Benchmarks/       # Performance benchmarks (BenchmarkDotNet)
├── docs/                               # Documentation site (MkDocs)
├── examples/                           # Example templates and generated outputs
└── scripts/                            # Helper scripts for the converter
```

## Projects

### 📚 Templify (Core Library)

The template processing library: `DocumentTemplateProcessor` for Word documents, `TextTemplateProcessor` for plain text, and `ConditionEvaluator` for standalone conditions.

**Target:** net8.0, net9.0, net10.0 ([support policy](#supported-net-versions))
**Dependency:** DocumentFormat.OpenXml 3.5.1

📖 [Library Documentation](TriasDev.Templify/README.md) | 🏗️ [Architecture](TriasDev.Templify/ARCHITECTURE.md) | 📝 [Code Examples](TriasDev.Templify/Examples.md)

### 🖥️ GUI Application

Cross-platform desktop application (Avalonia) to load a template and JSON data, process the template with Templify, and preview and save the result.

```bash
dotnet run --project TriasDev.Templify.Gui/TriasDev.Templify.Gui.csproj
```

See the [GUI README](TriasDev.Templify.Gui/README.md).

### 🔧 CLI Converter Tool

Command-line tool for migrating OpenXMLTemplates documents (content controls) to Templify placeholders, with analysis, validation, and cleanup commands.

```bash
# Full command
dotnet run --project TriasDev.Templify.Converter/TriasDev.Templify.Converter.csproj -- <command> <document> [options]

# Or use the helper scripts
./scripts/<command>.sh <document> [options]     # macOS/Linux
scripts\<command>.cmd <document> [options]      # Windows
```

| Command | Purpose |
|---------|---------|
| `analyze <template> [--output report.md]` | Inspect an OpenXMLTemplates document and report its content controls |
| `convert <template> [--output out.docx] [--unwrap-all-controls]` | Convert to Templify syntax (non-OpenXMLTemplates controls are kept unless `--unwrap-all-controls`) |
| `validate <document>` | Check that a document is well-formed and can be opened |
| `clean <document> [--output out.docx]` | Remove all content control (SDT) wrappers |

Options: `-o`/`--output <path>`, `-v`/`--verbose`. Exit codes: `0` success, `1` the command failed, `2` invalid arguments. The converter does not process templates with data; use the library, the demo (`--template`/`--data`) or the GUI for that.

**Migration workflow:**
```bash
./scripts/analyze.sh old-template.docx      # 1. analyze
cat old-template-analysis-report.md         # 2. review the report
./scripts/convert.sh old-template.docx      # 3. convert (writes old-template-templify.docx)
./scripts/validate.sh old-template-templify.docx   # 4. validate
```

The converter translates OpenXMLTemplates content control tags to Templify syntax:

| OpenXMLTemplates | Templify |
|-----------------|----------|
| `variable_CompanyName` | `{{CompanyName}}` |
| `conditionalRemove_IsActive` | `{{#if IsActive}}...{{/if}}` |
| `conditionalRemove_Count_gt_0` | `{{#if Count > 0}}...{{/if}}` |
| `repeating_LineItems` | `{{#foreach LineItems}}...{{/foreach}}` |

📖 **[Converter Documentation](TriasDev.Templify.Converter/README.md)** | 📜 **[Script Usage Guide](scripts/README.md)**

### 🎯 Demo Application

Console application that builds a template covering all features, processes it and writes template and output to `./output`. It can also process your own files:

```bash
dotnet run --project TriasDev.Templify.Demo/TriasDev.Templify.Demo.csproj
dotnet run --project TriasDev.Templify.Demo/TriasDev.Templify.Demo.csproj -- --template my.docx --data my.json [--output out.docx]
```

### ⚡ Benchmarks

Performance tests with BenchmarkDotNet (placeholders, conditionals, loops, condition engine, complex scenarios):

```bash
dotnet run --project TriasDev.Templify.Benchmarks/TriasDev.Templify.Benchmarks.csproj -c Release
```

📊 [Performance Details](TriasDev.Templify/PERFORMANCE.md)

---

## Development

### Prerequisites

- .NET 10 SDK (pinned in `global.json`); the library is also built and tested for net8.0 and net9.0, so install those runtimes to run all test targets
- Git
- Python 3 (only for building the documentation)

### Build and Test

```bash
git clone https://github.com/TriasDev/templify.git
cd templify

# Build (Debug); CI builds with warnings as errors:
dotnet build templify.sln
dotnet build templify.sln -c Release -p:ContinuousIntegrationBuild=true

# Run the core tests (all target frameworks, or one with --framework net10.0)
dotnet test TriasDev.Templify.Tests/TriasDev.Templify.Tests.csproj

# Run all test projects
dotnet test templify.sln

# Run specific tests
dotnet test TriasDev.Templify.Tests/TriasDev.Templify.Tests.csproj --filter "FullyQualifiedName~PlaceholderVisitorTests"

# Coverage
dotnet test TriasDev.Templify.Tests/TriasDev.Templify.Tests.csproj --collect:"XPlat Code Coverage"

# Formatting check (required before pushing)
dotnet format --verify-no-changes --no-restore
```

NuGet versions are managed centrally in `Directory.Packages.props`, and every project has a committed `packages.lock.json`. After changing a package, run `dotnet restore templify.sln` and commit the updated lock files.

### Build the Documentation

```bash
pip3 install -r requirements.txt
mkdocs serve          # live preview at http://127.0.0.1:8000/templify/
mkdocs build --strict # what CI builds
```

### Guidelines

- Read the **[Contributing Guide](CONTRIBUTING.md)** and the **[Code of Conduct](CODE_OF_CONDUCT.md)**
- Add tests for new features and bug fixes, and update the documentation
- Public API changes are tracked in `TriasDev.Templify/PublicAPI.Unshipped.txt`
- **[CLAUDE.md](CLAUDE.md)** has development workflows and architecture notes for AI-assisted coding

## Requirements

- **.NET 8.0, 9.0 or 10.0** (the library targets `net8.0`, `net9.0` and `net10.0`)
- **DocumentFormat.OpenXml 3.5.1** (restored automatically)
- GUI: **Avalonia 12**; tests: **xUnit v3**; benchmarks: **BenchmarkDotNet**

### Supported .NET Versions

The library targets `net8.0`, `net9.0` and `net10.0`.

**Support policy:** we support the .NET versions that are in [Microsoft support](https://dotnet.microsoft.com/platform/support/policy/dotnet-core). Target frameworks that reach end of life are dropped in a minor release, announced in the release notes.

> ⚠️ **net6.0 is no longer supported as of 1.8.0.** Projects that still target .NET 6 can stay on Templify 1.7.x.

## Architecture Highlights

Templify uses a **visitor pattern architecture** for document processing:

- **DocumentWalker** - Unified document traversal (body, tables, headers/footers, notes, text boxes, content controls)
- **Visitors** - ConditionalVisitor, LoopVisitor, PlaceholderVisitor
- **Condition engine** - Lexer, parser and operator registry shared by `{{#if}}`, inline expressions and `ConditionEvaluator`
- **Evaluation contexts** - Hierarchical variable resolution with loop scoping
- **PropertyPathResolver** - Nested data structure navigation

Processing order: **Conditionals → Loops → Placeholders** (enables conditionals inside loops and nested loops)

🏗️ [Full Architecture Documentation](TriasDev.Templify/ARCHITECTURE.md)

## About

**Templify** is created and maintained by **TriasDev GmbH & Co. KG**. It is used in production, processing thousands of documents daily. We believe in giving back to the .NET community and providing a modern, maintainable alternative to legacy Word templating solutions.

**Related project:** OpenXMLTemplates (predecessor, content-control based); the [converter](TriasDev.Templify.Converter/README.md) migrates its templates.

## Contributing

Contributions are welcome: bug reports, feature ideas, documentation improvements and pull requests. See the **[Contributing Guide](CONTRIBUTING.md)**.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

Copyright © 2025 TriasDev GmbH & Co. KG
