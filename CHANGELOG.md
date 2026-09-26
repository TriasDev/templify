# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.8.0](https://github.com/TriasDev/templify/compare/v1.7.0...v1.8.0) (2026-09-26)

### ⚠️ Upgrade notes

1.8.0 is a large quality release: about 40 pull requests from a full audit of the library. The public C# API is **source- and binary-compatible** with 1.7.0: new members only, no removals, verified by package validation. Some **behavior** was fixed where it was clearly wrong, though. Please read this section before upgrading.

#### .NET 6 is no longer supported
- The package now targets **net8.0, net9.0 and net10.0**.
- .NET 6 has been out of support since November 2024. If you are still on .NET 6, stay on **1.7.x**.
- Policy: we support the .NET versions Microsoft supports. End-of-life target frameworks are dropped in a minor release, with a notice here.

#### Template and data errors are returned instead of thrown (#149)
- `ProcessTemplate` now returns `ProcessingResult.Failure` (with `IsSuccess == false` and a clear `ErrorMessage`) for template and data errors, instead of throwing `InvalidOperationException`. This covers:
  - an unmatched `{{#if}}` or `{{#foreach}}`
  - `{{#elseif}}` after `{{#else}}`
  - a loop over a value that is not a collection
  - an invalid iteration variable
- This is what the documentation always described. If you caught `InvalidOperationException` for these cases, check `result.IsSuccess` instead.
- **Unchanged:** `MissingVariableBehavior.ThrowException` still throws `InvalidOperationException`, with the same message.
- A malformed condition (for example `{{#if A && B}}`) still evaluates to false. It now also adds an `ExpressionFailed` warning to `result.Warnings`.

#### Output changes for existing templates
Each of these fixes a bug, but existing documents may render differently:

- **Numbers in conditions (#142)**
  - Numbers are compared by value across types, so `10 = 10.00m` is now true, as is JSON `10.50 = 10.5`.
  - Any numeric zero is falsy (`0m`, `0L`, `0.0` …), as documented. Before, only `int` 0 was.
  - Inline `{{(…)}}` expressions compare different numeric types correctly; before, they silently returned `False`.
  - `NaN` equals nothing, has no order and is falsy.
  - `contains` on a collection now checks membership (`Tags contains "urgent"`); before, it compared against the type name.
  - `startswith` / `endswith` on a collection are false.
- **Culture and time zone (#141)**
  - Numeric comparisons no longer depend on the server culture. Under `de-DE`, `{{#if Price < 100}}` with `99.99` used to be false.
  - Upper-case keywords (`{{#IF}}`, `{{#FOREACH}}`) now work under `tr-TR`.
  - An unset `DateTime` with a `:date:` format no longer fails the whole document in time zones ahead of UTC.
- **Null values in loops (#147)**
  - A `null` element in a collection no longer fails the document; `{{.}}` renders empty.
  - A `null` property on a loop item renders empty. Before, it fell through to a global variable with the same name.
- **Dictionary keys (#146):** a key named like a dictionary property (`Count`, `Keys`, `Values`, `Comparer`) now returns the key's value. Without such a key, `{{Dict.Count}}` still returns the entry count.
- **More parts of the document are processed (#144):** text boxes (VML and DrawingML), content controls, footnotes and endnotes. Comments are intentionally not processed.
- **Tables (#145):**
  - Conditional table rows now work.
  - A table whose rows are all removed (by a false row condition or an empty row loop) is removed entirely. Before, an invalid table without rows was left behind, and Word rejects that.
- **Inline conditionals (#143):**
  - Hyperlinks, fields, line breaks and bookmarks around an inline conditional are preserved. Before, they could be duplicated, turned into text or dropped.
  - Content inside a removed branch (images, breaks, field results) is removed together with that branch.
- **Keywords as variable names (#149):**
  - The words reserved in 1.7.0 (`in`, `is`, `empty`, `exists`, `contains`, `startswith`, `endswith`) work as variable names again where an operand is expected.
  - To force any word to be read as a variable, write it in brackets: `[Empty]`.
- **Plain-text templates (#150):** `TextTemplateProcessor` now matches the Word processor:
  - `{{#foreach item in Items}}` iterates; before, it silently removed the block.
  - `{{#elseif}}` and inline `{{(…)}}` work, markers are case-insensitive, and there is no longer a 100-block limit.
  - Conditionals are evaluated first, as in Word documents.
- **`ValidateTemplate` (#149, #198):**
  - It no longer reports operators, literals or loop-item properties in table-row loops as missing variables.
  - It now reports invalid condition expressions and missing variables in nested loops.
- **`:raw` (#148):** `raw` is now a built-in format specifier. A custom boolean formatter registered under the name `raw` is no longer used.

#### Deprecations (warnings now, removal in 2.0)
- `ConditionEvaluator.EvaluateAsync`, `ConditionContext.EvaluateAsync` and their interface members. They only wrapped synchronous work in a `Task`; use `Evaluate`.
- `PlaceholderFinder`, `PlaceholderMatch`: they become internal in 2.0. To list placeholders, use `ValidateTemplate(...)` and `ValidationResult.AllPlaceholders`.
- `TemplateElementType`: not used by any API.

If you build with `TreatWarningsAsErrors`, these show up as `CS0618`.

### 🔒 Security

- **Table-row loops re-processed their output with global data (#140).** A value like `"{{Secret}}"` inside a table-row loop was replaced with the global variable `Secret`, so user-supplied data could read other template variables. **Upgrade if your templates render untrusted data in table-row loops.**
- **Release pipeline hardened:**
  - Packages are published through NuGet **Trusted Publishing** (OIDC, no long-lived API key), only after the full test suite has passed on net8.0, net9.0 and net10.0, and behind a manual approval.
  - Builds are deterministic, with SourceLink and a symbol package (`.snupkg`).

### ✨ Highlights

- **New options and syntax**
  - `PlaceholderReplacementOptions.EnableMarkdown` (default `true`) and the per-placeholder `{{Value:raw}}` for inserting values containing `_`, `*` or `~` literally (#148).
  - 1-based loop metadata: `{{@number}}` (#174).
- **New overloads**
  - `IReadOnlyDictionary<string, object?>` data.
  - `ProcessTemplate(byte[], data, out byte[])`.
  - `ProcessTemplateFile(templatePath, outputPath, data)` (#156).
- **Data sources:** `JsonElement` values (e.g. from `JsonSerializer.Deserialize<Dictionary<string, object>>`), `ExpandoObject`, read-only dictionaries and typed dictionary keys (`{{Map[1]}}`) now resolve (#146).
- **Correctness**
  - Images and shapes cloned by loops get unique ids (#178).
  - `UpdateFieldsOnOpenMode.Auto` detects simple fields (#196).
  - Markdown formatting produces schema-valid run properties (#148).
- **Performance:** condition evaluation is 4–15× faster with about 98% fewer allocations, thanks to a parsed-expression cache and source-generated regexes (#155).
- **Converter CLI (#151):**
  - No more data loss on in-place `clean`.
  - Repeating table rows and `and`/`or` conditions convert correctly.
  - Headers, footers and notes are converted.
  - Proper exit codes.



### Features

* **api:** add IReadOnlyDictionary/file/byte[] overloads; deprecate fake-async and internal parsing helpers ([#156](https://github.com/TriasDev/templify/issues/156)) ([#195](https://github.com/TriasDev/templify/issues/195)) ([699bf57](https://github.com/TriasDev/templify/commit/699bf57960a648cf0a4123571601e107209e57cf))
* **loops:** add 1-based `{{@number}}` loop metadata; converter maps variable_index to it ([#174](https://github.com/TriasDev/templify/issues/174)) ([#175](https://github.com/TriasDev/templify/issues/175)) ([5633f57](https://github.com/TriasDev/templify/commit/5633f572a2741f71076befb61dba2a317e865dd9))
* **markdown:** add EnableMarkdown option and :raw format specifier; emit schema-valid run properties ([#148](https://github.com/TriasDev/templify/issues/148)) ([#166](https://github.com/TriasDev/templify/issues/166)) ([dd3fee9](https://github.com/TriasDev/templify/commit/dd3fee92861210c97afcb2c18461cffc5174b30d))


### Bug Fixes

* **conditionals:** inline conditionals keep hyperlinks, fields, breaks, drawings and bookmarks ([#143](https://github.com/TriasDev/templify/issues/143)) ([#173](https://github.com/TriasDev/templify/issues/173)) ([338c957](https://github.com/TriasDev/templify/commit/338c957ae72b8b874e563fa98c0d02c0fd2a592b))
* **conditionals:** numeric equality and truthiness across numeric types; contains on collections checks membership ([#142](https://github.com/TriasDev/templify/issues/142)) ([#185](https://github.com/TriasDev/templify/issues/185)) ([895bb03](https://github.com/TriasDev/templify/commit/895bb038dedb47a551d96991448b3d8d7ea94f54))
* consistent error model, warnings for malformed conditions, AST-based template validation ([#149](https://github.com/TriasDev/templify/issues/149)) ([#180](https://github.com/TriasDev/templify/issues/180)) ([8b568a7](https://github.com/TriasDev/templify/commit/8b568a7cb156d4e1e8ce97122611da52d59cbeee))
* **converter:** prevent data loss and wrong conversions; add Converter.Tests ([#169](https://github.com/TriasDev/templify/issues/169)) ([f027039](https://github.com/TriasDev/templify/commit/f0270398ad5d7e95baf7f059f64f87c89169a11d)), closes [#151](https://github.com/TriasDev/templify/issues/151)
* **fields:** detect simple fields (w:fldSimple) for UpdateFieldsOnOpenMode.Auto ([#196](https://github.com/TriasDev/templify/issues/196)) ([#205](https://github.com/TriasDev/templify/issues/205)) ([db415bd](https://github.com/TriasDev/templify/commit/db415bd64f65ac5a049b8a5e676cc2899f26d4a5))
* **gui:** fix output handling, command states and progress; add ViewModel tests ([#168](https://github.com/TriasDev/templify/issues/168)) ([f5876cd](https://github.com/TriasDev/templify/commit/f5876cd93454a6d821a0f65473b62e07abf761bf))
* **i18n:** make conditions, keywords and dates culture/timezone independent ([#141](https://github.com/TriasDev/templify/issues/141)) ([#163](https://github.com/TriasDev/templify/issues/163)) ([4c22577](https://github.com/TriasDev/templify/commit/4c22577cf3034ca5d483bae669d9452505744dfe))
* **loops:** assign unique drawing object ids to cloned loop content ([#178](https://github.com/TriasDev/templify/issues/178)) ([#179](https://github.com/TriasDev/templify/issues/179)) ([7a9c0b4](https://github.com/TriasDev/templify/commit/7a9c0b4c4b63f50d51aa81b1e89224b74ed24549))
* **loops:** do not re-process table rows produced by loop expansion ([#140](https://github.com/TriasDev/templify/issues/140)) ([#161](https://github.com/TriasDev/templify/issues/161)) ([f589fed](https://github.com/TriasDev/templify/commit/f589fed212f33326d726bba6066ef20cb231a231))
* **loops:** support null items and null item properties without parent-scope fallthrough ([#147](https://github.com/TriasDev/templify/issues/147)) ([#162](https://github.com/TriasDev/templify/issues/162)) ([0423141](https://github.com/TriasDev/templify/commit/04231419248381b0f0caf8ab57082e205afaf0ea))
* **paths:** dictionary keys take precedence over dictionary properties; support ExpandoObject, IReadOnlyDictionary and typed keys ([#146](https://github.com/TriasDev/templify/issues/146)) ([#165](https://github.com/TriasDev/templify/issues/165)) ([c981923](https://github.com/TriasDev/templify/commit/c981923fc0b7a1b74760504388c4b246ebbdcf3a))
* **scripts:** run converter scripts from any directory; fix batch examples ([#172](https://github.com/TriasDev/templify/issues/172)) ([bb42961](https://github.com/TriasDev/templify/commit/bb429616e6f888f8205963451f3919d81291c1aa)), closes [#152](https://github.com/TriasDev/templify/issues/152)
* **tables:** row detection handles cell content controls and ignores text box content ([#178](https://github.com/TriasDev/templify/issues/178)) ([#188](https://github.com/TriasDev/templify/issues/188)) ([903dfea](https://github.com/TriasDev/templify/commit/903dfeabd074e9f30105312a21270a0a4e86ebbd))
* **tables:** support conditional table rows and named loops in single cells ([#145](https://github.com/TriasDev/templify/issues/145)) ([#167](https://github.com/TriasDev/templify/issues/167)) ([8961ff0](https://github.com/TriasDev/templify/commit/8961ff0b433642dd4f1e619ce262dbbd21c977b6))
* **text:** TextTemplateProcessor parity with the Word processor ([#150](https://github.com/TriasDev/templify/issues/150)) ([#186](https://github.com/TriasDev/templify/issues/186)) ([3bec733](https://github.com/TriasDev/templify/commit/3bec733604812c51843aa876a37208f87b1211c8))
* **tools:** DocumentGenerator, Demo and Benchmarks fixes ([#171](https://github.com/TriasDev/templify/issues/171)) ([c34d3a3](https://github.com/TriasDev/templify/commit/c34d3a392bdf82dac51ba4861e89d51da4a6c920))
* **validation:** scope table-row loop item variables in ValidateTemplate ([#198](https://github.com/TriasDev/templify/issues/198)) ([#206](https://github.com/TriasDev/templify/issues/206)) ([fbf6ddb](https://github.com/TriasDev/templify/commit/fbf6ddb7a7efabc9d0fc505f36f2583806f4989f))
* **walker:** process text boxes, content controls, footnotes and endnotes ([#144](https://github.com/TriasDev/templify/issues/144)) ([#176](https://github.com/TriasDev/templify/issues/176)) ([936b5c9](https://github.com/TriasDev/templify/commit/936b5c9e7d9ade16a7fc39d3fe20dfbf0368e575))


### Performance Improvements

* cache parsed condition ASTs and default boolean formatters; source-generated regexes ([#155](https://github.com/TriasDev/templify/issues/155)) ([#193](https://github.com/TriasDev/templify/issues/193)) ([398d55c](https://github.com/TriasDev/templify/commit/398d55c2f3219c42135f4484da03f1d69ad1a11b))

## [1.7.0](https://github.com/TriasDev/templify/compare/v1.6.2...v1.7.0) (2026-07-21)


### Features

* **conditionals:** extensible operator engine + in/string/existence operators + grouping ([#128](https://github.com/TriasDev/templify/issues/128)) ([#129](https://github.com/TriasDev/templify/issues/129)) ([035669c](https://github.com/TriasDev/templify/commit/035669c90ee2c106d33b216d96ea1e789b56a9b1))
  * New operators in `{{#if}}`, inline `{{(...)}}`, text templates and `ConditionEvaluator`: `in` (list literal `(a, b)`, collection or comma-separated string), `contains` / `startswith` / `endswith`, postfix `exists`, `is empty`, `is not empty`, and parentheses for grouping.
  * One precedence table for all entry points (loosest to tightest): `or`, `and`, `not`, comparisons / `in` / string operators, postfix `exists` / `is empty`. `and` now binds tighter than `or` in `{{#if}}` too (before, `{{#if}}` evaluated `and`/`or` left to right with equal precedence), and `not A = B` means `not (A = B)`.
  * The new operator words (`in`, `is`, `empty`, `exists`, `contains`, `startswith`, `endswith`) became keywords, so variables with these names could no longer be used bare in conditions. Since 1.8.0 they are read as variables again where an operand is expected, and `[Name]` escapes any keyword.

## [1.6.2](https://github.com/TriasDev/templify/compare/v1.6.1...v1.6.2) (2026-07-03)


### Bug Fixes

* **conditionals:** keep table cells valid when a branch is removed ([#117](https://github.com/TriasDev/templify/issues/117)) ([#118](https://github.com/TriasDev/templify/issues/118)) ([fbd159a](https://github.com/TriasDev/templify/commit/fbd159a555b58f29b9cd72c6c89ad5e101112284))

## [1.6.1](https://github.com/TriasDev/templify/compare/v1.6.0...v1.6.1) (2026-04-01)


### Bug Fixes

* sanitize invalid XML characters in template values ([#87](https://github.com/TriasDev/templify/issues/87)) ([71a1fa6](https://github.com/TriasDev/templify/commit/71a1fa689c24bf27c86b24a096332c7f720a2b78))

## [1.6.0](https://github.com/TriasDev/templify/compare/v1.5.0...v1.6.0) - 2026-03-16

### Added
- **Header & Footer Support** - Process placeholders, conditionals, and loops in document headers and footers (#15)
  - All header/footer types supported: Default, First Page, Even Page
  - Same syntax and features as document body - no additional API calls needed
  - Formatting is preserved in headers and footers
- **Non-Boolean Format Specifiers** - Format numeric, string, and date values directly in placeholders (#22)
  - `:currency` — locale-aware currency formatting (e.g., `$1,234.56` or `1.234,56 €`)
  - `:number:FORMAT` — any .NET numeric format string (e.g., `:number:N2`, `:number:F3`, `:number:P`)
  - `:uppercase` / `:lowercase` — string casing transformations
  - `:date:FORMAT` — any .NET date format string (e.g., `:date:yyyy-MM-dd`, `:date:MMMM d, yyyy`)
  - Works with int, long, decimal, double, float, DateTime, DateTimeOffset, and ISO date strings
  - Culture-aware formatting throughout
- **Unified Equality Operators** - `==` now works as an alias for `=` in both `{{#if}}` conditionals and `{{(...)}}` boolean expressions (#84)
  - Condition validation detects unknown operators (`===`, `<>`, `&&`, `||`) and structural issues (missing operands, unbalanced quotes, consecutive operators)
- **GUI Culture Selector** - Dropdown to choose formatting culture (Invariant, en-US, de-DE, fr-FR, es-ES)

### Improved
- Test coverage increased to 1,095 tests

## [1.5.0](https://github.com/TriasDev/templify/compare/v1.4.2...v1.5.0) - 2026-02-13

### Added
- **DocumentProperties Option** - Set document metadata properties on the output document (#77)
  - `Author`, `Title`, `Subject`, `Description`, `Keywords`, `Category`, `LastModifiedBy`
  - Null properties preserve original template values; non-null values overwrite
  - Configure via `PlaceholderReplacementOptions.DocumentProperties`
- **Custom Templify Icon and Branding** - New icon for the library, GUI, and documentation (#76)

### Improved
- Test coverage increased to 972 tests

## [1.4.2](https://github.com/TriasDev/templify/compare/v1.4.1...v1.4.2) - 2026-02-09

### Fixed
- **Case-insensitive boolean comparison** in ConditionalEvaluator - boolean values like `True`/`False`/`TRUE`/`FALSE` are now correctly compared regardless of case, while preserving case-sensitive string comparisons (#72)

### Improved
- Test coverage increased to 965 tests

## [1.4.1](https://github.com/TriasDev/templify/compare/v1.4.0...v1.4.1) - 2026-01-07

### Added
- **Processing Warnings System** - Collect non-fatal warnings during template processing
  - `ProcessingWarning` class with warning type, variable name, context, and message
  - Warning types: `MissingVariable`, `MissingLoopCollection`, `NullLoopCollection`, `ExpressionFailed`
  - Access warnings via `ProcessingResult.Warnings` and `ProcessingResult.HasWarnings`
  - Generate Word document warning reports with `GetWarningReport()` and `GetWarningReportBytes()`
- GUI: "Generate Warning Report" button and warning summary display

### Improved
- Test coverage increased to 953 tests

## [1.4.0](https://github.com/TriasDev/templify/compare/v1.3.0...v1.4.0) - 2026-01-07

### Added
- **UpdateFieldsOnOpen Option** - Automatically prompt Word to refresh TOC and dynamic fields when documents are opened
  - `UpdateFieldsOnOpenMode.Never` - Never prompt (default, backward compatible)
  - `UpdateFieldsOnOpenMode.Always` - Always prompt to update fields
  - `UpdateFieldsOnOpenMode.Auto` - Only prompt if document contains dynamic fields (recommended)
  - Auto mode detects: TOC, PAGE, NUMPAGES, PAGEREF, DATE, TIME, FILENAME, REF, NOTEREF, SECTIONPAGES
  - Solves stale page numbers when content changes via conditionals/loops

### Improved
- Test coverage increased to 939 tests
- New test helpers: DocumentBuilder and DocumentVerifier for cleaner TOC testing
- Updated DocumentFormat.OpenXml to 3.4.1 (performance improvements)

## [1.3.0](https://github.com/TriasDev/templify/compare/v1.2.0...v1.3.0) - 2026-01-05

### Added
- **Text Replacement Lookup Tables** - Pre-process text before template processing
  - `TextReplacementLookup` for custom character/string replacements
  - `HtmlEntityPreset` for common HTML entities (`&amp;`, `&lt;`, `&gt;`, `&nbsp;`, `&mdash;`, `&ndash;`, etc.)
  - GUI support for HTML entity replacement option
- **Named Iteration Variable Syntax** - Access parent loop variables in nested loops
  - New syntax: `{{#foreach item in CollectionName}}...{{item.Property}}...{{/foreach}}`
  - Access parent scope: `{{category.Name}}` inside `{{#foreach product in category.Products}}`
- **ElseIf Support for Conditionals** - Multi-branch conditional logic with `{{#elseif condition}}` syntax
  - Chain multiple conditions: `{{#if A}}...{{#elseif B}}...{{#elseif C}}...{{#else}}...{{/if}}`
  - Conditions evaluated in order - first matching branch wins
  - Strict validation: `{{#else}}` must be the last branch
  - Full support for block-level, inline, and table row conditionals
- **Newline Character Support** - Variable values can now contain `\n` for line breaks
- **Inline Conditionals** - Conditionals within a single paragraph
- **Highlight and Shading Preservation** - Per-run placeholder replacement now preserves highlight and shading formatting
- **Typographic Quote Support** - Conditional expressions now accept curly/smart quotes (`""` `''`)
- **.NET 10 Support** - Added `net10.0` target framework

### Changed
- **BREAKING:** `{{else}}` syntax changed to `{{#else}}` for consistency with other control tags

### Fixed
- Conditionals inside loops now evaluate with correct context
- Loop-scoped variables are now correctly validated in template validation
- Null values are now treated as valid in template validation
- Nested paragraphs no longer generated in RepeatingConverter

### Improved
- Test coverage increased to 929 tests
- Updated NuGet dependencies

## [1.2.0](https://github.com/TriasDev/templify/compare/v1.1.0...v1.2.0) - 2025-12-15

### Added
- **TextTemplateProcessor** - Process email and plain text templates using the same syntax as Word documents

## [1.1.0](https://github.com/TriasDev/templify/compare/v1.0.0...v1.1.0) - 2025-12-02

### Added
- **Standalone Condition Evaluation API** - Use Templify's condition engine without processing Word documents
  - `IConditionEvaluator` interface for evaluating conditional expressions against data
  - `ConditionEvaluator` implementation with full operator support
  - `IConditionContext` interface for efficient batch evaluation of multiple expressions
  - `ConditionContext` implementation for reusable evaluation contexts
  - `CreateConditionContext()` methods for creating batch evaluation contexts
  - Async methods with `CancellationToken` support
  - Thread-safe implementation
- **Developer Documentation** - New documentation section for developers
  - Comprehensive condition evaluation API guide
  - Code examples for Dictionary and JSON data sources

### Changed
- Documentation reorganized into template author and developer sections
- Clarified case sensitivity behavior for JSON keys vs object properties

### Improved
- Code quality enforcement via `.editorconfig` rules
- Test coverage increased to 743 tests

## [1.0.0](https://github.com/TriasDev/templify/releases/tag/v1.0.0) - 2025-11-20

### Added
- Initial public release of Templify - a high-performance Word document templating engine for .NET
- **Core Features:**
  - Placeholder replacement with `{{variableName}}` syntax
  - Nested property paths: `{{Customer.Address.City}}`
  - Array/list indexing: `{{Items[0].Name}}`
  - Dictionary access: `{{Settings[Theme]}}` or `{{Settings.Theme}}`
- **Conditional Blocks:**
  - If/else statements: `{{#if condition}}...{{#else}}...{{/if}}`
  - Boolean operators: `and`, `or`, `not`
  - Comparison operators: `=`, `!=`, `>`, `<`, `>=`, `<=`
  - Nested conditionals support
- **Loops:**
  - Collection iteration: `{{#foreach Items}}...{{/foreach}}`
  - Table row loops for dynamic tables
  - Loop metadata: `{{@index}}`, `{{@first}}`, `{{@last}}`, `{{@count}}`
  - Nested loops support (arbitrary depth)
- **Markdown Formatting:**
  - Bold: `**text**` or `__text__`
  - Italic: `*text*` or `_text_`
  - Strikethrough: `~~text~~`
  - Combined: `***text***` for bold+italic
- **Format Specifiers:**
  - Boolean formatters: `:checkbox`, `:yesno`, `:truefalse`, `:onoff`
  - Date/number formatting via standard .NET format strings
- **Architecture:**
  - Visitor pattern for extensible document processing
  - Multi-targeting support for .NET 6, 8, and 9
  - Zero dependencies (only DocumentFormat.OpenXml)
  - 100% test coverage with 109+ tests
- **Tools:**
  - TriasDev.Templify.Converter - CLI for migrating from OpenXMLTemplates
  - TriasDev.Templify.Gui - Cross-platform Avalonia GUI application
  - TriasDev.Templify.Demo - Example console application
  - Helper scripts for common operations
- **Documentation:**
  - Comprehensive README with examples
  - Architecture documentation
  - API reference
  - Tutorials and guides
  - Contributing guidelines
  - Security policy

### Performance
- Processes 1,000 placeholders in ~50ms
- Handles 100 loops in ~150ms
- Evaluates 500 conditionals in ~30ms
- Complex 50-page documents in ~500ms

### Compatibility
- Supports .NET 6.0, 8.0, and 9.0
- Works with Word 2007+ documents (.docx)
- Cross-platform: Windows, Linux, macOS
- No Microsoft Word installation required
