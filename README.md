# Templify

[![NuGet](https://img.shields.io/nuget/v/TriasDev.Templify.svg)](https://www.nuget.org/packages/TriasDev.Templify/)
[![Build Status](https://img.shields.io/github/actions/workflow/status/TriasDev/templify/build.yml?branch=main)](https://github.com/TriasDev/templify/actions)
[![Test Coverage](https://img.shields.io/badge/coverage-100%25-brightgreen)](https://github.com/TriasDev/templify)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-6.0%2B-purple)](https://dotnet.microsoft.com/download)

> A modern .NET library for processing Word document templates without Microsoft Word

**High-performance, battle-tested document generation for .NET**

---

## Overview

Templify is a focused .NET library built on the OpenXML SDK that enables dynamic Word document generation through simple placeholder replacement, conditionals, and loops. Unlike complex templating systems, Templify provides an intuitive API for the most common use case: replacing `{{placeholders}}` in Word templates with actual data, without requiring Microsoft Word installation.

**Key Features:**
- 📝 Simple placeholder syntax: `{{variableName}}`
- ✨ Markdown formatting in variable values: `**bold**`, `*italic*`, `~~strikethrough~~`
- 🔀 Conditional blocks: `{{#if condition}}...{{else}}...{{/if}}`
- 🔁 Loops and iterations: `{{#foreach collection}}...{{/foreach}}`
- 🌳 Nested data structures with dot notation and array indexing
- 🎨 Automatic formatting preservation (bold, italic, fonts, colors)
- 📊 Full table support including row loops
- 🚀 No Microsoft Word required (pure OpenXML processing)

---

## Why Templify?

### The Problem
Generating Word documents programmatically is typically:
- **Complex**: Manual OpenXML manipulation requires 50-200 lines of code
- **Error-prone**: Easy to corrupt documents with incorrect XML
- **Hard to maintain**: Business users can't update templates
- **Time-consuming**: Steep learning curve for XML/OpenXML

### The Solution
Templify lets you:
1. **Create templates in Word** - Use familiar tools, not code
2. **Add simple placeholders** - Just `{{Name}}` and `{{#if}}...{{/if}}`
3. **Process with 3 lines of code** - Clean, simple API
4. **Let business users maintain templates** - No developer needed

### Comparison

| Approach | Lines of Code | Template Creation | Maintainability | Learning Curve |
|----------|---------------|-------------------|-----------------|----------------|
| **Templify** | **~10 lines** | **In Word (visual)** | **High** | **Low** |
| Manual OpenXML | ~200 lines | Programmatic | Low | Steep |
| XSLT Templating | ~150 lines | XML | Medium | High |
| DocX Library | ~50 lines | Programmatic | Medium | Medium |

**Example Comparison:**

<details>
<summary><b>Manual OpenXML (200+ lines)</b></summary>

```csharp
using (var doc = WordprocessingDocument.Open(stream, true))
{
    var body = doc.MainDocumentPart.Document.Body;

    // Find and replace text
    foreach (var text in body.Descendants<Text>())
    {
        if (text.Text.Contains("{{Name}}"))
        {
            text.Text = text.Text.Replace("{{Name}}", customerName);
        }
    }

    // Handle tables
    foreach (var table in body.Descendants<Table>())
    {
        foreach (var row in table.Elements<TableRow>())
        {
            // ... 50+ more lines for loops
        }
    }

    // Handle conditionals - complex XML manipulation
    // ... 100+ more lines
}
```
</details>

<details>
<summary><b>Templify (10 lines)</b></summary>

```csharp
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

**Result: 95% less code, infinite times easier to maintain.**

---

## Quick Start

### Installation

```bash
dotnet add package TriasDev.Templify
```

### Your First Document (5 Minutes)

1. **Create a Word template** with placeholders:
   ```
   Hello {{Name}}!
   Your order #{{OrderId}} has been confirmed.
   ```

2. **Process it**:
   ```csharp
   using TriasDev.Templify;

   var data = new Dictionary<string, object>
   {
       ["Name"] = "John Doe",
       ["OrderId"] = "12345"
   };

   var processor = new DocumentTemplateProcessor();
   using var templateStream = File.OpenRead("template.docx");
   using var outputStream = File.Create("output.docx");

   var result = processor.ProcessTemplate(templateStream, outputStream, data);
   ```

3. **Done!** Open `output.docx` and see the result.

### Markdown Formatting (New!)

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

📖 **[Full Quick Start Guide](docs/quick-start.md)** | 📚 **[Tutorial Series](docs/tutorials/)**

---

## Repository Structure

This repository contains multiple projects organized as a complete solution:

```
templify/
├── TriasDev.Templify/          # Core library (.NET 6.0+)
├── TriasDev.Templify.Tests/    # xUnit test suite (109+ tests, 100% coverage)
├── TriasDev.Templify.Gui/      # Cross-platform GUI application (Avalonia)
├── TriasDev.Templify.Converter/# CLI tool for document conversion
├── TriasDev.Templify.Benchmarks/# Performance benchmarks (BenchmarkDotNet)
└── TriasDev.Templify.Demo/     # Demo console application
```

---

## Documentation

### 📖 For Users
- **[Quick Start Guide](docs/quick-start.md)** - Get started in 5 minutes
- **[Tutorial Series](docs/tutorials/)** - Step-by-step learning path
- **[FAQ](docs/FAQ.md)** - Common questions and answers
- **[API Reference](TriasDev.Templify/README.md)** - Complete feature documentation
- **[Examples Collection](TriasDev.Templify/Examples.md)** - 1,900+ lines of code samples

### 🏗️ For Developers
- **[Architecture Guide](TriasDev.Templify/ARCHITECTURE.md)** - Design patterns and technical decisions
- **[Performance Benchmarks](TriasDev.Templify/PERFORMANCE.md)** - Speed and optimization details
- **[CLAUDE.md](CLAUDE.md)** - Development guide for AI-assisted coding
- **[Contributing Guide](CONTRIBUTING.md)** - How to contribute *(coming soon)*

---

## Development Setup

### Prerequisites

**Prerequisites:**
- .NET 6.0 SDK or later
- Git

**Clone and build:**
```bash
git clone git@github.com:TriasDev/templify.git
cd templify
dotnet build templify.sln
```

**Run tests:**
```bash
dotnet test TriasDev.Templify.Tests/TriasDev.Templify.Tests.csproj
```

**Run demo:**
```bash
dotnet run --project TriasDev.Templify.Demo/TriasDev.Templify.Demo.csproj
```

## Projects

### 📚 TriasDev.Templify (Core Library)

The main template processing library. Provides `DocumentTemplateProcessor` for replacing placeholders, evaluating conditionals, and processing loops in Word documents.

**Architecture:** Visitor pattern with context-aware evaluation
**Target:** .NET 6.0+
**Dependencies:** DocumentFormat.OpenXml 3.3.0

📖 [Full Library Documentation](TriasDev.Templify/README.md) | 🏗️ [Architecture Details](TriasDev.Templify/ARCHITECTURE.md) | 📝 [Code Examples](TriasDev.Templify/Examples.md)

### 🖥️ GUI Application

Cross-platform desktop application built with Avalonia for visual template editing and processing.

**Run the GUI:**
```bash
dotnet run --project TriasDev.Templify.Gui/TriasDev.Templify.Gui.csproj
```

**Features:**
- Visual template editor
- Data input and preview
- Real-time template processing
- Cross-platform (Windows, macOS, Linux)

### 🔧 CLI Converter Tool

Command-line tool for migrating OpenXMLTemplates documents to Templify format, with analysis, validation, and cleanup capabilities.

**Run the converter:**
```bash
# Full command
dotnet run --project TriasDev.Templify.Converter/TriasDev.Templify.Converter.csproj -- [command] [options]

# Or use helper scripts (recommended)
./scripts/[command].sh [options]          # macOS/Linux
scripts\[command].cmd [options]           # Windows
```

**Available Commands:**

- **`analyze`** - Inspect OpenXMLTemplates documents and identify content controls
  ```bash
  ./scripts/analyze.sh template.docx
  ./scripts/analyze.sh template.docx --output report.md
  ```

- **`convert`** - Convert OpenXMLTemplates to Templify format
  ```bash
  ./scripts/convert.sh template.docx
  ./scripts/convert.sh template.docx --output new-template.docx
  ```

- **`validate`** - Validate Word document structure and schema
  ```bash
  ./scripts/validate.sh template.docx
  ```

- **`clean`** - Remove Structured Document Tag (SDT) wrappers
  ```bash
  ./scripts/clean.sh template.docx
  ./scripts/clean.sh template.docx --output cleaned.docx
  ```

**Migration Workflow Example:**
```bash
# Step 1: Analyze the template
./scripts/analyze.sh old-template.docx

# Step 2: Review the analysis report
cat old-template-analysis-report.md

# Step 3: Convert to Templify format
./scripts/convert.sh old-template.docx

# Step 4: Validate the converted document
./scripts/validate.sh old-template-templify.docx

# Step 5: Test with actual data
# Use demo or custom code with Templify library
```

**Batch Processing Example:**
```bash
# Convert all templates in a directory
for template in templates/*.docx; do
  ./scripts/convert.sh "$template"
done
```

📖 **[Full Converter Documentation](TriasDev.Templify.Converter/README.md)** | 📜 **[Script Usage Guide](scripts/README.md)**

#### Migrating from OpenXMLTemplates

The converter automatically translates OpenXMLTemplates content control tags to Templify placeholders:

| OpenXMLTemplates | Templify |
|-----------------|----------|
| `variable_CompanyName` | `{{CompanyName}}` |
| `conditionalRemove_IsActive` | `{{#if IsActive}}...{{/if}}` |
| `conditionalRemove_Count_gt_0` | `{{#if Count > 0}}...{{/if}}` |
| `repeating_LineItems` | `{{#foreach LineItems}}...{{/foreach}}` |

**Benefits of migrating:**
- ✅ Simpler template creation (no content controls required)
- ✅ Human-readable placeholders
- ✅ Better Word compatibility (no SDT corruption)
- ✅ Modern architecture with better performance
- ✅ Easier maintenance and debugging

### 🎯 Demo Application

Console application demonstrating all library features with comprehensive examples.

**Run demos:**
```bash
dotnet run --project TriasDev.Templify.Demo/TriasDev.Templify.Demo.csproj
```

**Includes demonstrations of:**
- Basic placeholder replacement
- Nested data structures
- Conditional blocks
- Loop processing
- Table operations
- Formatting preservation
- Complex real-world scenarios

### ⚡ Benchmarks

Performance testing using BenchmarkDotNet for measuring template processing speed.

**Run benchmarks:**
```bash
dotnet run --project TriasDev.Templify.Benchmarks/TriasDev.Templify.Benchmarks.csproj -c Release
```

**Benchmark categories:**
- Placeholder replacement
- Conditional evaluation
- Loop processing
- Complex scenarios

📊 [Performance Details](TriasDev.Templify/PERFORMANCE.md)

### ✅ Tests

Comprehensive test suite with 109+ tests covering all features.

**Test coverage:**
- Unit tests: 70 (component-level testing)
- Integration tests: 39 (end-to-end scenarios)
- Coverage: 100%

**Run all tests:**
```bash
dotnet test TriasDev.Templify.Tests/TriasDev.Templify.Tests.csproj
```

**Run specific test:**
```bash
dotnet test --filter "FullyQualifiedName~PlaceholderVisitorTests"
```

**Run with coverage:**
```bash
dotnet test --collect:"XPlat Code Coverage"
```

## Documentation

Comprehensive documentation is organized by purpose:

- 📖 **[Library README](TriasDev.Templify/README.md)** - API reference, usage guide, feature documentation
- 🏗️ **[ARCHITECTURE.md](TriasDev.Templify/ARCHITECTURE.md)** - Design patterns, visitor pattern flow, technical decisions
- 📝 **[Examples.md](TriasDev.Templify/Examples.md)** - Extensive code samples and use cases
- ⚡ **[PERFORMANCE.md](TriasDev.Templify/PERFORMANCE.md)** - Benchmark results and optimization details
- 🤖 **[CLAUDE.md](CLAUDE.md)** - Development guide for AI-assisted coding
- 📋 **[TODO.md](TriasDev.Templify/TODO.md)** - Feature roadmap and implementation status
- 🔄 **[REFACTORING.md](TriasDev.Templify/REFACTORING.md)** - Refactoring history and decisions

## Building & Testing

### Build entire solution
```bash
# Debug build
dotnet build templify.sln

# Release build
dotnet build templify.sln -c Release
```

### Run all tests
```bash
dotnet test TriasDev.Templify.Tests/TriasDev.Templify.Tests.csproj --verbosity normal
```

### Clean solution
```bash
dotnet clean templify.sln
```

### Restore dependencies
```bash
dotnet restore templify.sln
```

## Development

### Requirements

- **.NET 6.0 SDK** or later
- **Visual Studio 2022** (optional, for GUI development) or **Rider**
- **Git** for version control

### Project Guidelines

- **Code style:** Follow existing conventions (see CLAUDE.md)
- **Testing:** Maintain 100% test coverage for new features
- **Documentation:** Update relevant README and documentation files
- **Commits:** Use descriptive commit messages

### For AI-Assisted Development

This repository includes **CLAUDE.md** with comprehensive guidance for AI coding assistants:
- Common commands and workflows
- Architecture overview
- Design patterns and conventions
- Testing strategies
- Troubleshooting common issues

🤖 [Read CLAUDE.md](CLAUDE.md) for AI-assisted development guidance

## Requirements

- **.NET 6.0** or later
- **DocumentFormat.OpenXml 3.3.0** (automatically restored)
- **Avalonia 11.3.8** (for GUI project)
- **xUnit** (for test project)
- **BenchmarkDotNet** (for benchmarks)

## Architecture Highlights

Templify uses a **visitor pattern architecture** for clean, extensible document processing:

- **DocumentWalker** - Unified document traversal
- **Visitors** - ConditionalVisitor, LoopVisitor, PlaceholderVisitor
- **Evaluation Context** - Hierarchical variable resolution with loop scoping
- **PropertyPathResolver** - Nested data structure navigation

Processing order: **Conditionals → Loops → Placeholders** (enables conditionals inside loops and nested loops)

🏗️ [Full Architecture Documentation](TriasDev.Templify/ARCHITECTURE.md)

## Design Philosophy

Templify prioritizes:
1. **Simplicity** - Focus on common use cases (placeholder replacement, conditionals, loops)
2. **Maintainability** - Small, composable classes with single responsibilities
3. **Testability** - Pure functions, dependency injection, 100% test coverage
4. **Explicit behavior** - No magic, predictable results
5. **Fail-fast** - Clear error messages, no silent failures

## About

**Templify** is created and maintained by **TriasDev GmbH & Co. KG**.

### Production-Tested
Templify is battle-tested in production, processing thousands of documents daily with enterprise-grade reliability and performance.

### Why Open Source?
We believe in giving back to the .NET community and providing developers with a modern, maintainable alternative to legacy Word templating solutions.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

Copyright © 2025 TriasDev GmbH & Co. KG

## Related Projects

- **OpenXMLTemplates** (predecessor) - Original templating library (content controls-based)

---

**Getting Started:** For library usage, see [TriasDev.Templify/README.md](TriasDev.Templify/README.md)
**Contributing:** For development guidelines, see [CLAUDE.md](CLAUDE.md)
**Architecture:** For technical deep-dive, see [ARCHITECTURE.md](TriasDev.Templify/ARCHITECTURE.md)
