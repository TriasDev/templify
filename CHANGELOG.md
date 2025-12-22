# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- **ElseIf Support for Conditionals** - Multi-branch conditional logic with `{{#elseif condition}}` syntax
  - Chain multiple conditions: `{{#if A}}...{{#elseif B}}...{{#elseif C}}...{{else}}...{{/if}}`
  - Conditions evaluated in order - first matching branch wins
  - Strict validation: `{{else}}` must be the last branch
  - Full support for block-level, inline, and table row conditionals
  - Works with all existing operators (`=`, `!=`, `>`, `<`, `>=`, `<=`, `and`, `or`, `not`)

### Improved
- Test coverage increased to 826 tests
- Updated user documentation with elseif examples and troubleshooting

## [1.1.0] - 2025-12-02

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

## [1.0.0] - 2025-11-20

### Added
- Initial public release of Templify - a high-performance Word document templating engine for .NET
- **Core Features:**
  - Placeholder replacement with `{{variableName}}` syntax
  - Nested property paths: `{{Customer.Address.City}}`
  - Array/list indexing: `{{Items[0].Name}}`
  - Dictionary access: `{{Settings[Theme]}}` or `{{Settings.Theme}}`
- **Conditional Blocks:**
  - If/else statements: `{{#if condition}}...{{else}}...{{/if}}`
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

[Unreleased]: https://github.com/TriasDev/templify/compare/v1.1.0...HEAD
[1.1.0]: https://github.com/TriasDev/templify/compare/v1.0.0...v1.1.0
[1.0.0]: https://github.com/TriasDev/templify/releases/tag/v1.0.0
