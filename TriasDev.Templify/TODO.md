# Templify TODO

## Test Statistics

- **Total Tests:** 109 ✅ **ALL PASSING!**
  - Unit Tests: 70
  - Integration Tests: 39
- **Test Coverage:** 100%

### Coverage by Feature
- ✅ Basic Placeholder Replacement: 9/9 tests (100%)
- ✅ Nested Structures: 7/7 tests (100%)
- ✅ Loop Processing: 8/8 tests (100%)
- ✅ Formatting Preservation: 9/9 tests (100%)
- ✅ Table Operations: 6/6 tests (100%)

## Future Enhancements

### Feature Requests

#### High Priority

- [x] **Add Conditional Blocks** - `{{#if condition}}...{{/if}}` with `{{else}}` - ✅ **IMPLEMENTED**

  **Inspiration**: OpenXMLTemplates ConditionalUtils (see `/Utilities/OpenXMLTemplates/ControlReplacers/Utils/ConditionalUtils.cs`)

  **Proposed Syntax**:
  ```
  {{#if VariableName}}
    Content shown when true
  {{/if}}

  {{#if VariableName}}
    Content when true
  {{else}}
    Content when false
  {{/if}}
  ```

  **Supported Operators** (inspired by OpenXMLTemplates):
  - **Logical**: `and`, `or`, `not`
  - **Comparison**: `eq` (equals), `gt` (greater than), `lt` (less than), `gte` (greater or equal), `lte` (less or equal), `ne` (not equal)

  **Expression Syntax**:
  ```
  {{#if Status = "Active"}}...{{/if}}
  {{#if Count > 0}}...{{/if}}
  {{#if IsEnabled and Count > 0}}...{{/if}}
  {{#if Status = "Active" or Status = "Pending"}}...{{/if}}
  {{#if Status != "Deleted"}}...{{/if}}
  {{#if not IsDeleted}}...{{/if}}
  {{#if Price > 100 and Price < 1000}}...{{/if}}
  ```

  **Boolean Evaluation Rules** (from OpenXMLTemplates):
  - `null` → `false`
  - `true`/`false` → boolean value
  - `"true"`/`"false"` (string) → boolean value
  - `1`/`0` (int) → `true`/`false`
  - `"1"`/`"0"` (string) → `true`/`false`
  - Empty string/whitespace → `false`
  - Empty collection → `false`
  - Non-empty string → `true`
  - Non-empty collection → `true`

  **Implementation Plan**:

  1. **New Classes** (follow existing LoopProcessor pattern):
     - `ConditionalBlock.cs` - Represents an if/else block with start/end positions
     - `ConditionalDetector.cs` - Finds conditional blocks in document
     - `ConditionalProcessor.cs` - Processes conditional blocks
     - `ConditionalEvaluator.cs` - Evaluates conditional expressions (port from OpenXMLTemplates ConditionalUtils)

  2. **Expression Parsing**:
     - Parse operator precedence: `not` > `and` > `or`
     - Support quoted strings: `Status = "In Progress"` (spaces allowed in quotes)
     - Handle variable references vs literals
     - Support nested property paths: `Customer.Address.Country = "Germany"`

  3. **Integration with DocumentBodyReplacer**:
     - Process conditionals BEFORE loops (to allow conditional loops)
     - Or process conditionals AFTER loops (to allow loops with conditionals inside)
     - Need to decide on precedence order

  4. **Content Removal Logic**:
     - If condition is `false`, remove all content between `{{#if}}` and `{{else}}` (or `{{/if}}`)
     - If condition is `true`, remove all content between `{{else}}` and `{{/if}}`
     - Remove the conditional tags themselves after evaluation

  5. **Table Support**:
     - Similar to loop support - detect if conditional spans table rows
     - Remove entire rows if condition is false

  **Testing Requirements**:
  - Unit tests for ConditionalEvaluator (all operators, all data types)
  - Unit tests for ConditionalDetector (nested conditionals, malformed blocks)
  - Integration tests for simple conditionals
  - Integration tests for conditionals with else blocks
  - Integration tests for complex expressions (multiple operators)
  - Integration tests for conditionals in tables
  - Integration tests for conditionals + loops (both orders)
  - Integration tests for nested conditionals

  **Implementation Status**: ✅ **FULLY COMPLETE!**
  - ✅ ConditionalEvaluator with all operators (51 unit tests passing)
  - ✅ ConditionalBlock data structure with nesting level tracking
  - ✅ ConditionalDetector for finding {{#if}}/{{else}}/{{/if}} blocks (recursive detection)
  - ✅ ConditionalProcessor for evaluating and removing branches (deepest-first processing)
  - ✅ Integration with DocumentTemplateProcessor
  - ✅ 20 integration tests covering end-to-end scenarios (including 7 nested conditional tests)
  - ✅ **180 total tests passing** (109 original + 51 evaluator + 20 integration)
  - ✅ No regression - all existing tests still pass
  - ✅ Supports table row conditionals
  - ✅ **Supports nested conditionals** to any depth
  - ✅ README.md fully documented with examples including nested conditionals

  **Files Created/Enhanced**:
  - ✅ `ConditionalBlock.cs` - Data structure for conditional blocks with nesting level (80 lines)
  - ✅ `ConditionalDetector.cs` - Recursive detection with nesting level tracking (336 lines)
  - ✅ `ConditionalProcessor.cs` - Deepest-first processing with parent checks (143 lines)
  - ✅ `ConditionalEvaluator.cs` - Expression evaluation with all operators (305 lines)
  - ✅ `ConditionalEvaluatorTests.cs` - 51 comprehensive unit tests (581 lines)
  - ✅ `Integration/ConditionalTests.cs` - 20 end-to-end integration tests including nested (684 lines)
  - ✅ `DocumentTemplateProcessor.cs` - Integrated conditional processing
  - ✅ `README.md` - Fully documented with examples including nested conditionals
  - ✅ `Examples.md` - Updated with comprehensive conditional examples (3 sections, 550+ lines added)
  - ✅ `Demo/Program.cs` - Added 5 conditional demo sections (14-18)

#### Medium Priority

- [ ] Add support for headers and footers
- [ ] Add support for text boxes
- [ ] Add support for footnotes/endnotes
- [ ] Add support for custom XML parts
- [ ] Add support for simple expressions in placeholders
- [ ] Add custom value formatters (dates, numbers, currency)

### Code Quality
- [ ] Add performance benchmarks
- [ ] Add memory usage profiling
- [ ] Consider async API for large documents
- [ ] Add XML documentation to all public APIs

### Documentation
- [ ] Add video tutorial or animated GIF examples
- [ ] Create migration guide from OpenXMLTemplates v1
- [ ] Add troubleshooting guide
- [ ] Document performance characteristics
- [ ] Add real-world examples from production use cases
