# Standalone Condition Evaluation

Templify exposes its condition evaluation engine as a standalone API, allowing you to evaluate conditional expressions against data without processing Word documents.

## Use Cases

- **Filtering**: Evaluate conditions to filter collections based on user-defined rules
- **Access Control**: Check permissions using boolean expressions
- **Business Rules**: Evaluate complex conditions in rule engines
- **Validation**: Validate data against conditional requirements

## Quick Start

```csharp
using TriasDev.Templify.Conditionals;

var evaluator = new ConditionEvaluator();

var data = new Dictionary<string, object>
{
    ["IsActive"] = true,
    ["Count"] = 5,
    ["Status"] = "Active"
};

// Simple evaluation
bool isActive = evaluator.Evaluate("IsActive", data);                    // true
bool hasItems = evaluator.Evaluate("Count > 0", data);                   // true
bool isReady = evaluator.Evaluate("IsActive and Count > 3", data);       // true
bool statusMatch = evaluator.Evaluate("Status = \"Active\"", data);      // true
```

## IConditionEvaluator Interface

The main interface for condition evaluation.

### Methods

#### Evaluate (Synchronous)

```csharp
// With Dictionary
bool Evaluate(string expression, Dictionary<string, object> data);

// With JSON string
bool Evaluate(string expression, string jsonData);

// With pre-created context
bool Evaluate(string expression, IEvaluationContext context);
```

#### Validate

```csharp
ConditionValidationResult Validate(string expression);
```

Checks the syntax of an expression without data (see [Validating Expressions](#validating-expressions)).

#### EvaluateAsync (obsolete)

!!! warning "Obsolete since 1.8.0, will be removed in 2.0"
    The `EvaluateAsync` members evaluate synchronously and wrap the result in a completed `Task`. They are
    marked `[Obsolete]`; use `Evaluate(...)` instead.

```csharp
Task<bool> EvaluateAsync(string expression, Dictionary<string, object> data,
    CancellationToken cancellationToken = default);

Task<bool> EvaluateAsync(string expression, string jsonData,
    CancellationToken cancellationToken = default);

Task<bool> EvaluateAsync(string expression, IEvaluationContext context,
    CancellationToken cancellationToken = default);
```

#### CreateContext / CreateConditionContext

```csharp
// Create evaluation context (for use with Evaluate overloads)
IEvaluationContext CreateContext(Dictionary<string, object> data);
IEvaluationContext CreateContext(string jsonData);

// Create condition context (for batch evaluation)
IConditionContext CreateConditionContext(Dictionary<string, object> data);
IConditionContext CreateConditionContext(string jsonData);
```

## IConditionContext Interface

For efficient batch evaluation of multiple expressions against the same data.

```csharp
var evaluator = new ConditionEvaluator();
var context = evaluator.CreateConditionContext(data);

// Evaluate multiple expressions efficiently
bool r1 = context.Evaluate("IsActive");
bool r2 = context.Evaluate("Count > 5");
bool r3 = context.Evaluate("Status = \"Active\" and IsEnabled");
```

### When to Use IConditionContext

Use `IConditionContext` when:

- Evaluating **multiple expressions** against the same data
- Processing data in a **loop** where conditions are checked repeatedly
- **Performance** is critical (avoids re-parsing data for each evaluation)

## Operators Reference

Complete list of all supported operators, one example each. Operator keywords are case-insensitive (`AND`,
`And` and `and` are the same). Symbols such as `&&`, `||`, `===` and `<>` are **not** operators: an expression
using them is malformed and evaluates to `false`.

| Operator | Description | Example |
|----------|-------------|---------|
| `=` / `==` | Equals | `Status = "Active"` |
| `!=` | Not equals | `Status != "Deleted"` |
| `>` | Greater than | `Count > 0` |
| `<` | Less than | `Price < 100` |
| `>=` | Greater or equal | `Age >= 18` |
| `<=` | Less or equal | `Score <= 100` |
| `and` | Logical AND | `IsActive and HasAccess` |
| `or` | Logical OR | `IsAdmin or IsModerator` |
| `not` | Logical NOT | `not IsDeleted` |
| `in` | Membership check | `Role in Roles` |
| `contains` | Substring check (membership on collections) | `Description contains "urgent"` |
| `startswith` | Prefix check | `Code startswith "US-"` |
| `endswith` | Suffix check | `FileName endswith ".pdf"` |
| `exists` | Variable is present | `Notes exists` |
| `is empty` | Variable is null/empty | `Notes is empty` |
| `is not empty` | Variable has a value | `Notes is not empty` |
| `(...)` | Grouping | `(A or B) and C` |

### The `in` Operator

`in` checks whether a value is a member of a collection. The right-hand side can take three forms:

```csharp
// Collection variable
evaluator.Evaluate("Role in Roles", data);

// List literal
evaluator.Evaluate("Status in (\"Active\", \"Pending\")", data);

// Comma-separated string
evaluator.Evaluate("Status in \"Active,Pending\"", data);
```

To negate membership, use `not` as a prefix: `not Role in Roles`.

### String Operators: `contains`, `startswith`, `endswith`

```csharp
evaluator.Evaluate("Description contains \"urgent\"", data);
evaluator.Evaluate("Code startswith \"US-\"", data);
evaluator.Evaluate("FileName endswith \".pdf\"", data);
```

When the left operand is a collection (any non-string `IEnumerable`, including JSON arrays), `contains` is a **membership** test using the same element equality as `in` (`Tags contains "urgent"` ≡ `"urgent" in Tags`). `startswith`/`endswith` on a collection evaluate to `false`. A collection is never matched through its `ToString()` (type name).

### Existence and Emptiness: `exists`, `is empty`, `is not empty`

These are postfix operators — the keyword follows the variable.

```csharp
evaluator.Evaluate("Notes exists", data);          // true if the key is present, even if its value is null
evaluator.Evaluate("Notes is empty", data);         // true if missing, null, whitespace/empty string, or an empty collection
evaluator.Evaluate("Notes is not empty", data);     // opposite of "is empty"
```

A missing variable counts as empty. A variable that is present but `null` satisfies both `exists` and `is empty` at the same time.

### Grouping with Parentheses

Parentheses override default precedence:

```csharp
evaluator.Evaluate("(IsActive or IsTrial) and not IsBanned", data);
```

!!! note "Case Sensitivity"
    String operators (`contains`, `startswith`, `endswith`) and `in` element equality compare values with ordinal, **case-sensitive** semantics — the same rule used by `=`. The operator keywords themselves (`in`, `contains`, `exists`, etc.) are case-insensitive, like `and`/`or`/`not`.

### Operator Precedence

From loosest to tightest binding:

| Level | Operators |
|-------|-----------|
| 1 | `or` |
| 2 | `and` |
| 3 | `not` (prefix) |
| 4 | `=`, `==`, `!=`, `>`, `<`, `>=`, `<=`, `in`, `contains`, `startswith`, `endswith` |
| 5 | `exists`, `is empty`, `is not empty` (postfix) |

So `A or B and C` is `A or (B and C)`, and `not` applies to a whole comparison: `not Status = "Active"` is
`not (Status = "Active")` and `not Role in Roles` is `not (Role in Roles)`. Use parentheses to make intent
explicit or to override the default precedence.

## Reserved Words and Literal Quoting

The operator keywords are **reserved words**:

```
and, or, not, in, contains, startswith, endswith, exists, is, empty
```

`and`, `or` and `not` are always operators, and `true`, `false` and `null` are always literals. The keywords added in 1.7.0 (`in`, `contains`, `startswith`, `endswith`, `exists`, `is`, `empty`) are operators only **where an operator is expected**. Where an operand is expected they are read as a variable name, as before 1.7.0, so existing templates with a variable called `Empty` or `Exists` keep working:

```csharp
evaluator.Evaluate("Exists", data);                    // variable "Exists"
evaluator.Evaluate("Notes is empty", data);            // the "is empty" operator
evaluator.Evaluate("Category = empty", data);          // bareword: variable "empty", or the text "empty" if there is no such variable
```

To reference a variable whose name is a keyword unambiguously, write it in **square brackets**. This works for every keyword, including `and`, `or`, `not`, `true`, `false` and `null`, and can be followed by a path:

```csharp
evaluator.Evaluate("[Empty] = \"yes\" and not [Not]", data);
evaluator.Evaluate("[Exists].Count > 0", data);
```

`ValidateTemplate(template, data)` reports a `ReservedWordAsVariable` warning when a condition uses a bare keyword as a variable that exists in the data, recommending the bracketed form.

**Quote string literals** that could collide with a keyword (`= "empty"` rather than `= empty`).

### Unquoted Words (Bareword Fallback)

An unquoted word on either side of a comparison (`=`, `!=`, `>`, ...) is first looked up as a variable. When no
such variable exists, the word itself is used as a string literal, so `Status = Active` works like
`Status = "Active"`. The flip side: a typo in a variable name is compared as text, and
`Missing = "Missing"` is `true` when `Missing` is not in the data. Quote literals to avoid surprises. The
`in`, string and emptiness operators do not fall back: a missing variable there is simply absent (`null`), except
for unquoted items of a list literal such as `Status in (Active, Pending)`.

### String Literals and Escapes

String literals are enclosed in double quotes (`"..."`; typographic quotes inserted by Word are accepted). Single quotes are only string delimiters in inline `{{(...)}}` expressions; in `{{#if}}` conditions and in this API, `'Active'` is not a string literal. Inside a literal, `\"` is a quote and `\\` is a backslash; any other backslash is kept as-is (so `"C:\Temp"` works):

```csharp
evaluator.Evaluate("Title = \"say \\\"hi\\\"\"", data);   // Title is: say "hi"
evaluator.Evaluate("Path = \"C:\\\\\"", data);            // Path is: C:\
```

A literal without a closing quote is a syntax error: `Evaluate` returns `false`, `Validate` reports `UnbalancedQuotes`, and document/text processing emits an `ExpressionFailed` warning.

### Inline Expressions Use a Stricter Dialect

`ConditionEvaluator` and `{{#if}}`/`{{#elseif}}` share the rules on this page. Inline expressions in Word and text
templates (`{{(A and B)}}`, `{{(Count > 0):yesno}}`) use the same parser and operators, with these differences:

| | `{{#if}}` and `ConditionEvaluator` | Inline `{{(...)}}` |
|---|---|---|
| Truthiness of a bare value | See [Truthiness](#truthiness) | Only boolean `true` is true; `"true"`, `1` or a non-empty string are false |
| `=` / `!=` | Bool-aware, numeric across numeric types, otherwise ordinal string comparison (`5 = "5"` is true) | Numeric across numeric types, otherwise `object.Equals` (`5 = "5"` is false) |
| `>`, `<`, `>=`, `<=` | Numeric; numeric strings are parsed, `null` counts as 0 | Numeric across numeric types, otherwise `IComparable` of the same type |
| Single quotes | Not a string delimiter | `'text'` is a string literal |

The bareword fallback applies to both: in `{{(A = B)}}`, `B` is the variable `B` if it exists, otherwise the
text `B`.

## Expression Syntax

### Simple Variables

```csharp
evaluator.Evaluate("IsActive", data);     // Boolean check (truthy)
evaluator.Evaluate("Count", data);        // Truthy check (non-zero of any numeric type, non-null)
```

### Truthiness

A bare value (`{{#if X}}`, `X and Y`, `not X`) is:

- **false** for: missing variables, `null`, `false`, any numeric zero or NaN, empty or whitespace strings, the
  strings `"false"` and `"0"` (case-insensitive), and empty collections (including empty lazy `IEnumerable`s);
- **true** for everything else (`true`, non-zero numbers, other strings, non-empty collections, objects).

### Boolean Comparisons

```csharp
evaluator.Evaluate("IsActive = true", data);     // Explicit boolean comparison
evaluator.Evaluate("IsActive = false", data);     // Check if explicitly false
evaluator.Evaluate("Config.Debug = true", data);  // Works with nested paths too
```

### Comparisons

```csharp
evaluator.Evaluate("Count > 5", data);
evaluator.Evaluate("Status = \"Active\"", data);
evaluator.Evaluate("Price <= 99.99", data);
```

### Numeric Semantics

Numbers are normalized before they are compared, so all CLR numeric primitives (`byte`, `sbyte`, `short`, `ushort`, `int`, `uint`, `long`, `ulong`, `float`, `double`, `decimal`) and JSON numbers behave the same:

- **Equality** (`=`, `==`, `!=`, `in` elements, `contains` on collections) is numeric when both sides are numbers: `Price = 10` is true for `10.00m`, JSON `10.50` equals `10.5`, `5L = 5`. Integers and `decimal` compare exactly; `float`/`double` are compared through `decimal` (rounded to the type's significant digits, so `0.1` equals `0.1m`), falling back to `double` outside the `decimal` range.
- A number compared with a **string** keeps string semantics: the number's invariant string form is compared ordinally (`Count = "5"` matches `5`, not `5.0`).
- **Ordering** (`>`, `<`, `>=`, `<=`) is numeric across types; numeric strings are parsed with the invariant culture.
- **Truthiness:** any numeric zero (`0`, `0L`, `0m`, `0.0`, `-0.0`, JSON `0.0`, ...) is `false`.
- **NaN** equals nothing (not even NaN), is not ordered (`NaN > 5` and `NaN < 5` are both false) and is falsy. Infinities compare as expected.

The same numeric equality and ordering apply to inline `{{(...)}}` expressions; their truthiness rules (only boolean `true` is truthy) are unchanged.

### Nested Properties

```csharp
var data = new Dictionary<string, object>
{
    ["Customer"] = new Dictionary<string, object>
    {
        ["Name"] = "John",
        ["Address"] = new Dictionary<string, object>
        {
            ["City"] = "Berlin"
        }
    }
};

evaluator.Evaluate("Customer.Name = \"John\"", data);       // true
evaluator.Evaluate("Customer.Address.City = \"Berlin\"", data);  // true
```

### Complex Expressions

```csharp
// Multiple conditions
evaluator.Evaluate("IsActive and Count > 0 and Status = \"Ready\"", data);

// OR conditions
evaluator.Evaluate("Status = \"Active\" or Status = \"Pending\"", data);

// Negation
evaluator.Evaluate("not IsDeleted", data);
evaluator.Evaluate("not Status = \"Archived\"", data);
```

## Validating Expressions

`Validate` checks an expression's syntax without data. It accepts exactly what `Evaluate` accepts:

```csharp
ConditionValidationResult validation = evaluator.Validate("Count > 2 && IsActive");

if (!validation.IsValid)
{
    foreach (ConditionValidationIssue issue in validation.Issues)
    {
        Console.WriteLine($"{issue.Type}: {issue.Message}");   // e.g. UnknownOperator: Unknown operator '&&'.
    }
}
```

`ConditionValidationIssueType` values: `EmptyExpression`, `UnknownOperator`, `UnbalancedQuotes`,
`MissingOperand`, `ConsecutiveOperators`, `ConsecutiveOperands`. `IConditionContext` has the same `Validate`
method. To check all conditions of a Word template, use `DocumentTemplateProcessor.ValidateTemplate`, which
reports them as `InvalidConditionalExpression` errors.

## JSON Data Support

Evaluate conditions directly against JSON strings. The JSON is parsed with `JsonDataParser` (the root must be an object), so nested objects and arrays behave like dictionaries and lists:

```csharp
string json = """
{
    "IsActive": true,
    "Count": 5,
    "Customer": {
        "Name": "John",
        "IsVip": true
    }
}
""";

var evaluator = new ConditionEvaluator();

bool result1 = evaluator.Evaluate("IsActive", json);           // true
bool result2 = evaluator.Evaluate("Customer.IsVip", json);     // true
bool result3 = evaluator.Evaluate("Count > 3", json);          // true
```

## Thread Safety

`ConditionEvaluator` and `ConditionContext` are **thread-safe**. The underlying evaluator has no mutable instance state, so multiple threads can call `Evaluate` concurrently without synchronization.

```csharp
var evaluator = new ConditionEvaluator();
var context = evaluator.CreateConditionContext(data);

// Safe to use from multiple threads
Parallel.ForEach(expressions, expression =>
{
    bool result = context.Evaluate(expression);
    // Process result...
});
```

## Error Handling

### Missing Variables

Missing variables evaluate to `false` (no exception thrown):

```csharp
var data = new Dictionary<string, object>();
bool result = evaluator.Evaluate("MissingVariable", data);  // false
```

### Malformed Expressions

A malformed expression (`Count >`, `A && B`, an unclosed string) does not throw: `Evaluate` returns `false`. Use
[`Validate`](#validating-expressions) to find out why.

### Invalid JSON

Invalid JSON throws `JsonException`:

```csharp
try
{
    evaluator.Evaluate("IsActive", "{ invalid json }");
}
catch (JsonException ex)
{
    // Handle invalid JSON
}
```

### Null Parameters

Null parameters throw `ArgumentNullException`:

```csharp
evaluator.Evaluate(null, data);           // ArgumentNullException
evaluator.Evaluate("IsActive", (Dictionary<string, object>)null);  // ArgumentNullException
```

## Complete Example

```csharp
using TriasDev.Templify.Conditionals;

// Sample data representing user permissions
var userData = new Dictionary<string, object>
{
    ["User"] = new Dictionary<string, object>
    {
        ["Name"] = "Alice",
        ["Role"] = "Editor",
        ["IsActive"] = true,
        ["AccessLevel"] = 3
    },
    ["Feature"] = new Dictionary<string, object>
    {
        ["RequiredLevel"] = 2,
        ["IsEnabled"] = true
    }
};

var evaluator = new ConditionEvaluator();

// Create context for batch evaluation
var context = evaluator.CreateConditionContext(userData);

// Check various conditions
bool canAccess = context.Evaluate(
    "User.IsActive and User.AccessLevel >= Feature.RequiredLevel");

bool canEdit = context.Evaluate(
    "User.Role = \"Editor\" or User.Role = \"Admin\"");

bool featureAvailable = context.Evaluate(
    "Feature.IsEnabled and User.IsActive");

Console.WriteLine($"Can Access: {canAccess}");        // true
Console.WriteLine($"Can Edit: {canEdit}");            // true
Console.WriteLine($"Feature Available: {featureAvailable}");  // true
```

## See Also

- [Boolean Expressions](../for-template-authors/boolean-expressions.md) - Expression syntax reference
- [Conditionals in Templates](../for-template-authors/conditionals.md) - Using conditions in Word templates
