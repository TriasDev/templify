# Builder: text keys in JSON definitions — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** One JSON definition for every language: texts are `[[key]]`, translated at build from the JSON's `texts` section or the host catalog.

**Architecture:** Export composes with key markers (`TemplateTexts` in export mode returns `key`); the writer turns markers into `[[key]]`. Import resolves `[[key]]` in every string before the existing reader runs (JSON texts first, then `TemplateTexts`; markers again in export mode).

**Tech Stack:** C# / .NET 11+10, System.Text.Json, xUnit v3.

**Spec:** `docs/superpowers/specs/2026-11-26-builder-json-text-keys-design.md`

## Global Constraints

- Same workflow as the compact-JSON plan: worktree per task (`feat/tk-<n>-<name>`, `../templify-tk-<n>`), `$S/check.sh`, ff-merge into `feature/builder`, push; Conventional Commits `(builder)`, `#274`; public API in `PublicAPI.Unshipped.txt`.
- C# API unchanged except the additive export options.
- Byte-identical round trip for every builder test; the `language` member is removed from the JSON format (no compatibility).
- Key syntax `[A-Za-z0-9_.-]+`; markers U+E000 / U+E001; new code TB9204 (export: text not representable).

## Review Focus

1. A text inside a format argument or raw XML (`date:[[dateFormat]]`, `Gedruckt am` in raw) round-trips.
2. A key whose translation is missing in the JSON `texts` but present in the catalog (and vice versa) — JSON wins, catalog fills gaps.
3. A literal `[[` written in C#, or a key with characters outside the key syntax — export error, never a silently different template.
4. A translation containing `{{` (would become a placeholder) — rejected at build with a pointer.
5. Re-export of an imported JSON with `texts` and without `TextLanguages` keeps the section unchanged.

---

### Task 1: Keys in the JSON (markers out, `[[key]]` in)

**Files:** `TextCatalog.cs` (`TemplateTexts` export mode + `Resolve`), `TemplateBuilder.cs` (internal compose with key markers), `Json/TemplateJson.cs`, `Json/ModelJsonWriter.cs` (drop `language`; markers → `[[key]]`; TB9204), `Json/JsonDefinition.cs` (drop `language`; resolve `[[key]]`), `Json/TextKeys.cs` (new: marker/key helpers), `BuildContext.cs` (TB9204); tests `JsonTextKeyTests.cs`; migrate JSON literals (remove `"language"`).

**Interfaces:**
- `internal static class TextKeys`: `string Marker(string key)`; `bool IsKey(string key)`; `string MarkersToKeys(string text)`; `string ResolveKeys(string text, Func<string, string> lookup)` (throws `FormatException` for malformed `[[`).
- `internal bool TemplateTexts.WritesKeys`; `internal string TemplateTexts.Resolve(string key, ITextCatalog? preferred)`.
- `TemplateBuilder.Compose(definition, options, keyMarkers: bool)`.

- [ ] Tests (RED): export of `x.Text(t["hello"] + " "); x.Value(p, ValueFormat.Date(t["fmt"]))` → `"[[hello]] {{p:date:[[fmt]]}}"`; properties title and image alt and raw XML carry `[[key]]`; import + build with a catalog gives the C# bytes (de and en); missing key → TB5001; `"[[bad key]]"`/`"[[x"` → TB9200 with pointer; `x.Text("a [[b")` → export TB9204; translation `"{{x}}"` → build TB9201 at the string.
- [ ] Implement; migrate tests; whole suite green; `check.sh`; commit `feat(builder): text keys in JSON definitions (#274)`.

### Task 2: Embedded `texts` section and export options

**Files:** `Json/TemplateJsonExportOptions.cs` (new, public), `Json/TemplateJson.cs` (overload), `Json/ModelJsonWriter.cs` (`texts`), `Json/JsonDefinition.cs` (read `texts`, lookup order), tests in `JsonTextKeyTests.cs`.

**Interfaces:** `TemplateJsonExportOptions { Components, TextLanguages }`; `TemplateJson.Export(definition, options, TemplateJsonExportOptions export)`; `JsonDefinition.Texts` (internal: languages and texts, for re-export).

- [ ] Tests (RED): export with `TextLanguages = [de, en]` writes `texts` in first-use key order; import **without** catalog builds the C# bytes for de-DE and en-GB; JSON text wins over catalog; catalog fills a key the JSON lacks; `de-DE` build reads `"de"`; re-export without `TextLanguages` keeps `texts`; malformed `texts` → TB9200 pointers.
- [ ] Implement; declare API; suite green; `check.sh`; commit `feat(builder): translations embedded in JSON definitions (#274)`.

### Task 3: Sample, docs, evaluation

- [ ] Sample `phase3` mode writes one `order-confirmation.json` (texts de + en), imports it without a catalog and checks same bytes for both languages; `SampleTests` updated; developer docs JSON section (`[[key]]`, `texts`, lookup order); phase 3 spec section 9 numbers; refresh `~/Desktop/templify-builder-phase3/`.
- [ ] `check.sh`; commits `feat(builder): sample exports one JSON for all languages (#274)` and `docs(builder): text keys (#274)`.
