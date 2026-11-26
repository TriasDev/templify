# Builder: typed path source generator — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** `order.Number`, `line.Quantity` instead of `order.Path(o => o.Number)` through generated C# 14 extension properties.

**Architecture:** An incremental Roslyn generator (netstandard2.0) finds `TemplateComposer.DataAs<T>()` calls and `[assembly: TemplateData(typeof(X))]`, walks the reachable data types with the `DataCatalog` type rules, and emits extension blocks on `TemplatePath<X>` and `LoopItem<X>` that call the existing lambda API.

**Tech Stack:** C# 14, Microsoft.CodeAnalysis.CSharp 5.0.0 (generator), xUnit v3.

**Spec:** `docs/superpowers/specs/2026-11-26-builder-typed-path-generator-design.md`

## Global Constraints

- Same workflow as before: worktree per task (`feat/gen-<n>-<name>`), `$S/check.sh`, ff-merge into `feature/builder`, push; Conventional Commits `(builder)`, `#274`; shared files (solution, `Directory.Packages.props`, lock files) in a separate `build` commit.
- New dependency only for the generator and tests: `Microsoft.CodeAnalysis.CSharp` 5.0.0 (central version), `PrivateAssets=all`.
- Diagnostics: TBG001 warning (name collision), TBG002 error (LangVersion < 14). Release tracking files for RS2008.
- Generated file `TemplifyDataPaths.g.cs`, namespace `TriasDev.Templify.Builder`, `internal static class TemplifyDataPaths`, `#nullable disable`, deterministic order (types by fully qualified name, properties in declaration order, base properties after derived).

## Review Focus

1. A client data type in another assembly (metadata symbols, inherited properties) — generated like a source type.
2. Private/protected nested data types (common in tests) — skipped, the client still compiles.
3. A property named like a C# keyword (`@class`) or colliding with a receiver member — escaped / TBG001, never a compile error.
4. A strict client (nullable enabled, warnings as errors) — the generated code produces no warnings.
5. Recursive and mutually referencing types, and the same type reached from two roots — generated once.

---

### Task 1: Generator project, attribute, packaging, generator tests

**Files:** `src/TriasDev.Templify.Builder.Generators/` (csproj, `TypedPathGenerator.cs`, `DataTypeModel.cs`, `EquatableArray.cs`, `PublicAPI.*.txt`, `AnalyzerReleases.*.md`), `src/TriasDev.Templify.Builder/TemplateDataAttribute.cs`, builder csproj (pack the generator), `templify.slnx`, `Directory.Packages.props`; tests `tests/TriasDev.Templify.Builder.Tests/GeneratorTests.cs` (driver).

- [ ] Tests (RED): driver over a source compilation referencing the builder: generated text for value/object/collection/open properties on both receivers; inheritance; `[JsonIgnore]`; keyword property; TBG001 on `Line.Number` for `LoopItem<Line>` only; TBG002 with LangVersion 13; private nested type skipped and output compiles; `[assembly: TemplateData(typeof(X))]` root; two runs give identical text; no compile diagnostics in the output compilation (warnings as errors, nullable enabled).
- [ ] Implement; `check.sh`; commits `build(builder): source generator project` and `feat(builder): typed path source generator (#274)`.

### Task 2: End-to-end, sample, docs

- [ ] The builder test project and the sample run the generator (`OutputItemType="Analyzer"`). Tests (RED): generated properties equal lambda paths (`[JsonPropertyName]`, snake_case policy, loop items, nested collections).
- [ ] Rewrite `TypedOrderConfirmation`/`OrderAddressBlock` with generated properties; `SampleTests` byte equality stays; `dotnet pack` contains `analyzers/dotnet/cs/TriasDev.Templify.Builder.Generators.dll`; developer docs "Typed data paths" section; Desktop refresh; `check.sh`; commits.
