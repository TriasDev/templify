# Builder phase 4, step 1: indexed paths and render-safe containers (design)

Status: approved as part of the phase 4 plan (maintainer, 2026-11-26) · #290, #291 · epic #261
Source: `2026-11-26-builder-direction-review.md` (gaps found by porting a real-world template).

## #290 Indexed path segments

Templify reads `{{Items[0].Name}}` (also inside conditions: its lexer keeps `[`, `]` in words). The builder rejects
it. Calls made here:

- **Grammar:** a segment is `name` or `name[n]` with one constant index, `n` from 0 to 999 (the witness creates `n + 1`
  items, so the cap keeps it small). `order["items[0].textValue"]`, `line["parts[1].name"]`, root `TemplatePath.Root`.
- **Typed paths:** a constant `int` index on a list or array (`o => o.Items[0].Name`, `o.Array[2]`) maps to `items[0]`;
  a captured variable or computed index stays an error.
- **JSON:** works through the same paths, in text (`{{items[0].name}}`) and in conditions (`items[0].ok`).
- **Data contract:** the canonical path keeps the index (`order.items[0].textValue`), so the contract says exactly what
  the template reads. The witness makes a collection as long as its highest index + 1, every item with the merged shape
  of all its uses (loop and indexed), so `ValidateTemplate`/the upload check find every value.
- **Data catalogue and loop checks** treat `items[0]` like the item path `items[]` (kind check, "root path hidden by a
  loop variable" compares the bare name).

## #291 Render-safe containers

1. **Closing paragraph:** when the content of a table cell (and so of a box) does not end with an ordinary paragraph
   after flattening — it ends with a marker (`{{/if}}`, `{{/foreach}}`), a table or a raw block — the writer appends a
   spacer paragraph. Once Templify removes the markers, the cell still ends with a paragraph, and LibreOffice keeps the
   outer table's borders and padding.
2. **Tables apart:** a table followed by a marker paragraph gets a spacer paragraph after it, so it never touches a
   table that follows the markers after filling. (Directly adjacent tables keep today's separator.)
3. The **spacer** is a paragraph in the builder's style "Template Spacer" (1 pt, no spacing before/after, exact 1 pt
   line height), added to the style sheet only when a spacer is written — like "Template Marker". It is a writer
   detail: it is not in the model, so JSON definitions and the data contract are unchanged.
4. **Style names:** a display name containing a comma is a build error (Word reads `w:name` commas as alias lists; the
   DOCX is schema-invalid).

## Verification

- #290: string, typed and JSON forms give `items[0]`; the filled document shows the value at the index, also inside a
  loop item and in a condition; the contract keeps the index; the witness validates the template (no missing
  variables) when loops and indices share a collection; a captured index and an index above 999 are errors.
- #291: after filling with data that removes a conditional's content, the cell's last element is a paragraph (DOCX
  `w:tc`, ODT `table:table-cell`) and the two tables around a removed conditional are separated by a paragraph; the
  spacer style is only present when used; a comma in a style name is an error. (The lost border itself is only visible in
  LibreOffice's rendering; the structural check is what the tests pin.)
