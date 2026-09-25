# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**Templify** is a .NET library (net8.0/net9.0/net10.0) for replacing placeholders in Word documents (.docx) without requiring Microsoft Word. It uses the OpenXML SDK and provides a visitor pattern architecture for processing templates with placeholders (`{{variableName}}`), conditionals, and loops.

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

See `TriasDev.Templify/ARCHITECTURE.md` for the full description. Summary:

### Visitor Pattern Pipeline

The library walks the Word document once per story part and dispatches template constructs to visitors, which enables
conditionals inside loops, nested loops (arbitrary depth), table row loops/conditionals and a single code path for
body, headers/footers and notes.

**Key architectural components:**

1. **DocumentTemplateProcessor** (Core/DocumentTemplateProcessor.cs)
   - Public entry point: `ProcessTemplate` (Stream/Stream, byte[]/out byte[]), `ProcessTemplateFile`, `ValidateTemplate`;
     data as `Dictionary<string, object>`, `IReadOnlyDictionary<string, object?>` or a JSON string
   - Checks streams (template readable; output readable, writable, seekable), copies the template to the output
   - Creates the `TemplatePipeline` and `GlobalEvaluationContext`, then runs post-processing
     (`DrawingIdAllocator`, `UpdateFieldsOnOpen`, `DocumentProperties`)

2. **TemplatePipeline** (Visitors/TemplatePipeline.cs)
   - Factory for the visitor graph (conditional + loop + placeholder in a `CompositeVisitor`) and the `DocumentWalker`
   - Wires the circular LoopVisitor reference (see below)
   - `Process()` = `Walk` (body) → `WalkHeadersAndFooters` → `WalkFootnotesAndEndnotes`

3. **DocumentWalker** (Visitors/DocumentWalker.cs)
   - Walks body, tables/rows/cells (row-aware loop and conditional detection), content controls
     (`SdtBlock`/`SdtRow`/`SdtCell`), text boxes, headers/footers, footnotes/endnotes. Comments are not processed.
   - Per element list: conditionals (deepest first, skipping those inside loops) → loops → placeholders
   - Removes tables whose rows were all removed

4. **Visitors:**
   - **ConditionalVisitor** - Evaluates conditions, removes branches (block and inline conditionals)
   - **LoopVisitor** - Resolves collections, clones content, walks clones with a `LoopEvaluationContext`
   - **PlaceholderVisitor** - Resolves variables and inline `{{(expr)}}` expressions, replaces text
   - **CompositeVisitor** - Delegates to multiple visitors

5. **Condition engine** (Conditionals/Engine/)
   - `ConditionLexer` → `ConditionParser` (Pratt parser, AST in `ConditionNode`) → `ConditionEvaluatorCore`
   - `ConditionOperatorRegistry` + `Operators/` (one class per operator, single source of truth); `OperatorPrecedence`
   - `ConditionDialect`: `DefaultConditionDialect` (`{{#if}}`, text, `ConditionEvaluator`) vs `InlineConditionDialect` (`{{(...)}}`)
   - `ConditionAstCache` - process-wide bounded cache of parsed ASTs

6. **Evaluation contexts:**
   - **IEvaluationContext** (Core/IEvaluationContext.cs) - Interface for variable resolution
   - **GlobalEvaluationContext** (Core/GlobalEvaluationContext.cs) - Root data dictionary
   - **LoopEvaluationContext** (Loops/LoopEvaluationContext.cs) - Loop-scoped variables, then parent context
   - **LoopContext** (Loops/LoopContext.cs) - Current item, loop metadata, named iteration variable

7. **Supporting components:**
   - **PropertyPathResolver** (PropertyPaths/) - Nested paths: properties, dictionaries, lists, `ExpandoObject`, `JsonElement`
   - **ValueConverter** (Placeholders/) + **BooleanFormatterRegistry** (Formatting/) - Value → text with format specifiers
   - **ParagraphTextModel / ParagraphTextRewriter / ReplacementContent** (Utilities/) - Paragraph text across runs, in-place range rewriting, newlines and markdown pieces
   - **FormattingPreserver** (Utilities/) - Clones run properties, applies markdown formatting in schema order
   - **TextTemplateProcessor** (Core/) - Same syntax for plain text
   - **TemplateValidator** (Core/) - `ValidateTemplate` implementation
   - **WarningCollector / WarningReportGenerator** (Core/) - Processing warnings and the warning report document

### Processing Flow

```
1. DocumentTemplateProcessor.ProcessTemplate()
   ├─▶ Check streams, copy template → output
   ├─▶ TemplatePipeline.Create(options, missingVariables, warningCollector)
   ├─▶ Create GlobalEvaluationContext(data)
   ├─▶ pipeline.Process(document, globalContext)
   │   ├─▶ walker.Walk (body), WalkHeadersAndFooters, WalkFootnotesAndEndnotes
   │   │   ├─▶ Step 1: Detect & visit conditionals (deepest first)
   │   │   ├─▶ Step 2: Detect & visit loops
   │   │   └─▶ Step 3: Visit paragraphs for placeholders
   ├─▶ DrawingIdAllocator.EnsureUniqueIds(document)
   └─▶ UpdateFieldsOnOpen / DocumentProperties, save, return ProcessingResult

2. When LoopVisitor processes a loop:
   ├─▶ Resolve collection from context (missing/null → warning + remove; non-collection → Failure)
   ├─▶ For each item:
   │   ├─▶ Create LoopContext(item, index, count, ...)
   │   ├─▶ Create LoopEvaluationContext(loopContext, parentContext)
   │   ├─▶ Clone content elements, insert them
   │   └─▶ walker.WalkElements(clonedElements, nestedVisitor, loopEvalContext)
   └─▶ Remove original loop block
```

### Code Organization by Feature

**Core Processing:**
- `Core/DocumentTemplateProcessor.cs` - Main entry point
- `Core/TextTemplateProcessor.cs` / `Core/TextProcessingResult.cs` - Plain text templates
- `Core/PlaceholderReplacementOptions.cs` - Configuration (`MissingVariableBehavior`, `Culture`, `BooleanFormatterRegistry`, `EnableNewlineSupport`, `EnableMarkdown`, `WarnOnEmptyLoopCollections`, `TextReplacements`, `UpdateFieldsOnOpen`, `DocumentProperties`)
- `Core/ProcessingResult.cs` - Result (`IsSuccess`, `ErrorMessage`, `ReplacementCount`, `MissingVariables`, `Warnings`, `HasWarnings`, `GetWarningReport()`)
- `Core/ProcessingWarning.cs`, `Core/IWarningCollector.cs`, `Core/WarningReportGenerator.cs` - Warnings
- `Core/TemplateValidator.cs`, `Core/ValidationResult.cs` - Template validation
- `Core/TemplateExceptions.cs` - Internal typed exceptions (`MissingVariableException`, `TemplateSyntaxException`, `TemplateDataException`)
- `Core/IEvaluationContext.cs`, `Core/GlobalEvaluationContext.cs` - Contexts

**Visitors (Visitor Pattern):**
- `Visitors/TemplatePipeline.cs` - Visitor graph factory
- `Visitors/DocumentWalker.cs` - Document traversal
- `Visitors/ITemplateElementVisitor.cs` - Visitor interface
- `Visitors/ConditionalVisitor.cs` - Conditional processing
- `Visitors/LoopVisitor.cs` - Loop processing
- `Visitors/PlaceholderVisitor.cs` - Placeholder replacement
- `Visitors/CompositeVisitor.cs` - Visitor composition
- `Visitors/TemplateElementHelper.cs` - Cloning and safe removal of elements

**Conditionals:**
- `Conditionals/ConditionalBlock.cs`, `ConditionalBranch.cs` - Data structures for if/elseif/else blocks
- `Conditionals/ConditionalDetector.cs` - Finds block and table-row conditionals
- `Conditionals/InlineConditionalParser.cs` - Finds conditionals within one paragraph/text
- `Conditionals/ConditionalPatterns.cs` - Shared marker regexes
- `Conditionals/ConditionalEvaluator.cs` - Internal facade: evaluation with warnings, parser-based validation
- `Conditionals/IConditionEvaluator.cs`, `ConditionEvaluator.cs` - Public standalone evaluator
- `Conditionals/IConditionContext.cs`, `ConditionContext.cs` - Public batch evaluation context
- `Conditionals/ConditionValidationResult.cs`, `ConditionValidationIssue.cs` - Public validation results
- `Conditionals/Engine/` - Lexer, parser, AST, operator registry, operators, dialects, AST cache

**Loops:**
- `Loops/LoopBlock.cs` - Data structure for loop blocks
- `Loops/LoopDetector.cs` - Finds foreach blocks (paragraph and table-row loops)
- `Loops/LoopContext.cs` - Loop iteration state and metadata
- `Loops/LoopEvaluationContext.cs` - Loop-scoped variable resolution

**Placeholders:**
- `Placeholders/PlaceholderScanner.cs` / `PlaceholderToken.cs` - Pattern matching for {{placeholders}} (internal)
- `Placeholders/PlaceholderFinder.cs` / `PlaceholderMatch.cs` - Obsolete public wrappers (internal in 2.0)
- `Placeholders/ExpressionPlaceholderEvaluator.cs` - Inline `{{(expr)}}` evaluation
- `Placeholders/ValueResolver.cs` - Variable lookup
- `Placeholders/ValueConverter.cs` - Type conversion and format specifiers

**Property Paths:**
- `PropertyPaths/PropertyPath.cs` - Parsed property path representation
- `PropertyPaths/PropertyPathSegment.cs` - Path segment types
- `PropertyPaths/PropertyPathResolver.cs` - Resolves nested paths (Customer.Address.City, Items[0], JsonElement)

**Formatting / Replacements / Markdown:**
- `Formatting/BooleanFormatter.cs`, `BooleanFormatterRegistry.cs` - Boolean format specifiers (culture-aware)
- `Replacements/TextReplacements.cs` - Text replacement tables (e.g. `HtmlEntities`)
- `Markdown/MarkdownSegment.cs`, `MarkdownParser.cs` - Markdown parsing

**Utilities:**
- `Utilities/ParagraphTextModel.cs` - A paragraph's own text and its segments
- `Utilities/ParagraphTextRewriter.cs` - In-place replacement/removal of text ranges
- `Utilities/ReplacementContent.cs` - Replacement text as pieces (markdown, line breaks)
- `Utilities/TemplateElementText.cs` - Marker text of block elements
- `Utilities/FormattingPreserver.cs` - Run property cloning and markdown formatting
- `Utilities/JsonDataParser.cs` - Parses JSON to a data dictionary
- `Utilities/NumericValue.cs` - Numeric normalization/comparison across CLR types and JSON numbers
- `Utilities/XmlCharacterSanitizer.cs` - Removes XML-invalid characters from values
- `Utilities/DrawingIdAllocator.cs` - Makes drawing ids unique after loop cloning

## Key Implementation Details

### Placeholder Syntax
- Simple: `{{VariableName}}` (no spaces inside the braces; names are letters, digits, `_`)
- Nested: `{{Customer.Address.City}}`
- Array indexing: `{{Items[0].Name}}`
- Dictionary: `{{Settings[Theme]}}` or `{{Settings.Theme}}` (keys without spaces)
- Current item in loops: `{{.}}` or `{{this}}`
- Inline expression: `{{(Count > 0)}}`, `{{(IsActive and IsVerified):yesno}}`
- Boolean format: `{{Flag:yesno}}`, `:checkbox`, `:checkmark`/`:check`, `:truefalse`, `:onoff`, `:enabled`, `:active`
- Currency format: `{{Amount:currency}}`
- Number format: `{{Value:number:N2}}`, `{{Rate:number:F3}}`, `{{Pct:number:P}}`
- String format: `{{Name:uppercase}}`, `{{Code:lowercase}}`
- Date format: `{{OrderDate:date:yyyy-MM-dd}}`, `{{Date:date:MMMM d, yyyy}}`
- No markdown for one value: `{{FileName:raw}}`

### Conditional Syntax
```
{{#if VariableName}}...{{/if}}
{{#if Status = "Active"}}...{{#else}}...{{/if}}
{{#if Count > 0 and IsEnabled}}...{{/if}}
{{#if Status = "Active"}}...{{#elseif Status = "Pending"}}...{{#else}}...{{/if}}
{{#if Role in ("Admin", "Owner")}}...{{/if}}
{{#if (A or B) and not C}}...{{/if}}
```

**Operators:**
- Comparison: `=`, `==`, `!=`, `>`, `<`, `>=`, `<=`
- Logical: `and`, `or`, `not` (no `&&` / `||`)
- Membership: `in` (list literal `(a, b)`, collection, or comma-separated string)
- String: `contains`, `startswith`, `endswith` (ordinal, case-sensitive; `contains` on a collection = membership)
- Existence: `exists`, `is empty`, `is not empty` (postfix)
- Grouping with parentheses

**Precedence** (loosest → tightest): `or` (1) < `and` (2) < `not` (3) < comparisons / `in` / string operators (4) <
postfix `exists` / `is empty` (5). So `not A = B` means `not (A = B)`.

**Semantics:**
- Unresolved comparison operands are bareword string literals (`Status = Active` works; `Missing = "Missing"` is true).
- `{{#if}}` truthiness: null, `false`, `"false"`, `"0"`, blank strings, numeric zero, NaN and empty collections are false.
- Numbers compare numerically across types (`10 = 10.00m`); strings are ordinal and case-sensitive.
- Keywords added in 1.7.0 (`in`, `is`, `empty`, `exists`, `contains`, `startswith`, `endswith`) are variables when
  used as operands; `[Name]` escapes any keyword (also `and`/`or`/`not`/`true`/`false`/`null`).
- Inline `{{(...)}}` uses a stricter dialect: only `true` is truthy, `object.Equals` for non-numeric equality,
  single-quoted strings allowed.
- Typographic quotes are normalized; `\"` and `\\` are escapes inside string literals.

**Elseif chains:** Multiple conditions can be chained using `{{#elseif condition}}`. The `{{#else}}` branch must be last.

**Placement:** block conditionals have markers in their own paragraphs (or table rows); inline conditionals can sit
inside one paragraph.

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

**Loop metadata:** `{{@index}}` (0-based), `{{@number}}` (1-based), `{{@first}}`, `{{@last}}`, `{{@count}}` (innermost loop only)

**Rules:**
- Loop markers are block-level: each in its own paragraph; in tables, `{{#foreach}}` and `{{/foreach}}` in their own rows
  (the marker rows are removed). Markers in one cell loop over that cell's paragraphs.
- Missing/null collection: loop removed with a warning; a non-collection (including a string) is a Failure.
- Null items are allowed: `{{.}}` / `{{item.X}}` render empty; implicit names on a null item resolve from outer scopes.
  A property that exists with a null value does not fall through to outer scopes.
- `in` and names starting with `@` are invalid iteration variable names.

### Markdown Syntax

Variable values support markdown formatting for dynamic text styling (Word documents only, `EnableMarkdown` defaults to
`true`; `:raw` disables it per placeholder):

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
- `MarkdownParser.Parse()` returns `List<MarkdownSegment>` with text + formatting flags
- `ReplacementContent.FromValue()` turns a value into pieces (markdown segments, line breaks)
- `ParagraphTextRewriter` writes one run per piece; `FormattingPreserver.ApplyMarkdownFormatting()` merges markdown
  formatting into the template run's properties (red template + markdown bold = red bold text)
- Malformed markdown (unclosed markers) renders as plain text

### Text Processing Strategy

OpenXML splits text into `Run` elements for formatting. A placeholder like `{{CompanyName}}` might be split across
multiple runs. The solution:

1. `ParagraphTextModel` builds the paragraph's own text (excluding text boxes) and maps offsets to text elements
2. `PlaceholderScanner` finds placeholders in that text
3. `ParagraphTextRewriter` rewrites only the text elements overlapping each placeholder range, last placeholder first
4. The replacement takes the formatting of the run holding the placeholder's first character; other runs, hyperlinks,
   fields, drawings and bookmarks are untouched

### Formatting Preservation

Replacement text keeps the run formatting of the template (bold, italic, underline, font, size, color); paragraph
properties (styles, list numbering) are not touched.

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
- Template syntax errors (unmatched markers, `{{#elseif}}` after `{{#else}}`, invalid iteration variables), data errors
  (loop over a non-collection) and OpenXML errors → `ProcessingResult.Failure` ("Processing failed: ...")
- Only a missing variable with `MissingVariableBehavior.ThrowException` throws (plain `InvalidOperationException`);
  internally `Core/TemplateExceptions.cs` types errors, classify by type, never by message
- Malformed conditions → false + `ExpressionFailed` warning; malformed inline expressions → `ExpressionFailed` + `MissingVariable`
- Invalid placeholder syntax is ignored (treated as text)
- Missing variables: configurable via `MissingVariableBehavior`
- Invalid JSON in the JSON overloads throws `JsonException` before processing

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
// In TemplatePipeline.Create(options, missingVariables, warningCollector):
// Create temporary composite without loop
CompositeVisitor tempComposite = new CompositeVisitor(conditionalVisitor, placeholderVisitor);
LoopVisitor loopVisitor = new LoopVisitor(walker, tempComposite, warningCollector);

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
- Rewrites only the text elements a replacement overlaps (`ParagraphTextRewriter`)
- Parsed condition ASTs are cached process-wide (`ConditionAstCache`); default boolean formatters are cached per language
- Linear search through elements (O(n))

### InternalsVisibleTo
The test project has access to internal members via `<InternalsVisibleTo Include="TriasDev.Templify.Tests" />` in the .csproj file. Use `internal` for classes that need testing but shouldn't be public API.

## Troubleshooting Common Issues

### Text Spans Multiple Runs
If placeholder replacement fails, check if the placeholder is split across runs. The paragraph-level processing should handle this, but verify by examining the OpenXML structure.

### Formatting Lost
The replacement takes the formatting of the run holding the placeholder's first character. If formatting varies within a placeholder, only that run's formatting is applied. This is intentional.

### Nested Loops Not Working
Ensure LoopVisitor has the correct nested visitor set via `SetNestedVisitor()`. The circular reference is required for nested processing.

### Missing Variables
Check `MissingVariableBehavior` in options:
- `LeaveUnchanged` (default) - Keeps `{{placeholder}}`
- `ReplaceWithEmpty` - Removes placeholder
- `ThrowException` - Fails fast

## Additional Documentation

For comprehensive information, see:
- **TriasDev.Templify/ARCHITECTURE.md** - Current design: pipeline, walker, visitors, condition engine, error model
- **README.md** (root) and **TriasDev.Templify/README.md** (NuGet readme) - User-facing overview, API reference
- **TriasDev.Templify/Examples.md** - Extensive code samples and use cases
- **TriasDev.Templify/PERFORMANCE.md** - Historical benchmark snapshot
- **docs/** - Documentation site (MkDocs, `mkdocs.yml`; build with `pip install -r requirements.txt && mkdocs build --strict`)
- **docs/archive/** - Historical planning documents (TODO, REFACTORING, DOCUMENTATION_PLAN/SYSTEM), not maintained, not published
- **CONTRIBUTING.md** - Contribution workflow, release-please, public API rules

## Design Philosophy

This library prioritizes:
1. **Simplicity** - Focus on common use case (variable replacement, conditionals, loops)
2. **Maintainability** - Small, composable classes with single responsibilities
3. **Testability** - Pure functions, dependency injection, high test coverage
4. **Explicit behavior** - No magic, predictable results
5. **Fail-fast** - Clear error messages, no silent failures
