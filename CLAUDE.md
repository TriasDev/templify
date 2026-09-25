# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**Templify** is a .NET 10 library for replacing placeholders in Word documents (.docx) without requiring Microsoft Word. It uses the OpenXML SDK and provides a visitor pattern architecture for processing templates with placeholders (`{{variableName}}`), conditionals, and loops.

**Target Frameworks:** net10.0, net9.0, net8.0 (library); tools use net10.0, `TriasDev.Templify.Tests` multi-targets all library TFMs
**Primary Dependency:** DocumentFormat.OpenXml 3.5.1
**Test Framework:** xUnit v3 (`xunit.v3.mtp-off`: test projects are executables, run through VSTest with `dotnet test`; use `TestContext.Current.CancellationToken` for cancellable calls, analyzer xUnit1051)

## Solution Structure

This is a multi-project solution with 9 projects:

- **TriasDev.Templify** - Core library with template processing logic
- **TriasDev.Templify.Tests** - xUnit test suite (~1,750 tests, ~91% line / ~85% branch coverage)
- **TriasDev.Templify.Benchmarks** - BenchmarkDotNet performance tests
- **TriasDev.Templify.Converter** - CLI tool for converting Word documents
- **TriasDev.Templify.Gui** - Avalonia-based GUI application
- **TriasDev.Templify.Demo** - Demo console application
- **TriasDev.Templify.DocumentGenerator** - Generates the example templates/outputs used in the documentation
- **TriasDev.Templify.Tools.Tests** - xUnit tests for the tools/apps (GUI ViewModel/services headless, DocumentGenerator smoke tests)
- **TriasDev.Templify.Converter.Tests** - xUnit tests for the Converter CLI (round trips through the core, file safety, CLI parsing)

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

### Build Configuration
- `global.json` pins the SDK (10.0.100, `rollForward: latestFeature`).
- `Directory.Build.props` holds shared settings (Nullable, ImplicitUsings, LangVersion, AnalysisLevel, EnforceCodeStyleInBuild). Nullable warnings are always errors; in CI (`GITHUB_ACTIONS=true` → `ContinuousIntegrationBuild`) all warnings are errors. Reproduce locally with `dotnet build templify.sln -c Release -p:ContinuousIntegrationBuild=true`.
- `Directory.Packages.props` (Central Package Management) holds all NuGet versions; `PackageReference` items have no `Version`.
- Every project has a committed `packages.lock.json`; CI restores in locked mode. After changing a package, run `dotnet restore templify.sln` and commit the updated lock files.
- The library targets `net10.0;net9.0;net8.0`. Support policy: .NET versions in Microsoft support; EOL TFMs are dropped in a minor release with a release-notes notice (net6.0 dropped in 1.8.0). `TriasDev.Templify/CompatibilitySuppressions.xml` suppresses only the net6.0 TFM removal (PKV006) for package validation; delete it once the baseline is 1.8.0+.
- `TriasDev.Templify.Tests` multi-targets the library's TFMs (`net10.0;net9.0;net8.0`); run a single one with `--framework net10.0`.

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

Benchmarks throw if `ProcessTemplate` fails (`BenchmarkGuard`), so failures cannot look fast. `ConditionEngineBenchmarks`
covers the condition engine (`in`, `contains`, `exists`, `is empty`, grouping, `elseif`). BenchmarkDotNet output
(`BenchmarkDotNet.Artifacts/`) is not committed; `PERFORMANCE.md` holds a historical snapshot.

### Running Applications
```bash
# Run demo application (writes to ./output; process your own files with --template/--data)
dotnet run --project TriasDev.Templify.Demo/TriasDev.Templify.Demo.csproj
dotnet run --project TriasDev.Templify.Demo/TriasDev.Templify.Demo.csproj -- --template my.docx --data my.json

# Regenerate documentation examples (examples/, docs/images/examples/)
dotnet run --project TriasDev.Templify.DocumentGenerator -- --skip-images

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
- Work from any directory (paths are resolved relative to the script; file arguments relative to the CWD)

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
   - Walks document tree (body, tables, rows, cells, headers, footers)
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
   ├─▶ walker.Walk(document, composite, globalContext)
   │   ├─▶ Step 1: Detect & visit conditionals (deepest first)
   │   ├─▶ Step 2: Detect & visit loops
   │   └─▶ Step 3: Visit paragraphs for placeholders
   └─▶ walker.WalkHeadersAndFooters(document, composite, globalContext)
       └─▶ Same 3-step processing for each header/footer part

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
- `Conditionals/ConditionalEvaluator.cs` - Evaluates expressions with operators
- `Conditionals/IConditionEvaluator.cs` - Public interface for standalone condition evaluation
- `Conditionals/ConditionEvaluator.cs` - Public implementation of standalone evaluator
- `Conditionals/IConditionContext.cs` - Public interface for batch evaluation context
- `Conditionals/ConditionContext.cs` - Public implementation of batch evaluation context

**Loops:**
- `Loops/LoopBlock.cs` - Data structure for loop blocks
- `Loops/LoopDetector.cs` - Finds foreach blocks
- `Loops/LoopContext.cs` - Loop iteration state
- `Loops/LoopEvaluationContext.cs` - Loop-scoped variable resolution

**Placeholders:**
- `Placeholders/PlaceholderFinder.cs` - Pattern matching for {{placeholders}}
- `Placeholders/ValueResolver.cs` - Variable lookup
- `Placeholders/ValueConverter.cs` - Type conversion to strings

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
- Currency format: `{{Amount:currency}}`
- Number format: `{{Value:number:N2}}`, `{{Rate:number:F3}}`, `{{Pct:number:P}}`
- String format: `{{Name:uppercase}}`, `{{Code:lowercase}}`
- Date format: `{{OrderDate:date:yyyy-MM-dd}}`, `{{Date:date:MMMM d, yyyy}}`

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

**Named iteration variable syntax** (for accessing parent scope in nested loops):
```
{{#foreach item in CollectionName}}
  {{item.PropertyName}}
{{/foreach}}

{{#foreach category in Categories}}
  {{#foreach product in category.Products}}
    {{category.Name}}: {{product.Name}}  ← Access parent loop variable
  {{/foreach}}
{{/foreach}}
```

**Loop metadata:** `{{@index}}` (0-based), `{{@number}}` (1-based), `{{@first}}`, `{{@last}}`, `{{@count}}`

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
`TriasDev.Templify.Tests` has about 1,750 tests (on each of net10/net9/net8). Core line coverage is about 91% and branch coverage about 85%. Excluding the source-generated regex code, the figures are about 96% and 91% (as of 2026-09).
- **Unit tests:** one folder per library namespace (see below). Internal types are used directly through `InternalsVisibleTo`, not through reflection.
- **Integration tests** (`Integration/`): end-to-end processing of generated Word documents. This covers placeholders, loops, conditionals, tables, headers/footers, text boxes, markdown, culture independence, validation, output schema validity and unicode.
- **Skipped tests** use `[Fact(Skip = "Bug: #<issue>")]` and document known library bugs. Remove the `Skip` when you fix the bug.
- **Performance guards** use `[Trait("Category", "Performance")]` with generous limits. Exclude them with `--filter "Category!=Performance"`.

### Test File Locations
Test folders mirror the library namespaces, and each test namespace matches its folder (`TriasDev.Templify.Tests.<Folder>`):
- `Conditionals/` (`Conditionals/Engine/` for the expression engine), `Core/`, `Formatting/`, `Loops/`, `Markdown/`, `Placeholders/`, `PropertyPaths/`, `Replacements/`, `Utilities/`, `Visitors/`: unit tests for that namespace
- `Integration/`: end-to-end tests
- `Helpers/`: `DocumentBuilder` / `DocumentVerifier`, `TemplateTestHarness` (build → process → verify, `InvariantCulture` by default), `ConditionEngineTestHelper` (`Eval` / `EvalInline`) and `TestBlocks`
- Root: only `ObsoleteApiTests` (cross-namespace deprecated public API)
- Naming: `ConditionalEvaluatorTests` tests the internal `ConditionalEvaluator` used by templates. `PublicConditionEvaluatorTests` tests the public standalone `ConditionEvaluator` API.
- Inside the test project, fully qualify library types (`TriasDev.Templify.Core.X`). A partial name like `Core.X` resolves to the test namespace `TriasDev.Templify.Tests.Core`.
- Integration tests that assert on numbers or dates must use an explicit culture. Prefer `TemplateTestHarness.Process(...)`. CI runs the suite under de-DE, tr-TR and ar-SA.

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

### Public API Compatibility (hard rule)
The library has external consumers. **Do not change, remove or rename public API** (types, members, signatures, namespaces) as a side effect of a fix or refactoring.
- Public API is tracked in `TriasDev.Templify/PublicAPI.Shipped.txt` / `PublicAPI.Unshipped.txt`; the build fails on undeclared additions (RS0016) or removals/changes (RS0017). `dotnet pack` validates against the last released package (`PackageValidationBaselineVersion`).
- New public API → add it to `PublicAPI.Unshipped.txt` deliberately. Prefer `internal` unless the symbol is meant for consumers.
- Retire API with `[Obsolete]`; remove only in a major version.
- Behavior changes that alter output of existing templates are also potentially breaking: call them out, and make them opt-in via an option when they are not clearly bug fixes.
- State the public API impact (none / additive / behavior change / breaking) in every PR.
- See CONTRIBUTING.md → "Public API Compatibility".

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
3. **Testability** - Pure functions, dependency injection, high test coverage
4. **Explicit behavior** - No magic, predictable results
5. **Fail-fast** - Clear error messages, no silent failures
