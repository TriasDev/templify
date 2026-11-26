# Builder phase 4, step 4: checking stored templates (design)

Status: approved by the maintainer, 2026-11-26 · #294 · epic #261

## Intent

The data a host gives its templates changes. A renamed or removed field silently prints nothing in a stored template
that still reads it: a customer's edited copy (DOCX/ODT) or a stored JSON definition. The host must be able to find
those templates and mark them. Templates the host builds from code are rebuilt and need nothing.

**Boundary:** the library checks, the host decides. Storage, when to re-check (for example on deploy), how to show
the result (yellow, red) and fixing (by hand or by an agent that reads the host's changelog of data changes) belong to
the host.

## Approaches

1. **Check only (chosen).** One check for any stored template against the current data catalogue, with a status and a
   list of problems. Renames are breaking changes; affected templates turn red. Simple, and works for any template,
   also for ones the builder did not produce.
2. **Declared renames** (`formerly` marks in the catalogue, contract diff in CI, automatic rewriting of JSON
   definitions). Rejected by the maintainer: every data change needs extra bookkeeping, and an agent with the host's
   changelog can fix templates better than fixed rules.
3. **Updating Word copies in place** (component boundaries in the document). Rejected: fragile, Word edits break the
   marks. Components are updated through JSON definitions, which name registered components and get the current
   version on every build.

## Design

- **Check:** `TemplateInspector.Check(Stream, TemplateCheckOptions)` for DOCX/ODT and
  `TemplateInspector.CheckDefinition(string json, …)` for JSON definitions. Both return a `TemplateCheckResult`:
  - status `Ok`, `Warning` (works, but outdated or of unknown origin) or `Broken` (will not fill correctly);
  - problems, each with a code, severity, message, and the data path and location when known.
- **Against the catalogue, not the contract:** a copy may read any field the data has (a customer may add one the
  standard template does not use). The check replaces `CheckUpload`, which compared with the current template's
  contract.
- **Broken:** a path the data does not have; a loop over something that is not a collection; template syntax errors;
  a JSON definition that does not import or build.
- **Warning:** no stamp (unknown origin), the stamp names another template, an older definition version than expected,
  a component built at an older version (or one no longer known).
- **Component versions:** `[ComponentVersion(n)]` on a component class (default 1); the stamp records the components a
  template was built from and their versions. A JSON definition is never outdated by a component: it is rebuilt with
  the current one.
- **Catalogue for agents and hosts:** `DataCatalog.ToJson()`. A host that commits this snapshot sees removed fields in
  its diff, without code in the library.
- **Policy (docs):** adding fields is safe; renaming or removing one is a breaking change: re-check all stored
  templates, the affected ones turn red.

## Not in scope

Contract diff, rename marks, rewriting templates, a definition hash in the stamp, reading a catalogue from JSON.

## Left to the builder

Codes (a new TB93xx range), the shape of the stamp property for components, whether `CheckDefinition` also builds,
how open catalogue shapes (dictionaries) are accepted in the check.
