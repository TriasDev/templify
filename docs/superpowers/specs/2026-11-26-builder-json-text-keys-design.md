# Builder: text keys in JSON definitions (design)

Status: approved in conversation 2026-11-26 · epic #261, follows `2026-11-26-builder-compact-json-design.md`
Amends: phase 3 spec section 4.6 ("Texts and languages (deliberately minimal)").

## 1. Intent

One JSON definition serves every language. Today `template.Texts["key"]` returns the translated string at composition,
so the export writes German or English text and there is one JSON file per language (the evaluation's proposal 2).
The maintainer wants a single template whose texts are keys, translated when the template is built.

## 2. Decisions (maintainer, 2026-11-26)

1. **Hybrid storage.** A JSON definition may carry its translations in a `texts` section, so it is self-contained
   (an agent or a customer edits a sentence in the file); it may also carry only keys, translated from the host's
   catalog (`TemplateBuildOptions.Texts`, resx etc.). When both exist, the JSON's text wins and missing keys fall
   back to the catalog.
2. **Syntax `[[key]]`** inside any JSON string: data in `{{…}}`, texts in `[[…]]`. It works inside concatenations
   (`"[[orderNo]] {{order.number}}"`) and inside formats (`"{{order.date:date:[[dateFormat]]}}"`).
3. **A missing translation is a build error** (TB5001), as for C# definitions today.

## 3. Approach (builder's call)

The C# API does not change. Texts flow as plain strings through C# code (concatenated, used as date patterns, in raw
XML, in document properties), so the key cannot be carried by a type. Instead the export composes the definition with
**key markers**: in export mode `TemplateTexts[key]` returns `U+E000 key U+E001` instead of a translation. Wherever a
text ends up in the model, the marker is there; the JSON writer turns every marker into `[[key]]`. The exported JSON
therefore no longer depends on the language, and the `language` member is dropped from the format.

Import resolves `[[key]]` in every string of the definition (except the `texts` section) before the existing reader
sees it: from the JSON's `texts` for the build language (same culture fallback as `TextCatalog.FromDictionary`:
`de-DE` → `de` → invariant), else through `TemplateTexts` (catalog; a miss is TB5001). In export mode (re-exporting an
imported JSON) the reader resolves keys to markers again, so the round trip holds. A translated text must not contain
`{{` (like any literal text today); `[[` inside a translation is literal.

Rules:
- A key is `[A-Za-z0-9_.-]+`. `[[` that does not start a valid `[[key]]` is TB9200 at the string's pointer. A lone `]]`
  is literal.
- Export fails with TB9204 when a literal string of the definition contains `[[` (it would read as a key) or a key used
  through `Texts[…]` is not a valid key.
- `texts` section: `{"<culture name>": {"<key>": "<text>", …}, …}`; cultures as given (`"de"`, `"en-GB"`), keys in first-use
  order. Written for the languages in `TemplateJsonExportOptions.TextLanguages`, each text looked up in the build
  options' catalog; a key a language lacks is left out (the build reports it). Re-exporting an imported definition
  without `TextLanguages` keeps the languages of its own `texts` section.

## 4. API (additive)

```csharp
public sealed class TemplateJsonExportOptions
{
    public ComponentCatalog? Components { get; init; }
    public IReadOnlyList<CultureInfo>? TextLanguages { get; init; }   // null/empty: keys only
}
TemplateJson.Export(ITemplateDefinition definition, TemplateBuildOptions options, TemplateJsonExportOptions export)
```

The existing `Export(definition, options)` and `Export(definition, options, components)` stay (keys only, no `texts`).

## 5. Verification

- Every builder test keeps round-tripping (keys only; the build options' catalog translates).
- The sample exports **one** `order-confirmation.json` with `texts` for de and en; importing it **without** a catalog
  builds byte-identical DOCX/ODT to the C# definition for de-DE and en-GB.
- Tests: markers in text/format/raw/properties/alt text become `[[key]]`; import resolves from `texts`, from the
  catalog, JSON over catalog, culture fallback; TB5001 for a missing key; TB9200 for malformed `[[`; TB9204 for a literal
  `[[` in C# text; a translation containing `{{` is rejected; re-export keeps `texts`.
