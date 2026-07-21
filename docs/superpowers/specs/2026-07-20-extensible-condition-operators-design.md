# Design: Unified Extensible Condition Engine + New Operators

- **Issue:** [#128](https://github.com/TriasDev/templify/issues/128)
- **Date:** 2026-07-20
- **Status:** Approved (design direction), pending final spec review + implementation plan

## 1. Goal & Compatibility Principle

Unify the two existing condition/boolean engines into **one** extensible engine (**lexer → parser → AST → operator registry → evaluator**) and add:

- membership operator `in` (with negation via `not`),
- string operators `contains`, `startswith`, `endswith`,
- existence/emptiness operators `exists`, `is empty`, `is not empty`,
- **parentheses for grouping** (available uniformly).

**Hard compatibility requirement (external):** every already-authored template and every already-stored condition keeps producing the same result. The **internal implementation may be replaced entirely.** The existing test suites (conditionals, boolean-expressions, integration) are the compatibility oracle — they must pass unchanged.

The engine is consumed both by the template engine (`{{#if ...}}`, inline `{{(...)}}`, text templates) and by a standalone public API (`IConditionEvaluator`, e.g. in ViasPro).

## 2. Current State (why unification)

Two independent engines exist today with **different semantics**:

| Aspect | `ConditionalEvaluator` (`Conditionals/`) | `BooleanExpressionParser` + `BooleanExpression` (`Expressions/`) |
|---|---|---|
| Consumers | `{{#if}}` (`ConditionalVisitor`), text templates (`TextTemplateProcessor`), **public `IConditionEvaluator`/`ConditionContext`** | inline expression placeholder `{{(...)}}` (`PlaceholderVisitor`), rendered via boolean formatters (checkbox/yes-no) |
| Structure | flat left-to-right token loop, **no parentheses** | recursive-descent, **AST, parentheses supported** |
| `and`/`or` precedence | **equal**, left-associative | **standard `and > or`** |
| Truthiness | rich (`EvaluateValue`: strings/ints/collections/"true"/"1"…) | weak (bare variable true only if `bool true`) |
| Comparison | `double.Parse` for `>`/`<`; `ToString` equality (bool case-insensitive) | `IComparable.CompareTo`; `object.Equals` |
| Context | `IEvaluationContext` | `IDataContext` via `EvaluationContextAdapter` |
| Validation | heuristic `Validate()` with `ConditionValidationIssueType` | none (parse failure → treated as plain variable) |

These differences are **observable externally**, so they must be preserved. Unification therefore shares *structure* (parsing, AST, operator registry) while keeping *semantics* selectable per entry point (see Dialects).

All `Expressions/` types (`BooleanExpressionParser`, `BooleanExpression` and subclasses, `ComparisonOperator`, `IDataContext`, `EvaluationContextAdapter`) are `internal` and can be removed once their call site is migrated.

## 3. Unified Architecture

New namespace `Conditionals/Engine/` (final name TBD in plan):

- **`ConditionLexer`** — expression string → tokens: variable paths, string/number/bool/null literals, operator tokens, `(`, `)`, `,`. Quote normalization (curly → ASCII) lives here.
- **AST** (`ConditionNode`): `LiteralNode`, `VariableNode`, `ListNode`, `BinaryNode`, `UnaryNode`.
- **`ConditionParser`** — Pratt / precedence-climbing parser. Operator precedence, fixity and arity come from the **operator registry** (single, uniform precedence — see §5). Supports parenthesized grouping and list literals.
- **`IConditionOperator` + `ConditionOperatorRegistry`** — each operator = token(s), precedence, fixity (prefix/infix/postfix), `Evaluate`. **Adding an operator = registering one class; the parser is not touched.**
- **`ConditionEvaluatorCore`** — walks the AST, resolving variables via `IEvaluationContext`, applying the dialect's value policy.
- **`ConditionDialect`** — the per-entry-point value policy: truthiness rule and base comparison rule (precedence is uniform, not dialect-specific). Two instances (see §4).

Entry points become thin facades over the core:
- `ConditionalEvaluator` (internal facade) → **Default** dialect. Keeps public `ConditionEvaluator`/`IConditionEvaluator`/`ConditionContext` signatures unchanged; used by `ConditionalVisitor` and `TextTemplateProcessor`.
- inline `{{(...)}}` in `PlaceholderVisitor` → **Inline** dialect, replacing `BooleanExpressionParser`.

### Unit boundaries

| Unit | Responsibility | Depends on |
|---|---|---|
| `ConditionLexer` | string → tokens | — |
| `ConditionParser` | tokens → AST | registry |
| `ConditionOperatorRegistry` | token → operator metadata + eval | `IConditionOperator` impls |
| `IConditionOperator` (per op) | one operator's parse metadata + evaluation | dialect value policy |
| `ConditionEvaluatorCore` | AST → bool | `IEvaluationContext`, dialect |
| `ConditionDialect` | truthiness + base comparison policy | — |
| facades | orchestrate lex→parse→eval per entry point | all of the above |

## 4. Dialects (how both behaviors are preserved)

One shared engine, two dialects differing only in semantic policy:

**Precedence is now unified** to the standard `and > or` for all entry points (see §5). This was confirmed safe: no existing test in either suite (conditionals or boolean-expressions) asserts a mixed `and`/`or` precedence, and no known template relies on it. This removes precedence as a per-dialect concern.

Dialects therefore differ only in **truthiness** and **comparison**, which are externally observable and relied upon:

**Default dialect** — used by `{{#if}}`, text templates, standalone `IConditionEvaluator`.
- Truthiness: current `EvaluateValue` rules (rich: non-empty strings/collections, `"true"`/`"1"`, non-zero ints…).
- Comparison: current `double.Parse` / `ToString` rules.

**Inline dialect** — used by `{{(...)}}` placeholders.
- Truthiness: bare variable true only if `bool true` (current behavior).
- Comparison: `IComparable.CompareTo` / `object.Equals` (current behavior).

New operators (`in`, `contains`, `startswith`, `endswith`, `exists`, `is empty`, grouping) are registered **once** and available in **both** dialects. Their own comparison sub-semantics (e.g. `in` element equality, string ops case-sensitivity) are defined by the operator and are identical across dialects (see §6); dialects only govern truthiness and the base comparison rule above.

> Note: dialects preserve today's truthiness/comparison divergences rather than converge them (that would change externally observable output). Converging truthiness is a separate, later decision.

## 5. Precedence & Grouping

Single precedence table for all entry points (binds looser → tighter):

| Level | Operators | Fixity / associativity |
|---|---|---|
| 1 (loosest) | `or` | infix, left-assoc |
| 2 | `and` | infix, left-assoc |
| 3 | `not` | prefix |
| 4 | `=` `==` `!=` `>` `<` `>=` `<=` `in` `contains` `startswith` `endswith` | infix |
| 5 | `exists` `is empty` `is not empty` | postfix |
| 6 (tightest) | `( ... )` grouping, `( a, b, ... )` list literal | — |

`and` binds tighter than `or` (standard). Because comparison/membership (level 4) bind tighter than `not` (level 3), `not Status in Roles` parses as `not (Status in Roles)`, reading naturally. Parentheses provide explicit grouping: `(A or B) and C`.

## 6. Operator Catalog (shared across dialects)

### Membership
- **`in`** — `scalar in <source>`. RHS forms: (1) variable resolving to `ICollection`/array; (2) list literal `("Active", "Pending")`; (3) comma-separated string `"Active,Pending"`. Element comparison reuses the Default engine's `AreEqual` semantics (strings case-sensitive; booleans case-insensitive). `true` if the scalar equals any element.
- **Negation** — via `not`: `not Status in Roles`. `not in` sugar is out of scope.

### String operators (case-sensitive, ordinal — consistent with `=`)
- **`contains`** — `string contains substring` (operand direction opposite of `in`).
- **`startswith`** — `string startswith prefix`.
- **`endswith`** — `string endswith suffix`.

### Existence / emptiness (postfix)
- **`Foo exists`** — variable present in context (`TryResolveVariable == true`), regardless of value. Closes the current gap where a missing variable and a `false`/empty value are indistinguishable.
- **`Foo is empty`** — resolved value is `null`, empty/whitespace string, or empty collection; a **missing variable is also `is empty = true`**.
- **`Foo is not empty`** — negation of `is empty`.

For a present-but-null variable: `exists == true` and `is empty == true` (coherent and explicit).

### Grouping & list literals
- `( expr )` — grouping. `( a, b, c )` — list literal, valid as RHS of `in`. Parser distinguishes by commas: `(expr)` is grouping, `(expr, expr, …)` is a list.

## 7. Call-site Migration

1. `ConditionalVisitor` — no change (still calls `ConditionalEvaluator` facade / Default dialect).
2. `TextTemplateProcessor` — no change (Default dialect facade).
3. Public `ConditionEvaluator` / `ConditionContext` — signatures unchanged (Default dialect); new operators available automatically.
4. `PlaceholderVisitor` inline `{{(...)}}` — switch from `BooleanExpressionParser` to the unified engine with the **Inline** dialect. Expression detection (placeholder starts with `(`) stays.
5. Remove `Expressions/` engine types once (4) is migrated.

## 8. Validation

Parser-based validation replaces the heuristic, plus a **mapping layer** preserving existing `ConditionValidationIssueType` outcomes for existing cases. If an existing validation test yields a legitimately improved but different result, it is surfaced for a human decision rather than changed silently.

## 9. Public API Impact

- `IConditionEvaluator` / `ConditionEvaluator` / `IConditionContext` / `ConditionContext` — signatures unchanged; new operators available.
- `ConditionOperatorRegistry` / `ConditionDialect` — internal for now; the architecture allows exposing user-defined operators later (out of scope).
- Template author syntax: `{{#if}}` and `{{(...)}}` unchanged, gain the new operators + grouping (grouping already existed for `{{(...)}}`).

## 10. Testing Strategy

- **Characterization gate:** the entire existing test suite (conditionals + boolean-expressions + integration) passes without edits. This proves both dialects preserve external behavior.
- **Lexer:** tokenization, quote normalization, multi-word tokens (`is empty`, `is not empty`).
- **Parser:** precedence (`and > or`, uniform), parenthesized grouping, list literals, associativity.
- **Per operator:** `in` in all three RHS forms + negation; `contains`/`startswith`/`endswith`; `exists`/`is empty`/`is not empty` — tested in both dialects.
- **Edge cases:** `in` with non-collection RHS, empty collection, `null` LHS; string operators with non-string operand; `exists`/`is empty` on nested property paths.

## 11. Documentation (GitHub Pages, `docs/`)

Update in the same PR:
- `docs/for-template-authors/conditionals.md` — new operators, grouping, `exists`/`is empty`.
- `docs/for-template-authors/boolean-expressions.md` — new operators in inline `{{(...)}}`; note unified operator set.
- `docs/for-developers/condition-evaluation.md` — standalone API operator reference.
- `docs/for-template-authors/best-practices.md`, `docs/for-template-authors/template-syntax.md`, `docs/FAQ.md` — touch where operator lists / capabilities appear.

## 12. Out of Scope

- Converging the two dialects' truthiness into one behavior (would change external output). Precedence is already unified.
- Collection size comparison in conditions (`Items > 0`) — separate concern.
- Regex `matches`, range `between` — deferred (YAGNI).
- Public user-defined operator registration — architecture supports it, not implemented now.
