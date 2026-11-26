# Builder phase 4 step 1 — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Indexed data paths (#290) and render-safe containers (#291).

**Architecture:** Paths gain an optional `[n]` per segment through `TemplatePath`, `PathExpression` and the contract/witness; the writers add a spacer paragraph where Templify's marker removal would leave a cell ending with a table or two tables touching.

**Tech Stack:** C# / .NET 11+10, OpenXML SDK, System.Xml.Linq, xUnit v3.

**Spec:** `docs/superpowers/specs/2026-11-26-builder-phase4-step1-design.md`

## Global Constraints

- Same workflow: worktree per task (`feat/p4-<n>-<name>`), `$S/check.sh`, ff-merge into `feature/builder`, push; commits reference the issue (`#290`, `#291`).
- Every builder test keeps its JSON round trip; public API changes declared in `PublicAPI.Unshipped.txt`.
- Index range 0..999; spacer style id `TemplateSpacer`, display name "Template Spacer".

## Review Focus

1. An index after a loop item (`line.parts[1].name`) and in a nested loop — canonical path and witness.
2. The same collection used by a loop and by an index — the witness satisfies both.
3. A cell that ends with a nested box whose own content ends with a conditional — both cells get spacers.
4. Headers/footers with tables followed by markers.
5. A typed index on a dictionary vs a list (`get_Item(string)` stays a key, `get_Item(int)` becomes an index).

---

### Task 1: Indexed path segments (#290)

**Files:** `TemplatePath.cs`, `Typed/PathExpression.cs`, `DataContract.cs`, `Loops.cs` (hidden-root check), `Typed/DataCatalog.cs` (`KindOf` normalisation); tests `IndexedPathTests.cs`.

- [ ] Tests (RED): `order["items[0].textValue"]` builds `{{order.items[0].textValue}}` and fills the first item's value (DOCX, ODT); `line["parts[1].name"]` inside a loop; typed `o => o.Items[0].Name` and array index; captured index and `[1000]`, `[x]`, `items[0][1]` are errors; condition `order.items[0].ok` in C# and JSON; JSON text `{{order.items[1].name}}`; contract entry `order.items[0].textValue`; witness validation of a template with a loop over `order.items` and `order.items[2].name` reports no missing variables; catalogue check accepts `order.items[0].name` for a typed catalogue.
- [ ] Implement; suite green; `check.sh`; commit `feat(builder): indexed path segments (#290)`.

### Task 2: Render-safe containers (#291)

**Files:** `Writers/DocxWriter.cs`, `Writers/OdtWriter.cs`, `Model/DocumentModel.cs` (`BlockFlattener` helpers), `Styles/BuilderStyles.cs` (spacer definition), `TemplateBuilder.cs` (inject the spacer style when used), `Styles/StyleSheet.cs` (comma check); tests `RenderSafetyTests.cs`.

- [ ] Tests (RED): cell ending with `If` → filled with the condition false, the cell's last child is a paragraph (DOCX and ODT); box ending with `ForEach` over an empty list → same; table, `If` (false), table → a paragraph between the two tables after filling; a cell ending with a paragraph gets no spacer; the spacer style appears only when used; comma in a style display name → TB2006.
- [ ] Implement; suite green; `check.sh`; commit `fix(builder): cells end with a paragraph, tables stay apart, style name check (#291)`.
