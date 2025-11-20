# Contributing to Templify

Thank you for your interest in contributing to Templify! We welcome contributions from the community and appreciate your efforts to improve this project.

## Table of Contents

- [Code of Conduct](#code-of-conduct)
- [How Can I Contribute?](#how-can-i-contribute)
  - [Reporting Bugs](#reporting-bugs)
  - [Suggesting Enhancements](#suggesting-enhancements)
  - [Pull Requests](#pull-requests)
- [Development Setup](#development-setup)
- [Development Guidelines](#development-guidelines)
  - [Code Style](#code-style)
  - [Testing Requirements](#testing-requirements)
  - [Commit Message Format](#commit-message-format)
  - [Branch Naming Conventions](#branch-naming-conventions)
- [Documentation](#documentation)
- [Additional Resources](#additional-resources)

---

## Code of Conduct

This project and everyone participating in it is governed by our Code of Conduct. By participating, you are expected to uphold this code. Please report unacceptable behavior to the project maintainers.

---

## How Can I Contribute?

### Reporting Bugs

Before creating bug reports, please check existing issues to avoid duplicates. When creating a bug report, include as many details as possible:

**Bug Report Checklist:**
- **Clear, descriptive title** - Summarize the problem in one sentence
- **Steps to reproduce** - Detailed steps to reproduce the issue
- **Expected behavior** - What you expected to happen
- **Actual behavior** - What actually happened
- **Environment details**:
  - Templify version
  - .NET version
  - Operating system
  - Any relevant configuration
- **Sample code/template** - Minimal code to reproduce the issue (if applicable)
- **Error messages** - Full stack traces or error messages

**Example:**

```markdown
## Bug: Placeholder not replaced in nested table cells

**Steps to reproduce:**
1. Create a Word template with a table containing nested tables
2. Add placeholder `{{CompanyName}}` in the nested table cell
3. Process template with data containing CompanyName

**Expected:** Placeholder should be replaced with actual value
**Actual:** Placeholder remains unchanged

**Environment:**
- Templify: 1.0.0
- .NET: 9.0
- OS: Windows 11

**Sample code:** [link or inline code]
```

### Suggesting Enhancements

Enhancement suggestions are tracked as GitHub issues. When creating an enhancement suggestion:

- **Use a clear, descriptive title**
- **Provide detailed description** of the proposed enhancement
- **Explain the use case** - Why is this enhancement needed?
- **Describe alternatives** - Have you considered alternative solutions?
- **Provide examples** - Show how the feature would be used

### Pull Requests

We actively welcome pull requests! To increase the likelihood of your PR being accepted:

1. **Discuss first** - For major changes, open an issue first to discuss your proposal
2. **Follow guidelines** - Adhere to our coding standards and conventions
3. **Write tests** - Maintain 100% test coverage
4. **Update documentation** - Keep docs in sync with code changes
5. **Keep it focused** - One feature/fix per PR

**Pull Request Process:**

1. Fork the repository and create your branch from `main`
2. Make your changes following our [development guidelines](#development-guidelines)
3. Add tests for any new functionality
4. Ensure all tests pass (`dotnet test`)
5. Update documentation (README.md, ARCHITECTURE.md, etc.) if needed
6. Commit your changes using our [commit message format](#commit-message-format)
7. Push to your fork and submit a pull request
8. Link relevant issues in your PR description

**PR Title Format:**
```
Add feature X
Fix bug in Y
Update documentation for Z
```

**PR Description Template:**
```markdown
## Summary
Brief description of what this PR does

## Changes
- Bullet point list of changes
- Another change

## Related Issues
Fixes #123
Relates to #456

## Testing
- [ ] All existing tests pass
- [ ] New tests added for new functionality
- [ ] Manual testing completed

## Documentation
- [ ] README.md updated (if needed)
- [ ] Code comments added/updated
- [ ] ARCHITECTURE.md updated (if needed)
```

---

## Commit Message Format

We follow the [Conventional Commits](https://www.conventionalcommits.org/) specification for commit messages. This leads to more readable commit history and enables automated changelog generation.

### Format

```
<type>(<scope>): <description>

[optional body]

[optional footer(s)]
```

### Types

**Types that appear in CHANGELOG:**
- `feat:` - New feature → Goes in "Added" section
- `fix:` - Bug fix → Goes in "Fixed" section
- `perf:` - Performance improvement → Goes in "Performance" or "Changed" section
- Breaking changes: Add `BREAKING CHANGE:` in commit body → Goes in "Breaking Changes" section

**Types for internal changes (NOT in CHANGELOG):**
- `docs:` - Documentation changes only
- `style:` - Code formatting, whitespace, missing semicolons
- `refactor:` - Code restructuring without changing behavior
- `test:` - Adding or updating tests
- `chore:` - Maintenance tasks, dependency updates
- `ci:` - CI/CD configuration changes
- `build:` - Build system or tooling changes

### Scope (Optional)

The scope provides additional context:
- `feat(loops):` - Loop-related feature
- `fix(placeholders):` - Placeholder bug fix
- `docs(contributing):` - Contributing documentation
- `ci(workflow):` - Workflow configuration

### Examples

```bash
# Feature with body
feat: add support for custom date format specifiers

Implements #42

# Bug fix
fix: resolve placeholder replacement in nested table cells

Fixes #38

# Documentation
docs(readme): update installation instructions

# Breaking change
feat!: change placeholder syntax to use curly braces

BREAKING CHANGE: Placeholder syntax changed from {{var}} to ${var}.
Update all templates before upgrading.

# Multiple changes
feat(conditionals): add support for 'or' operator in conditions

- Implements logical OR operator
- Updates conditional evaluator
- Adds comprehensive tests
```

### When Making a Commit

1. **Choose the appropriate type** based on your change
2. **If type is `feat`, `fix`, `perf`, or breaking change:**
   - Also update `CHANGELOG.md` in the `[Unreleased]` section
   - Ensure commit description matches changelog entry
3. **Write clear, descriptive commit messages**
   - Use present tense: "add feature" not "added feature"
   - Be specific: "fix date formatting in German locale" not "fix bug"
4. **Reference issues** in commit body or footer

### CHANGELOG Mapping

| Commit Type | CHANGELOG Section | Required |
|-------------|-------------------|----------|
| `feat:` | `### Added` | Yes |
| `fix:` | `### Fixed` | Yes |
| `perf:` | `### Performance` or `### Changed` | Yes |
| `BREAKING CHANGE:` | `### Breaking Changes` | Yes |
| `docs:`, `style:`, `refactor:`, etc. | Not included | No |

---

## Development Setup

### Prerequisites

- **.NET 9.0 SDK** or later - [Download](https://dotnet.microsoft.com/download)
- **Git** - [Download](https://git-scm.com/)
- **IDE** (optional but recommended):
  - Visual Studio 2022
  - JetBrains Rider
  - Visual Studio Code with C# extension

### Clone and Build

```bash
# Clone the repository
git clone https://github.com/TriasDev/templify.git
cd templify

# Restore dependencies
dotnet restore templify.sln

# Build the solution
dotnet build templify.sln

# Run tests
dotnet test TriasDev.Templify.Tests/TriasDev.Templify.Tests.csproj

# Run tests with coverage
dotnet test --collect:"XPlat Code Coverage"
```

### Project Structure

```
templify/
├── TriasDev.Templify/          # Core library
├── TriasDev.Templify.Tests/    # Test suite (690+ tests)
├── TriasDev.Templify.Gui/      # GUI application
├── TriasDev.Templify.Converter/# CLI converter tool
├── TriasDev.Templify.Benchmarks/# Performance benchmarks
└── TriasDev.Templify.Demo/     # Demo console application
```

### Running Specific Projects

```bash
# Run demo application
dotnet run --project TriasDev.Templify.Demo/TriasDev.Templify.Demo.csproj

# Run GUI application
dotnet run --project TriasDev.Templify.Gui/TriasDev.Templify.Gui.csproj

# Run benchmarks
dotnet run --project TriasDev.Templify.Benchmarks/TriasDev.Templify.Benchmarks.csproj -c Release

# Run converter tool
dotnet run --project TriasDev.Templify.Converter/TriasDev.Templify.Converter.csproj -- [command] [args]
```

---

## Development Guidelines

### Code Style

We follow standard C# coding conventions. Key guidelines:

**Naming Conventions:**
- **Private fields:** `_camelCase` (with underscore prefix)
- **Public properties:** `PascalCase`
- **Local variables:** `camelCase`
- **Constants:** `PascalCase`
- **Methods:** `PascalCase`
- **Interfaces:** `IPascalCase` (with I prefix)

**Code Organization:**
- **One class per file** (except for closely related nested types)
- **Namespace matches folder structure**
- **Group members by type:** fields → constructors → properties → methods
- **Order by visibility:** public → internal → private

**Code Quality:**
- **Use nullable reference types** - `<Nullable>enable</Nullable>` is enabled
- **Check for null** before processing nullable values
- **Avoid magic numbers** - Use named constants
- **Keep methods focused** - Single Responsibility Principle
- **Prefer composition over inheritance**
- **Use meaningful names** - Code should be self-documenting

**XML Documentation:**
- Add XML documentation to all **public APIs**
- Document **parameters**, **return values**, and **exceptions**
- Internal classes: document where complexity warrants explanation
- Complex algorithms: add inline comments explaining "why" not "what"

**Example:**
```csharp
/// <summary>
/// Processes a Word document template by replacing placeholders with actual values.
/// </summary>
/// <param name="templateStream">The input template stream.</param>
/// <param name="outputStream">The output stream for the processed document.</param>
/// <param name="data">The data dictionary containing replacement values.</param>
/// <returns>A <see cref="ProcessingResult"/> indicating success or failure.</returns>
/// <exception cref="ArgumentNullException">Thrown when any parameter is null.</exception>
public ProcessingResult ProcessTemplate(Stream templateStream, Stream outputStream, Dictionary<string, object> data)
{
    // Implementation
}
```

### Testing Requirements

**We maintain 100% test coverage.** All contributions must include tests.

**Testing Guidelines:**

1. **Unit Tests** - Test individual components in isolation
   - Location: `TriasDev.Templify.Tests/`
   - Use xUnit framework
   - Mock dependencies where appropriate

2. **Integration Tests** - Test end-to-end scenarios
   - Location: `TriasDev.Templify.Tests/Integration/`
   - Process actual Word documents
   - Verify output using `DocumentVerifier` helper

3. **Test Naming Convention:**
   ```csharp
   [Fact]
   public void MethodName_Scenario_ExpectedBehavior()
   {
       // Arrange
       // Act
       // Assert
   }
   ```

4. **Test Organization:**
   - **Arrange** - Set up test data and dependencies
   - **Act** - Execute the code under test
   - **Assert** - Verify expected outcomes

5. **Coverage Requirements:**
   - **100% line coverage** for new code
   - **100% branch coverage** for new code
   - Run: `dotnet test --collect:"XPlat Code Coverage"`

**Example Test:**
```csharp
[Fact]
public void ProcessTemplate_ValidTemplate_ReplacesPlaceholders()
{
    // Arrange
    var builder = new DocumentBuilder();
    builder.AddParagraph("Hello {{Name}}!");
    var templateStream = builder.ToStream();

    var data = new Dictionary<string, object>
    {
        ["Name"] = "World"
    };

    var processor = new DocumentTemplateProcessor();
    var outputStream = new MemoryStream();

    // Act
    var result = processor.ProcessTemplate(templateStream, outputStream, data);

    // Assert
    Assert.True(result.IsSuccess);
    using var verifier = new DocumentVerifier(outputStream);
    Assert.Contains("Hello World!", verifier.GetParagraphText(0));
}
```

### Commit Message Format

Use clear, imperative commit messages that explain **what** and **why** (not how).

**Format:**
```
<type>: <subject>

<body>

<footer>
```

**Type:**
- `Add` - New feature or functionality
- `Fix` - Bug fix
- `Update` - Enhancement to existing feature
- `Remove` - Code/feature removal
- `Refactor` - Code restructuring without behavior change
- `Docs` - Documentation changes
- `Test` - Test additions or modifications
- `Chore` - Maintenance tasks (dependencies, configs, etc.)

**Subject Line:**
- Use imperative mood ("Add feature" not "Added feature")
- Don't capitalize first letter (unless proper noun)
- No period at the end
- Limit to 72 characters

**Body (optional):**
- Explain **what** and **why**, not **how**
- Wrap at 72 characters
- Separate from subject with blank line

**Footer (optional):**
- Reference issues: `Fixes #123` or `Relates to #456`
- Breaking changes: `BREAKING CHANGE: description`

**Examples:**

**Simple:**
```
Add support for array indexing in placeholders
```

**With body:**
```
Fix placeholder replacement in nested tables

Placeholders in nested table cells were not being detected due to
incorrect table traversal logic. Updated DocumentWalker to recursively
process nested tables.

Fixes #42
```

**Multiple changes:**
```
Add markdown formatting support

Changes:
- Implement MarkdownParser to detect markdown syntax
- Update PlaceholderVisitor to apply markdown formatting
- Add FormattingPreserver.ApplyMarkdownFormatting() method
- Support for bold, italic, strikethrough, and combined styles
- Add 15 integration tests for markdown features

Relates to #5
```

### Branch Naming Conventions

Use descriptive branch names that indicate the purpose of the changes.

**Format:**
```
<type>/<short-description>
```

**Types:**
- `feature/` - New features
- `fix/` - Bug fixes
- `docs/` - Documentation changes
- `refactor/` - Code refactoring
- `test/` - Test additions/modifications
- `chore/` - Maintenance tasks

**Examples:**
```
feature/add-array-indexing
fix/nested-table-placeholders
docs/update-api-reference
refactor/visitor-pattern
test/add-loop-coverage
chore/update-dependencies
```

**Guidelines:**
- Use lowercase with hyphens
- Keep it short but descriptive
- Don't use issue numbers alone (e.g., avoid `fix/123`)
- Branch from `main` for new work
- Delete branch after PR is merged

---

## Documentation

### When to Update Documentation

Update documentation when you:
- **Add new features** - Document in README.md, Examples.md, and docs/
- **Change public APIs** - Update API reference and XML comments
- **Modify architecture** - Update ARCHITECTURE.md
- **Add complex logic** - Add inline code comments
- **Fix bugs** - Update troubleshooting sections if applicable

### Documentation Files

**Repository Documentation:**
- **[README.md](README.md)** - Project overview, getting started
- **[TriasDev.Templify/README.md](TriasDev.Templify/README.md)** - API reference, usage guide
- **[TriasDev.Templify/Examples.md](TriasDev.Templify/Examples.md)** - Comprehensive code examples
- **[TriasDev.Templify/ARCHITECTURE.md](TriasDev.Templify/ARCHITECTURE.md)** - Design patterns, technical decisions
- **[CLAUDE.md](CLAUDE.md)** - Development guide for contributors

**User Documentation (docs/ folder):**
- **[docs/quick-start.md](docs/quick-start.md)** - Quick start guide
- **[docs/tutorials/](docs/tutorials/)** - Step-by-step tutorials
- **[docs/guides/](docs/guides/)** - Feature guides
- **[docs/FAQ.md](docs/FAQ.md)** - Frequently asked questions

**XML Documentation:**
- All public APIs must have XML documentation comments
- XML comments are automatically generated into `.xml` files for IntelliSense
- Run `dotnet build` to verify XML comment validity

### Documentation Website

We use [DocFX](https://dotnet.github.io/docfx/) to generate our documentation website.

**Preview Documentation Locally:**
```bash
# Build the library (generates XML docs)
dotnet build TriasDev.Templify/TriasDev.Templify.csproj -c Release

# Install DocFX (one-time setup)
dotnet tool install -g docfx

# Generate API metadata
docfx metadata docfx_project/docfx.json

# Build the documentation site
docfx build docfx_project/docfx.json

# Serve locally (view at http://localhost:8080)
docfx serve _site
```

**Documentation Workflow:**
1. Make changes to markdown files in `docs/` or XML comments in code
2. Build and preview locally to verify changes
3. Submit PR - documentation is automatically deployed on merge to `main`
4. View live site at https://triasdev.github.io/templify/

**Adding New Documentation:**
- Place user-facing docs in `docs/` folder
- Add entries to `docfx_project/articles/toc.yml` for navigation
- Follow existing structure and formatting

### Documentation Style

- **Clear and concise** - Avoid jargon where possible
- **Use examples** - Show, don't just tell
- **Keep updated** - Docs should match current code
- **Use proper markdown** - Follow CommonMark specification
- **Add code samples** - Include runnable examples where applicable

---

## Changelog Maintenance

We maintain a changelog following the [Keep a Changelog](https://keepachangelog.com/) format in `CHANGELOG.md`.

### When to Update the Changelog

Update `CHANGELOG.md` for **every PR** that changes functionality:
- ✅ New features (even small ones)
- ✅ Bug fixes
- ✅ API changes
- ✅ Performance improvements
- ✅ Breaking changes
- ❌ Documentation-only changes (optional)
- ❌ Internal refactoring with no user impact (optional)

### How to Update the Changelog

**During Development:**
1. Add your changes to the `[Unreleased]` section
2. Use the appropriate category:
   - **Added** - New features
   - **Changed** - Changes in existing functionality
   - **Deprecated** - Soon-to-be removed features
   - **Removed** - Removed features
   - **Fixed** - Bug fixes
   - **Security** - Vulnerability fixes

**Example Entry:**
```markdown
## [Unreleased]

### Added
- Support for custom date formats in placeholders (#42)

### Fixed
- Placeholder replacement in nested table cells (#38)
```

**On Release:**

Maintainers will:
1. Move `[Unreleased]` items to a new version section
2. Add the release date
3. Update version links at the bottom
4. Create a git tag matching the version

**Example:**
```markdown
## [Unreleased]

## [1.1.0] - 2025-11-25

### Added
- Support for custom date formats in placeholders (#42)

[Unreleased]: https://github.com/TriasDev/templify/compare/v1.1.0...HEAD
[1.1.0]: https://github.com/TriasDev/templify/releases/tag/v1.1.0
```

### Changelog Best Practices

- **Write for users**, not developers - Focus on impact, not implementation
- **Use present tense** - "Add support for..." not "Added support for..."
- **Be specific** - "Fix date formatting in German locale" not "Fix bug"
- **Group related changes** - Combine similar items into one bullet
- **Link to issues** - Use issue numbers for context (#123)

---

## Additional Resources

### Helpful Documentation

- **[CLAUDE.md](CLAUDE.md)** - Comprehensive development guide
- **[ARCHITECTURE.md](TriasDev.Templify/ARCHITECTURE.md)** - Architecture and design patterns
- **[PERFORMANCE.md](TriasDev.Templify/PERFORMANCE.md)** - Performance considerations
- **[Examples.md](TriasDev.Templify/Examples.md)** - 1,900+ lines of examples

### Getting Help

- **GitHub Issues** - Ask questions, report bugs
- **GitHub Discussions** - Community discussions
- **Email** - Contact maintainers for sensitive issues

### Design Philosophy

Templify prioritizes:
1. **Simplicity** - Focus on common use cases
2. **Maintainability** - Small, composable classes with single responsibilities
3. **Testability** - Pure functions, dependency injection, 100% test coverage
4. **Explicit behavior** - No magic, predictable results
5. **Fail-fast** - Clear error messages, no silent failures

---

## Recognition

Contributors will be recognized in:
- **GitHub contributors list**
- **Release notes** - Major contributions highlighted
- **CHANGELOG.md** - Credit for significant features/fixes

---

## Questions?

If you have questions about contributing, feel free to:
- **Open a discussion** on GitHub Discussions
- **Ask in an issue** - We're happy to help!
- **Check [CLAUDE.md](CLAUDE.md)** - Detailed development guide

---

Thank you for contributing to Templify! Your efforts help make document templating easier for the entire .NET community. 🎉
