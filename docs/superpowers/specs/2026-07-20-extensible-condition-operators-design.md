# Design: Extensible Condition Evaluator + New Operators

- **Issue:** [#128](https://github.com/TriasDev/templify/issues/128)
- **Date:** 2026-07-20
- **Status:** Approved (design), pending implementation plan

## 1. Goal & Compatibility Principle

Rearchitect the core of `ConditionalEvaluator` into a **lexer → parser → AST → evaluation** pipeline backed by an **operator registry**, and add:

- membership operator `in` (with negation via `not`),
- string operators `contains`, `startswith`, `endswith`,
- existence/emptiness operators `exists`, `is empty`, `is not empty`,
- **parentheses for grouping**.

The condition evaluator is used both by the template engine (`{{#if ...}}`) and as a standalone public API (`IConditionEvaluator`, e.g. in ViasPro).

**Hard compatibility requirement:** all existing unit and integration tests pass **unchanged**. This is the compatibility oracle. No existing operator or expression changes behavior. The engine internals may be rewritten freely; what must not change is the observable behavior of already-stored conditions/templates and the current test suite.

## 2. Architecture

New namespace `Conditionals/Expressions/`:

- **`ConditionLexer`** — turns the expression string into tokens: variable paths, string literals, numeric literals, operator tokens, `(`, `)`, `,`. Quote normalization (curly/typographic → ASCII) moves here from the current `NormalizeQuotes`.
- **AST** (`ConditionNode` hierarchy): `LiteralNode`, `VariableNode`, `ListNode`, `BinaryNode`, `UnaryNode`.
- **`ConditionParser`** — Pratt / precedence-climbing parser. Precedence, arity and fixity come **from the operator registry**. Supports parenthesized grouping and list literals.
- **`IConditionOperator` + `ConditionOperatorRegistry`** — each operator = token(s), precedence, fixity (prefix/infix/postfix), and an `Evaluate` function. **Adding an operator = registering one class; the parser is not touched.**
- **`ConditionExpressionEvaluator`** — walks the AST, resolving variables via the existing `IEvaluationContext`.

`ConditionalEvaluator` (internal) becomes a thin facade (`lex → parse → evaluate`). Public `ConditionEvaluator` / `IConditionEvaluator` / `ConditionContext` keep their signatures unchanged; new operators become available automatically. The template engine (`ConditionalDetector` / `ConditionalVisitor`) is unchanged — it just passes an expression string to the evaluator.

### Unit boundaries

| Unit | Responsibility | Depends on |
|---|---|---|
| `ConditionLexer` | string → token list | — |
| `ConditionParser` | tokens → AST | `ConditionOperatorRegistry` |
| `ConditionOperatorRegistry` | token → operator metadata + eval | `IConditionOperator` implementations |
| `IConditionOperator` (per op) | one operator's parse metadata + evaluation | `IEvaluationContext` |
| `ConditionExpressionEvaluator` | AST → bool | `IEvaluationContext`, registry |
| `ConditionalEvaluator` (facade) | orchestrates the above; public entry | all of the above |

## 3. Precedence & Grouping (compatibility core)

Precedence table (binds looser → tighter):

| Level | Operators | Fixity / associativity |
|---|---|---|
| 1 (loosest) | `or`, `and` | **equal precedence, left-associative** (preserves current left-to-right) |
| 2 | `not` | prefix |
| 3 | `=` `==` `!=` `>` `<` `>=` `<=` `in` `contains` `startswith` `endswith` | infix |
| 4 | `exists` `is empty` `is not empty` | postfix |
| 5 (tightest) | `( ... )` grouping, `( a, b, ... )` list literal | — |

Key decisions:

- `and`/`or` share **one** precedence level and are **left-associative**, exactly matching the current behavior. We deliberately do **not** introduce standard `and > or` precedence, because `A or B and C` would then evaluate differently and silently change stored conditions.
- Because comparison/membership operators (level 3) bind tighter than `not` (level 2), `not Status in Roles` parses as `not (Status in Roles)`, which reads naturally.
- Parentheses provide explicit grouping: `(A or B) and C`.

The existing test suite is the acceptance gate for these precedence choices: if any existing test encodes a different expectation, the precedence/associativity is adjusted to keep that test green.

## 4. Operator Catalog

### Membership

- **`in`** — `scalar in <source>`. Right-hand side forms:
  1. a variable resolving to `ICollection` / array;
  2. a list literal `("Active", "Pending")`;
  3. a comma-separated string `"Active,Pending"`.
  Element comparison reuses the existing `AreEqual` semantics (strings case-sensitive; booleans case-insensitive). Result is `true` if the scalar equals any element.
- **Negation** — via `not`: `not Status in Roles`. Optional `not in` sugar is **out of scope**.

### String operators (case-sensitive, ordinal — consistent with `=`)

- **`contains`** — `string contains substring`. Note: operand direction is the opposite of `in` (string ⊃ substring, not element ⊂ collection).
- **`startswith`** — `string startswith prefix`.
- **`endswith`** — `string endswith suffix`.

Operands are compared via `.ToString()`, ordinal, case-sensitive.

### Existence / emptiness (postfix)

- **`Foo exists`** — the variable is present in the context (`TryResolveVariable == true`), regardless of its value. Closes the current gap where a missing variable and a `false`/empty value are indistinguishable.
- **`Foo is empty`** — the resolved value is `null`, an empty/whitespace string, or an empty collection; a **missing variable is also `is empty = true`**.
- **`Foo is not empty`** — negation of `is empty`.

### Grouping & list literals

- `( expr )` — boolean grouping.
- `( a, b, c )` — list literal, valid as the RHS of `in`. The parser distinguishes by the presence of commas: `(expr)` is grouping, `(expr, expr, ...)` is a list literal.

## 5. Validation (main compatibility risk)

Today `Validate()` is a heuristic returning specific `ConditionValidationIssueType` values, and tests assert those. A parser gives validation "for free" (a parse error = invalid), but issue-type classification may differ.

Plan: parser-based validation plus a **mapping layer** that preserves existing `ConditionValidationIssueType` outcomes for existing cases. If any existing validation test yields a legitimately improved but different result, it is surfaced for a human decision rather than changed silently.

## 6. Public API Impact

- `ConditionalEvaluator` (internal) — internals rewritten, entry behavior preserved.
- `ConditionEvaluator` / `IConditionEvaluator` (public standalone) — signatures unchanged; new operators available automatically.
- `ConditionContext` / batch evaluation — unchanged.
- Template engine (`ConditionalDetector`, `ConditionalVisitor`) — unchanged.
- `ConditionOperatorRegistry` — internal for now. The architecture allows exposing user-defined operators later; that is **out of scope**.

## 7. Testing Strategy

- **Characterization gate:** the entire existing test suite passes without edits.
- **Lexer:** tokenization, quote normalization, multi-word tokens (`is empty`, `is not empty`).
- **Parser:** precedence, parenthesized grouping, list literals, left-associativity of `and`/`or`.
- **Per operator:** `in` in all three RHS forms + negation; `contains`/`startswith`/`endswith`; `exists`/`is empty`/`is not empty`.
- **Edge cases:** `in` with a non-collection RHS, empty collection, `null` LHS; string operators with a non-string operand; `exists`/`is empty` on nested property paths.

## 8. Out of Scope

- Collection size comparison in conditions (`Items > 0`) — separate concern (current `IsGreaterThan` can't compare collections).
- Regex `matches`, range `between` — deferred (YAGNI).
- Public user-defined operator registration — architecture supports it, not implemented now.
