# Conditionals Guide

Conditionals let you show or hide content in your document based on data values. They're perfect for creating flexible templates that adapt to different scenarios.

## Basic Conditional Syntax

### Simple If Statement

```
{{#if VariableName}}
  Content to show if true
{{/if}}
```

**When content is shown:**
- Variable exists and is `true`
- Variable exists and is not empty/zero/false

**JSON:**
```json
{
  "IsVIP": true
}
```

**Template:**
```
Dear Customer,

{{#if IsVIP}}
Thank you for being a VIP member! You get 20% off today.
{{/if}}

Best regards
```

**Output (when IsVIP is true):**
```
Dear Customer,

Thank you for being a VIP member! You get 20% off today.

Best regards
```

### If-Else Statement

```
{{#if Condition}}
  Content when true
{{else}}
  Content when false
{{/if}}
```

**JSON:**
```json
{
  "IsPremium": false
}
```

**Template:**
```
{{#if IsPremium}}
Welcome, Premium Member! Enjoy unlimited access.
{{else}}
Upgrade to Premium to unlock all features.
{{/if}}
```

**Output:**
```
Upgrade to Premium to unlock all features.
```

## Comparison Operators

### Equality (`=`)

Check if two values are equal:

**JSON:**
```json
{
  "Status": "Active"
}
```

**Template:**
```
{{#if Status = "Active"}}
Your account is active and ready to use.
{{/if}}

{{#if Status = "Pending"}}
Your account is pending approval.
{{/if}}
```

**Tips:**
- Use quotes around text values: `Status = "Active"`
- Numbers don't need quotes: `Age = 18`
- Comparison is case-sensitive: `"active"` ≠ `"Active"`

### Inequality (`!=`)

Check if two values are NOT equal:

**JSON:**
```json
{
  "PaymentStatus": "Paid"
}
```

**Template:**
```
{{#if PaymentStatus != "Paid"}}
⚠️ Payment required - please submit payment to proceed.
{{/if}}
```

This message only shows when payment is NOT paid.

### Greater Than (`>`)

**JSON:**
```json
{
  "Age": 25,
  "Score": 95
}
```

**Template:**
```
{{#if Age > 18}}
You are eligible to vote.
{{/if}}

{{#if Score > 90}}
Excellent work! You earned an A grade.
{{/if}}
```

### Less Than (`<`)

**JSON:**
```json
{
  "Temperature": -5,
  "Stock": 3
}
```

**Template:**
```
{{#if Temperature < 0}}
⚠️ Freezing conditions - take precautions.
{{/if}}

{{#if Stock < 5}}
⚠️ Low stock alert - only {{Stock}} items remaining.
{{/if}}
```

### Greater Than or Equal (`>=`)

**JSON:**
```json
{
  "YearsExperience": 5,
  "MinimumOrder": 100,
  "OrderAmount": 100
}
```

**Template:**
```
{{#if YearsExperience >= 5}}
You qualify for the Senior Developer position.
{{/if}}

{{#if OrderAmount >= MinimumOrder}}
✓ Order qualifies for free shipping!
{{/if}}
```

### Less Than or Equal (`<=`)

**JSON:**
```json
{
  "ItemsInCart": 3,
  "DaysUntilExpiry": 7
}
```

**Template:**
```
{{#if ItemsInCart <= 5}}
Add more items to qualify for bulk discount!
{{/if}}

{{#if DaysUntilExpiry <= 7}}
⚠️ Your subscription expires soon - renew now!
{{/if}}
```

## Logical Operators

### AND Operator

Both conditions must be true:

```
{{#if Condition1 and Condition2}}
  Content
{{/if}}
```

**JSON:**
```json
{
  "Age": 25,
  "HasLicense": true,
  "HasInsurance": true
}
```

**Template:**
```
{{#if Age >= 18 and HasLicense}}
You can rent a car.
{{/if}}

{{#if HasLicense and HasInsurance}}
You're approved for vehicle rental.
{{/if}}
```

### OR Operator

At least one condition must be true:

```
{{#if Condition1 or Condition2}}
  Content
{{/if}}
```

**JSON:**
```json
{
  "IsVIP": false,
  "IsPremium": true,
  "Role": "Admin"
}
```

**Template:**
```
{{#if IsVIP or IsPremium}}
You have access to exclusive features.
{{/if}}

{{#if Role = "Admin" or Role = "Moderator"}}
You have moderation permissions.
{{/if}}
```

### NOT Operator

Negates a condition:

```
{{#if not Condition}}
  Content
{{/if}}
```

**JSON:**
```json
{
  "IsExpired": false,
  "IsBanned": false
}
```

**Template:**
```
{{#if not IsExpired}}
Your subscription is active.
{{/if}}

{{#if not IsBanned}}
Welcome back! Your account is in good standing.
{{/if}}
```

### Combining Multiple Operators

**JSON:**
```json
{
  "Age": 25,
  "Country": "USA",
  "HasPassport": true,
  "IsBanned": false
}
```

**Template:**
```
{{#if Age >= 18 and (Country = "USA" or HasPassport) and not IsBanned}}
You are eligible to travel internationally.
{{/if}}
```

**Operator precedence:**
1. Parentheses `()`
2. `not`
3. Comparison operators (`=`, `!=`, `>`, `<`, `>=`, `<=`)
4. `and`
5. `or`

## Common Patterns

### Boolean Flags

**JSON:**
```json
{
  "ShowHeader": true,
  "ShowFooter": false,
  "EnableTracking": true
}
```

**Template:**
```
{{#if ShowHeader}}
=== HEADER SECTION ===
Company Name | Contact Info
{{/if}}

[Main content here]

{{#if ShowFooter}}
=== FOOTER SECTION ===
© 2024 Company Name
{{/if}}
```

### Status Checks

**JSON:**
```json
{
  "OrderStatus": "Shipped"
}
```

**Template:**
```
Order Status:

{{#if OrderStatus = "Pending"}}
⏳ Your order is being processed.
{{/if}}

{{#if OrderStatus = "Shipped"}}
📦 Your order has been shipped!
{{/if}}

{{#if OrderStatus = "Delivered"}}
✓ Your order was delivered.
{{/if}}

{{#if OrderStatus = "Cancelled"}}
❌ This order was cancelled.
{{/if}}
```

### Tiered Messaging

**JSON:**
```json
{
  "Score": 85
}
```

**Template:**
```
Your Score: {{Score}}

{{#if Score >= 90}}
🏆 Outstanding! You achieved an A grade.
{{else}}
  {{#if Score >= 80}}
  👍 Great job! You achieved a B grade.
  {{else}}
    {{#if Score >= 70}}
    ✓ Good work! You achieved a C grade.
    {{else}}
    📚 Keep studying! You can improve.
    {{/if}}
  {{/if}}
{{/if}}
```

**Note:** Nested conditionals work, but try to keep them simple for readability.

### Access Control

**JSON:**
```json
{
  "UserRole": "Admin",
  "IsAuthenticated": true
}
```

**Template:**
```
{{#if IsAuthenticated}}
Welcome to the dashboard!

{{#if UserRole = "Admin"}}
[Admin Panel]
- User Management
- System Settings
- Reports
{{/if}}

{{#if UserRole = "Editor"}}
[Editor Panel]
- Edit Content
- Publish Articles
{{/if}}

{{#if UserRole = "Viewer"}}
[Viewer Panel]
- View Content Only
{{/if}}

{{else}}
Please log in to access this page.
{{/if}}
```

## Working with Numbers

### Range Checks

**JSON:**
```json
{
  "Age": 35,
  "Temperature": 72,
  "Price": 150
}
```

**Template:**
```
{{#if Age >= 18 and Age < 65}}
Adult pricing applies.
{{/if}}

{{#if Temperature >= 60 and Temperature <= 80}}
Perfect weather today!
{{/if}}

{{#if Price > 100 and Price <= 200}}
Mid-range product pricing.
{{/if}}
```

### Inventory Checks

**JSON:**
```json
{
  "StockLevel": 5,
  "ReorderPoint": 10
}
```

**Template:**
```
{{#if StockLevel = 0}}
❌ OUT OF STOCK
{{else}}
  {{#if StockLevel < ReorderPoint}}
  ⚠️ LOW STOCK: {{StockLevel}} remaining
  {{else}}
  ✓ In Stock: {{StockLevel}} available
  {{/if}}
{{/if}}
```

### Discount Qualification

**JSON:**
```json
{
  "OrderTotal": 150,
  "IsFirstOrder": false,
  "LoyaltyPoints": 500
}
```

**Template:**
```
{{#if OrderTotal >= 100 or IsFirstOrder or LoyaltyPoints >= 1000}}
🎉 You qualify for a discount!

{{#if OrderTotal >= 100}}
  - Free shipping on orders over $100
{{/if}}

{{#if IsFirstOrder}}
  - 15% off first order discount
{{/if}}

{{#if LoyaltyPoints >= 1000}}
  - Loyalty member discount available
{{/if}}
{{/if}}
```

## Working with Text

### Text Comparison

Remember: text comparisons are **case-sensitive**!

**JSON:**
```json
{
  "Category": "Electronics",
  "Priority": "high"
}
```

**Template:**
```
{{#if Category = "Electronics"}}
Shipping: 2-3 business days
{{/if}}

{{#if Category = "electronics"}}
This won't match - wrong case!
{{/if}}

{{#if Priority = "high"}}
⚠️ HIGH PRIORITY ORDER
{{/if}}
```

### Multiple Text Options

**JSON:**
```json
{
  "PaymentMethod": "Credit Card"
}
```

**Template:**
```
{{#if PaymentMethod = "Credit Card" or PaymentMethod = "Debit Card"}}
Card payment processing fee: $2.50
{{/if}}

{{#if PaymentMethod = "PayPal" or PaymentMethod = "Venmo"}}
Online payment processing fee: 3%
{{/if}}

{{#if PaymentMethod = "Cash" or PaymentMethod = "Check"}}
No processing fees!
{{/if}}
```

## Nested Conditionals

You can nest conditionals inside each other:

**JSON:**
```json
{
  "IsLoggedIn": true,
  "UserType": "Premium",
  "HasActiveSubscription": true
}
```

**Template:**
```
{{#if IsLoggedIn}}
  Welcome!

  {{#if UserType = "Premium"}}
    {{#if HasActiveSubscription}}
      [Premium Content Unlocked]
      Access to all features!
    {{else}}
      [Subscription Expired]
      Please renew your subscription.
    {{/if}}
  {{else}}
    [Free Account]
    Upgrade to Premium for more features.
  {{/if}}
{{else}}
  Please log in.
{{/if}}
```

**Best Practice:** Limit nesting to 2-3 levels deep to keep templates readable.

## Conditionals with Loops

You can use conditionals inside loops:

**JSON:**
```json
{
  "Products": [
    { "Name": "Widget", "Price": 10, "InStock": true },
    { "Name": "Gadget", "Price": 25, "InStock": false },
    { "Name": "Doohickey", "Price": 15, "InStock": true }
  ]
}
```

**Template:**
```
Product List:

{{#foreach Products}}
- {{Name}}: ${{Price}}
  {{#if InStock}}
  ✓ Available
  {{else}}
  ❌ Out of Stock
  {{/if}}
{{/foreach}}
```

**Output:**
```
Product List:

- Widget: $10
  ✓ Available
- Gadget: $25
  ❌ Out of Stock
- Doohickey: $15
  ✓ Available
```

## Loop Variables in Conditionals

Use loop-specific variables in conditionals:

**JSON:**
```json
{
  "Items": ["Apple", "Banana", "Cherry", "Date"]
}
```

**Template:**
```
{{#foreach Items}}
{{#if @first}}*** First item: {{.}} ***{{/if}}
{{#if not @first and not @last}}- {{.}}{{/if}}
{{#if @last}}*** Last item: {{.}} ***{{/if}}
{{/foreach}}
```

**Output:**
```
*** First item: Apple ***
- Banana
- Cherry
*** Last item: Date ***
```

## Common Use Cases

### Personalized Greetings

**JSON:**
```json
{
  "CustomerName": "Alice",
  "LastPurchaseDate": "2024-01-10",
  "DaysSinceLastPurchase": 45
}
```

**Template:**
```
Dear {{CustomerName}},

{{#if DaysSinceLastPurchase < 30}}
Great to see you again so soon!
{{else}}
We've missed you! It's been a while since your last visit.
{{/if}}

{{#if DaysSinceLastPurchase > 60}}
Here's a 15% discount to welcome you back!
{{/if}}
```

### Terms and Conditions

**JSON:**
```json
{
  "IncludeWarranty": true,
  "IncludeInsurance": false,
  "IncludeExtendedSupport": true
}
```

**Template:**
```
TERMS AND CONDITIONS

{{#if IncludeWarranty}}
1. Warranty Coverage
   This product includes a 2-year manufacturer warranty...
{{/if}}

{{#if IncludeInsurance}}
2. Insurance Policy
   Additional insurance coverage provides...
{{/if}}

{{#if IncludeExtendedSupport}}
3. Extended Support
   24/7 customer support is included for...
{{/if}}
```

### Regional Content

**JSON:**
```json
{
  "Country": "USA",
  "Language": "English"
}
```

**Template:**
```
{{#if Country = "USA"}}
Customer Service: 1-800-555-0123
Business Hours: 9 AM - 5 PM EST
{{/if}}

{{#if Country = "UK"}}
Customer Service: 0800 123 4567
Business Hours: 9 AM - 5 PM GMT
{{/if}}

{{#if Country = "Germany"}}
Kundenservice: 0800 123 4567
Geschäftszeiten: 9:00 - 17:00 Uhr MEZ
{{/if}}
```

## Troubleshooting

### Conditional Not Working

**Check these common issues:**

1. **Syntax errors:**
   - ✅ `{{#if Status = "Active"}}`
   - ❌ `{{if Status = "Active"}}` (missing `#`)
   - ❌ `{{#if Status = "Active"` (missing closing `}}`)

2. **Missing closing tag:**
   - ✅ `{{#if ...}}...{{/if}}`
   - ❌ `{{#if ...}}...{{#endif}}` (wrong closing tag)

3. **Case sensitivity:**
   - ✅ `{{#if Status = "Active"}}` with JSON: `"Status": "Active"`
   - ❌ `{{#if Status = "active"}}` with JSON: `"Status": "Active"}`

4. **Wrong operator:**
   - ✅ `{{#if Age = 18}}` (checking equality)
   - ❌ `{{#if Age == 18}}` (wrong operator, use single `=`)

5. **Quotes around text:**
   - ✅ `{{#if Name = "Alice"}}`
   - ❌ `{{#if Name = Alice}}` (missing quotes)

6. **Comparing wrong types:**
   - ✅ `{{#if Age > 18}}` with JSON: `"Age": 25` (number)
   - ⚠️ `{{#if Age > 18}}` with JSON: `"Age": "25"` (string - may not work as expected)

### Content Always Shows/Never Shows

**Debug steps:**

1. **Print the variable value** to see what you're working with:
   ```
   Status value: {{Status}}
   {{#if Status = "Active"}}Content{{/if}}
   ```

2. **Check JSON structure:**
   ```json
   {
     "Status": "Active"    ← Should match exactly
   }
   ```

3. **Simplify the condition:**
   Start with a simple boolean:
   ```
   {{#if IsActive}}Content{{/if}}
   ```

### Nested Conditionals Not Working

Make sure each `{{#if}}` has a matching `{{/if}}`:

**❌ Wrong:**
```
{{#if A}}
  {{#if B}}
    Content
  {{/if}}
  ← Missing {{/if}} for A!
```

**✅ Correct:**
```
{{#if A}}
  {{#if B}}
    Content
  {{/if}}
{{/if}}
```

## Best Practices

1. **Keep conditions simple** - Break complex logic into multiple simpler conditions
2. **Use meaningful variable names** - `IsEligibleForDiscount` is better than `Flag1`
3. **Test edge cases** - What happens when values are null, zero, empty, etc.?
4. **Add comments in Word** - Use Word comments to document complex conditional logic
5. **Use else clauses** - Provide feedback for both true and false cases when appropriate
6. **Limit nesting** - Deep nesting is hard to read; try to keep it to 2-3 levels maximum

## Next Steps

- **[Loops Guide](loops.md)** - Repeat content for arrays and lists
- **[Boolean Expressions](boolean-expressions.md)** - Advanced boolean expression techniques
- **[Placeholders Guide](placeholders.md)** - Using variables in your templates
- **[Template Syntax Reference](template-syntax.md)** - Complete syntax guide
- **[Examples Gallery](examples-gallery.md)** - Real-world examples

## Related Topics

- [Format Specifiers](format-specifiers.md) - Display boolean values as Yes/No, checkboxes, etc.
- [Best Practices](best-practices.md) - Tips for maintainable templates
- [JSON Basics](json-basics.md) - Understanding your data structure
