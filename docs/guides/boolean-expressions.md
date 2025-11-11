# Boolean Expressions Guide

Boolean expressions allow you to evaluate complex logical conditions directly within placeholders. Combine variables with operators to create dynamic, conditional content without using separate `{{#if}}` blocks.

## Table of Contents

- [Quick Start](#quick-start)
- [Expression Syntax](#expression-syntax)
- [Logical Operators](#logical-operators)
- [Comparison Operators](#comparison-operators)
- [Combining with Format Specifiers](#combining-with-format-specifiers)
- [Advanced Usage](#advanced-usage)
- [Best Practices](#best-practices)
- [Real-World Examples](#real-world-examples)

## Quick Start

Wrap any boolean expression in parentheses within a placeholder:

**Template:**
```
Eligible: {{(Age >= 18)}}
```

**C# Data:**
```csharp
var data = new Dictionary<string, object>
{
    ["Age"] = 25
};
```

**JSON Data:**
```json
{
  "Age": 25
}
```

**Output:**
```
Eligible: True
```

## Expression Syntax

### Basic Rules

1. **Expressions must start with `(`** - This distinguishes them from simple variables
2. **Expressions must end with `)`** - Properly close the parentheses
3. **Operators are case-insensitive** - `and`, `AND`, `And` all work
4. **Spaces are optional** - `(var1 and var2)` equals `(var1and var2)`

### Expression vs. Simple Variable

**Simple Variable (no evaluation):**
```
Status: {{IsActive}}
```

**Expression (evaluated):**
```
Status: {{(IsActive)}}
```

**Complex Expression:**
```
Status: {{(IsActive and IsVerified)}}
```

## Logical Operators

### AND Operator

Returns `true` only if **both** operands are true.

**Syntax:** `and`

**Truth Table:**

| Left | Right | Result |
|------|-------|--------|
| true | true | **true** |
| true | false | false |
| false | true | false |
| false | false | false |

**Template:**
```
Access granted: {{(IsActive and IsVerified)}}
```

**C# Data:**
```csharp
var data = new Dictionary<string, object>
{
    ["IsActive"] = true,
    ["IsVerified"] = true
};
```

**JSON Data:**
```json
{
  "IsActive": true,
  "IsVerified": true
}
```

**Output:**
```
Access granted: True
```

### OR Operator

Returns `true` if **at least one** operand is true.

**Syntax:** `or`

**Truth Table:**

| Left | Right | Result |
|------|-------|--------|
| true | true | **true** |
| true | false | **true** |
| false | true | **true** |
| false | false | false |

**Template:**
```
Can proceed: {{(HasPermissionA or HasPermissionB)}}
```

**C# Data:**
```csharp
var data = new Dictionary<string, object>
{
    ["HasPermissionA"] = false,
    ["HasPermissionB"] = true
};
```

**JSON Data:**
```json
{
  "HasPermissionA": false,
  "HasPermissionB": true
}
```

**Output:**
```
Can proceed: True
```

### NOT Operator

Returns the **opposite** of the operand.

**Syntax:** `not`

**Truth Table:**

| Value | Result |
|-------|--------|
| true | false |
| false | **true** |

**Template:**
```
Account locked: {{(not IsActive)}}
```

**C# Data:**
```csharp
var data = new Dictionary<string, object>
{
    ["IsActive"] = false
};
```

**JSON Data:**
```json
{
  "IsActive": false
}
```

**Output:**
```
Account locked: True
```

## Comparison Operators

### Equality (==)

Checks if two values are equal.

**Template:**
```
Status is active: {{(Status == "active")}}
```

**C# Data:**
```csharp
var data = new Dictionary<string, object>
{
    ["Status"] = "active"
};
```

**JSON Data:**
```json
{
  "Status": "active"
}
```

**Output:**
```
Status is active: True
```

### Inequality (!=)

Checks if two values are **not** equal.

**Template:**
```
Status is not pending: {{(Status != "pending")}}
```

**Data:**
```json
{
  "Status": "active"
}
```

**Output:**
```
Status is not pending: True
```

### Greater Than (>)

Checks if left value is greater than right value.

**Template:**
```
Has items: {{(Count > 0)}}
```

**Data:**
```json
{
  "Count": 5
}
```

**Output:**
```
Has items: True
```

### Greater Than or Equal (>=)

Checks if left value is greater than or equal to right value.

**Template:**
```
Is adult: {{(Age >= 18)}}
```

**Data:**
```json
{
  "Age": 18
}
```

**Output:**
```
Is adult: True
```

### Less Than (<)

Checks if left value is less than right value.

**Template:**
```
Under budget: {{(Spent < Budget)}}
```

**C# Data:**
```csharp
var data = new Dictionary<string, object>
{
    ["Spent"] = 800,
    ["Budget"] = 1000
};
```

**JSON Data:**
```json
{
  "Spent": 800,
  "Budget": 1000
}
```

**Output:**
```
Under budget: True
```

### Less Than or Equal (<=)

Checks if left value is less than or equal to right value.

**Template:**
```
At or under limit: {{(Count <= MaxCount)}}
```

**Data:**
```json
{
  "Count": 10,
  "MaxCount": 10
}
```

**Output:**
```
At or under limit: True
```

## Combining with Format Specifiers

The real power comes from combining expressions with format specifiers for human-readable output.

### Basic Combination

**Template:**
```
Eligible: {{(Age >= 18):yesno}}
```

**Data:**
```json
{
  "Age": 20
}
```

**Output:**
```
Eligible: Yes
```

### Complex Expression with Checkbox

**Template:**
```
Access: {{(IsActive and IsVerified):checkbox}}
```

**Data:**
```json
{
  "IsActive": true,
  "IsVerified": true
}
```

**Output:**
```
Access: ☑
```

### Multiple Conditions with Checkmark

**Template:**
```
Qualified: {{(Age >= 18 and HasLicense):checkmark}}
```

**C# Data:**
```csharp
var data = new Dictionary<string, object>
{
    ["Age"] = 25,
    ["HasLicense"] = true
};
```

**JSON Data:**
```json
{
  "Age": 25,
  "HasLicense": true
}
```

**Output:**
```
Qualified: ✓
```

### OR Logic with Format

**Template:**
```
Can edit: {{(IsOwner or IsAdmin):enabled}}
```

**Data:**
```json
{
  "IsOwner": false,
  "IsAdmin": true
}
```

**Output:**
```
Can edit: Enabled
```

## Advanced Usage

### Nested Expressions

Use nested parentheses to control evaluation order.

**Template:**
```
Result: {{((var1 or var2) and var3)}}
```

**C# Data:**
```csharp
var data = new Dictionary<string, object>
{
    ["var1"] = true,
    ["var2"] = false,
    ["var3"] = true
};
```

**JSON Data:**
```json
{
  "var1": true,
  "var2": false,
  "var3": true
}
```

**Output:**
```
Result: True
```

**Evaluation:**
1. `(var1 or var2)` → `(true or false)` → `true`
2. `(true and var3)` → `(true and true)` → `true`

### Complex Nested Expressions

**Template:**
```
Approved: {{((Age >= 18) and (HasLicense or HasPermit)):yesno}}
```

**Data:**
```json
{
  "Age": 20,
  "HasLicense": false,
  "HasPermit": true
}
```

**Output:**
```
Approved: Yes
```

### With Nested Properties

**Template:**
```
User active: {{(User.IsActive):checkbox}}
Profile complete: {{(User.Profile.IsComplete):yesno}}
```

**C# Data:**
```csharp
var data = new Dictionary<string, object>
{
    ["User"] = new
    {
        IsActive = true,
        Profile = new
        {
            IsComplete = false
        }
    }
};
```

**JSON Data:**
```json
{
  "User": {
    "IsActive": true,
    "Profile": {
      "IsComplete": false
    }
  }
}
```

**Output:**
```
User active: ☑
Profile complete: No
```

### In Loops

Use expressions within loop bodies for dynamic row-level logic.

**Template:**
```
{{#foreach Employees}}
- {{Name}}: {{(IsActive and HasCompletedTraining):checkbox}}
{{/foreach}}
```

**C# Data:**
```csharp
var data = new Dictionary<string, object>
{
    ["Employees"] = new[]
    {
        new { Name = "Alice", IsActive = true, HasCompletedTraining = true },
        new { Name = "Bob", IsActive = true, HasCompletedTraining = false },
        new { Name = "Carol", IsActive = false, HasCompletedTraining = true }
    }
};
```

**JSON Data:**
```json
{
  "Employees": [
    { "Name": "Alice", "IsActive": true, "HasCompletedTraining": true },
    { "Name": "Bob", "IsActive": true, "HasCompletedTraining": false },
    { "Name": "Carol", "IsActive": false, "HasCompletedTraining": true }
  ]
}
```

**Output:**
```
- Alice: ☑
- Bob: ☐
- Carol: ☐
```

### Comparing Numbers from Different Sources

**Template:**
```
Over threshold: {{(CurrentValue > Threshold):yesno}}
Within budget: {{(Spent <= Budget):yesno}}
```

**Data:**
```json
{
  "CurrentValue": 150,
  "Threshold": 100,
  "Spent": 800,
  "Budget": 1000
}
```

**Output:**
```
Over threshold: Yes
Within budget: Yes
```

### String Comparisons

**Template:**
```
Is pending: {{(Status == "pending"):yesno}}
Not cancelled: {{(Status != "cancelled"):checkmark}}
```

**Data:**
```json
{
  "Status": "pending"
}
```

**Output:**
```
Is pending: Yes
Not cancelled: ✓
```

## Best Practices

### 1. Use Expressions for Simple Logic

For inline boolean checks, expressions are cleaner than separate conditionals.

**Good (concise):**
```
Status: {{(IsActive and IsVerified):checkbox}}
```

**Less Good (verbose):**
```
{{#if IsActive}}
{{#if IsVerified}}
Status: ☑
{{/if}}
{{/if}}
{{#if (not IsActive)}}
Status: ☐
{{/if}}
{{#if (not IsVerified)}}
Status: ☐
{{/if}}
```

### 2. Use Conditionals for Complex Content

When you need to show/hide entire blocks of content, use `{{#if}}` instead.

**Good:**
```
{{#if IsActive}}
Account Details:
- Name: {{Name}}
- Email: {{Email}}
- Joined: {{JoinDate}}
{{/if}}
```

**Not Recommended:**
```
Account Details: {{(IsActive):yesno}}
```

### 3. Keep Expressions Readable

Break complex logic into multiple lines or use separate variables.

**Good:**
```
Qualified: {{(Age >= 18 and HasLicense):yesno}}
```

**Less Readable:**
```
Status: {{(((Age >= 18) and (HasLicense or HasPermit)) and (not IsBlacklisted) and (CreditScore > 600)):checkmark}}
```

**Better Approach:**
```csharp
// In your C# code:
var data = new Dictionary<string, object>
{
    ["Age"] = 25,
    ["HasLicense"] = true,
    ["HasPermit"] = false,
    ["IsBlacklisted"] = false,
    ["CreditScore"] = 650,
    ["IsQualified"] = (25 >= 18) && (true || false) && !false && (650 > 600)
};
```

**Template:**
```
Qualified: {{IsQualified:checkmark}}
```

### 4. Use Meaningful Variable Names

Clear variable names make expressions self-documenting.

**Good:**
```
Eligible: {{(HasValidLicense and IsInsured):yesno}}
```

**Less Clear:**
```
Eligible: {{(var1 and var2):yesno}}
```

### 5. Consider Evaluation Cost

Complex expressions are evaluated every time they're encountered. For frequently used values, pre-calculate in your data.

**Efficient:**
```csharp
var data = new Dictionary<string, object>
{
    ["IsEligible"] = (age >= 18 && hasLicense)
};
```

**Template:**
```
Eligible: {{IsEligible:yesno}}
```

## Real-World Examples

### Approval Workflow Document

**Template:**
```
Approval Request
================

Request Details:
- Submitted by: {{Requester.Name}}
- Amount: ${{Amount}}
- Date: {{SubmittedDate}}

Approval Status:
- Manager Approval: {{(ManagerApproved):checkbox}}
- Director Approval: {{(DirectorApproved):checkbox}}
- CFO Approval Required: {{(Amount > 10000):yesno}}
- CFO Approved: {{(CFOApproved or Amount <= 10000):checkbox}}

Final Status: {{((ManagerApproved and DirectorApproved) and (CFOApproved or Amount <= 10000)):checkmark}}
```

**C# Data:**
```csharp
var data = new Dictionary<string, object>
{
    ["Requester"] = new { Name = "John Smith" },
    ["Amount"] = 15000,
    ["SubmittedDate"] = "2025-01-15",
    ["ManagerApproved"] = true,
    ["DirectorApproved"] = true,
    ["CFOApproved"] = true
};
```

**JSON Data:**
```json
{
  "Requester": { "Name": "John Smith" },
  "Amount": 15000,
  "SubmittedDate": "2025-01-15",
  "ManagerApproved": true,
  "DirectorApproved": true,
  "CFOApproved": true
}
```

### Employee Benefits Eligibility

**Template:**
```
Benefits Eligibility Report
===========================

Employee: {{Employee.Name}}
Hire Date: {{Employee.HireDate}}
Employment Status: {{Employee.Status}}

Eligibility:
- Health Insurance: {{(Employee.Status == "Full-Time" and Employee.TenureMonths >= 1):yesno}}
- 401(k) Match: {{(Employee.Status == "Full-Time" and Employee.TenureMonths >= 6):yesno}}
- Stock Options: {{(Employee.Status == "Full-Time" and Employee.TenureMonths >= 12):yesno}}
- Gym Membership: {{(Employee.Status != "Contractor"):yesno}}
```

**Data:**
```json
{
  "Employee": {
    "Name": "Sarah Johnson",
    "HireDate": "2024-08-15",
    "Status": "Full-Time",
    "TenureMonths": 5
  }
}
```

### System Requirements Check

**Template:**
```
System Requirements Check
=========================

{{#foreach Requirements}}
{{Name}}: {{(IsM et):checkmark}}
{{/foreach}}

Overall Status: {{((CPUMeetsReq and MemoryMeetsReq) and DiskMeetsReq):active}}
Installation Ready: {{(((CPUMeetsReq and MemoryMeetsReq) and DiskMeetsReq) and OSSupported):yesno}}
```

**C# Data:**
```csharp
var data = new Dictionary<string, object>
{
    ["Requirements"] = new[]
    {
        new { Name = "CPU: 2GHz or faster", IsMet = true },
        new { Name = "RAM: 8GB minimum", IsMet = true },
        new { Name = "Disk: 50GB free space", IsMet = false },
        new { Name = "OS: Windows 10 or later", IsMet = true }
    },
    ["CPUMeetsReq"] = true,
    ["MemoryMeetsReq"] = true,
    ["DiskMeetsReq"] = false,
    ["OSSupported"] = true
};
```

**JSON Data:**
```json
{
  "Requirements": [
    { "Name": "CPU: 2GHz or faster", "IsMet": true },
    { "Name": "RAM: 8GB minimum", "IsMet": true },
    { "Name": "Disk: 50GB free space", "IsMet": false },
    { "Name": "OS: Windows 10 or later", "IsMet": true }
  ],
  "CPUMeetsReq": true,
  "MemoryMeetsReq": true,
  "DiskMeetsReq": false,
  "OSSupported": true
}
```

### Project Risk Assessment

**Template:**
```
Project Risk Assessment
=======================

Project: {{Project.Name}}

Risk Indicators:
- Behind Schedule: {{(Project.DaysOverdue > 0):yesno}}
- Over Budget: {{(Project.SpentPercentage > 100):yesno}}
- Low Team Morale: {{(Project.MoraleScore < 3):yesno}}
- High Complexity: {{(Project.ComplexityScore >= 8):yesno}}

Risk Level:
- Low Risk: {{((Project.DaysOverdue <= 0) and (Project.SpentPercentage <= 100)):checkbox}}
- Medium Risk: {{((Project.DaysOverdue > 0 or Project.SpentPercentage > 100) and Project.MoraleScore >= 3):checkbox}}
- High Risk: {{((Project.DaysOverdue > 5 or Project.SpentPercentage > 120) or Project.MoraleScore < 3):checkbox}}
```

**Data:**
```json
{
  "Project": {
    "Name": "Website Redesign",
    "DaysOverdue": 3,
    "SpentPercentage": 95,
    "MoraleScore": 4,
    "ComplexityScore": 7
  }
}
```

## Operator Precedence

Expressions are evaluated with standard operator precedence:

1. **Parentheses** `()` - Highest priority
2. **NOT** `not`
3. **Comparison** `>`, `>=`, `<`, `<=`, `==`, `!=`
4. **AND** `and`
5. **OR** `or` - Lowest priority

### Examples

**Expression:** `var1 or var2 and var3`
**Evaluation:** `var1 or (var2 and var3)` ← AND binds tighter

**Expression:** `(var1 or var2) and var3`
**Evaluation:** Parentheses force OR first

**Expression:** `not var1 and var2`
**Evaluation:** `(not var1) and var2` ← NOT binds tightest

## Summary

Boolean expressions enable powerful inline logic in your templates:

- ✅ Logical operators: `and`, `or`, `not`
- ✅ Comparison operators: `==`, `!=`, `>`, `>=`, `<`, `<=`
- ✅ Nested expressions with parentheses
- ✅ Combine with format specifiers for readable output
- ✅ Works with nested properties and arrays
- ✅ Use in loops and conditionals
- ✅ Same data works with C# Dictionaries or JSON
- ✅ Case-insensitive operators

For more information, see:
- [Format Specifiers Guide](format-specifiers.md) - Format boolean output
- [API Reference](../../TriasDev.Templify/README.md) - Complete API documentation
- [Examples](../../TriasDev.Templify/Examples.md) - More code examples
- [FAQ](../FAQ.md) - Common questions
