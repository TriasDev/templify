# Templify

> A modern .NET library for processing Word document templates without Microsoft Word

## Overview

Templify is a focused .NET 9 library built on the OpenXML SDK that enables dynamic Word document generation through simple placeholder replacement, conditionals, and loops. Unlike complex templating systems, Templify provides an intuitive API for the most common use case: replacing `{{placeholders}}` in Word templates with actual data, without requiring Microsoft Word installation.

**Key Features:**
- 📝 Simple placeholder syntax: `{{variableName}}`
- 🔀 Conditional blocks: `{{#if condition}}...{{else}}...{{/if}}`
- 🔁 Loops and iterations: `{{#foreach collection}}...{{/foreach}}`
- 🌳 Nested data structures with dot notation and array indexing
- 🎨 Automatic formatting preservation (bold, italic, fonts, colors)
- 📊 Full table support including row loops
- 🚀 No Microsoft Word required (pure OpenXML processing)

## Repository Structure

This repository contains multiple projects organized as a complete solution:

```
templify/
├── TriasDev.Templify/          # Core library (.NET 9.0)
├── TriasDev.Templify.Tests/    # xUnit test suite (109+ tests, 100% coverage)
├── TriasDev.Templify.Gui/      # Cross-platform GUI application (Avalonia)
├── TriasDev.Templify.Converter/# CLI tool for document conversion
├── TriasDev.Templify.Benchmarks/# Performance benchmarks (BenchmarkDotNet)
└── TriasDev.Templify.Demo/     # Demo console application
```

## Quick Start

### For Library Users

Install the NuGet package:
```bash
dotnet add package TriasDev.Templify
```

Basic usage:
```csharp
using TriasDev.Templify;

var data = new Dictionary<string, object>
{
    ["CompanyName"] = "TriasDev GmbH & Co. KG",
    ["Date"] = DateTime.Now,
    ["Amount"] = 1250.50m
};

var processor = new DocumentTemplateProcessor();
using var templateStream = File.OpenRead("template.docx");
using var outputStream = File.Create("output.docx");

var result = processor.ProcessTemplate(templateStream, outputStream, data);
```

📖 **For detailed API documentation, see [TriasDev.Templify/README.md](TriasDev.Templify/README.md)**

### For Developers

**Prerequisites:**
- .NET 9.0 SDK or later
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
**Target:** .NET 9.0
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

Command-line tool for converting documents and analyzing templates.

**Run the converter:**
```bash
dotnet run --project TriasDev.Templify.Converter/TriasDev.Templify.Converter.csproj -- [command] [options]
```

**Commands:**
- `analyze` - Analyze document structure
- `convert` - Convert OpenXMLTemplates documents to Templify format
- `validate` - Validate template syntax
- `clean` - Clean up document structure

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

- **.NET 9.0 SDK** or later
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

- **.NET 9.0** or later
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

## License

Part of the **TriasDev ViasPro** project.

© TriasDev GmbH & Co. KG

## Related Projects

- **ViasPro** - Enterprise compliance and risk management platform
- **OpenXMLTemplates** (predecessor) - Original templating library (content controls-based)

---

**Getting Started:** For library usage, see [TriasDev.Templify/README.md](TriasDev.Templify/README.md)
**Contributing:** For development guidelines, see [CLAUDE.md](CLAUDE.md)
**Architecture:** For technical deep-dive, see [ARCHITECTURE.md](TriasDev.Templify/ARCHITECTURE.md)
