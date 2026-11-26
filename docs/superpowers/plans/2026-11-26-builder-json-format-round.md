# Builder: JSON format round (#293) — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Settle the JSON format before any customer definition is stored, following the style review (decisions A1–A9 and the "change now" list, accepted by the maintainer 2026-11-26).

**Architecture:** Reader/writer changes in `Json/`, small model and API additions (theme tokens on `Color`/`FontFace`, `SectionBlock`, named assets), renames in C# and JSON together. The byte-identical round trip stays the invariant of every test.

**Spec:** `docs/superpowers/specs/2026-11-26-builder-direction-review.md` (findings), the style review A1–A9 (summarised below), issue #293 and its comment (port findings F4, F5, F12).

## Decisions (style review, accepted)

- **A1 strict reader:** value formats checked against the known list (host can add names); `padding`/`cellPadding` take one or two values; `as` equal to the first segment of its own collection path is an error; a text key missing in one language of `texts` is an import error.
- **A2:** every conditional's branch is `then` (also in lists); `elseIf` branches are `{"if": …, "then": …}`.
- **A3:** `paragraphStyle` / `headerParagraphStyle` (table style, row), `defaults.paragraphStyle`, cell `verticalAlign`; C#: `ParagraphStyle(…)`, `HeaderParagraphStyle(…)`, `Defaults(d => d.ParagraphStyle(…))`, `RowComposer.ParagraphStyle(…)`.
- **A4:** a cell is a string or an object; no bare array (`{"content": [blocks]}`); a cell `style` option for the one-paragraph case (`{"style": "Label", "content": "…"}`).
- **A5:** theme tokens: root `theme` with `colors` and `fonts`; values `"$accent"` wherever a colour or font family goes (also inside borders, `"1pt $line"`); `TemplateJsonImportOptions.Theme` overrides values; C# `Color.Token(name, value)` / `FontFace.Token(name, family)` keep the reference through export.
- **A6:** root `assets` map (`"logo": "sha256:…"`); images reference the name; an image may give only `width` or only `height` (the other from the image's proportions).
- **A7:** `{"section": "name", "blocks": […]}` and `c.Section("name", b => …)` replace `group`/`from`; names unique in a file; unregistered components export as sections named after the component.
- **A8:** `texts` key-first: `"title": {"de": …, "en": …}`.
- **A9:** layout width counted without the indentation.
- **Small:** `\n`/`\t` in strings are the official line break and tab (the `lineBreak`/`tab` objects go); `==` is accepted as `=`; root keys `components`, `$schema` reserved (`$schema` ignored).
- **Port findings:** components reach the build's texts and theme (`BlockComposer.Texts`, `BlockComposer.Theme`) so they can be registered (F5); export option without indentation (F12); TB9204 names the first difference (F4 message; per-language values stay out of scope).
- **C# consistency:** the style parameter comes first everywhere (inline `Text(style, text)`, `Value(path, …)` keeps the path first and the style after the format — see Task 6); content-adding methods return their composer.

## Global Constraints

- Worktree per task (`feat/fmt-<n>-<name>`), `$S/check.sh`, ff-merge to `feature/builder`, push; commits reference #293.
- Every builder test keeps its JSON round trip; public API changes in `PublicAPI.Unshipped.txt` (renames are free: nothing is released).
- The sample, its exported JSON and the developer docs follow every change; the Desktop artefacts are refreshed at the end.

## Tasks

1. **Strict reader and small decisions** — A1, `==`, reserved root keys, TB9204 names the first difference. Tests: each silent mistake of the style review's probes becomes an error with a pointer.
2. **Names** — A2, A3 in JSON and C#.
3. **Structure** — A8 texts key-first, A7 sections (C# `Section`), A4 cells, `\n`/`\t` strings.
4. **Assets and images** — A6.
5. **Theme tokens and component context** — A5, F5.
6. **Layout and C# consistency** — A9, export without indentation, style-first parameters, chaining.
7. **Docs, sample, artefacts** — developer docs (JSON reference section rewritten), sample definitions and JSON, Desktop refresh, a short re-run of the Haiku edit trials on the new format.
