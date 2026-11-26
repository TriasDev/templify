# Builder: checking stored templates (#294) — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** One check that tells a host whether a stored template (DOCX/ODT copy or JSON definition) still works with the current data, with a status and located problems.

**Architecture:** A catalogue witness (data in which every catalogue path exists) drives the engine's validation and a trial fill for document copies; JSON definitions are imported with the catalogue and built. The stamp gains component versions; a result type collects problems into `Ok`/`Warning`/`Broken`.

**Tech Stack:** .NET 10/11, xUnit v3, TriasDev.Templify core (`TemplateProcessor.ValidateTemplate`, `ProcessTemplate`).

**Spec:** `docs/superpowers/specs/2026-11-26-builder-evolution-policy-design.md`

## Global Constraints

- Worktree `templify-evo-294`, branch `feat/evo-294-design`, `$S/check.sh`, ff-merge to `feature/builder`, push; commits reference #294.
- Nothing is released: `CheckUpload`, `UploadCheckResult` and `TemplateOrigin` are replaced, not obsoleted. Public API in `PublicAPI.Unshipped.txt`.
- Public repository: no customer or product names in code, docs, commits or the issue.
- Codes: TB9300 template syntax, TB9301 unknown data path, TB9302 wrong kind (loop over a non-collection), TB9303 no stamp, TB9304 other template, TB9305 older definition version, TB9306 older or unknown component. JSON definitions keep their own codes (TB92xx, build codes).

## Review Focus

1. A copy reading a field the catalogue has but the standard template does not use → `Ok` (the reason for checking against the catalogue).
2. Paths under an open catalogue shape (a dictionary property) → accepted, also in loops and conditions.
3. Loop variables: `{{line.description}}` inside `{{#foreach line in order.lines}}` resolves through the loop → no false unknown path.
4. A damaged or non-template file → `Broken` with TB9300, never an exception.
5. A stamp written before this change (no component property) still reads; components are then simply not compared.

---

### Task 1: Catalogue witness and JSON

**Files:** Modify `src/TriasDev.Templify.Builder/Typed/DataCatalog.cs`; Test `tests/TriasDev.Templify.Builder.Tests/DataCatalogEvolutionTests.cs` (folder as the existing catalogue tests).

**Produces:** `public string DataCatalog.ToJson()` — `{"version":1,"paths":[{"path":…,"kind":"value|object|collection|open","type":"<C# type name>"}]}`, indented, in `Entries` order; `internal IReadOnlyDictionary<string, object?> DataCatalog.CreateWitness()` — values `"x"`, collections with one item, open shapes an accept-everything node (an `IReadOnlyDictionary<string, object?>` whose `TryGetValue` always succeeds with another such node, and which enumerates as one item).

- [ ] Tests: `ToJson_ListsEveryPath_WithKindAndType`; `Witness_ValidatesTemplateReadingAnyCataloguePath` (a docx built with the core test helpers that reads `order.customer.name`, loops `order.lines` with `line.description`, has a condition on `order.isExpress` → `ValidateTemplate(witness)` has no missing variables); `Witness_AcceptsAnyPathBelowAnOpenShape` (`order.extra.anything.deep` with `extra` a `Dictionary<string, object>`); watch them fail, implement, pass.
- [ ] Commit `feat(builder): data catalogue as JSON and witness (#294)`.

### Task 2: Component versions in the stamp

**Files:** Create `src/TriasDev.Templify.Builder/Components/ComponentVersionAttribute.cs`; modify `TemplateStamp.cs`, `TemplateBuilder.cs` (collect `ComponentBlock`s of the model, skipping the builder's own `SectionComponent`), `Json/ModelJsonWriter.cs` only if it reads the stamp; tests `ComponentVersionTests.cs`.

**Produces:** `[AttributeUsage(Class)] public sealed class ComponentVersionAttribute(int version)`, `int Version` (≥ 1, else `ArgumentOutOfRangeException`); `TemplateStamp.Components : IReadOnlyDictionary<string, int>` (name = type name without generic suffix, as `ComponentSectionName`); document property `Templify.Components` = `Name=version` pairs joined by `;`, sorted ordinal, omitted when empty; `FromProperties` reads it when present (absent → empty), a malformed value → empty. `internal static string ComponentName(Type)` and `internal static int ComponentVersionOf(Type)` shared with Task 3.

- [ ] Tests: `Stamp_RecordsComponentsAndVersions` (`[ComponentVersion(2)] Terms` + unversioned `Card` → `Card=1;Terms=2`, read back by `TemplateInspector.Inspect`, DOCX and ODT); `Stamp_WithoutComponentProperty_StillReads`; `Stamp_SectionsAreNotComponents`; byte-identical JSON round trip unchanged (the existing invariant tests).
- [ ] Commit `feat(builder): component versions in the template stamp (#294)`.

### Task 3: `TemplateInspector.Check` for document copies

**Files:** Create `src/TriasDev.Templify.Builder/TemplateCheck.cs` (result types); modify `TemplateInspector.cs` (replace `CheckUpload`); update the tests of `CheckUpload` to the new API; tests `TemplateCheckTests.cs`.

**Produces:**
- `public enum TemplateCheckStatus { Ok, Warning, Broken }`, `public enum TemplateCheckSeverity { Warning, Error }`.
- `public sealed record TemplateCheckProblem(string Code, TemplateCheckSeverity Severity, string Message) { string? DataPath; string? Location; }` (init).
- `public sealed class TemplateCheckResult { TemplateCheckStatus Status; IReadOnlyList<TemplateCheckProblem> Problems; TemplateFormat Format; TemplateStamp? Stamp; }` — status `Broken` if any error, `Warning` if any warning, else `Ok`.
- `public sealed record TemplateCheckOptions { required DataCatalog Data; TemplateIdentity? Expected; ComponentCatalog? Components; PlaceholderReplacementOptions? Fill }`.
- `public static TemplateCheckResult TemplateInspector.Check(Stream template, TemplateCheckOptions options)`.

Approach: syntax errors of `ValidateTemplate(stream)` → TB9300 (location from the engine); missing variables of `ValidateTemplate(stream, witness)` (and missing condition variables, as `CheckUpload` reads them) → TB9301 with `DataPath`; then a trial `ProcessTemplate` with the witness: a failure → TB9302 with the engine's message (a loop over a value). Stamp: none → TB9303; `Expected` given and another id → TB9304; same id and lower version → TB9305; with `Components`, each stamped component whose registered type has a higher `ComponentVersion` or that no registered type has → TB9306 (registered types matched by `ComponentName`).

- [ ] Tests: `Check_CopyReadingAnotherCatalogueField_IsOk`; `Check_RemovedField_IsBroken_WithPath` (catalogue of a type without the field); `Check_LoopOverValue_IsBroken`; `Check_SyntaxError_IsBroken_WithLocation`; `Check_NotATemplate_IsBroken` (random bytes); `Check_NoStamp_IsWarning`; `Check_OtherTemplate_IsWarning`; `Check_OlderVersion_IsWarning`; `Check_OlderComponent_IsWarning`; `Check_LoopVariables_ResolveThroughTheLoop`; `Check_OpenShape_AcceptsAnyPath`. ODT for the Ok and Broken cases.
- [ ] Commit `feat(builder): check stored template copies against the data catalogue (#294)`.

### Task 4: `TemplateInspector.CheckDefinition` for JSON definitions

**Files:** modify `TemplateInspector.cs` (or `TemplateCheck.cs`); tests in `TemplateCheckTests.cs`.

**Produces:** `public static TemplateCheckResult TemplateInspector.CheckDefinition(string json, TemplateJsonImportOptions import, TemplateBuildOptions? build = null, TemplateIdentity? expected = null)`. `import.Data` is required (`ArgumentException` without it). Import errors → `Error` problems with their own code and the JSON pointer as `Location`, `DataPath` for TB9203 when known; with `build`, the definition is built and build errors/warnings become problems; `expected` → TB9304/TB9305 from the identity. `Stamp` is null, `Format` `Unknown`.

- [ ] Tests: `CheckDefinition_Current_IsOk` (sample definition exported with `TemplateJson.Export`); `CheckDefinition_RemovedField_IsBroken_AtPointer`; `CheckDefinition_NotJson_IsBroken`; `CheckDefinition_BuildError_IsBroken` (a rule only the build checks); `CheckDefinition_OlderVersion_IsWarning`.
- [ ] Commit `feat(builder): check stored JSON definitions (#294)`.

### Task 5: Docs, sample, issue

- [ ] `docs/for-developers/template-builder.md`: "Stamp and upload check" becomes "Checking stored templates" (both calls, statuses, codes table, `ComponentVersion`, `DataCatalog.ToJson`) and a short "Changing the data" policy: adding is safe; renaming or removing is breaking, re-check stored templates; the host keeps the changelog and decides.
- [ ] Sample: `[ComponentVersion(1)]` on `Terms`; `SampleRunner` runs `Check` on the built DOCX and prints the status.
- [ ] Desktop artefacts refreshed; #294 body updated to the reduced scope (sanitised); commit `docs(builder): checking stored templates (#294)`.
- [ ] Final review (fresh reviewer, most capable model), one fix pass, ff-merge, push, tick #294 in #261.
