# LibreOffice / OpenDocument Templates

Templify processes templates written in **LibreOffice Writer**, **Collabora Online** or **Apache OpenOffice**, saved
as OpenDocument Text (`.odt`) or OpenDocument Text Template (`.ott`). The template syntax is the same as for Word
templates: everything in the [Template Syntax](template-syntax.md), [Placeholders](placeholders.md),
[Conditionals](conditionals.md), [Loops](loops.md) and [Format Specifiers](format-specifiers.md) guides works in
LibreOffice too.

The output is always an OpenDocument Text document (`.odt`), also when the template is an `.ott` file.

## Authoring a Template in LibreOffice Writer

1. Create a new document in LibreOffice Writer and type the text of your document.
2. Type placeholders, conditionals and loops directly in the text, exactly as in Word:

    ```
    Hello {{Customer.Name}}!

    {{#if IsVip}}
    Thank you for being a VIP customer.
    {{/if}}

    {{#foreach Items}}
    {{Name}}: {{Price:currency}}
    {{/foreach}}
    ```

3. Format the text as you like. A placeholder takes the formatting of its first character, so a bold, red
   `{{Name}}` produces a bold, red name.
4. Save with **File → Save As** and choose **ODF Text Document (.odt)**, or **File → Templates → Save as Template**
   for an `.ott` template.

!!! tip "Tips for LibreOffice"
    - AutoCorrect may turn straight quotes into typographic quotes (`"Active"` → `“Active”`). Templify accepts both
      in conditions, so you do not need to turn AutoCorrect off.
    - Do not save as **Flat XML ODF Text Document (.fodt)**: that format is not supported.
    - If you save the template as a Word document (`.docx`) from LibreOffice, it is processed as a Word template and
      the output is a `.docx` file.

## What Is Supported

Everything that works in Word templates works in OpenDocument templates:

| Feature | Notes |
|---|---|
| Placeholders, nested paths, indexes, format specifiers, expressions | In paragraphs, headings, tables, lists, sections, text boxes and frames, footnotes and endnotes, headers and footers |
| Placeholders with mixed formatting | A placeholder is found even when its characters are formatted differently, for example a bold `Name` in `{{Name}}` |
| Conditionals (`{{#if}}`, `{{#elseif}}`, `{{#else}}`) | As blocks (markers in their own paragraphs), inline within one paragraph, over table rows and over list items |
| Loops (`{{#foreach}}`) | Over paragraphs and other blocks, table rows and list items, nested to any depth, with `@index`, `@number`, `@first`, `@last` and `@count` |
| Markdown in values | `**bold**`, `*italic*`, `~~strikethrough~~`, combined with the template formatting |
| Line breaks in values | A newline in a value becomes a line break |
| Headers and footers | Including first-page and left-page headers and footers, and their left, center and right regions |
| Document properties | Title, subject, author and other properties set by the application are written to the document metadata |

### Lists

In LibreOffice, bulleted and numbered lists are separate list items rather than numbered paragraphs:

- Put loop and conditional markers in **their own list items**. Markers in a list work like markers in table
  rows: the marker items are removed, and the items between them are repeated, kept or removed.
- A single bullet between normal paragraphs is a list of its own, so a marker there works as if it were a normal
  paragraph.
- A marker inside a longer list must have its matching end marker in the **same list**. Otherwise processing
  fails with a "has no matching `{{/if}}`" (or `{{/foreach}}`) error.
- **Numbering continues across loop iterations.** When a loop repeats a numbered list (for example a single
  numbered item between `{{#foreach}}` and `{{/foreach}}` paragraphs, or a numbered item in a repeated table row),
  the copies are numbered on (1., 2., 3.), as in Word. Each copy continues the first copy of the same list, even
  when other paragraphs or lists come between them. Other lists are not changed: a numbered list after the loop
  still starts at 1 unless you set it to continue in LibreOffice. In nested loops, the numbering also continues
  across the repetitions of the outer loop, as in Word.

## Differences from Word Templates

A few things behave differently from Word templates, mostly because of how the OpenDocument format works:

- **Spaces collapse.** OpenDocument shows several ordinary spaces in a row as one space, like a web page. Templify
  writes replacement values so that they appear exactly as given. But when a missing variable is removed (with the
  "replace with empty" setting) or an inline conditional is removed, two spaces can end up next to each other, and
  LibreOffice then shows only one. Word would show two.
- **Merged cells across rows.** When a loop or conditional adds or removes table rows, cells merged vertically
  across those rows are not adjusted. Do not merge cells across loop or conditional rows.
- **Bookmarks are not renamed.** Bookmarks copied by a loop keep their names. LibreOffice renames duplicates when it
  opens the document, so links to a copied bookmark point to the first copy. Frame, table and section names are
  made unique.
- **"Update fields on open" does not apply.** LibreOffice updates page numbers and dates itself when it opens a
  document. A table of contents is not updated automatically. Use **Tools → Update → Update All** in LibreOffice.
- **Comments are not processed.** Placeholders inside comments (annotations) stay as they are, as in Word templates.
- **Digital signatures are removed.** A signed template produces an unsigned document, because the content has
  changed.
- **The preview thumbnail is removed.** The thumbnail in a template shows the template itself, with its `{{...}}`
  markers. The output has no thumbnail, so file managers show a generic icon until LibreOffice saves the document
  again.
- **Document statistics are not updated.** The page, word and character counts in the document properties
  (`meta:document-statistic`) are those of the template. LibreOffice recounts them when it saves the document.
- **Damaged or unusual packages are rejected.** A package that contains an entry name twice, or an entry path that is
  absolute or contains `..`, fails with a clear message. LibreOffice does not write such packages.
- **Password-protected documents are not supported.** Processing fails with a clear message.
- **Flat OpenDocument (`.fodt`) is not supported.** Save the template as `.odt` or `.ott`.

## Trying It Out

The Templify GUI application accepts `.odt` and `.ott` templates as well as `.docx` files. Select the template and
a JSON data file, and the output is saved as `<template>-output.odt`. See
[Getting Started](getting-started.md) for the GUI and the JSON data format.

## Next Steps

- [Template Syntax](template-syntax.md): the complete syntax reference
- [Examples Gallery](examples-gallery.md): example templates and their output
- [OpenDocument for developers](../for-developers/opendocument.md): processing `.odt` templates in code
