# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**Templify** is a .NET 10 library for replacing placeholders in Word documents (.docx) without requiring Microsoft Word. It uses the OpenXML SDK and provides a visitor pattern architecture for processing templates with placeholders (`{{variableName}}`), conditionals, and loops.

**Target Framework:** .NET 10.0
**Primary Dependency:** DocumentFormat.OpenXml 3.3.0
**Test Framework:** xUnit

## Solution Structure

This is a multi-project solution with 6 projects:

- **TriasDev.Templify** - Core library with template processing logic
- **TriasDev.Templify.Tests** - xUnit test suite (109+ tests, 100% coverage)
- **TriasDev.Templify.Benchmarks** - BenchmarkDotNet performance tests
- **TriasDev.Templify.Converter** - CLI tool for converting Word documents
- **TriasDev.Templify.Gui** - Avalonia-based GUI application
- **TriasDev.Templify.Demo** - Demo console application

## Development Workflow

Standard flow for changes:

1. **Issue** - Create or reference a GitHub issue
2. **Branch** - Create a feature/fix branch from main
3. **Fix** - Implement the change with tests
4. **Docu** (optional) - Update documentation if needed
5. **Review** - Self-review and run pre-push checks
6. **PR** - Create pull request for review

## Common Development Commands

### Building
```bash
# Build entire solution
dotnet build templify.sln

# Build in Release mode
dotnet build templify.sln -c Release

# Build specific project
dotnet build TriasDev.Templify/TriasDev.Templify.csproj
```

### Testing
```bash
# Run all tests
dotnet test TriasDev.Templify.Tests/TriasDev.Templify.Tests.csproj

# Run tests with detailed output
dotnet test TriasDev.Templify.Tests/TriasDev.Templify.Tests.csproj --verbosity normal

# Run specific test class
dotnet test TriasDev.Templify.Tests/TriasDev.Templify.Tests.csproj --filter "FullyQualifiedName~PlaceholderVisitorTests"

# Run single test method
dotnet test TriasDev.Templify.Tests/TriasDev.Templify.Tests.csproj --filter "FullyQualifiedName~ProcessTemplate_ValidTemplate_ReplacesPlaceholders"

# Run tests with code coverage
dotnet test TriasDev.Templify.Tests/TriasDev.Templify.Tests.csproj --collect:"XPlat Code Coverage"
```

### Pre-Push Checks

**IMPORTANT:** Always run these checks before pushing to ensure code quality:

```bash
# Check code formatting (must pass with no warnings)
dotnet format --verify-no-changes --no-restore

# If formatting issues are found, fix them with:
dotnet format --no-restore
```

### Benchmarking
```bash
# Run all benchmarks
dotnet run --project TriasDev.Templify.Benchmarks/TriasDev.Templify.Benchmarks.csproj -c Release

# Run specific benchmark class
dotnet run --project TriasDev.Templify.Benchmarks/TriasDev.Templify.Benchmarks.csproj -c Release -- --filter *PlaceholderBenchmarks*
```

### Running Applications
```bash
# Run demo application
dotnet run --project TriasDev.Templify.Demo/TriasDev.Templify.Demo.csproj

# Run converter CLI (full command)
dotnet run --project TriasDev.Templify.Converter/TriasDev.Templify.Converter.csproj -- [command] [arguments]

# Run converter CLI (using helper scripts - recommended)
./scripts/analyze.sh template.docx
./scripts/convert.sh template.docx
./scripts/validate.sh template.docx
./scripts/clean.sh template.docx

# Run GUI application
dotnet run --project TriasDev.Templify.Gui/TriasDev.Templify.Gui.csproj
```

### Converter CLI Tool

The converter tool helps migrate OpenXMLTemplates documents to Templify format.

**Location:** `TriasDev.Templify.Converter/`

**Available Commands:**
```bash
# Analyze OpenXMLTemplates document
./scripts/analyze.sh template.docx [--output report.md]

# Convert to Templify format
./scripts/convert.sh template.docx [--output new-template.docx]

# Validate Word document structure
./scripts/validate.sh template.docx

# Remove SDT wrappers
./scripts/clean.sh template.docx [--output cleaned.docx]
```

**Helper Scripts:**
- Location: `scripts/` directory
- Bash scripts (`.sh`) for macOS/Linux
- CMD scripts (`.cmd`) for Windows
- Reduce verbose `dotnet run` commands to simple calls
- Must be run from repository root

**Common Development Tasks:**

Testing converter changes:
```bash
# Make changes to converter code
# Build and test with a sample document
./scripts/analyze.sh test-templates/sample.docx
./scripts/convert.sh test-templates/sample.docx
./scripts/validate.sh test-templates/sample-templify.docx
```

Adding new conversion logic:
1. Modify converters in `TriasDev.Templify.Converter/Converters/`
2. Update analyzers in `TriasDev.Templify.Converter/Analyzers/` if needed
3. Test with various OpenXMLTemplates formats
4. Update converter README with new capabilities

Debugging converter issues:
```bash
# Use full command for more control
dotnet run --project TriasDev.Templify.Converter/TriasDev.Templify.Converter.csproj -- analyze template.docx --output debug-report.md

# Review generated reports
cat template-analysis-report.md
cat template-templify-conversion-report.md
```

**Key Converter Components:**
- `Program.cs` - CLI entry point, command parsing
- `Analyzers/` - Content control detection and analysis
- `Converters/` - OpenXMLTemplates to Templify conversion logic
- `Validators/` - Document validation
- `Models/` - Data structures for analysis results

**Documentation:**
- 📖 [Full Converter Documentation](TriasDev.Templify.Converter/README.md)
- 📜 [Script Usage Guide](scripts/README.md)

## Architecture & Code Organization

### Current Architecture: Visitor Pattern (Post-Phase 2 Refactoring)

The library uses a **visitor pattern** for processing Word documents, enabling:
- Conditionals inside loops
- Nested loops (arbitrary depth)
- Table row loops
- Clean, extensible architecture with no code duplication

**Key architectural components:**

1. **DocumentTemplateProcessor** (Core/DocumentTemplateProcessor.cs:16)
   - Main entry point for template processing
   - Orchestrates visitor creation and document walking
   - Creates GlobalEvaluationContext from data dictionary
   - Handles circular visitor references for nested processing

2. **DocumentWalker** (Visitors/DocumentWalker.cs)
   - Unified document traversal engine
   - Walks document tree (body, tables, rows, cells)
   - Detects template elements (conditionals, loops, placeholders)
   - Dispatches to appropriate visitors

3. **Visitor Implementations:**
   - **ConditionalVisitor** (Visitors/ConditionalVisitor.cs) - Evaluates conditions, removes branches
   - **LoopVisitor** (Visitors/LoopVisitor.cs) - Resolves collections, clones content, creates LoopContext
   - **PlaceholderVisitor** (Visitors/PlaceholderVisitor.cs) - Resolves variables, replaces text
   - **CompositeVisitor** (Visitors/CompositeVisitor.cs) - Delegates to multiple visitors

4. **Evaluation Context Hierarchy:**
   - **IEvaluationContext** (Core/IEvaluationContext.cs) - Interface for variable resolution
   - **GlobalEvaluationContext** (Core/GlobalEvaluationContext.cs) - Root data dictionary
   - **LoopEvaluationContext** (Loops/LoopEvaluationContext.cs) - Loop-scoped variables with parent chain

5. **Supporting Components:**
   - **PropertyPathResolver** (PropertyPaths/) - Navigates nested data structures (dot notation, array indexing)
   - **ConditionalEvaluator** (Conditionals/ConditionalEvaluator.cs) - Evaluates conditional expressions
   - **FormattingPreserver** (Utilities/FormattingPreserver.cs) - Preserves text formatting (bold, italic, fonts)

### Processing Flow

```
1. DocumentTemplateProcessor.ProcessTemplate()
   ├─▶ Create GlobalEvaluationContext(data)
   ├─▶ Create DocumentWalker
   ├─▶ Create visitor composite (conditional + loop + placeholder)
   └─▶ walker.Walk(document, composite, globalContext)
       ├─▶ Step 1: Detect & visit conditionals (deepest first)
       ├─▶ Step 2: Detect & visit loops
       └─▶ Step 3: Visit paragraphs for placeholders

2. When LoopVisitor processes a loop:
   ├─▶ Resolve collection from context
   ├─▶ For each item:
   │   ├─▶ Create LoopContext(item, index, count, parent)
   │   ├─▶ Create LoopEvaluationContext(loopContext, parentContext)
   │   ├─▶ Clone content elements
   │   └─▶ walker.WalkElements(clonedElements, nestedVisitor, loopEvalContext)
   │       └─▶ Processes nested conditionals, loops, placeholders
   └─▶ Remove original loop block
```

### Code Organization by Feature

**Core Processing:**
- `Core/DocumentTemplateProcessor.cs` - Main entry point
- `Core/PlaceholderReplacementOptions.cs` - Configuration
- `Core/ProcessingResult.cs` - Result wrapper
- `Core/IEvaluationContext.cs` - Context interface

**Visitors (Visitor Pattern):**
- `Visitors/DocumentWalker.cs` - Document traversal
- `Visitors/ITemplateElementVisitor.cs` - Visitor interface
- `Visitors/ConditionalVisitor.cs` - Conditional processing
- `Visitors/LoopVisitor.cs` - Loop processing
- `Visitors/PlaceholderVisitor.cs` - Placeholder replacement
- `Visitors/CompositeVisitor.cs` - Visitor composition

**Conditionals:**
- `Conditionals/ConditionalBlock.cs` - Data structure for if/else blocks
- `Conditionals/ConditionalDetector.cs` - Finds conditional blocks
- `Conditionals/ConditionalProcessor.cs` - Legacy processor (kept for reference)
- `Conditionals/ConditionalEvaluator.cs` - Evaluates expressions with operators
- `Conditionals/IConditionEvaluator.cs` - Public interface for standalone condition evaluation
- `Conditionals/ConditionEvaluator.cs` - Public implementation of standalone evaluator
- `Conditionals/IConditionContext.cs` - Public interface for batch evaluation context
- `Conditionals/ConditionContext.cs` - Public implementation of batch evaluation context

**Loops:**
- `Loops/LoopBlock.cs` - Data structure for loop blocks
- `Loops/LoopDetector.cs` - Finds foreach blocks
- `Loops/LoopProcessor.cs` - Legacy processor (kept for reference)
- `Loops/LoopContext.cs` - Loop iteration state
- `Loops/LoopEvaluationContext.cs` - Loop-scoped variable resolution

**Placeholders:**
- `Placeholders/PlaceholderFinder.cs` - Pattern matching for {{placeholders}}
- `Placeholders/ValueResolver.cs` - Variable lookup
- `Placeholders/ValueConverter.cs` - Type conversion to strings
- `Placeholders/DocumentBodyReplacer.cs` - Legacy body replacer
- `Placeholders/TableReplacer.cs` - Legacy table replacer

**Property Paths:**
- `PropertyPaths/PropertyPath.cs` - Parsed property path representation
- `PropertyPaths/PropertyPathSegment.cs` - Path segment types
- `PropertyPaths/PropertyPathResolver.cs` - Resolves nested paths (Customer.Address.City, Items[0])

**Markdown:**
- `Markdown/MarkdownSegment.cs` - Data structure for text + formatting flags
- `Markdown/MarkdownParser.cs` - Parses markdown syntax into segments

**Utilities:**
- `Utilities/FormattingPreserver.cs` - Preserves OpenXML formatting and applies markdown formatting
- `Utilities/JsonDataParser.cs` - Parses JSON to data dictionary

## Key Implementation Details

### Placeholder Syntax
- Simple: `{{VariableName}}`
- Nested: `{{Customer.Address.City}}`
- Array indexing: `{{Items[0].Name}}`
- Dictionary: `{{Settings[Theme]}}` or `{{Settings.Theme}}`

### Conditional Syntax
```
{{#if VariableName}}...{{/if}}
{{#if Status = "Active"}}...{{#else}}...{{/if}}
{{#if Count > 0 and IsEnabled}}...{{/if}}
{{#if Status = "Active"}}...{{#elseif Status = "Pending"}}...{{#else}}...{{/if}}
```

**Operators:** `=`, `!=`, `>`, `<`, `>=`, `<=`, `and`, `or`, `not`

**Elseif chains:** Multiple conditions can be chained using `{{#elseif condition}}`. The `{{#else}}` branch must be last.

### Loop Syntax
```
{{#foreach CollectionName}}
  Content with {{PropertyName}}
{{/foreach}}
```

**Loop metadata:** `{{@index}}`, `{{@first}}`, `{{@last}}`, `{{@count}}`

### Markdown Syntax

Variable values support markdown formatting for dynamic text styling:

```csharp
var data = new Dictionary<string, object>
{
    ["Message"] = "My name is **Alice**"  // Bold
};
```

**Supported markdown:**
- `**text**` or `__text__` → Bold
- `*text*` or `_text_` → Italic
- `~~text~~` → Strikethrough
- `***text***` → Bold + Italic

**Implementation notes:**
- MarkdownParser detects and parses markdown syntax into MarkdownSegment objects
- PlaceholderVisitor checks for markdown using `MarkdownParser.ContainsMarkdown()`
- When markdown detected, creates multiple Run elements (one per segment) instead of single Run
- FormattingPreserver.ApplyMarkdownFormatting() merges markdown formatting with template formatting
- Malformed markdown (unclosed markers) renders as plain text

**Architecture:**
- `MarkdownParser.Parse()` returns List<MarkdownSegment> with text + formatting flags
- `UpdateParagraphTextWithMarkdown()` in PlaceholderVisitor generates Run elements for each segment
- Formatting is merged, not replaced: red template + markdown bold = red bold text

### Text Processing Strategy

OpenXML splits text into `Run` elements for formatting. A placeholder like `{{CompanyName}}` might be split across multiple runs. The solution:

1. Concatenate all run texts in a paragraph
2. Find placeholders in combined text
3. Perform string replacement
4. Reconstruct runs with replaced text
5. Preserve original formatting from first run

### Formatting Preservation

FormattingPreserver extracts RunProperties from original runs and applies them to replacement text:
- Bold, italic, underline
- Font family, size, color
- Paragraph styles (Heading 1, Normal, etc.)
- List formatting (bullets, numbering)

## Testing Strategy

### Test Organization
- **Unit Tests:** Individual component testing (70 tests)
  - ConditionalEvaluator (51 tests for all operators)
  - Visitors (TemplateElement, PlaceholderVisitor, LoopVisitor, CompositeVisitor)
  - PropertyPathResolver, ValueResolver

- **Integration Tests:** End-to-end template processing (39 tests)
  - Basic placeholder replacement
  - Nested structures
  - Loop processing
  - Conditional blocks (including nested conditionals)
  - Formatting preservation
  - Table operations

### Test File Locations
- Unit tests: `TriasDev.Templify.Tests/Visitors/`, `TriasDev.Templify.Tests/Conditionals/`, etc.
- Integration tests: `TriasDev.Templify.Tests/Integration/`

### Writing Tests

When adding new features:
1. Start with unit tests for core logic (e.g., evaluator, detector)
2. Add integration tests that process actual Word documents
3. Test edge cases: empty collections, missing variables, malformed syntax
4. Ensure backward compatibility - all existing tests must pass

## Common Patterns & Conventions

### Immutability
- Configuration objects use `init` properties
- Result objects are immutable
- Context objects are immutable per iteration

### Error Handling
- Use `ProcessingResult` for success/failure
- Invalid placeholder syntax is ignored (treated as text)
- Missing variables: configurable via `MissingVariableBehavior`
- OpenXML errors: caught and wrapped in ProcessingResult

### Null Safety
- Nullable reference types enabled (`<Nullable>enable</Nullable>`)
- Use `?` for nullable parameters
- Check for null before processing

### Naming Conventions
- Private fields: `_camelCase`
- Public properties: `PascalCase`
- Local variables: `camelCase`
- Constants: `PascalCase`

### Code Documentation
- XML documentation on all public APIs
- Internal classes: documented where complexity warrants
- Complex algorithms: inline comments explaining "why" not "what"

## Important Constraints & Design Decisions

### Visitor Pattern Circular Reference
The LoopVisitor needs access to the final composite visitor (which includes itself) to support nested loops. This is achieved via:
```csharp
// Create temporary composite without loop
CompositeVisitor tempComposite = new CompositeVisitor(conditionalVisitor, placeholderVisitor);
LoopVisitor loopVisitor = new LoopVisitor(walker, tempComposite);

// Create final composite with loop
CompositeVisitor composite = new CompositeVisitor(conditionalVisitor, loopVisitor, placeholderVisitor);

// Update loop visitor to use final composite (enables nesting)
loopVisitor.SetNestedVisitor(composite);
```

### Processing Order
1. **Conditionals first** (deepest first) - Removes branches before loops/placeholders
2. **Loops second** - Expands collections
3. **Placeholders last** - Replaces remaining variables

This order allows conditionals inside loops and loops with conditionals.

### Performance Trade-offs
- Loads entire document into memory (not suitable for documents >50MB)
- Rebuilds paragraph runs (simpler, more reliable than partial updates)
- Linear search through elements (O(n))

### InternalsVisibleTo
The test project has access to internal members via `<InternalsVisibleTo Include="TriasDev.Templify.Tests" />` in the .csproj file. Use `internal` for classes that need testing but shouldn't be public API.

## Troubleshooting Common Issues

### Text Spans Multiple Runs
If placeholder replacement fails, check if the placeholder is split across runs. The paragraph-level processing should handle this, but verify by examining the OpenXML structure.

### Formatting Lost
FormattingPreserver extracts properties from the first run. If formatting varies within a placeholder, only the first run's formatting is applied. This is intentional.

### Nested Loops Not Working
Ensure LoopVisitor has the correct nested visitor set via `SetNestedVisitor()`. The circular reference is required for nested processing.

### Missing Variables
Check `MissingVariableBehavior` in options:
- `LeaveUnchanged` (default) - Keeps `{{placeholder}}`
- `ReplaceWithEmpty` - Removes placeholder
- `ThrowException` - Fails fast

## Additional Documentation

For comprehensive information, see:
- **ARCHITECTURE.md** - Detailed design, visitor pattern flow, legacy architecture
- **README.md** - User-facing documentation, API reference, examples
- **Examples.md** - Extensive code samples and use cases
- **PERFORMANCE.md** - Performance characteristics and benchmarks
- **TODO.md** - Feature roadmap and implementation status
- **REFACTORING.md** - Refactoring history and decisions

## Design Philosophy

This library prioritizes:
1. **Simplicity** - Focus on common use case (variable replacement, conditionals, loops)
2. **Maintainability** - Small, composable classes with single responsibilities
3. **Testability** - Pure functions, dependency injection, 100% test coverage
4. **Explicit behavior** - No magic, predictable results
5. **Fail-fast** - Clear error messages, no silent failures
