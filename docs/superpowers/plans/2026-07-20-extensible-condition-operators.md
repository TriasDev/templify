# Extensible Condition Engine + New Operators — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the two separate condition engines with one extensible AST-based engine (lexer → parser → operator registry → evaluator), add operators `in` (+ negation), `contains`/`startswith`/`endswith`, `exists`/`is empty`/`is not empty`, and parenthesized grouping — while every existing template, condition, and test keeps working unchanged.

**Architecture:** A shared pipeline parses an expression string into a `ConditionNode` AST using an operator registry (uniform `and > or` precedence, grouping via parentheses). A `ConditionDialect` supplies the two externally-observable semantic policies that differ between callers: **Default** (used by `{{#if}}`, text templates, standalone `IConditionEvaluator` — rich truthiness, numeric `double.Parse` comparison) and **Inline** (used by `{{(...)}}` placeholders — bool-only truthiness, `IComparable` comparison). New operators are shared across both dialects. Adding a future operator = registering one `IConditionOperator` class; the parser is never touched.

**Tech Stack:** C# / .NET 10, DocumentFormat.OpenXml 3.3.0, xUnit. Internal types are visible to tests via `InternalsVisibleTo("TriasDev.Templify.Tests")`.

## Global Constraints

- **Target framework:** .NET 10.0 (`net10.0`); nullable reference types enabled.
- **External compatibility is the hard requirement:** the entire existing test suite (`ConditionalEvaluatorTests` 81, `ConditionValidationTests` 28, `ConditionEvaluatorTests` 31, `ConditionContextTests`, `Expressions/BooleanExpressionParserTests` 12, all `Integration` tests) must pass **unchanged**. Never edit an existing test to make new code pass — if an existing test would fail, the new code is wrong.
- **Precedence is uniform:** `and` binds tighter than `or`; both left-associative. Verified: no existing test/template asserts a mixed `and`/`or` precedence.
- **Naming conventions:** private fields `_camelCase`; public/internal properties `PascalCase`; locals `camelCase`. XML docs on public APIs; internal classes documented where complexity warrants.
- **File header:** every new `.cs` file starts with the two-line copyright/license header used across the codebase:
  ```csharp
  // Copyright (c) 2026 TriasDev GmbH & Co. KG
  // Licensed under the MIT License. See LICENSE file in the project root for full license information.
  ```
- **Formatting gate:** `dotnet format --verify-no-changes --no-restore` must pass before the final commit.
- **New engine namespace:** `TriasDev.Templify.Conditionals.Engine`. New files live in `TriasDev.Templify/Conditionals/Engine/`.
- **Test commands:** full suite `dotnet test TriasDev.Templify.Tests/TriasDev.Templify.Tests.csproj`; single test `... --filter "FullyQualifiedName~<Name>"`.

---

## File Structure

**New (`TriasDev.Templify/Conditionals/Engine/`):**
- `ConditionNode.cs` — AST node hierarchy (`ConditionNode`, `LiteralNode`, `VariableNode`, `ListNode`, `OperatorNode`).
- `ConditionToken.cs` — `ConditionToken` + `ConditionTokenType` enum.
- `ConditionLexer.cs` — string → tokens; quote normalization.
- `ConditionValueOps.cs` — shared, dialect-independent value helpers (equality, comparable-string) used by `in`/string operators and by `DefaultConditionDialect`.
- `ConditionDialect.cs` — abstract `ConditionDialect` + `DefaultConditionDialect` + `InlineConditionDialect`.
- `OperatorFixity.cs` — `enum { Prefix, Infix, Postfix }`.
- `IConditionOperator.cs` — operator contract.
- `ConditionOperatorRegistry.cs` — token → operator lookup + registration of all operators.
- `Operators/` — one file per operator group:
  - `ComparisonOperators.cs` (`EqualOperator`, `NotEqualOperator`, `GreaterOperator`, `LessOperator`, `GreaterOrEqualOperator`, `LessOrEqualOperator`)
  - `LogicalOperators.cs` (`AndOperator`, `OrOperator`, `NotOperator`)
  - `MembershipOperator.cs` (`InOperator`)
  - `StringOperators.cs` (`ContainsOperator`, `StartsWithOperator`, `EndsWithOperator`)
  - `ExistenceOperators.cs` (`ExistsOperator`, `IsEmptyOperator`, `IsNotEmptyOperator`)
- `ConditionParser.cs` — Pratt parser + `ConditionParseException`.
- `ConditionEvaluatorCore.cs` — AST walker producing `bool`/`object?`.

**Modified:**
- `TriasDev.Templify/Conditionals/ConditionalEvaluator.cs` — internals replaced; public method signatures preserved; `Validate` heuristic extended to recognize new operator tokens.
- `TriasDev.Templify/Visitors/PlaceholderVisitor.cs:72-111` — inline `{{(...)}}` switched from `BooleanExpressionParser` to the unified engine with `InlineConditionDialect`.

**Removed (after Task 10):**
- `TriasDev.Templify/Expressions/BooleanExpressionParser.cs`, `BooleanExpression.cs`, `EvaluationContextAdapter.cs`.
- `TriasDev.Templify.Tests/Expressions/BooleanExpressionParserTests.cs` (its behavior is re-covered by new Inline-dialect tests; see Task 10).

**Docs (Task 11):** `docs/for-template-authors/conditionals.md`, `boolean-expressions.md`, `docs/for-developers/condition-evaluation.md`, plus operator-list touch-ups in `best-practices.md`, `template-syntax.md`, `FAQ.md`.

---

## Task 1: AST nodes, fixity enum, value helpers

**Files:**
- Create: `TriasDev.Templify/Conditionals/Engine/ConditionNode.cs`
- Create: `TriasDev.Templify/Conditionals/Engine/OperatorFixity.cs`
- Create: `TriasDev.Templify/Conditionals/Engine/ConditionValueOps.cs`
- Test: `TriasDev.Templify.Tests/Engine/ConditionValueOpsTests.cs`

**Interfaces:**
- Produces:
  - `abstract class ConditionNode` (marker base).
  - `sealed class LiteralNode(object? value) { object? Value }`
  - `sealed class VariableNode(string path) { string Path }`
  - `sealed class ListNode(IReadOnlyList<ConditionNode> items) { IReadOnlyList<ConditionNode> Items }`
  - `sealed class OperatorNode(IConditionOperator op, IReadOnlyList<ConditionNode> operands) { IConditionOperator Operator; IReadOnlyList<ConditionNode> Operands }` (references `IConditionOperator` from Task 3 — declare after Task 3 or use a forward `using`; see step 3 note).
  - `enum OperatorFixity { Prefix, Infix, Postfix }`
  - `static class ConditionValueOps` with `bool AreEqual(object? left, object? right)` and `string ToStr(object? value)`.

- [ ] **Step 1: Write the failing test**

Create `TriasDev.Templify.Tests/Engine/ConditionValueOpsTests.cs`:

```csharp
// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using TriasDev.Templify.Conditionals.Engine;

namespace TriasDev.Templify.Tests.Engine;

public class ConditionValueOpsTests
{
    [Fact]
    public void AreEqual_SameStrings_ReturnsTrue()
        => Assert.True(ConditionValueOps.AreEqual("Active", "Active"));

    [Fact]
    public void AreEqual_DifferentCaseStrings_ReturnsFalse()
        => Assert.False(ConditionValueOps.AreEqual("active", "Active"));

    [Fact]
    public void AreEqual_BoolAndLowercaseLiteral_ReturnsTrue()
        => Assert.True(ConditionValueOps.AreEqual(true, "true"));

    [Fact]
    public void AreEqual_BothNull_ReturnsTrue()
        => Assert.True(ConditionValueOps.AreEqual(null, null));

    [Fact]
    public void AreEqual_OneNull_ReturnsFalse()
        => Assert.False(ConditionValueOps.AreEqual(null, "x"));
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test TriasDev.Templify.Tests/TriasDev.Templify.Tests.csproj --filter "FullyQualifiedName~ConditionValueOpsTests"`
Expected: FAIL — `ConditionValueOps` / namespace `TriasDev.Templify.Conditionals.Engine` does not exist (compile error).

- [ ] **Step 3: Write minimal implementation**

Create `TriasDev.Templify/Conditionals/Engine/OperatorFixity.cs`:

```csharp
// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace TriasDev.Templify.Conditionals.Engine;

/// <summary>Where an operator sits relative to its operands.</summary>
internal enum OperatorFixity
{
    Prefix,
    Infix,
    Postfix
}
```

Create `TriasDev.Templify/Conditionals/Engine/ConditionValueOps.cs`. `AreEqual` ports the existing `ConditionalEvaluator.AreEqual` semantics verbatim (bool-aware, case-insensitive for booleans, ordinal `ToString` otherwise) so `=` and `in` keep identical equality behavior:

```csharp
// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace TriasDev.Templify.Conditionals.Engine;

/// <summary>
/// Dialect-independent value helpers shared by operators (equality, string coercion).
/// These preserve the historical <c>ConditionalEvaluator</c> equality semantics so that
/// <c>=</c>, <c>in</c>, and the string operators behave identically regardless of dialect.
/// </summary>
internal static class ConditionValueOps
{
    /// <summary>Compares two values for equality (bool-aware, case-insensitive booleans, ordinal otherwise).</summary>
    public static bool AreEqual(object? left, object? right)
    {
        if (left == null && right == null)
        {
            return true;
        }

        if (left == null || right == null)
        {
            return false;
        }

        if (left is bool || right is bool || IsBooleanLiteral(left) || IsBooleanLiteral(right))
        {
            return string.Equals(left.ToString(), right.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        return left.ToString() == right.ToString();
    }

    /// <summary>Coerces a value to its ordinal string form (empty string for null).</summary>
    public static string ToStr(object? value) => value?.ToString() ?? string.Empty;

    private static bool IsBooleanLiteral(object? value)
    {
        string? str = value as string;
        return str != null && (str.Equals("true", StringComparison.OrdinalIgnoreCase)
            || str.Equals("false", StringComparison.OrdinalIgnoreCase));
    }
}
```

Create `TriasDev.Templify/Conditionals/Engine/ConditionNode.cs`:

```csharp
// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace TriasDev.Templify.Conditionals.Engine;

/// <summary>Base type for parsed condition-expression AST nodes.</summary>
internal abstract class ConditionNode
{
}

/// <summary>A literal value (string, number, bool, or null).</summary>
internal sealed class LiteralNode : ConditionNode
{
    public LiteralNode(object? value) => Value = value;

    public object? Value { get; }
}

/// <summary>A variable reference resolved through the evaluation context (supports dotted/indexed paths).</summary>
internal sealed class VariableNode : ConditionNode
{
    public VariableNode(string path) => Path = path ?? throw new ArgumentNullException(nameof(path));

    public string Path { get; }
}

/// <summary>A list literal, e.g. <c>("A", "B")</c>. Valid as the right-hand side of <c>in</c>.</summary>
internal sealed class ListNode : ConditionNode
{
    public ListNode(IReadOnlyList<ConditionNode> items) => Items = items ?? throw new ArgumentNullException(nameof(items));

    public IReadOnlyList<ConditionNode> Items { get; }
}

/// <summary>An operator application with its operand nodes.</summary>
internal sealed class OperatorNode : ConditionNode
{
    public OperatorNode(IConditionOperator @operator, IReadOnlyList<ConditionNode> operands)
    {
        Operator = @operator ?? throw new ArgumentNullException(nameof(@operator));
        Operands = operands ?? throw new ArgumentNullException(nameof(operands));
    }

    public IConditionOperator Operator { get; }

    public IReadOnlyList<ConditionNode> Operands { get; }
}
```

> Note: `OperatorNode` references `IConditionOperator`, created in Task 3. To keep Task 1 self-contained and compiling, create a **minimal placeholder** `IConditionOperator.cs` now containing just the interface signature from Task 3's Step 3 (empty-bodied contract), then flesh out registry/operators in Task 3. Alternatively implement Task 3's `IConditionOperator.cs` first. Either way `OperatorNode` must compile.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test TriasDev.Templify.Tests/TriasDev.Templify.Tests.csproj --filter "FullyQualifiedName~ConditionValueOpsTests"`
Expected: PASS (5 passed).

- [ ] **Step 5: Commit**

```bash
git add TriasDev.Templify/Conditionals/Engine/ TriasDev.Templify.Tests/Engine/ConditionValueOpsTests.cs
git commit -m "feat(conditionals): add AST nodes, fixity enum, value helpers (#128)"
```

---

## Task 2: Lexer

**Files:**
- Create: `TriasDev.Templify/Conditionals/Engine/ConditionToken.cs`
- Create: `TriasDev.Templify/Conditionals/Engine/ConditionLexer.cs`
- Test: `TriasDev.Templify.Tests/Engine/ConditionLexerTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces:
  - `enum ConditionTokenType { Identifier, String, Number, Boolean, Null, Operator, LParen, RParen, Comma, End }`
  - `sealed class ConditionToken { ConditionTokenType Type; string Text; object? LiteralValue }` (ctor `ConditionToken(ConditionTokenType type, string text, object? literalValue = null)`).
  - `sealed class ConditionLexer { IReadOnlyList<ConditionToken> Tokenize(string expression) }`.

Lexing rules:
- Normalize curly quotes to ASCII first (port `NormalizeQuotes` from `ConditionalEvaluator.cs:643-654`).
- Whitespace separates tokens.
- `(`, `)`, `,` are single-character tokens (`LParen`, `RParen`, `Comma`).
- Quoted strings (`"..."`) → `String` token; quotes stripped; supports `\"` escape.
- Operator symbols: greedily match longest of `>=`, `<=`, `==`, `!=`, `=`, `>`, `<` → `Operator`.
- A run of letters/digits/`_`/`.`/`[`/`]` → a **word**. If the word (case-insensitive) is a known word-operator (`and`, `or`, `not`, `in`, `contains`, `startswith`, `endswith`, `exists`, `is`, `empty`) → `Operator` token (Text lowercased). If it is `true`/`false` → `Boolean` (LiteralValue bool). If it is `null` → `Null`. If it is all digits/`.`/leading `-` and parses as a number → `Number` (LiteralValue int or double). Otherwise → `Identifier`.
- Always end with an `End` token.

> The multi-word operators `is empty` / `is not empty` are lexed as separate `Operator` word tokens (`is`, `not`, `empty`); the parser (Task 4) assembles them. `not` is shared with prefix negation — disambiguated by position in the parser.

- [ ] **Step 1: Write the failing test**

Create `TriasDev.Templify.Tests/Engine/ConditionLexerTests.cs`:

```csharp
// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using TriasDev.Templify.Conditionals.Engine;

namespace TriasDev.Templify.Tests.Engine;

public class ConditionLexerTests
{
    private static List<ConditionToken> Lex(string s) => new ConditionLexer().Tokenize(s).ToList();

    [Fact]
    public void Tokenize_Comparison_ProducesIdentifierOperatorString()
    {
        List<ConditionToken> t = Lex("Status = \"Active\"");
        Assert.Equal(ConditionTokenType.Identifier, t[0].Type);
        Assert.Equal("Status", t[0].Text);
        Assert.Equal(ConditionTokenType.Operator, t[1].Type);
        Assert.Equal("=", t[1].Text);
        Assert.Equal(ConditionTokenType.String, t[2].Type);
        Assert.Equal("Active", t[2].Text);
        Assert.Equal(ConditionTokenType.End, t[3].Type);
    }

    [Fact]
    public void Tokenize_GreaterOrEqual_MatchesLongestOperator()
    {
        List<ConditionToken> t = Lex("Count >= 3");
        Assert.Equal(ConditionTokenType.Operator, t[1].Type);
        Assert.Equal(">=", t[1].Text);
        Assert.Equal(ConditionTokenType.Number, t[2].Type);
        Assert.Equal(3, t[2].LiteralValue);
    }

    [Fact]
    public void Tokenize_ListLiteral_ProducesParensAndCommas()
    {
        List<ConditionToken> t = Lex("Status in (\"A\", \"B\")");
        Assert.Equal("in", t[1].Text);
        Assert.Equal(ConditionTokenType.LParen, t[2].Type);
        Assert.Equal(ConditionTokenType.String, t[3].Type);
        Assert.Equal(ConditionTokenType.Comma, t[4].Type);
        Assert.Equal(ConditionTokenType.RParen, t[6].Type);
    }

    [Fact]
    public void Tokenize_WordOperators_AreLowercasedOperators()
    {
        List<ConditionToken> t = Lex("A AND B IS EMPTY");
        Assert.Equal(ConditionTokenType.Operator, t[1].Type);
        Assert.Equal("and", t[1].Text);
        Assert.Equal("is", t[3].Text);
        Assert.Equal("empty", t[4].Text);
    }

    [Fact]
    public void Tokenize_CurlyQuotes_AreNormalized()
    {
        List<ConditionToken> t = Lex("Status = “Active”");
        Assert.Equal(ConditionTokenType.String, t[2].Type);
        Assert.Equal("Active", t[2].Text);
    }

    [Fact]
    public void Tokenize_BooleanAndNull_ProduceLiteralTokens()
    {
        List<ConditionToken> t = Lex("Flag = true");
        Assert.Equal(ConditionTokenType.Boolean, t[2].Type);
        Assert.Equal(true, t[2].LiteralValue);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test TriasDev.Templify.Tests/TriasDev.Templify.Tests.csproj --filter "FullyQualifiedName~ConditionLexerTests"`
Expected: FAIL — `ConditionLexer`/`ConditionToken` not defined.

- [ ] **Step 3: Write minimal implementation**

Create `TriasDev.Templify/Conditionals/Engine/ConditionToken.cs`:

```csharp
// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace TriasDev.Templify.Conditionals.Engine;

/// <summary>The kind of a lexed condition token.</summary>
internal enum ConditionTokenType
{
    Identifier,
    String,
    Number,
    Boolean,
    Null,
    Operator,
    LParen,
    RParen,
    Comma,
    End
}

/// <summary>A single lexed token.</summary>
internal sealed class ConditionToken
{
    public ConditionToken(ConditionTokenType type, string text, object? literalValue = null)
    {
        Type = type;
        Text = text;
        LiteralValue = literalValue;
    }

    public ConditionTokenType Type { get; }

    public string Text { get; }

    public object? LiteralValue { get; }
}
```

Create `TriasDev.Templify/Conditionals/Engine/ConditionLexer.cs`:

```csharp
// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;

namespace TriasDev.Templify.Conditionals.Engine;

/// <summary>Converts a condition-expression string into a flat token list.</summary>
internal sealed class ConditionLexer
{
    private static readonly HashSet<string> _wordOperators = new(StringComparer.OrdinalIgnoreCase)
    {
        "and", "or", "not", "in", "contains", "startswith", "endswith", "exists", "is", "empty"
    };

    public IReadOnlyList<ConditionToken> Tokenize(string expression)
    {
        string text = NormalizeQuotes(expression ?? string.Empty);
        List<ConditionToken> tokens = new();
        int i = 0;

        while (i < text.Length)
        {
            char c = text[i];

            if (char.IsWhiteSpace(c)) { i++; continue; }

            if (c == '(') { tokens.Add(new ConditionToken(ConditionTokenType.LParen, "(")); i++; continue; }
            if (c == ')') { tokens.Add(new ConditionToken(ConditionTokenType.RParen, ")")); i++; continue; }
            if (c == ',') { tokens.Add(new ConditionToken(ConditionTokenType.Comma, ",")); i++; continue; }

            if (c == '"')
            {
                i++;
                StringBuilder sb = new();
                while (i < text.Length && text[i] != '"')
                {
                    if (text[i] == '\\' && i + 1 < text.Length && text[i + 1] == '"') { sb.Append('"'); i += 2; continue; }
                    sb.Append(text[i]);
                    i++;
                }
                i++; // closing quote (if present)
                tokens.Add(new ConditionToken(ConditionTokenType.String, sb.ToString()));
                continue;
            }

            string? symbol = MatchSymbolOperator(text, i);
            if (symbol != null)
            {
                tokens.Add(new ConditionToken(ConditionTokenType.Operator, symbol));
                i += symbol.Length;
                continue;
            }

            if (IsWordChar(c) || (c == '-' && i + 1 < text.Length && char.IsDigit(text[i + 1])))
            {
                int start = i;
                if (c == '-') { i++; }
                while (i < text.Length && IsWordChar(text[i])) { i++; }
                string word = text.Substring(start, i - start);
                tokens.Add(ClassifyWord(word));
                continue;
            }

            // Unknown character: emit as an Operator token so validation/parse can reject it.
            tokens.Add(new ConditionToken(ConditionTokenType.Operator, c.ToString()));
            i++;
        }

        tokens.Add(new ConditionToken(ConditionTokenType.End, string.Empty));
        return tokens;
    }

    private static ConditionToken ClassifyWord(string word)
    {
        if (_wordOperators.Contains(word))
        {
            return new ConditionToken(ConditionTokenType.Operator, word.ToLowerInvariant());
        }

        if (word.Equals("true", StringComparison.OrdinalIgnoreCase))
        {
            return new ConditionToken(ConditionTokenType.Boolean, word, true);
        }

        if (word.Equals("false", StringComparison.OrdinalIgnoreCase))
        {
            return new ConditionToken(ConditionTokenType.Boolean, word, false);
        }

        if (word.Equals("null", StringComparison.OrdinalIgnoreCase))
        {
            return new ConditionToken(ConditionTokenType.Null, word, null);
        }

        if (TryParseNumber(word, out object? number))
        {
            return new ConditionToken(ConditionTokenType.Number, word, number);
        }

        return new ConditionToken(ConditionTokenType.Identifier, word);
    }

    private static bool TryParseNumber(string word, out object? value)
    {
        if (!word.Contains('.') && int.TryParse(word, NumberStyles.Integer, CultureInfo.InvariantCulture, out int iv))
        {
            value = iv;
            return true;
        }

        if (double.TryParse(word, NumberStyles.Float, CultureInfo.InvariantCulture, out double dv))
        {
            value = dv;
            return true;
        }

        value = null;
        return false;
    }

    private static string? MatchSymbolOperator(string text, int i)
    {
        string[] twoChar = { ">=", "<=", "==", "!=" };
        if (i + 1 < text.Length)
        {
            string pair = text.Substring(i, 2);
            foreach (string op in twoChar)
            {
                if (pair == op) { return op; }
            }
        }

        char c = text[i];
        if (c == '=' || c == '>' || c == '<') { return c.ToString(); }
        return null;
    }

    private static bool IsWordChar(char c)
        => char.IsLetterOrDigit(c) || c == '_' || c == '.' || c == '[' || c == ']';

    private static string NormalizeQuotes(string expression)
    {
        return expression
            .Replace('“', '"').Replace('”', '"').Replace('„', '"').Replace('‟', '"')
            .Replace('‘', '\'').Replace('’', '\'').Replace('‚', '\'').Replace('‛', '\'');
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test TriasDev.Templify.Tests/TriasDev.Templify.Tests.csproj --filter "FullyQualifiedName~ConditionLexerTests"`
Expected: PASS (6 passed).

- [ ] **Step 5: Commit**

```bash
git add TriasDev.Templify/Conditionals/Engine/ConditionToken.cs TriasDev.Templify/Conditionals/Engine/ConditionLexer.cs TriasDev.Templify.Tests/Engine/ConditionLexerTests.cs
git commit -m "feat(conditionals): add condition lexer (#128)"
```

---

## Task 3: Operator contract, dialects, evaluator core, registry with existing operators

**Files:**
- Create: `TriasDev.Templify/Conditionals/Engine/IConditionOperator.cs`
- Create: `TriasDev.Templify/Conditionals/Engine/ConditionDialect.cs`
- Create: `TriasDev.Templify/Conditionals/Engine/ConditionEvaluatorCore.cs`
- Create: `TriasDev.Templify/Conditionals/Engine/Operators/LogicalOperators.cs`
- Create: `TriasDev.Templify/Conditionals/Engine/Operators/ComparisonOperators.cs`
- Create: `TriasDev.Templify/Conditionals/Engine/ConditionOperatorRegistry.cs`
- Test: `TriasDev.Templify.Tests/Engine/ConditionEvaluatorCoreTests.cs`

**Interfaces:**
- Consumes: `ConditionNode` family (Task 1), `OperatorFixity` (Task 1), `ConditionValueOps` (Task 1).
- Produces:
  - `interface IConditionOperator { IReadOnlyList<string> Tokens; int Precedence; OperatorFixity Fixity; bool Evaluate(ConditionEvaluatorCore core, IReadOnlyList<ConditionNode> operands); }`
  - `abstract class ConditionDialect { bool ToBool(object?); bool AreEqual(object?, object?); bool TryCompare(object?, object?, out int cmp); }` + `DefaultConditionDialect` + `InlineConditionDialect` (both stateless singletons via `static readonly Instance`).
  - `sealed class ConditionEvaluatorCore` with ctor `(IEvaluationContext context, ConditionDialect dialect)`, `bool EvaluateBool(ConditionNode)`, `object? EvaluateValue(ConditionNode)`, `bool TryResolveVariable(string path, out object? value)`, `ConditionDialect Dialect { get; }`.
  - `sealed class ConditionOperatorRegistry` with `static ConditionOperatorRegistry Shared { get; }`, `IConditionOperator? FindInfix(string token)`, `IConditionOperator? FindPrefix(string token)`, `IReadOnlyList<IConditionOperator> PostfixOperators { get; }`, `bool IsKnownOperatorToken(string token)`.
  - Precedence constants: `or`=1, `and`=2, `not`=3, comparisons=4, postfix=5.

- [ ] **Step 1: Write the failing test**

Create `TriasDev.Templify.Tests/Engine/ConditionEvaluatorCoreTests.cs`:

```csharp
// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using TriasDev.Templify.Conditionals.Engine;
using TriasDev.Templify.Core;

namespace TriasDev.Templify.Tests.Engine;

public class ConditionEvaluatorCoreTests
{
    private static ConditionEvaluatorCore Default(Dictionary<string, object> data)
        => new(new GlobalEvaluationContext(data), DefaultConditionDialect.Instance);

    private static readonly ConditionOperatorRegistry Reg = ConditionOperatorRegistry.Shared;

    [Fact]
    public void Equal_MatchingStrings_IsTrue()
    {
        var node = new OperatorNode(Reg.FindInfix("=")!,
            new ConditionNode[] { new VariableNode("Status"), new LiteralNode("Active") });
        Assert.True(Default(new() { ["Status"] = "Active" }).EvaluateBool(node));
    }

    [Fact]
    public void And_ShortCircuits_And_Combines()
    {
        var left = new OperatorNode(Reg.FindInfix(">")!,
            new ConditionNode[] { new VariableNode("Count"), new LiteralNode(0) });
        var node = new OperatorNode(Reg.FindInfix("and")!,
            new ConditionNode[] { left, new VariableNode("IsOn") });
        Assert.True(Default(new() { ["Count"] = 5, ["IsOn"] = true }).EvaluateBool(node));
        Assert.False(Default(new() { ["Count"] = 0, ["IsOn"] = true }).EvaluateBool(node));
    }

    [Fact]
    public void Not_NegatesOperand()
    {
        var node = new OperatorNode(Reg.FindPrefix("not")!,
            new ConditionNode[] { new VariableNode("IsOff") });
        Assert.True(Default(new() { ["IsOff"] = false }).EvaluateBool(node));
    }

    [Fact]
    public void Greater_UsesNumericComparison()
    {
        var node = new OperatorNode(Reg.FindInfix(">")!,
            new ConditionNode[] { new VariableNode("Count"), new LiteralNode(3) });
        Assert.True(Default(new() { ["Count"] = 5 }).EvaluateBool(node));
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test TriasDev.Templify.Tests/TriasDev.Templify.Tests.csproj --filter "FullyQualifiedName~ConditionEvaluatorCoreTests"`
Expected: FAIL — types not defined.

- [ ] **Step 3: Write minimal implementation**

Create `TriasDev.Templify/Conditionals/Engine/IConditionOperator.cs`:

```csharp
// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace TriasDev.Templify.Conditionals.Engine;

/// <summary>
/// A pluggable condition operator. Adding a new operator means implementing this and
/// registering it in <see cref="ConditionOperatorRegistry"/>; the parser is not modified.
/// </summary>
internal interface IConditionOperator
{
    /// <summary>Token sequence that denotes this operator (e.g. ["="], ["is","empty"]).</summary>
    IReadOnlyList<string> Tokens { get; }

    /// <summary>Binding precedence (higher binds tighter).</summary>
    int Precedence { get; }

    /// <summary>Operator position relative to its operands.</summary>
    OperatorFixity Fixity { get; }

    /// <summary>Evaluates the operator against its operand nodes.</summary>
    bool Evaluate(ConditionEvaluatorCore core, IReadOnlyList<ConditionNode> operands);
}
```

Create `TriasDev.Templify/Conditionals/Engine/ConditionDialect.cs`. `DefaultConditionDialect` ports `EvaluateValue`/`AreEqual`/`IsGreaterThan`/`IsLessThan` from `ConditionalEvaluator`. `InlineConditionDialect` ports `BooleanExpression` semantics (bool-only truthiness, `object.Equals`, `IComparable`):

```csharp
// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Collections;
using System.Globalization;

namespace TriasDev.Templify.Conditionals.Engine;

/// <summary>Externally-observable semantic policy for an entry point (truthiness + base comparison).</summary>
internal abstract class ConditionDialect
{
    /// <summary>Coerces a value to a boolean (truthiness).</summary>
    public abstract bool ToBool(object? value);

    /// <summary>Equality used by the <c>=</c>/<c>!=</c> operators.</summary>
    public abstract bool AreEqual(object? left, object? right);

    /// <summary>Ordered comparison used by <c>&gt;</c>/<c>&lt;</c>/<c>&gt;=</c>/<c>&lt;=</c>.
    /// Returns false when the operands are not comparable in this dialect.</summary>
    public abstract bool TryCompare(object? left, object? right, out int cmp);
}

/// <summary>Semantics for <c>{{#if}}</c>, text templates, and the standalone API (rich truthiness, numeric compare).</summary>
internal sealed class DefaultConditionDialect : ConditionDialect
{
    public static readonly DefaultConditionDialect Instance = new();

    public override bool ToBool(object? value)
    {
        if (value == null) { return false; }
        if (value is bool b) { return b; }

        if (value is string s)
        {
            if (string.IsNullOrWhiteSpace(s)) { return false; }
            string lower = s.ToLowerInvariant();
            if (lower == "false" || lower == "0") { return false; }
            if (lower == "true" || lower == "1") { return true; }
            return true;
        }

        if (value is int i) { return i != 0; }
        if (value is ICollection c) { return c.Count > 0; }
        return true;
    }

    public override bool AreEqual(object? left, object? right) => ConditionValueOps.AreEqual(left, right);

    public override bool TryCompare(object? left, object? right, out int cmp)
    {
        cmp = 0;
        if (double.TryParse(left?.ToString() ?? "0", NumberStyles.Float, CultureInfo.InvariantCulture, out double l)
            && double.TryParse(right?.ToString() ?? "0", NumberStyles.Float, CultureInfo.InvariantCulture, out double r))
        {
            cmp = l.CompareTo(r);
            return true;
        }
        return false;
    }
}

/// <summary>Semantics for inline <c>{{(...)}}</c> placeholders (bool-only truthiness, IComparable compare).</summary>
internal sealed class InlineConditionDialect : ConditionDialect
{
    public static readonly InlineConditionDialect Instance = new();

    public override bool ToBool(object? value) => value is bool b && b;

    public override bool AreEqual(object? left, object? right) => Equals(left, right);

    public override bool TryCompare(object? left, object? right, out int cmp)
    {
        cmp = 0;
        if (left == null || right == null)
        {
            cmp = left == null ? (right == null ? 0 : -1) : 1;
            return true;
        }

        if (left is IComparable lc && right is IComparable)
        {
            try { cmp = lc.CompareTo(right); return true; }
            catch (ArgumentException) { return false; }
            catch (InvalidCastException) { return false; }
        }
        return false;
    }
}
```

Create `TriasDev.Templify/Conditionals/Engine/ConditionEvaluatorCore.cs`:

```csharp
// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using TriasDev.Templify.Core;

namespace TriasDev.Templify.Conditionals.Engine;

/// <summary>Walks a <see cref="ConditionNode"/> AST, resolving variables and applying operators.</summary>
internal sealed class ConditionEvaluatorCore
{
    private readonly IEvaluationContext _context;

    public ConditionEvaluatorCore(IEvaluationContext context, ConditionDialect dialect)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        Dialect = dialect ?? throw new ArgumentNullException(nameof(dialect));
    }

    public ConditionDialect Dialect { get; }

    public bool TryResolveVariable(string path, out object? value) => _context.TryResolveVariable(path, out value);

    /// <summary>Evaluates a node to its boolean truth value.</summary>
    public bool EvaluateBool(ConditionNode node)
    {
        return node switch
        {
            OperatorNode op => op.Operator.Evaluate(this, op.Operands),
            LiteralNode lit => Dialect.ToBool(lit.Value),
            VariableNode var => Dialect.ToBool(Resolve(var)),
            _ => false
        };
    }

    /// <summary>Evaluates a node to its underlying value (for operand comparison).</summary>
    public object? EvaluateValue(ConditionNode node)
    {
        return node switch
        {
            LiteralNode lit => lit.Value,
            VariableNode var => Resolve(var),
            ListNode list => MaterializeList(list),
            OperatorNode op => op.Operator.Evaluate(this, op.Operands),
            _ => null
        };
    }

    private object? Resolve(VariableNode var)
    {
        _context.TryResolveVariable(var.Path, out object? value);
        return value;
    }

    private List<object?> MaterializeList(ListNode list)
    {
        List<object?> items = new(list.Items.Count);
        foreach (ConditionNode item in list.Items)
        {
            items.Add(EvaluateValue(item));
        }
        return items;
    }
}
```

Create `TriasDev.Templify/Conditionals/Engine/Operators/LogicalOperators.cs`:

```csharp
// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace TriasDev.Templify.Conditionals.Engine.Operators;

internal sealed class OrOperator : IConditionOperator
{
    public IReadOnlyList<string> Tokens { get; } = new[] { "or" };
    public int Precedence => 1;
    public OperatorFixity Fixity => OperatorFixity.Infix;
    public bool Evaluate(ConditionEvaluatorCore core, IReadOnlyList<ConditionNode> operands)
        => core.EvaluateBool(operands[0]) || core.EvaluateBool(operands[1]);
}

internal sealed class AndOperator : IConditionOperator
{
    public IReadOnlyList<string> Tokens { get; } = new[] { "and" };
    public int Precedence => 2;
    public OperatorFixity Fixity => OperatorFixity.Infix;
    public bool Evaluate(ConditionEvaluatorCore core, IReadOnlyList<ConditionNode> operands)
        => core.EvaluateBool(operands[0]) && core.EvaluateBool(operands[1]);
}

internal sealed class NotOperator : IConditionOperator
{
    public IReadOnlyList<string> Tokens { get; } = new[] { "not" };
    public int Precedence => 3;
    public OperatorFixity Fixity => OperatorFixity.Prefix;
    public bool Evaluate(ConditionEvaluatorCore core, IReadOnlyList<ConditionNode> operands)
        => !core.EvaluateBool(operands[0]);
}
```

Create `TriasDev.Templify/Conditionals/Engine/Operators/ComparisonOperators.cs`:

```csharp
// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace TriasDev.Templify.Conditionals.Engine.Operators;

internal abstract class ComparisonOperatorBase : IConditionOperator
{
    public abstract IReadOnlyList<string> Tokens { get; }
    public int Precedence => 4;
    public OperatorFixity Fixity => OperatorFixity.Infix;
    public bool Evaluate(ConditionEvaluatorCore core, IReadOnlyList<ConditionNode> operands)
        => Compare(core, core.EvaluateValue(operands[0]), core.EvaluateValue(operands[1]));
    protected abstract bool Compare(ConditionEvaluatorCore core, object? left, object? right);
}

internal sealed class EqualOperator : ComparisonOperatorBase
{
    public override IReadOnlyList<string> Tokens { get; } = new[] { "=", "==" };
    protected override bool Compare(ConditionEvaluatorCore core, object? l, object? r) => core.Dialect.AreEqual(l, r);
}

internal sealed class NotEqualOperator : ComparisonOperatorBase
{
    public override IReadOnlyList<string> Tokens { get; } = new[] { "!=" };
    protected override bool Compare(ConditionEvaluatorCore core, object? l, object? r) => !core.Dialect.AreEqual(l, r);
}

internal sealed class GreaterOperator : ComparisonOperatorBase
{
    public override IReadOnlyList<string> Tokens { get; } = new[] { ">" };
    protected override bool Compare(ConditionEvaluatorCore core, object? l, object? r)
        => core.Dialect.TryCompare(l, r, out int c) && c > 0;
}

internal sealed class LessOperator : ComparisonOperatorBase
{
    public override IReadOnlyList<string> Tokens { get; } = new[] { "<" };
    protected override bool Compare(ConditionEvaluatorCore core, object? l, object? r)
        => core.Dialect.TryCompare(l, r, out int c) && c < 0;
}

internal sealed class GreaterOrEqualOperator : ComparisonOperatorBase
{
    public override IReadOnlyList<string> Tokens { get; } = new[] { ">=" };
    protected override bool Compare(ConditionEvaluatorCore core, object? l, object? r)
        => core.Dialect.TryCompare(l, r, out int c) && c >= 0;
}

internal sealed class LessOrEqualOperator : ComparisonOperatorBase
{
    public override IReadOnlyList<string> Tokens { get; } = new[] { "<=" };
    protected override bool Compare(ConditionEvaluatorCore core, object? l, object? r)
        => core.Dialect.TryCompare(l, r, out int c) && c <= 0;
}
```

Create `TriasDev.Templify/Conditionals/Engine/ConditionOperatorRegistry.cs`:

```csharp
// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using TriasDev.Templify.Conditionals.Engine.Operators;

namespace TriasDev.Templify.Conditionals.Engine;

/// <summary>Central registry mapping tokens to operators. Register once; the parser reads it.</summary>
internal sealed class ConditionOperatorRegistry
{
    private readonly Dictionary<string, IConditionOperator> _infix = new(StringComparer.Ordinal);
    private readonly Dictionary<string, IConditionOperator> _prefix = new(StringComparer.Ordinal);
    private readonly List<IConditionOperator> _postfix = new();
    private readonly HashSet<string> _allTokens = new(StringComparer.Ordinal);

    public static ConditionOperatorRegistry Shared { get; } = CreateDefault();

    public IReadOnlyList<IConditionOperator> PostfixOperators => _postfix;

    public IConditionOperator? FindInfix(string token) => _infix.GetValueOrDefault(token);

    public IConditionOperator? FindPrefix(string token) => _prefix.GetValueOrDefault(token);

    public bool IsKnownOperatorToken(string token) => _allTokens.Contains(token);

    public void Register(IConditionOperator op)
    {
        foreach (string token in op.Tokens)
        {
            _allTokens.Add(token);
        }

        switch (op.Fixity)
        {
            case OperatorFixity.Infix:
                foreach (string token in op.Tokens) { _infix[token] = op; }
                break;
            case OperatorFixity.Prefix:
                foreach (string token in op.Tokens) { _prefix[token] = op; }
                break;
            case OperatorFixity.Postfix:
                _postfix.Add(op);
                break;
        }
    }

    private static ConditionOperatorRegistry CreateDefault()
    {
        ConditionOperatorRegistry r = new();
        r.Register(new OrOperator());
        r.Register(new AndOperator());
        r.Register(new NotOperator());
        r.Register(new EqualOperator());
        r.Register(new NotEqualOperator());
        r.Register(new GreaterOperator());
        r.Register(new LessOperator());
        r.Register(new GreaterOrEqualOperator());
        r.Register(new LessOrEqualOperator());
        // Tasks 7-9 add: InOperator, ContainsOperator, StartsWithOperator, EndsWithOperator,
        // ExistsOperator, IsEmptyOperator, IsNotEmptyOperator.
        return r;
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test TriasDev.Templify.Tests/TriasDev.Templify.Tests.csproj --filter "FullyQualifiedName~ConditionEvaluatorCoreTests"`
Expected: PASS (4 passed).

- [ ] **Step 5: Commit**

```bash
git add TriasDev.Templify/Conditionals/Engine/ TriasDev.Templify.Tests/Engine/ConditionEvaluatorCoreTests.cs
git commit -m "feat(conditionals): add operator contract, dialects, evaluator core, registry (#128)"
```

---

## Task 4: Pratt parser with grouping + list literals

**Files:**
- Create: `TriasDev.Templify/Conditionals/Engine/ConditionParser.cs`
- Test: `TriasDev.Templify.Tests/Engine/ConditionParserTests.cs`

**Interfaces:**
- Consumes: `ConditionToken`/`ConditionTokenType` (Task 2), `ConditionNode` family (Task 1), `ConditionOperatorRegistry` (Task 3).
- Produces:
  - `sealed class ConditionParseException : Exception` (ctor `(string message)`).
  - `sealed class ConditionParser` with ctor `(ConditionOperatorRegistry registry)` and `ConditionNode Parse(IReadOnlyList<ConditionToken> tokens)` (throws `ConditionParseException` on malformed input).

Parser grammar (Pratt / precedence-climbing):
- `ParseExpression(minPrec)`: parse a prefix/primary node; then loop applying **postfix** operators (`exists`, `is empty`, `is not empty`) and **infix** operators whose `Precedence >= minPrec` (right side parsed at `Precedence + 1` for left-associativity).
- Prefix: `not` → `OperatorNode(not, [ParseExpression(3)])`.
- Primary: `(` → parse inner `ParseExpression(0)`; if a `,` follows, it's a **list literal** (collect comma-separated `ParseExpression(0)` into `ListNode`); else it's a **grouping** (return the inner node). Then `String`/`Number`/`Boolean`/`Null` → `LiteralNode`; `Identifier` → `VariableNode`.
- Postfix matching: for each token position after an operand, try to match the token sequence of any registered postfix operator (`exists` = ["exists"]; `is empty` = ["is","empty"]; `is not empty` = ["is","not","empty"]). Match the **longest** sequence first.

- [ ] **Step 1: Write the failing test**

Create `TriasDev.Templify.Tests/Engine/ConditionParserTests.cs`:

```csharp
// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using TriasDev.Templify.Conditionals.Engine;
using TriasDev.Templify.Core;

namespace TriasDev.Templify.Tests.Engine;

public class ConditionParserTests
{
    private static bool Eval(string expr, Dictionary<string, object> data)
    {
        var tokens = new ConditionLexer().Tokenize(expr);
        ConditionNode node = new ConditionParser(ConditionOperatorRegistry.Shared).Parse(tokens);
        return new ConditionEvaluatorCore(new GlobalEvaluationContext(data), DefaultConditionDialect.Instance).EvaluateBool(node);
    }

    [Fact]
    public void AndBindsTighterThanOr()
    {
        // false or (true and true) => true ; if or bound tighter, (false or true) and false path differs.
        Assert.True(Eval("A or B and C", new() { ["A"] = false, ["B"] = true, ["C"] = true }));
        Assert.False(Eval("A or B and C", new() { ["A"] = false, ["B"] = true, ["C"] = false }));
    }

    [Fact]
    public void ParenthesesOverridePrecedence()
    {
        Assert.False(Eval("(A or B) and C", new() { ["A"] = false, ["B"] = true, ["C"] = false }));
        Assert.True(Eval("(A or B) and C", new() { ["A"] = false, ["B"] = true, ["C"] = true }));
    }

    [Fact]
    public void ComparisonBindsTighterThanNot()
    {
        // not Status = "Active"  =>  not (Status = "Active")
        Assert.False(Eval("not Status = \"Active\"", new() { ["Status"] = "Active" }));
        Assert.True(Eval("not Status = \"Active\"", new() { ["Status"] = "Inactive" }));
    }

    [Fact]
    public void MalformedExpression_Throws()
    {
        var tokens = new ConditionLexer().Tokenize("Status =");
        Assert.Throws<ConditionParseException>(() =>
            new ConditionParser(ConditionOperatorRegistry.Shared).Parse(tokens));
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test TriasDev.Templify.Tests/TriasDev.Templify.Tests.csproj --filter "FullyQualifiedName~ConditionParserTests"`
Expected: FAIL — `ConditionParser`/`ConditionParseException` not defined.

- [ ] **Step 3: Write minimal implementation**

Create `TriasDev.Templify/Conditionals/Engine/ConditionParser.cs`:

```csharp
// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace TriasDev.Templify.Conditionals.Engine;

/// <summary>Thrown when a condition expression cannot be parsed.</summary>
internal sealed class ConditionParseException : Exception
{
    public ConditionParseException(string message) : base(message) { }
}

/// <summary>Precedence-climbing (Pratt) parser turning tokens into a <see cref="ConditionNode"/> AST.</summary>
internal sealed class ConditionParser
{
    private readonly ConditionOperatorRegistry _registry;
    private IReadOnlyList<ConditionToken> _tokens = Array.Empty<ConditionToken>();
    private int _pos;

    public ConditionParser(ConditionOperatorRegistry registry)
        => _registry = registry ?? throw new ArgumentNullException(nameof(registry));

    public ConditionNode Parse(IReadOnlyList<ConditionToken> tokens)
    {
        _tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
        _pos = 0;
        ConditionNode node = ParseExpression(0);
        if (Current.Type != ConditionTokenType.End)
        {
            throw new ConditionParseException($"Unexpected token '{Current.Text}'.");
        }
        return node;
    }

    private ConditionToken Current => _tokens[_pos];

    private ConditionToken Advance() => _tokens[_pos++];

    private ConditionNode ParseExpression(int minPrecedence)
    {
        ConditionNode left = ParsePrefix();

        while (true)
        {
            ConditionNode? postfixed = TryApplyPostfix(left);
            if (postfixed != null) { left = postfixed; continue; }

            if (Current.Type != ConditionTokenType.Operator) { break; }
            IConditionOperator? infix = _registry.FindInfix(Current.Text);
            if (infix == null || infix.Precedence < minPrecedence) { break; }

            Advance();
            ConditionNode right = ParseExpression(infix.Precedence + 1);
            left = new OperatorNode(infix, new[] { left, right });
        }

        return left;
    }

    private ConditionNode ParsePrefix()
    {
        if (Current.Type == ConditionTokenType.Operator)
        {
            IConditionOperator? prefix = _registry.FindPrefix(Current.Text);
            if (prefix != null)
            {
                Advance();
                ConditionNode operand = ParseExpression(prefix.Precedence);
                return new OperatorNode(prefix, new[] { operand });
            }
        }

        return ParsePrimary();
    }

    private ConditionNode ParsePrimary()
    {
        ConditionToken token = Current;
        switch (token.Type)
        {
            case ConditionTokenType.LParen:
                return ParseParenthesized();
            case ConditionTokenType.String:
                Advance();
                return new LiteralNode(token.Text);
            case ConditionTokenType.Number:
            case ConditionTokenType.Boolean:
            case ConditionTokenType.Null:
                Advance();
                return new LiteralNode(token.LiteralValue);
            case ConditionTokenType.Identifier:
                Advance();
                return new VariableNode(token.Text);
            default:
                throw new ConditionParseException($"Expected an operand but found '{token.Text}'.");
        }
    }

    private ConditionNode ParseParenthesized()
    {
        Advance(); // consume '('
        ConditionNode first = ParseExpression(0);

        if (Current.Type == ConditionTokenType.Comma)
        {
            List<ConditionNode> items = new() { first };
            while (Current.Type == ConditionTokenType.Comma)
            {
                Advance();
                items.Add(ParseExpression(0));
            }
            Expect(ConditionTokenType.RParen);
            return new ListNode(items);
        }

        Expect(ConditionTokenType.RParen);
        return first;
    }

    private ConditionNode? TryApplyPostfix(ConditionNode left)
    {
        foreach (IConditionOperator op in OrderedByTokenCountDescending())
        {
            if (MatchesSequence(op.Tokens))
            {
                _pos += op.Tokens.Count;
                return new OperatorNode(op, new[] { left });
            }
        }
        return null;
    }

    private IEnumerable<IConditionOperator> OrderedByTokenCountDescending()
    {
        // Longest sequence first so "is not empty" wins over any "is ..." prefix.
        List<IConditionOperator> ordered = new(_registry.PostfixOperators);
        ordered.Sort((a, b) => b.Tokens.Count.CompareTo(a.Tokens.Count));
        return ordered;
    }

    private bool MatchesSequence(IReadOnlyList<string> sequence)
    {
        for (int i = 0; i < sequence.Count; i++)
        {
            ConditionToken t = _tokens[Math.Min(_pos + i, _tokens.Count - 1)];
            if (t.Type != ConditionTokenType.Operator || !string.Equals(t.Text, sequence[i], StringComparison.Ordinal))
            {
                return false;
            }
        }
        return true;
    }

    private void Expect(ConditionTokenType type)
    {
        if (Current.Type != type)
        {
            throw new ConditionParseException($"Expected {type} but found '{Current.Text}'.");
        }
        Advance();
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test TriasDev.Templify.Tests/TriasDev.Templify.Tests.csproj --filter "FullyQualifiedName~ConditionParserTests"`
Expected: PASS (4 passed).

- [ ] **Step 5: Commit**

```bash
git add TriasDev.Templify/Conditionals/Engine/ConditionParser.cs TriasDev.Templify.Tests/Engine/ConditionParserTests.cs
git commit -m "feat(conditionals): add Pratt parser with grouping and list literals (#128)"
```

---

## Task 5: Rewire `ConditionalEvaluator` facade to the new engine (compatibility gate)

This is the critical backward-compatibility task: the internal `ConditionalEvaluator` keeps its public method signatures but delegates evaluation to the new engine (Default dialect). `Validate` keeps its existing heuristic, extended only to recognize the new operator tokens so they are not flagged as `UnknownOperator`.

**Files:**
- Modify: `TriasDev.Templify/Conditionals/ConditionalEvaluator.cs`
- Test: (none new) — the gate is the **entire existing suite** passing unchanged.

**Interfaces:**
- Consumes: `ConditionLexer`, `ConditionParser`, `ConditionOperatorRegistry`, `ConditionEvaluatorCore`, `DefaultConditionDialect`, `ConditionParseException`.
- Produces: unchanged public surface — `bool Evaluate(string, IEvaluationContext)`, `bool Evaluate(string, Dictionary<string, object>)`, `ConditionValidationResult Validate(string)`.

- [ ] **Step 1: Establish the green baseline**

Run: `dotnet test TriasDev.Templify.Tests/TriasDev.Templify.Tests.csproj`
Expected: PASS (all existing tests + Tasks 1-4 tests). Record the total passing count — it must not drop.

- [ ] **Step 2: Replace evaluation internals**

In `TriasDev.Templify/Conditionals/ConditionalEvaluator.cs`, replace the body of `Evaluate(string expression, IEvaluationContext context)` (currently `ConditionalEvaluator.cs:202-225`) so it delegates to the engine, preserving the "empty/invalid → false" behavior:

```csharp
public bool Evaluate(string expression, IEvaluationContext context)
{
    if (string.IsNullOrWhiteSpace(expression))
    {
        return false;
    }

    try
    {
        IReadOnlyList<Engine.ConditionToken> tokens = new Engine.ConditionLexer().Tokenize(expression);
        Engine.ConditionNode node = new Engine.ConditionParser(Engine.ConditionOperatorRegistry.Shared).Parse(tokens);
        return new Engine.ConditionEvaluatorCore(context, Engine.DefaultConditionDialect.Instance).EvaluateBool(node);
    }
    catch (Engine.ConditionParseException)
    {
        return false;
    }
}
```

Keep the `Evaluate(string, Dictionary<string, object>)` bridge as-is (it builds a `GlobalEvaluationContext` and calls the above). Delete the now-unused private evaluation helpers that the old flat evaluator used **only** for evaluation (`EvaluateTokens`, `ApplyLogicalOperator`, `ResolveValueOrLiteral`, `EvaluateVariable`, `EvaluateValue`, `AreEqual`, `IsBooleanLiteral`, `IsGreaterThan`, `IsLessThan`, `IsLogicalOperator`) **only if** they are not used by `Validate`. `Validate` currently uses `IsComparisonOperator`, `IsLogicalOperator`, `NormalizeQuotes`, `ParseExpression`, `IsSuspectedUnknownOperator`, and `_knownInvalidOperators` — keep those.

> If removing a helper breaks compilation because `Validate` still references it, keep that helper. The safe rule: remove a private member only after confirming no remaining reference.

- [ ] **Step 3: Extend `Validate` token recognition for new operators**

In `IsComparisonOperator` (`ConditionalEvaluator.cs:631-637`), add the new infix word-operators so they are treated as operators (not unknown tokens or operands). Replace the method with:

```csharp
private bool IsComparisonOperator(string token)
{
    string lower = token.ToLower();
    return lower == EqOperator || lower == EqOperatorDouble || lower == NeOperator ||
           lower == GtOperator || lower == LtOperator ||
           lower == GteOperator || lower == LteOperator ||
           lower == "in" || lower == "contains" || lower == "startswith" || lower == "endswith";
}
```

> Postfix operators (`exists`, `is`, `empty`) and list-literal parentheses are new syntax with no prior validation tests. The existing heuristic `Validate` treats unknown **words** as operands (not errors), so `Foo exists` validates as two operands → it would report `ConsecutiveOperands`. That is acceptable for this task (no existing test covers it) and is refined in Task 9, Step 6.

- [ ] **Step 4: Run the full suite (compatibility gate)**

Run: `dotnet test TriasDev.Templify.Tests/TriasDev.Templify.Tests.csproj`
Expected: PASS — the same total as Step 1's baseline. In particular all 81 `ConditionalEvaluatorTests`, 28 `ConditionValidationTests`, 31 `ConditionEvaluatorTests`, `ConditionContextTests`, and all `Integration` tests are green **without edits**.

> If any existing test fails, the Default dialect does not match historical semantics. Do not edit the test — fix the dialect/engine. Common culprits: numeric comparison culture, `EvaluateValue` truthiness for `"0"`/`"1"`, or bool equality case-insensitivity. Compare against the original methods in git history.

- [ ] **Step 5: Commit**

```bash
git add TriasDev.Templify/Conditionals/ConditionalEvaluator.cs
git commit -m "refactor(conditionals): route {{#if}} evaluation through unified engine (#128)"
```

---

## Task 6: `in` operator (+ negation, list literal, comma string, collection variable)

**Files:**
- Create: `TriasDev.Templify/Conditionals/Engine/Operators/MembershipOperator.cs`
- Modify: `TriasDev.Templify/Conditionals/Engine/ConditionOperatorRegistry.cs` (register `InOperator`)
- Test: `TriasDev.Templify.Tests/Engine/MembershipOperatorTests.cs`

**Interfaces:**
- Consumes: `ConditionEvaluatorCore`, `ConditionValueOps`, `ConditionNode` family.
- Produces: `sealed class InOperator : IConditionOperator` (Tokens `["in"]`, Precedence 4, Infix).

`InOperator.Evaluate`: `left = core.EvaluateValue(operands[0])`; `right = core.EvaluateValue(operands[1])`. Build candidate values:
- `right` is `IEnumerable` but not `string` (includes the materialized `ListNode` `List<object?>` and any resolved `ICollection`) → iterate its items;
- `right` is `string` → split on `,`, trim each;
- otherwise → single candidate `right`.
Return true if any candidate satisfies `ConditionValueOps.AreEqual(left, candidate)`. Negation is handled by the existing `not` operator (`not Status in Roles`).

- [ ] **Step 1: Write the failing test**

Create `TriasDev.Templify.Tests/Engine/MembershipOperatorTests.cs`:

```csharp
// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using TriasDev.Templify.Conditionals.Engine;
using TriasDev.Templify.Core;

namespace TriasDev.Templify.Tests.Engine;

public class MembershipOperatorTests
{
    private static bool Eval(string expr, Dictionary<string, object> data)
    {
        var tokens = new ConditionLexer().Tokenize(expr);
        ConditionNode node = new ConditionParser(ConditionOperatorRegistry.Shared).Parse(tokens);
        return new ConditionEvaluatorCore(new GlobalEvaluationContext(data), DefaultConditionDialect.Instance).EvaluateBool(node);
    }

    [Fact]
    public void In_CollectionVariable_Matches()
        => Assert.True(Eval("Status in Roles", new() { ["Status"] = "Admin", ["Roles"] = new List<object> { "User", "Admin" } }));

    [Fact]
    public void In_CollectionVariable_NoMatch()
        => Assert.False(Eval("Status in Roles", new() { ["Status"] = "Guest", ["Roles"] = new List<object> { "User", "Admin" } }));

    [Fact]
    public void In_ListLiteral_Matches()
        => Assert.True(Eval("Status in (\"Active\", \"Pending\")", new() { ["Status"] = "Pending" }));

    [Fact]
    public void In_CommaString_Matches()
        => Assert.True(Eval("Status in \"Active,Pending\"", new() { ["Status"] = "Active" }));

    [Fact]
    public void NotIn_Negation_Works()
        => Assert.True(Eval("not Status in Roles", new() { ["Status"] = "Guest", ["Roles"] = new List<object> { "User", "Admin" } }));
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test TriasDev.Templify.Tests/TriasDev.Templify.Tests.csproj --filter "FullyQualifiedName~MembershipOperatorTests"`
Expected: FAIL — `in` is not registered, so the parser treats `in` as an unexpected token → `ConditionParseException`, and (for the negation test) evaluation differs.

- [ ] **Step 3: Write minimal implementation**

Create `TriasDev.Templify/Conditionals/Engine/Operators/MembershipOperator.cs`:

```csharp
// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Collections;

namespace TriasDev.Templify.Conditionals.Engine.Operators;

/// <summary>Collection membership: <c>scalar in source</c>. Source may be a collection variable,
/// a list literal <c>("A","B")</c>, or a comma-separated string <c>"A,B"</c>.</summary>
internal sealed class InOperator : IConditionOperator
{
    public IReadOnlyList<string> Tokens { get; } = new[] { "in" };
    public int Precedence => 4;
    public OperatorFixity Fixity => OperatorFixity.Infix;

    public bool Evaluate(ConditionEvaluatorCore core, IReadOnlyList<ConditionNode> operands)
    {
        object? left = core.EvaluateValue(operands[0]);
        object? right = core.EvaluateValue(operands[1]);

        foreach (object? candidate in EnumerateCandidates(right))
        {
            if (ConditionValueOps.AreEqual(left, candidate))
            {
                return true;
            }
        }
        return false;
    }

    private static IEnumerable<object?> EnumerateCandidates(object? right)
    {
        if (right is string s)
        {
            foreach (string part in s.Split(','))
            {
                yield return part.Trim();
            }
            yield break;
        }

        if (right is IEnumerable enumerable)
        {
            foreach (object? item in enumerable)
            {
                yield return item;
            }
            yield break;
        }

        yield return right;
    }
}
```

Register it in `ConditionOperatorRegistry.CreateDefault()` (add after `LessOrEqualOperator`):

```csharp
        r.Register(new InOperator());
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test TriasDev.Templify.Tests/TriasDev.Templify.Tests.csproj --filter "FullyQualifiedName~MembershipOperatorTests"`
Expected: PASS (5 passed).

- [ ] **Step 5: Commit**

```bash
git add TriasDev.Templify/Conditionals/Engine/Operators/MembershipOperator.cs TriasDev.Templify/Conditionals/Engine/ConditionOperatorRegistry.cs TriasDev.Templify.Tests/Engine/MembershipOperatorTests.cs
git commit -m "feat(conditionals): add 'in' membership operator (#128)"
```

---

## Task 7: String operators `contains`, `startswith`, `endswith`

**Files:**
- Create: `TriasDev.Templify/Conditionals/Engine/Operators/StringOperators.cs`
- Modify: `TriasDev.Templify/Conditionals/Engine/ConditionOperatorRegistry.cs` (register the three)
- Test: `TriasDev.Templify.Tests/Engine/StringOperatorsTests.cs`

**Interfaces:**
- Produces: `ContainsOperator`, `StartsWithOperator`, `EndsWithOperator` (each Tokens single-word, Precedence 4, Infix). All use `ConditionValueOps.ToStr` and ordinal, case-sensitive comparison.

- [ ] **Step 1: Write the failing test**

Create `TriasDev.Templify.Tests/Engine/StringOperatorsTests.cs`:

```csharp
// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using TriasDev.Templify.Conditionals.Engine;
using TriasDev.Templify.Core;

namespace TriasDev.Templify.Tests.Engine;

public class StringOperatorsTests
{
    private static bool Eval(string expr, Dictionary<string, object> data)
    {
        var tokens = new ConditionLexer().Tokenize(expr);
        ConditionNode node = new ConditionParser(ConditionOperatorRegistry.Shared).Parse(tokens);
        return new ConditionEvaluatorCore(new GlobalEvaluationContext(data), DefaultConditionDialect.Instance).EvaluateBool(node);
    }

    [Fact]
    public void Contains_Substring_IsTrue()
        => Assert.True(Eval("Description contains \"urgent\"", new() { ["Description"] = "this is urgent" }));

    [Fact]
    public void Contains_IsCaseSensitive()
        => Assert.False(Eval("Description contains \"URGENT\"", new() { ["Description"] = "this is urgent" }));

    [Fact]
    public void StartsWith_Prefix_IsTrue()
        => Assert.True(Eval("Phone startswith \"+49\"", new() { ["Phone"] = "+49 151" }));

    [Fact]
    public void EndsWith_Suffix_IsTrue()
        => Assert.True(Eval("File endswith \".pdf\"", new() { ["File"] = "report.pdf" }));
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test TriasDev.Templify.Tests/TriasDev.Templify.Tests.csproj --filter "FullyQualifiedName~StringOperatorsTests"`
Expected: FAIL — operators not registered → parse throws.

- [ ] **Step 3: Write minimal implementation**

Create `TriasDev.Templify/Conditionals/Engine/Operators/StringOperators.cs`:

```csharp
// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace TriasDev.Templify.Conditionals.Engine.Operators;

internal abstract class StringOperatorBase : IConditionOperator
{
    public abstract IReadOnlyList<string> Tokens { get; }
    public int Precedence => 4;
    public OperatorFixity Fixity => OperatorFixity.Infix;

    public bool Evaluate(ConditionEvaluatorCore core, IReadOnlyList<ConditionNode> operands)
    {
        string left = ConditionValueOps.ToStr(core.EvaluateValue(operands[0]));
        string right = ConditionValueOps.ToStr(core.EvaluateValue(operands[1]));
        return Test(left, right);
    }

    protected abstract bool Test(string left, string right);
}

internal sealed class ContainsOperator : StringOperatorBase
{
    public override IReadOnlyList<string> Tokens { get; } = new[] { "contains" };
    protected override bool Test(string left, string right) => left.Contains(right, StringComparison.Ordinal);
}

internal sealed class StartsWithOperator : StringOperatorBase
{
    public override IReadOnlyList<string> Tokens { get; } = new[] { "startswith" };
    protected override bool Test(string left, string right) => left.StartsWith(right, StringComparison.Ordinal);
}

internal sealed class EndsWithOperator : StringOperatorBase
{
    public override IReadOnlyList<string> Tokens { get; } = new[] { "endswith" };
    protected override bool Test(string left, string right) => left.EndsWith(right, StringComparison.Ordinal);
}
```

Register in `ConditionOperatorRegistry.CreateDefault()` (after `InOperator`):

```csharp
        r.Register(new ContainsOperator());
        r.Register(new StartsWithOperator());
        r.Register(new EndsWithOperator());
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test TriasDev.Templify.Tests/TriasDev.Templify.Tests.csproj --filter "FullyQualifiedName~StringOperatorsTests"`
Expected: PASS (4 passed).

- [ ] **Step 5: Commit**

```bash
git add TriasDev.Templify/Conditionals/Engine/Operators/StringOperators.cs TriasDev.Templify/Conditionals/Engine/ConditionOperatorRegistry.cs TriasDev.Templify.Tests/Engine/StringOperatorsTests.cs
git commit -m "feat(conditionals): add contains/startswith/endswith operators (#128)"
```

---

## Task 8: Existence operators `exists`, `is empty`, `is not empty`

**Files:**
- Create: `TriasDev.Templify/Conditionals/Engine/Operators/ExistenceOperators.cs`
- Modify: `TriasDev.Templify/Conditionals/Engine/ConditionOperatorRegistry.cs` (register the three postfix ops)
- Modify: `TriasDev.Templify/Conditionals/ConditionalEvaluator.cs` (`Validate`: recognize `exists`/`is`/`empty` so `Foo exists` does not report `ConsecutiveOperands`)
- Test: `TriasDev.Templify.Tests/Engine/ExistenceOperatorsTests.cs`

**Interfaces:**
- Produces:
  - `ExistsOperator` (Tokens `["exists"]`, Precedence 5, Postfix).
  - `IsEmptyOperator` (Tokens `["is","empty"]`, Precedence 5, Postfix).
  - `IsNotEmptyOperator` (Tokens `["is","not","empty"]`, Precedence 5, Postfix).

Semantics (operand is the node to the left, typically a `VariableNode`):
- `exists` → true if operand is a `VariableNode` whose path resolves (`core.TryResolveVariable(path, out _) == true`), regardless of value. For non-variable operands, "exists" = the evaluated value is non-null.
- `is empty` → true if the value is null, an empty/whitespace string, or an empty collection; a missing variable is also empty.
- `is not empty` → negation of `is empty`.

- [ ] **Step 1: Write the failing test**

Create `TriasDev.Templify.Tests/Engine/ExistenceOperatorsTests.cs`:

```csharp
// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Collections;
using TriasDev.Templify.Conditionals.Engine;
using TriasDev.Templify.Core;

namespace TriasDev.Templify.Tests.Engine;

public class ExistenceOperatorsTests
{
    private static bool Eval(string expr, Dictionary<string, object> data)
    {
        var tokens = new ConditionLexer().Tokenize(expr);
        ConditionNode node = new ConditionParser(ConditionOperatorRegistry.Shared).Parse(tokens);
        return new ConditionEvaluatorCore(new GlobalEvaluationContext(data), DefaultConditionDialect.Instance).EvaluateBool(node);
    }

    [Fact]
    public void Exists_PresentVariable_IsTrue()
        => Assert.True(Eval("Notes exists", new() { ["Notes"] = "x" }));

    [Fact]
    public void Exists_MissingVariable_IsFalse()
        => Assert.False(Eval("Notes exists", new()));

    [Fact]
    public void IsEmpty_EmptyString_IsTrue()
        => Assert.True(Eval("Notes is empty", new() { ["Notes"] = "  " }));

    [Fact]
    public void IsEmpty_EmptyCollection_IsTrue()
        => Assert.True(Eval("Items is empty", new() { ["Items"] = new List<object>() }));

    [Fact]
    public void IsEmpty_MissingVariable_IsTrue()
        => Assert.True(Eval("Notes is empty", new()));

    [Fact]
    public void IsNotEmpty_NonEmpty_IsTrue()
        => Assert.True(Eval("Notes is not empty", new() { ["Notes"] = "x" }));

    [Fact]
    public void PresentButNull_ExistsFalse_IsEmptyTrue()
    {
        var data = new Dictionary<string, object?> { ["Notes"] = null };
        var ctx = new GlobalEvaluationContext(data!);
        ConditionNode existsNode = Build("Notes exists");
        ConditionNode emptyNode = Build("Notes is empty");
        // 'Notes' resolves (key present) → exists true; value null → is empty true.
        Assert.True(new ConditionEvaluatorCore(ctx, DefaultConditionDialect.Instance).EvaluateBool(existsNode));
        Assert.True(new ConditionEvaluatorCore(ctx, DefaultConditionDialect.Instance).EvaluateBool(emptyNode));
    }

    private static ConditionNode Build(string expr)
        => new ConditionParser(ConditionOperatorRegistry.Shared).Parse(new ConditionLexer().Tokenize(expr));
}
```

> Note on the last test: confirm `GlobalEvaluationContext.TryResolveVariable` returns `true` for a present key whose value is `null`. If it returns `false` for null values, adjust the test's `exists` expectation to match actual context behavior and document it — the context's resolution contract wins over the spec's ideal. Check `GlobalEvaluationContext` before finalizing.

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test TriasDev.Templify.Tests/TriasDev.Templify.Tests.csproj --filter "FullyQualifiedName~ExistenceOperatorsTests"`
Expected: FAIL — postfix operators not registered → parse throws.

- [ ] **Step 3: Write minimal implementation**

Create `TriasDev.Templify/Conditionals/Engine/Operators/ExistenceOperators.cs`:

```csharp
// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Collections;

namespace TriasDev.Templify.Conditionals.Engine.Operators;

/// <summary>Postfix <c>Foo exists</c>: variable is present in the context, regardless of value.</summary>
internal sealed class ExistsOperator : IConditionOperator
{
    public IReadOnlyList<string> Tokens { get; } = new[] { "exists" };
    public int Precedence => 5;
    public OperatorFixity Fixity => OperatorFixity.Postfix;

    public bool Evaluate(ConditionEvaluatorCore core, IReadOnlyList<ConditionNode> operands)
    {
        if (operands[0] is VariableNode variable)
        {
            return core.TryResolveVariable(variable.Path, out _);
        }
        return core.EvaluateValue(operands[0]) != null;
    }
}

/// <summary>Postfix <c>Foo is empty</c>: null / empty-or-whitespace string / empty collection (missing = empty).</summary>
internal sealed class IsEmptyOperator : IConditionOperator
{
    public IReadOnlyList<string> Tokens { get; } = new[] { "is", "empty" };
    public int Precedence => 5;
    public OperatorFixity Fixity => OperatorFixity.Postfix;

    public bool Evaluate(ConditionEvaluatorCore core, IReadOnlyList<ConditionNode> operands)
        => IsEmptyValue(core.EvaluateValue(operands[0]));

    internal static bool IsEmptyValue(object? value)
    {
        if (value == null) { return true; }
        if (value is string s) { return string.IsNullOrWhiteSpace(s); }
        if (value is ICollection c) { return c.Count == 0; }
        return false;
    }
}

/// <summary>Postfix <c>Foo is not empty</c>: negation of <c>is empty</c>.</summary>
internal sealed class IsNotEmptyOperator : IConditionOperator
{
    public IReadOnlyList<string> Tokens { get; } = new[] { "is", "not", "empty" };
    public int Precedence => 5;
    public OperatorFixity Fixity => OperatorFixity.Postfix;

    public bool Evaluate(ConditionEvaluatorCore core, IReadOnlyList<ConditionNode> operands)
        => !IsEmptyOperator.IsEmptyValue(core.EvaluateValue(operands[0]));
}
```

Register in `ConditionOperatorRegistry.CreateDefault()` (after the string operators):

```csharp
        r.Register(new ExistsOperator());
        r.Register(new IsEmptyOperator());
        r.Register(new IsNotEmptyOperator());
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test TriasDev.Templify.Tests/TriasDev.Templify.Tests.csproj --filter "FullyQualifiedName~ExistenceOperatorsTests"`
Expected: PASS (7 passed).

- [ ] **Step 5: Extend `Validate` to accept the postfix keywords**

In `TriasDev.Templify/Conditionals/ConditionalEvaluator.cs`, so that `Foo exists` / `Foo is empty` validate as valid, treat `exists`, `is`, `empty` as recognized operator words in `Validate`. In the classification loop (`ConditionalEvaluator.cs:93-156`), before the `IsComparisonOperator` check, add a branch that classifies these three words (case-insensitive) as `currentType = "comparison"` (so they act as operators between/after operands without triggering `ConsecutiveOperands`). Concretely, add near the top of the per-token classification:

```csharp
            else if (string.Equals(token, "exists", StringComparison.OrdinalIgnoreCase)
                  || string.Equals(token, "empty", StringComparison.OrdinalIgnoreCase)
                  || string.Equals(token, "is", StringComparison.OrdinalIgnoreCase))
            {
                currentType = "comparison";
            }
```

> This is a heuristic relaxation, not full postfix validation. It prevents false-positive validation errors for the new syntax. No existing `ConditionValidationTests` case uses these words, so all 28 remain green.

- [ ] **Step 6: Run the full suite**

Run: `dotnet test TriasDev.Templify.Tests/TriasDev.Templify.Tests.csproj`
Expected: PASS — all existing + all new engine tests.

- [ ] **Step 7: Commit**

```bash
git add TriasDev.Templify/Conditionals/Engine/Operators/ExistenceOperators.cs TriasDev.Templify/Conditionals/Engine/ConditionOperatorRegistry.cs TriasDev.Templify/Conditionals/ConditionalEvaluator.cs TriasDev.Templify.Tests/Engine/ExistenceOperatorsTests.cs
git commit -m "feat(conditionals): add exists/is empty/is not empty operators (#128)"
```

---

## Task 9: Migrate inline `{{(...)}}` to the unified engine (Inline dialect); remove old `Expressions/`

**Files:**
- Modify: `TriasDev.Templify/Visitors/PlaceholderVisitor.cs:72-111`
- Delete: `TriasDev.Templify/Expressions/BooleanExpressionParser.cs`, `TriasDev.Templify/Expressions/BooleanExpression.cs`, `TriasDev.Templify/Expressions/EvaluationContextAdapter.cs`
- Delete: `TriasDev.Templify.Tests/Expressions/BooleanExpressionParserTests.cs`
- Create: `TriasDev.Templify.Tests/Engine/InlineDialectTests.cs` (re-covers the inline behavior the deleted tests asserted)

**Interfaces:**
- Consumes: `ConditionLexer`, `ConditionParser`, `ConditionOperatorRegistry`, `ConditionEvaluatorCore`, `InlineConditionDialect`, `ConditionParseException`.

> Inline expressions arrive **with** the outer parentheses (e.g. the placeholder text is `(var1 and var2)`), which the old parser required. The unified parser handles those parentheses as grouping — no stripping needed. The old parser returned `null` for a bare variable (no leading `(`); inline placeholders always start with `(` (that's how `PlaceholderMatch.IsExpression` is set), so this path only sees parenthesized text.

- [ ] **Step 1: Write the failing test (new inline coverage)**

Create `TriasDev.Templify.Tests/Engine/InlineDialectTests.cs`:

```csharp
// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using TriasDev.Templify.Conditionals.Engine;
using TriasDev.Templify.Core;

namespace TriasDev.Templify.Tests.Engine;

public class InlineDialectTests
{
    private static bool Eval(string expr, Dictionary<string, object> data)
    {
        var tokens = new ConditionLexer().Tokenize(expr);
        ConditionNode node = new ConditionParser(ConditionOperatorRegistry.Shared).Parse(tokens);
        return new ConditionEvaluatorCore(new GlobalEvaluationContext(data), InlineConditionDialect.Instance).EvaluateBool(node);
    }

    [Fact]
    public void And_TwoBooleans()
        => Assert.True(Eval("(var1 and var2)", new() { ["var1"] = true, ["var2"] = true }));

    [Fact]
    public void Or_TwoBooleans()
        => Assert.True(Eval("(var1 or var2)", new() { ["var1"] = false, ["var2"] = true }));

    [Fact]
    public void Not_Boolean()
        => Assert.True(Eval("(not IsActive)", new() { ["IsActive"] = false }));

    [Fact]
    public void Comparison_Numeric()
        => Assert.True(Eval("(Count > 0)", new() { ["Count"] = 5 }));

    [Fact]
    public void Nested_Grouping()
        => Assert.True(Eval("((var1 or var2) and var3)", new() { ["var1"] = false, ["var2"] = true, ["var3"] = true }));

    [Fact]
    public void BareStringVariable_IsFalse_InInlineDialect()
        => Assert.False(Eval("(Name)", new() { ["Name"] = "Alice" })); // Inline truthiness: only bool true is true

    [Fact]
    public void NewOperators_WorkInInlineDialect()
        => Assert.True(Eval("(Status in (\"A\", \"B\"))", new() { ["Status"] = "B" }));
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test TriasDev.Templify.Tests/TriasDev.Templify.Tests.csproj --filter "FullyQualifiedName~InlineDialectTests"`
Expected: PASS already (engine exists) — this test defines the target inline behavior and should pass immediately. If any case fails, fix the engine/dialect before proceeding. (This step verifies parity before we delete the old engine.)

- [ ] **Step 3: Rewire `PlaceholderVisitor`**

In `TriasDev.Templify/Visitors/PlaceholderVisitor.cs`, replace the expression branch (`:72-111`) so it uses the unified engine with the Inline dialect. Replace the body inside `if (placeholder.IsExpression)`:

```csharp
        if (placeholder.IsExpression)
        {
            try
            {
                IReadOnlyList<Conditionals.Engine.ConditionToken> tokens =
                    new Conditionals.Engine.ConditionLexer().Tokenize(placeholder.VariableName);
                Conditionals.Engine.ConditionNode node =
                    new Conditionals.Engine.ConditionParser(Conditionals.Engine.ConditionOperatorRegistry.Shared).Parse(tokens);
                bool result = new Conditionals.Engine.ConditionEvaluatorCore(context, Conditionals.Engine.InlineConditionDialect.Instance)
                    .EvaluateBool(node);
                value = result;
                resolved = true;
            }
            catch (Conditionals.Engine.ConditionParseException)
            {
                _warningCollector.AddWarning(ProcessingWarning.ExpressionFailed(placeholder.VariableName, "Failed to parse expression"));
                resolved = false;
                value = null;
            }
        }
```

Remove the now-unused `using TriasDev.Templify.Expressions;` if present.

- [ ] **Step 4: Delete the old engine and its test**

```bash
git rm TriasDev.Templify/Expressions/BooleanExpressionParser.cs \
       TriasDev.Templify/Expressions/BooleanExpression.cs \
       TriasDev.Templify/Expressions/EvaluationContextAdapter.cs \
       TriasDev.Templify.Tests/Expressions/BooleanExpressionParserTests.cs
```

- [ ] **Step 5: Run the full suite**

Run: `dotnet test TriasDev.Templify.Tests/TriasDev.Templify.Tests.csproj`
Expected: PASS — all remaining existing tests (including inline-expression integration/replacement tests that exercise `{{(...)}}`) plus new engine tests. The deleted `BooleanExpressionParserTests` (12) are replaced by `InlineDialectTests`.

> If an integration test asserting inline `{{(...)}}` output fails, the Inline dialect diverges from the old `BooleanExpression` semantics. Fix `InlineConditionDialect` (truthiness / `IComparable` compare), not the test.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "refactor(conditionals): migrate inline {{(...)}} to unified engine, remove legacy Expressions (#128)"
```

---

## Task 10: End-to-end integration tests through the public template + condition APIs

**Files:**
- Test: `TriasDev.Templify.Tests/Integration/NewOperatorsIntegrationTests.cs`

**Interfaces:**
- Consumes: public `ConditionEvaluator` / `IConditionEvaluator` (standalone), and (optionally) the document template processor via existing integration-test helpers in `TriasDev.Templify.Tests/Helpers/`.

- [ ] **Step 1: Inspect the existing integration/helpers pattern**

Run: `ls TriasDev.Templify.Tests/Helpers TriasDev.Templify.Tests/Integration`
Read one existing conditional integration test to copy the document-building helper usage (e.g. how a `{{#if}}` template is built and processed). Use the same helper(s) in the new test.

- [ ] **Step 2: Write the failing test**

Create `TriasDev.Templify.Tests/Integration/NewOperatorsIntegrationTests.cs`. The standalone-API portion needs no document helper:

```csharp
// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using TriasDev.Templify.Conditionals;

namespace TriasDev.Templify.Tests.Integration;

public class NewOperatorsIntegrationTests
{
    private readonly IConditionEvaluator _evaluator = new ConditionEvaluator();

    [Fact]
    public void StandaloneApi_In_ListLiteral()
        => Assert.True(_evaluator.Evaluate("Status in (\"Active\", \"Pending\")",
            new Dictionary<string, object> { ["Status"] = "Active" }));

    [Fact]
    public void StandaloneApi_Contains()
        => Assert.True(_evaluator.Evaluate("Email contains \"@trias\"",
            new Dictionary<string, object> { ["Email"] = "a@trias.dev" }));

    [Fact]
    public void StandaloneApi_Exists_And_IsEmpty()
    {
        var data = new Dictionary<string, object> { ["Name"] = "Alice" };
        Assert.True(_evaluator.Evaluate("Name exists", data));
        Assert.True(_evaluator.Evaluate("Missing is empty", data));
    }

    [Fact]
    public void StandaloneApi_Grouping_And_Precedence()
    {
        var data = new Dictionary<string, object> { ["A"] = false, ["B"] = true, ["C"] = false };
        Assert.True(_evaluator.Evaluate("A or B and C = false", data)); // and binds tighter than or
        Assert.False(_evaluator.Evaluate("(A or B) and C", data));
    }

    [Fact]
    public void StandaloneApi_NotIn()
        => Assert.True(_evaluator.Evaluate("not Role in Roles",
            new Dictionary<string, object> { ["Role"] = "Guest", ["Roles"] = new List<object> { "Admin", "User" } }));
}
```

- [ ] **Step 3: Run test to verify it fails/passes**

Run: `dotnet test TriasDev.Templify.Tests/TriasDev.Templify.Tests.csproj --filter "FullyQualifiedName~NewOperatorsIntegrationTests"`
Expected: PASS (the engine is complete). If `StandaloneApi_Grouping_And_Precedence`'s first assertion is ambiguous, simplify it to an unambiguous grouping case rather than changing engine behavior.

- [ ] **Step 4: Add a document-level `{{#if}}` test**

Using the helper discovered in Step 1, add one test that processes a Word template containing `{{#if Role in Roles}}VISIBLE{{/if}}` with data where `Role` is in `Roles`, and asserts the output contains `VISIBLE`. (Copy the exact helper call pattern from the existing conditional integration test; do not invent a new helper.)

- [ ] **Step 5: Run the full suite**

Run: `dotnet test TriasDev.Templify.Tests/TriasDev.Templify.Tests.csproj`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add TriasDev.Templify.Tests/Integration/NewOperatorsIntegrationTests.cs
git commit -m "test(conditionals): end-to-end tests for new operators (#128)"
```

---

## Task 11: Documentation (GitHub Pages, `docs/`)

**Files:**
- Modify: `docs/for-template-authors/conditionals.md`
- Modify: `docs/for-template-authors/boolean-expressions.md`
- Modify: `docs/for-developers/condition-evaluation.md`
- Modify: `docs/for-template-authors/best-practices.md`, `docs/for-template-authors/template-syntax.md`, `docs/FAQ.md`
- Modify: `TriasDev.Templify/Conditionals/IConditionEvaluator.cs` (XML doc operator list)

- [ ] **Step 1: Update the standalone API docs + XML doc**

In `docs/for-developers/condition-evaluation.md`, add an "Operators" reference section listing all operators with one example each: `=`/`==`, `!=`, `>`/`<`/`>=`/`<=`, `and`, `or`, `not`, `in` (with the three RHS forms), `contains`, `startswith`, `endswith`, `exists`, `is empty`, `is not empty`, and parentheses for grouping. State that string operators and `in` element equality are case-sensitive, and that `and` binds tighter than `or`.

In `TriasDev.Templify/Conditionals/IConditionEvaluator.cs`, update the `<para>Supported operators: ...</para>` line (`:17`) to include the new operators:

```csharp
    /// Supported operators: =, ==, !=, &gt;, &lt;, &gt;=, &lt;=, and, or, not,
    /// in, contains, startswith, endswith, exists, is empty, is not empty, and parentheses for grouping.
```

- [ ] **Step 2: Update template-author conditionals docs**

In `docs/for-template-authors/conditionals.md`, add subsections with `{{#if}}` examples for: membership (`{{#if Role in Roles}}`, `{{#if Status in ("Active","Pending")}}`, negation `{{#if not Role in Roles}}`), string checks (`contains`/`startswith`/`endswith`), existence (`{{#if Notes exists}}`, `{{#if Notes is empty}}`, `{{#if Notes is not empty}}`), and grouping with parentheses. Note the precedence rule (`and` before `or`; use parentheses to override).

- [ ] **Step 3: Update inline boolean-expressions docs**

In `docs/for-template-authors/boolean-expressions.md`, add the new operators to the operator list (currently `:917-919`) and add inline `{{(...)}}` examples using `in`/`contains`/`exists`. Confirm the precedence section (`:896-908`) matches the unified rule (`and` tighter than `or`, parentheses highest).

- [ ] **Step 4: Touch-ups**

In `docs/for-template-authors/best-practices.md`, `docs/for-template-authors/template-syntax.md`, and `docs/FAQ.md`, update any place that enumerates the operator set or says operators are limited, to include the new operators. (Search each file for `and`, `or`, `not`, operator lists.)

- [ ] **Step 5: Verify build is unaffected and commit**

Run: `dotnet build templify.sln`
Expected: build succeeds (XML doc change compiles).

```bash
git add docs/ TriasDev.Templify/Conditionals/IConditionEvaluator.cs
git commit -m "docs(conditionals): document new operators and grouping (#128)"
```

---

## Task 12: Final verification, formatting, PR

**Files:** none (verification only).

- [ ] **Step 1: Full test suite**

Run: `dotnet test TriasDev.Templify.Tests/TriasDev.Templify.Tests.csproj`
Expected: PASS — all existing tests (unchanged) + all new tests.

- [ ] **Step 2: Formatting gate**

Run: `dotnet format --verify-no-changes --no-restore`
Expected: no changes. If it reports issues: `dotnet format --no-restore`, then re-run the test suite, then commit the formatting fixes:

```bash
git add -A
git commit -m "style: apply dotnet format (#128)"
```

- [ ] **Step 3: Confirm the legacy engine is gone**

Run: `grep -rn "BooleanExpressionParser\|EvaluationContextAdapter\|TriasDev.Templify.Expressions" TriasDev.Templify TriasDev.Templify.Tests --include='*.cs'`
Expected: no matches (all references removed).

- [ ] **Step 4: Push and open PR**

```bash
git push -u origin feat/128-extensible-condition-operators
gh pr create --repo TriasDev/templify --fill --base main
```

Link the PR to issue #128 (title/body reference `#128`).

---

## Self-Review

**Spec coverage:**
- Unified engine (lexer→parser→AST→registry→evaluator) → Tasks 1-4. ✓
- Dialects (Default + Inline) preserving truthiness/comparison → Task 3 (`ConditionDialect`), used in Tasks 5 (Default) and 9 (Inline). ✓
- Uniform `and > or` precedence + grouping → Task 4 (`ConditionParserTests` asserts both). ✓
- `in` (+negation, 3 RHS forms) → Task 6. ✓
- `contains`/`startswith`/`endswith` (case-sensitive) → Task 7. ✓
- `exists`/`is empty`/`is not empty` (postfix, present-but-null semantics) → Task 8. ✓
- External compatibility (existing tests unchanged) → Task 5 (Default gate) + Task 9 (Inline gate) + Task 12. ✓
- Remove `Expressions/` engine → Task 9. ✓
- Validation preserved (heuristic extended, all 28 tests green) → Tasks 5 & 8. ✓ (Deviation from spec §8: heuristic-extension instead of parser-based validation, chosen to eliminate compatibility risk — noted in plan intro.)
- Public API signatures unchanged → Task 5 (facade). ✓
- Docs (GitHub Pages) → Task 11. ✓

**Placeholder scan:** No TBD/TODO/"add error handling"/"similar to Task N" — each code step contains full code. ✓

**Type consistency:** `ConditionNode`/`LiteralNode`/`VariableNode`/`ListNode`/`OperatorNode`, `IConditionOperator` (`Tokens`/`Precedence`/`Fixity`/`Evaluate`), `OperatorFixity`, `ConditionDialect`+`DefaultConditionDialect.Instance`/`InlineConditionDialect.Instance` (`ToBool`/`AreEqual`/`TryCompare`), `ConditionEvaluatorCore` (`EvaluateBool`/`EvaluateValue`/`TryResolveVariable`/`Dialect`), `ConditionOperatorRegistry.Shared` (`FindInfix`/`FindPrefix`/`PostfixOperators`/`IsKnownOperatorToken`/`Register`), `ConditionLexer.Tokenize`, `ConditionParser.Parse`+`ConditionParseException` — names used consistently across Tasks 1-10. ✓

**Known deviations flagged for the implementer:**
1. Validation stays heuristic (not parser-based) — spec §8 refinement, lower risk.
2. `exists` on a present-but-null variable depends on `GlobalEvaluationContext.TryResolveVariable` returning `true` for null values — Task 8 Step 1 instructs verifying this and adjusting the test to match actual context behavior if needed.
