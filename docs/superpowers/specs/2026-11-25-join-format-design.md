# `:join` format for collections — design

Date: 2026-11-25. Related: #258 (inline loops), #253 (block marker text loss).

## Intent

Template authors need a short list inside a sentence or label, e.g. "Rating for IT asset: A, B", where today they
must write a block loop with one paragraph per item. Inline loops (#258) would allow it but are expensive. A consumer
check (a downstream document generator, 20 built-in templates) found exactly one real case: a label followed by the
names of an item's sub-collection, inside a block loop, with "no separator after the last item". Joining in the data
is not wanted: separator wording would move from the (multilingual) template into code.

Today `{{Tags}}` with a list renders the CLR type name (`System.Collections.Generic.List`1[...]`) or raw JSON.

## Goals

- `{{Collection:join}}` renders the items separated by `, `, no separator after the last item.
- A property of each item: `{{Assets:join:Name}}`.
- A custom separator, written in the template: `{{Tags:join:" / "}}`, `{{Assets:join:Name:"; "}}`.
- Same behavior in Word, OpenDocument and text templates (one implementation in `ValueConverter`).

## Non-goals

- Per-item formatting differences (bold name, normal comma): the whole joined text takes the formatting of the
  placeholder's first run, as every placeholder does. Inline loops (#258) stay open for that.
- Per-item format specifiers (`number:N2` for each item), conditionals per item, "and" before the last item.
- Inline loops. A start marker in a text paragraph with the end marker in a later paragraph stays a block loop and is
  reported by #253; nothing here changes loop detection.

## Approaches

1. **`:join` format specifier** (picked). Small: one place (`ValueConverter`), all formats get it. Trade-off: one
   formatting for the whole list (a bold name list gets bold commas).
2. **Inline loops with run cloning** (#258): full fidelity (mixed formatting, `{{#if not @last}}` separators,
   several fields per item), but a new "repeat part of a paragraph" mechanism in both rewriters plus a processing
   order change. Kept open in #258 until a real need appears.
3. **Inline loops rendered as text**: same single-formatting limit as `:join` at the cost of a loop. Dropped.

## Syntax and behavior (decided)

```
{{Tags:join}}                      a, b, c
{{Assets:join:Name}}               A, B           (Name of each item)
{{Tags:join:" / "}}                a / b / c
{{Codes:join:""}}                  ABC            (no separator)
{{Assets:join:Name:"; "}}          A; B
{{aiCls.itAssets:join:Name}}       inside a block loop: the loop item's sub-collection
```

- Default separator `, ` (comma + space), not culture-dependent; `""` joins without any separator. Decided by the
  maintainer: lists in documents are almost always comma-separated, so the short form covers the common case, and the
  rare "no separator" case is explicit and readable. Defaults differ across engines (JS `,`, Liquid space, Jinja2
  empty, .NET required), so the docs state the default in the first line and show `""` right next to it.
- Arguments after `join`: an optional **property path** (unquoted, the same path syntax as placeholders: `Name`,
  `Owner.Name`, `Tags[0]`), then an optional **separator in double quotes**. Quotes make a separator that looks like
  a word (`" und "`) unambiguous. Typographic quotes (`“ ”`, `„ “`, Word's autocorrect) are accepted like straight
  ones, as in conditions; `\"` and `\\` escape inside the separator.
- The value must be a collection: any `IEnumerable` except `string` and dictionaries, or a `JsonElement` array.
  For any other value `:join` is ignored and the value renders as today (no error).
- Each item (or the item's property) is converted like a placeholder value without format: `ValueConverter` with the
  processor's culture (numbers and dates culture-aware). `JsonElement`, dictionaries, `ExpandoObject` and POCOs are
  resolved through `PropertyPathResolver`, as everywhere else.
- `null` items and items whose (property) value is `null` or an empty string are skipped, so no dangling separators.
- An empty or `null` collection renders as an empty string. A missing collection is a missing variable as today
  (`MissingVariableBehavior`).
- Markdown and newline handling apply **to each item on its own**, as to any placeholder value; the separator is
  literal template text (decided by the maintainer after review: parsing the joined string paired markers across items,
  `["file_a", "file_b"]` rendered `filea, fileb` with `a, file` in italics, and ate `*`/`_` separators). Word and ODT
  build the replacement from per-item pieces plus literal separator pieces. `:raw` cannot be combined.
  In Word and ODT, `TextReplacements` and XML sanitizing apply to the items (data), not to the separator (template
  text); text templates (no markdown) apply them to the joined string.

## Left to the builder

- Where the argument parsing lives (a small `JoinFormat` parser next to `ValueConverter` is suggested) and its tests.
- Docs: format specifier tables (`template-syntax.md`, placeholders docs, README API/format lists, CLAUDE.md
  placeholder syntax), one example in the loops docs ("short lists inside a sentence: use `:join`").
- Tests for Word, ODT and `TextTemplateProcessor`, culture (de-DE numbers), JSON input, nested in a block loop.

## Calls made without the maintainer (check these)

- Separator must be quoted; the unquoted argument is always the property path.
- Skipping `null`/empty items rather than rendering empty slots.
- A property that is missing on an item counts as `null` (skipped); **if the collection is non-empty and the property
  resolves on no item**, one `MissingVariable` warning for `Collection.Property` is added (catches typos such as
  `:join:Nmae`). Processing only: `ValidateTemplate` keeps checking that the collection itself exists, but does not
  check the item property — both validators see placeholder names without formats, and a `:join` inside a loop would
  need the loop items' sub-collections. Decided by the maintainer: moved to the #255 lint.
- Public API impact: none (format strings are not API). Behavior change: `{{List:join}}` previously printed the type
  name; release-note as a new feature.
- Tracking: a new feature issue for `:join` referencing #258; #258 stays open for real inline loops.
