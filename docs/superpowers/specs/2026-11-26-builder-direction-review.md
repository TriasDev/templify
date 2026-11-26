# Builder: direction review (2026-11-26)

Status: findings of an independent review, accepted by the maintainer as the plan for phase 4 · epic #261

After phase 3 (JSON definitions, compact format, text keys, typed path generator) the maintainer asked independent
reviewers (separate agents, not the authors) whether the direction holds for the pilot product: a backend that today
ships about ten built-in templates in German and English, produced by a homegrown generator, and that wants runtime
generation, customer theming, customer-edited copies and, later, JSON definitions edited by LLM agents. This was not a
code review. The full reports contain the pilot's material and are kept outside the repository.

## Verdicts

| Question | Answer |
|---|---|
| Can the pilot's templates be described with the builder (C# and/or JSON)? | **Yes.** The largest template (778 placeholders, 67 tables, 54 loops, 203 conditionals) was described almost completely as one JSON definition; filled with the pilot's fixture it matches the original in pages and words (16 pages, 1,906 words), DOCX and ODT, both languages. |
| Is "C# definitions + a JSON form of the same model" the right architecture? | **Yes**; no alternative (keeping the generator, hand-maintained documents, a separate DSL, HTML→DOCX, a document model library) combines runtime generation, ODT, theme slots and per-customer definitions. Proven on few documents so far: port a real template before building the agent layer. |
| Can a cheap model (Haiku-class) edit JSON definitions? | **8 of 8 typical edits correct on the first try**, without a validator, each minimal: rename a heading, add a table column, change a row condition, move a section, add a conditional box, restyle headings; two of them on the large definition. |
| Is the JSON format a good basis for agents and people? | **Yes, keep it.** Before customer definitions are stored: close silent mistakes, a few cheap renames, theme tokens and named assets. |

## Gaps found

Builder (needed to port the built-in templates):
- indexed path segments (`items[0].textValue`) are rejected — 63 placeholders in 6 of 10 templates, some inside legal
  sentences where a loop is no substitute;
- a cell or box ending with a conditional, loop or nested table ends with a table once Templify removes the markers;
  LibreOffice then silently drops the outer table's borders and padding;
- two tables separated only by marker paragraphs merge into one after filling;
- a style display name with a comma gives schema-invalid DOCX without a diagnostic;
- table geometry for a risk matrix (rotated text, vertical merge, per-cell borders) — otherwise a `Raw` component.

JSON format (before any customer definition is stored):
- silent mistakes: unknown value formats (`{{x:N2}}`), four-value padding truncated, a loop variable shadowing the data
  root, incomplete `texts`, unknown data paths without a data catalogue;
- colours baked into styles and images addressed by hash: theme tokens and named assets are needed;
- naming: `textStyle` means a paragraph style, list `if` uses `items` for its branch, `texts` grouped by language rather
  than by key, cell `align`;
- no named sections or definition-local fragments (one block repeats up to 23 times in the large definition).

Lifecycle: a policy for evolving data contracts, re-checking stored customer copies and versioning components; a data
catalogue that does not need a CLR type.

Agent layer (later): a JSON cheat sheet generated from the reader, error hints for common slips, edit operations
(replace section, set text, set style) instead of JSON Patch, JSON Schema, and a benchmark of 20–30 reference edits.

## Plan (phase 4, in this order)

1. Builder blockers: indexed paths; closing paragraph in cells and boxes; separated adjacent tables; style name check.
2. Port one real template end to end (in the pilot product, not in this repository) and compare in Word and in PDF.
3. JSON format round: theme tokens, named assets, strict reader, renames, `texts` by key, named sections.
4. Evolution policy for data contracts and customer copies.
5. Agent layer: cheat sheet, diagnostics, edit operations, schema, benchmark.
