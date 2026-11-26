# Builder: source generator for typed data paths (design)

Status: approved in conversation 2026-11-26 · epic #261 · approach B of phase 3 spec section 3.3, on the foundation of
approach A (`TemplatePath<T>`, `LoopItem<T>`, `TemplateCollection<T>`).

## 1. Intent

Typed paths today need a lambda per value: `order.Path(o => o.Number)`, `line.Path(l => l.Quantity)`. The maintainer
wants to see the cleanest form, `order.Number`, `line.Quantity`, `address.Country.NotEq("DE")`, for the classes a
customer of the builder writes for their data. Targets are .NET 10 and 11 only; older language versions are not
supported.

## 2. Decisions

- **Maintainer:** approach 1 — a Roslyn source generator writes **C# 14 extension properties** on the existing types
  `TemplatePath<T>` and `LoopItem<T>` (not a second, generated path API). A data property whose name an existing member
  already uses (`Number`, `Count`, `Index` on a loop item; `Path`, `Items`, `Exists`, `IsEmpty` on a path) is not
  generated; the lambda remains for that case. That is acceptable.
- **Builder's calls (stated in the conversation, not objected to):**
  - Generation is automatic for every type used in `template.DataAs<T>()` in the compilation and every type reachable
    from it; `[assembly: TemplateData(typeof(X))]` adds roots that are not used through `DataAs` (component libraries).
    The customer's classes need no attribute, no `partial`, no base class, and may live in other assemblies.
  - Each generated property calls the existing lambda API (`path.Path(x => x.Number)`), so names follow exactly the same
    rules (`[JsonPropertyName]`, the naming policy given to `DataAs`) and templates are byte-identical to the lambda form.
  - The generator ships inside the `TriasDev.Templify.Builder` package (`analyzers/dotnet/cs`); nothing to install.

## 3. What is generated

One file, `TemplifyDataPaths.g.cs`, namespace `TriasDev.Templify.Builder` (already imported by definitions), one
`internal static class TemplifyDataPaths` with, per reachable object type `X`, an `extension(TemplatePath<X> path)` and
an `extension(LoopItem<X> item)` block:

| Data property of type | Generated member |
|---|---|
| value (string, numbers, bool, enums, dates, Guid, …; nullable forms) or open (object, dictionaries, `JsonElement`) | `TemplatePath<TProp> Name => path.Path(x => x.Name);` |
| object | `TemplatePath<TProp> Name => …` — and `TProp` gets its own extension blocks |
| collection (array or `IEnumerable<T>`, not string) | `TemplateCollection<TItem> Name => path.Items<TItem>(x => x.Name);` — and an object `TItem` gets extension blocks |

The type rules mirror `DataCatalog` (value / open / collection / object). Properties: public, instance, readable, not
indexers, not `[JsonIgnore]` (always), including inherited ones (most derived wins). A property whose name collides with
a member of the receiver type (read from the compilation, including `object` members) is skipped with warning
**TBG001** naming the lambda to use. Types the generated code cannot name (private or protected, at any nesting level)
are skipped silently; they can still use lambdas. Without C# 14 (LangVersion below 14) the generator reports error
**TBG002** and generates nothing. Generated code is `#nullable disable` (no nullability mismatches in strict projects).

Added after the final review: TBG001 is information, not a warning (collisions are accepted, and a file-located
generator diagnostic cannot be suppressed locally); types in `System.*` are not walked (the lambda API rejects their
members); `file` types, ref structs, ref-returning properties and names several base interfaces declare are skipped;
`[Obsolete]` is copied onto the generated property; the generated file marks its blocks with
`[assembly: GeneratedDataPaths(typeof(T), loopItem)]`, and a compilation that a referenced assembly gives access to its
internals does not generate those blocks again.

## 4. Verification

- Generator tests (`CSharpGeneratorDriver`): generated text per type rule, inheritance, `[JsonIgnore]`, keywords,
  collisions (TBG001), LangVersion (TBG002), inaccessible types skipped, assembly attribute roots, deterministic output,
  generated code compiles without warnings.
- End-to-end: the builder test project runs the generator; generated properties give the same paths as the lambdas,
  also with `[JsonPropertyName]` and `DataAs<T>(JsonNamingPolicy.SnakeCaseLower)`.
- The sample's `TypedOrderConfirmation` and `OrderAddressBlock` use the generated properties and still build the same
  bytes as `OrderConfirmation`; the packed builder package contains the generator under `analyzers/dotnet/cs`.
