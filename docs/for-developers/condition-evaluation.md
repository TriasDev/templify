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

#### EvaluateAsync (Asynchronous)

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

// Async with cancellation support
bool r4 = await context.EvaluateAsync("IsActive", cancellationToken);
```

### When to Use IConditionContext

Use `IConditionContext` when:

- Evaluating **multiple expressions** against the same data
- Processing data in a **loop** where conditions are checked repeatedly
- **Performance** is critical (avoids re-parsing data for each evaluation)

## Supported Operators

| Operator | Description | Example |
|----------|-------------|---------|
| `=` | Equals | `Status = "Active"` |
| `!=` | Not equals | `Status != "Deleted"` |
| `>` | Greater than | `Count > 0` |
| `<` | Less than | `Price < 100` |
| `>=` | Greater or equal | `Age >= 18` |
| `<=` | Less or equal | `Score <= 100` |
| `and` | Logical AND | `IsActive and HasAccess` |
| `or` | Logical OR | `IsAdmin or IsModerator` |
| `not` | Logical NOT | `not IsDeleted` |

!!! note "Operator Case Insensitivity"
    Logical operators `and`, `or`, and `not` are case-insensitive.
    `AND`, `And`, `and` all work identically.

## Operators Reference

Complete list of all supported operators, one example each.

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
| `contains` | Substring check | `Description contains "urgent"` |
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

!!! note "Operator Precedence"
    `and` binds tighter than `or` (`A or B and C` is `A or (B and C)`). Use parentheses to override the default precedence.

## Reserved Words and Literal Quoting

The operator keywords are **reserved words**. The following names are always parsed as operators, never as variable names or bareword text:

```
and, or, not, in, contains, startswith, endswith, exists, is, empty
```

Because these words are reserved, **string literals must be quoted**. Use `= "empty"` rather than `= empty`:

```csharp
evaluator.Evaluate("Category = \"empty\"", data);   // compares against the text "empty" -> true when Category is "empty"
evaluator.Evaluate("Category = empty", data);        // "empty" is the reserved keyword, not a literal -> does not match
```

An unquoted reserved word on the right-hand side of a comparison is not a valid operand, so the expression fails to parse and `Evaluate` returns `false`. Always quote literals that could collide with a reserved word.

In inline `{{(...)}}` expressions, **both sides of a comparison are resolved as variables-or-literals**: in `{{(A = B)}}`, both `A` and `B` are looked up in the data and the comparison succeeds when the resolved values are equal. Quote a side (`{{(A = "B")}}`) when you mean the literal text instead of a variable lookup.

## Expression Syntax

### Simple Variables

```csharp
evaluator.Evaluate("IsActive", data);     // Boolean check (truthy)
evaluator.Evaluate("Count", data);        // Truthy check (non-zero, non-null)
```

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

## JSON Data Support

Evaluate conditions directly against JSON strings:

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
