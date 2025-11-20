# API Reference

Welcome to the TriasDev.Templify API reference documentation.

## Namespaces

Browse the API by namespace:

- **TriasDev.Templify.Core** - Core template processing classes
- **TriasDev.Templify.Visitors** - Visitor pattern implementations
- **TriasDev.Templify.Conditionals** - Conditional expression evaluation
- **TriasDev.Templify.Loops** - Loop processing and evaluation contexts
- **TriasDev.Templify.Placeholders** - Placeholder detection and replacement
- **TriasDev.Templify.PropertyPaths** - Property path resolution for nested data
- **TriasDev.Templify.Markdown** - Markdown parsing and formatting
- **TriasDev.Templify.Utilities** - Utility classes for formatting and parsing

## Key Classes

### Template Processing
- `DocumentTemplateProcessor` - Main entry point for template processing
- `PlaceholderReplacementOptions` - Configuration options
- `ProcessingResult` - Result wrapper with success/failure state

### Visitors
- `DocumentWalker` - Unified document traversal engine
- `PlaceholderVisitor` - Processes placeholder replacements
- `ConditionalVisitor` - Evaluates and removes conditional branches
- `LoopVisitor` - Expands loop blocks
- `CompositeVisitor` - Combines multiple visitors

### Evaluation
- `GlobalEvaluationContext` - Root data dictionary
- `LoopEvaluationContext` - Loop-scoped variable resolution
- `ConditionalEvaluator` - Evaluates boolean expressions
- `PropertyPathResolver` - Resolves nested property paths

## Examples

For code examples and usage patterns, see:
- [Quick Start Guide](../articles/quick-start.md)
- [Tutorials](../articles/tutorials/toc.yml)
- [Guides](../articles/guides/toc.yml)
