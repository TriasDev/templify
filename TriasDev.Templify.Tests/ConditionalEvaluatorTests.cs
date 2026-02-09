// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Reflection;
using TriasDev.Templify.Core;
using TriasDev.Templify.Conditionals;
using TriasDev.Templify.Loops;
using TriasDev.Templify.Placeholders;
using TriasDev.Templify.PropertyPaths;
using TriasDev.Templify.Utilities;

namespace TriasDev.Templify.Tests;

public class ConditionalEvaluatorTests
{
    private readonly object _evaluator;
    private readonly MethodInfo _evaluateMethod;
    private static readonly Type _evaluatorType = typeof(DocumentTemplateProcessor).Assembly
        .GetType("TriasDev.Templify.Conditionals.ConditionalEvaluator")!;

    public ConditionalEvaluatorTests()
    {
        _evaluator = Activator.CreateInstance(_evaluatorType)!;
        // Get the Evaluate method that accepts IEvaluationContext
        _evaluateMethod = _evaluatorType.GetMethod(
            "Evaluate",
            BindingFlags.Public | BindingFlags.Instance,
            null,
            new[] { typeof(string), typeof(IEvaluationContext) },
            null)!;
    }

    private bool Evaluate(string expression, Dictionary<string, object> data)
    {
        GlobalEvaluationContext context = new GlobalEvaluationContext(data);
        return (bool)_evaluateMethod.Invoke(_evaluator, new object[] { expression, context })!;
    }

    #region Simple Variable Evaluation

    [Fact]
    public void Evaluate_WithTrueBoolean_ReturnsTrue()
    {
        Dictionary<string, object> data = new() { ["IsActive"] = true };

        bool result = Evaluate("IsActive", data);

        Assert.True(result);
    }

    [Fact]
    public void Evaluate_WithFalseBoolean_ReturnsFalse()
    {
        Dictionary<string, object> data = new() { ["IsActive"] = false };

        bool result = Evaluate("IsActive", data);

        Assert.False(result);
    }

    [Fact]
    public void Evaluate_WithMissingVariable_ReturnsFalse()
    {
        Dictionary<string, object> data = new();

        bool result = Evaluate("MissingVar", data);

        Assert.False(result);
    }

    [Fact]
    public void Evaluate_WithNullValue_ReturnsFalse()
    {
        Dictionary<string, object> data = new() { ["Value"] = null! };

        bool result = Evaluate("Value", data);

        Assert.False(result);
    }

    #endregion

    #region Explicit Boolean Comparison

    [Fact]
    public void Evaluate_ExplicitBooleanEqualsTrue_WithTrueValue_ReturnsTrue()
    {
        Dictionary<string, object> data = new() { ["IsActive"] = true };

        bool result = Evaluate("IsActive = true", data);

        Assert.True(result);
    }

    [Fact]
    public void Evaluate_ExplicitBooleanEqualsFalse_WithFalseValue_ReturnsTrue()
    {
        Dictionary<string, object> data = new() { ["IsActive"] = false };

        bool result = Evaluate("IsActive = false", data);

        Assert.True(result);
    }

    [Fact]
    public void Evaluate_ExplicitBooleanEqualsTrue_WithFalseValue_ReturnsFalse()
    {
        Dictionary<string, object> data = new() { ["IsActive"] = false };

        bool result = Evaluate("IsActive = true", data);

        Assert.False(result);
    }

    [Fact]
    public void Evaluate_ExplicitBooleanEqualsFalse_WithTrueValue_ReturnsFalse()
    {
        Dictionary<string, object> data = new() { ["IsActive"] = true };

        bool result = Evaluate("IsActive = false", data);

        Assert.False(result);
    }

    [Fact]
    public void Evaluate_ExplicitBooleanEqualsTrue_WithNestedPath_ReturnsTrue()
    {
        Dictionary<string, object> data = new()
        {
            ["Config"] = new Dictionary<string, object>
            {
                ["Debug"] = true
            }
        };

        bool result = Evaluate("Config.Debug = true", data);

        Assert.True(result);
    }

    [Fact]
    public void Evaluate_ExplicitBooleanEqualsFalse_WithNestedPath_ReturnsTrue()
    {
        Dictionary<string, object> data = new()
        {
            ["Config"] = new Dictionary<string, object>
            {
                ["Debug"] = false
            }
        };

        bool result = Evaluate("Config.Debug = false", data);

        Assert.True(result);
    }

    [Fact]
    public void Evaluate_StringComparison_RemainsCaseSensitive()
    {
        Dictionary<string, object> data = new() { ["Status"] = "Active" };

        Assert.True(Evaluate("Status = Active", data));
        Assert.False(Evaluate("Status = active", data));
    }

    #endregion

    #region String Evaluation

    [Fact]
    public void Evaluate_WithStringTrue_ReturnsTrue()
    {
        Dictionary<string, object> data = new() { ["Value"] = "true" };

        bool result = Evaluate("Value", data);

        Assert.True(result);
    }

    [Fact]
    public void Evaluate_WithStringFalse_ReturnsFalse()
    {
        Dictionary<string, object> data = new() { ["Value"] = "false" };

        bool result = Evaluate("Value", data);

        Assert.False(result);
    }

    [Fact]
    public void Evaluate_WithString1_ReturnsTrue()
    {
        Dictionary<string, object> data = new() { ["Value"] = "1" };

        bool result = Evaluate("Value", data);

        Assert.True(result);
    }

    [Fact]
    public void Evaluate_WithString0_ReturnsFalse()
    {
        Dictionary<string, object> data = new() { ["Value"] = "0" };

        bool result = Evaluate("Value", data);

        Assert.False(result);
    }

    [Fact]
    public void Evaluate_WithEmptyString_ReturnsFalse()
    {
        Dictionary<string, object> data = new() { ["Value"] = "" };

        bool result = Evaluate("Value", data);

        Assert.False(result);
    }

    [Fact]
    public void Evaluate_WithWhitespaceString_ReturnsFalse()
    {
        Dictionary<string, object> data = new() { ["Value"] = "   " };

        bool result = Evaluate("Value", data);

        Assert.False(result);
    }

    [Fact]
    public void Evaluate_WithNonEmptyString_ReturnsTrue()
    {
        Dictionary<string, object> data = new() { ["Value"] = "Active" };

        bool result = Evaluate("Value", data);

        Assert.True(result);
    }

    #endregion

    #region Integer Evaluation

    [Fact]
    public void Evaluate_WithInt0_ReturnsFalse()
    {
        Dictionary<string, object> data = new() { ["Value"] = 0 };

        bool result = Evaluate("Value", data);

        Assert.False(result);
    }

    [Fact]
    public void Evaluate_WithInt1_ReturnsTrue()
    {
        Dictionary<string, object> data = new() { ["Value"] = 1 };

        bool result = Evaluate("Value", data);

        Assert.True(result);
    }

    [Fact]
    public void Evaluate_WithPositiveInt_ReturnsTrue()
    {
        Dictionary<string, object> data = new() { ["Value"] = 42 };

        bool result = Evaluate("Value", data);

        Assert.True(result);
    }

    #endregion

    #region Collection Evaluation

    [Fact]
    public void Evaluate_WithEmptyList_ReturnsFalse()
    {
        Dictionary<string, object> data = new() { ["Items"] = new List<string>() };

        bool result = Evaluate("Items", data);

        Assert.False(result);
    }

    [Fact]
    public void Evaluate_WithNonEmptyList_ReturnsTrue()
    {
        Dictionary<string, object> data = new() { ["Items"] = new List<string> { "A", "B" } };

        bool result = Evaluate("Items", data);

        Assert.True(result);
    }

    #endregion

    #region Equality Operator (=)

    [Fact]
    public void Evaluate_EqOperator_WithMatchingStrings_ReturnsTrue()
    {
        Dictionary<string, object> data = new() { ["Status"] = "Active" };

        bool result = Evaluate("Status = Active", data);

        Assert.True(result);
    }

    [Fact]
    public void Evaluate_EqOperator_WithMatchingQuotedStrings_ReturnsTrue()
    {
        Dictionary<string, object> data = new() { ["Status"] = "In Progress" };

        bool result = Evaluate("Status = \"In Progress\"", data);

        Assert.True(result);
    }

    [Fact]
    public void Evaluate_EqOperator_WithNonMatchingStrings_ReturnsFalse()
    {
        Dictionary<string, object> data = new() { ["Status"] = "Active" };

        bool result = Evaluate("Status = Inactive", data);

        Assert.False(result);
    }

    [Fact]
    public void Evaluate_EqOperator_WithMatchingNumbers_ReturnsTrue()
    {
        Dictionary<string, object> data = new() { ["Count"] = 5 };

        bool result = Evaluate("Count = 5", data);

        Assert.True(result);
    }

    #endregion

    #region Not Equal Operator (!=)

    [Fact]
    public void Evaluate_NeOperator_WithDifferentValues_ReturnsTrue()
    {
        Dictionary<string, object> data = new() { ["Status"] = "Active" };

        bool result = Evaluate("Status != Deleted", data);

        Assert.True(result);
    }

    [Fact]
    public void Evaluate_NeOperator_WithSameValues_ReturnsFalse()
    {
        Dictionary<string, object> data = new() { ["Status"] = "Active" };

        bool result = Evaluate("Status != Active", data);

        Assert.False(result);
    }

    #endregion

    #region Greater Than Operator (>)

    [Fact]
    public void Evaluate_GtOperator_WithGreaterValue_ReturnsTrue()
    {
        Dictionary<string, object> data = new() { ["Count"] = 10 };

        bool result = Evaluate("Count > 5", data);

        Assert.True(result);
    }

    [Fact]
    public void Evaluate_GtOperator_WithEqualValue_ReturnsFalse()
    {
        Dictionary<string, object> data = new() { ["Count"] = 5 };

        bool result = Evaluate("Count > 5", data);

        Assert.False(result);
    }

    [Fact]
    public void Evaluate_GtOperator_WithLesserValue_ReturnsFalse()
    {
        Dictionary<string, object> data = new() { ["Count"] = 3 };

        bool result = Evaluate("Count > 5", data);

        Assert.False(result);
    }

    #endregion

    #region Less Than Operator (<)

    [Fact]
    public void Evaluate_LtOperator_WithLesserValue_ReturnsTrue()
    {
        Dictionary<string, object> data = new() { ["Count"] = 3 };

        bool result = Evaluate("Count < 5", data);

        Assert.True(result);
    }

    [Fact]
    public void Evaluate_LtOperator_WithGreaterValue_ReturnsFalse()
    {
        Dictionary<string, object> data = new() { ["Count"] = 10 };

        bool result = Evaluate("Count < 5", data);

        Assert.False(result);
    }

    #endregion

    #region Greater or Equal Operator (>=)

    [Fact]
    public void Evaluate_GteOperator_WithGreaterValue_ReturnsTrue()
    {
        Dictionary<string, object> data = new() { ["Count"] = 10 };

        bool result = Evaluate("Count >= 5", data);

        Assert.True(result);
    }

    [Fact]
    public void Evaluate_GteOperator_WithEqualValue_ReturnsTrue()
    {
        Dictionary<string, object> data = new() { ["Count"] = 5 };

        bool result = Evaluate("Count >= 5", data);

        Assert.True(result);
    }

    [Fact]
    public void Evaluate_GteOperator_WithLesserValue_ReturnsFalse()
    {
        Dictionary<string, object> data = new() { ["Count"] = 3 };

        bool result = Evaluate("Count >= 5", data);

        Assert.False(result);
    }

    #endregion

    #region Less or Equal Operator (<=)

    [Fact]
    public void Evaluate_LteOperator_WithLesserValue_ReturnsTrue()
    {
        Dictionary<string, object> data = new() { ["Count"] = 3 };

        bool result = Evaluate("Count <= 5", data);

        Assert.True(result);
    }

    [Fact]
    public void Evaluate_LteOperator_WithEqualValue_ReturnsTrue()
    {
        Dictionary<string, object> data = new() { ["Count"] = 5 };

        bool result = Evaluate("Count <= 5", data);

        Assert.True(result);
    }

    [Fact]
    public void Evaluate_LteOperator_WithGreaterValue_ReturnsFalse()
    {
        Dictionary<string, object> data = new() { ["Count"] = 10 };

        bool result = Evaluate("Count <= 5", data);

        Assert.False(result);
    }

    #endregion

    #region AND Operator

    [Fact]
    public void Evaluate_AndOperator_WithBothTrue_ReturnsTrue()
    {
        Dictionary<string, object> data = new()
        {
            ["IsActive"] = true,
            ["IsEnabled"] = true
        };

        bool result = Evaluate("IsActive and IsEnabled", data);

        Assert.True(result);
    }

    [Fact]
    public void Evaluate_AndOperator_WithOneFalse_ReturnsFalse()
    {
        Dictionary<string, object> data = new()
        {
            ["IsActive"] = true,
            ["IsEnabled"] = false
        };

        bool result = Evaluate("IsActive and IsEnabled", data);

        Assert.False(result);
    }

    [Fact]
    public void Evaluate_AndOperator_WithComparison_ReturnsCorrectResult()
    {
        Dictionary<string, object> data = new()
        {
            ["Status"] = "Active",
            ["Count"] = 10
        };

        bool result = Evaluate("Status = Active and Count > 5", data);

        Assert.True(result);
    }

    #endregion

    #region OR Operator

    [Fact]
    public void Evaluate_OrOperator_WithBothTrue_ReturnsTrue()
    {
        Dictionary<string, object> data = new()
        {
            ["IsActive"] = true,
            ["IsEnabled"] = true
        };

        bool result = Evaluate("IsActive or IsEnabled", data);

        Assert.True(result);
    }

    [Fact]
    public void Evaluate_OrOperator_WithOneTrue_ReturnsTrue()
    {
        Dictionary<string, object> data = new()
        {
            ["IsActive"] = true,
            ["IsEnabled"] = false
        };

        bool result = Evaluate("IsActive or IsEnabled", data);

        Assert.True(result);
    }

    [Fact]
    public void Evaluate_OrOperator_WithBothFalse_ReturnsFalse()
    {
        Dictionary<string, object> data = new()
        {
            ["IsActive"] = false,
            ["IsEnabled"] = false
        };

        bool result = Evaluate("IsActive or IsEnabled", data);

        Assert.False(result);
    }

    [Fact]
    public void Evaluate_OrOperator_WithComparison_ReturnsCorrectResult()
    {
        Dictionary<string, object> data = new()
        {
            ["Status"] = "Active"
        };

        bool result = Evaluate("Status = Active or Status = Pending", data);

        Assert.True(result);
    }

    #endregion

    #region NOT Operator

    [Fact]
    public void Evaluate_NotOperator_WithTrue_ReturnsFalse()
    {
        Dictionary<string, object> data = new() { ["IsActive"] = true };

        bool result = Evaluate("not IsActive", data);

        Assert.False(result);
    }

    [Fact]
    public void Evaluate_NotOperator_WithFalse_ReturnsTrue()
    {
        Dictionary<string, object> data = new() { ["IsActive"] = false };

        bool result = Evaluate("not IsActive", data);

        Assert.True(result);
    }

    [Fact]
    public void Evaluate_NotOperator_WithComparison_ReturnsCorrectResult()
    {
        Dictionary<string, object> data = new() { ["Status"] = "Active" };

        bool result = Evaluate("not Status = Deleted", data);

        Assert.True(result);
    }

    #endregion

    #region Complex Expressions

    [Fact]
    public void Evaluate_ComplexExpression_MultipleAnds_ReturnsCorrectResult()
    {
        Dictionary<string, object> data = new()
        {
            ["Status"] = "Active",
            ["Count"] = 10,
            ["IsEnabled"] = true
        };

        bool result = Evaluate("Status = Active and Count > 5 and IsEnabled", data);

        Assert.True(result);
    }

    [Fact]
    public void Evaluate_ComplexExpression_MixedOperators_ReturnsCorrectResult()
    {
        Dictionary<string, object> data = new()
        {
            ["Status"] = "Active",
            ["Count"] = 3
        };

        bool result = Evaluate("Status = Active and Count > 5 or Count < 10", data);

        Assert.True(result); // True because Count < 10
    }

    [Fact]
    public void Evaluate_ComplexExpression_RangeCheck_ReturnsCorrectResult()
    {
        Dictionary<string, object> data = new() { ["Price"] = 150m };

        bool result = Evaluate("Price > 100 and Price < 200", data);

        Assert.True(result);
    }

    #endregion

    #region Nested Properties

    [Fact]
    public void Evaluate_WithNestedProperty_ReturnsCorrectResult()
    {
        Dictionary<string, object> data = new()
        {
            ["Customer"] = new
            {
                Address = new
                {
                    Country = "Germany"
                }
            }
        };

        bool result = Evaluate("Customer.Address.Country = Germany", data);

        Assert.True(result);
    }

    [Fact]
    public void Evaluate_WithNestedPropertyAndQuotedValue_ReturnsCorrectResult()
    {
        Dictionary<string, object> data = new()
        {
            ["Customer"] = new
            {
                Name = "Acme Corporation"
            }
        };

        bool result = Evaluate("Customer.Name = \"Acme Corporation\"", data);

        Assert.True(result);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Evaluate_WithEmptyExpression_ReturnsFalse()
    {
        Dictionary<string, object> data = new();

        bool result = Evaluate("", data);

        Assert.False(result);
    }

    [Fact]
    public void Evaluate_WithWhitespaceExpression_ReturnsFalse()
    {
        Dictionary<string, object> data = new();

        bool result = Evaluate("   ", data);

        Assert.False(result);
    }

    [Fact]
    public void Evaluate_WithDecimalComparison_ReturnsCorrectResult()
    {
        Dictionary<string, object> data = new() { ["Price"] = 99.99m };

        bool result = Evaluate("Price < 100", data);

        Assert.True(result);
    }

    #endregion

    #region Typographic Quotes

    [Fact]
    public void Evaluate_WithLeftRightCurlyQuotes_ReturnsTrue()
    {
        Dictionary<string, object> data = new() { ["Status"] = "In Progress" };

        bool result = Evaluate("Status = \u201CIn Progress\u201D", data); // U+201C and U+201D

        Assert.True(result);
    }

    [Fact]
    public void Evaluate_WithGermanQuotes_ReturnsTrue()
    {
        Dictionary<string, object> data = new() { ["Status"] = "Active" };

        bool result = Evaluate("Status = \u201EActive\u201C", data); // U+201E and U+201C

        Assert.True(result);
    }

    [Fact]
    public void Evaluate_WithCurlyQuotes_ReturnsTrue()
    {
        Dictionary<string, object> data = new() { ["Name"] = "Test" };

        bool result = Evaluate("Name = \u201CTest\u201D", data); // Curly quotes

        Assert.True(result);
    }

    [Fact]
    public void Evaluate_WithLeftSingleQuote_NormalizesToAscii()
    {
        Dictionary<string, object> data = new() { ["Name"] = "O'Connor" };

        // U+2018 (left single quote) should be normalized to ASCII apostrophe
        bool result = Evaluate("Name = \"O\u2018Connor\"", data);

        Assert.True(result);
    }

    [Fact]
    public void Evaluate_WithRightSingleQuote_NormalizesToAscii()
    {
        Dictionary<string, object> data = new() { ["Name"] = "O'Connor" };

        // U+2019 (right single quote) should be normalized to ASCII apostrophe
        bool result = Evaluate("Name = \"O\u2019Connor\"", data);

        Assert.True(result);
    }

    #endregion

    #region Deep Nested Dictionary Path Resolution

    [Fact]
    public void Evaluate_DeepNestedDictionaryPath_WithBoolTrue_ReturnsTrue()
    {
        // Test deeply nested dictionary paths with boolean values
        Dictionary<string, object> data = new()
        {
            ["config"] = new Dictionary<string, object>
            {
                ["options"] = new Dictionary<string, object>
                {
                    ["items"] = new Dictionary<string, object>
                    {
                        ["feature1"] = new Dictionary<string, object>
                        {
                            ["responses"] = new Dictionary<string, object>
                            {
                                ["options"] = new Dictionary<string, object>
                                {
                                    ["items"] = new Dictionary<string, object>
                                    {
                                        ["optionA"] = new Dictionary<string, object>
                                        {
                                            ["enabled"] = true
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        };

        string path = "config.options.items.feature1.responses.options.items.optionA.enabled";

        bool result = Evaluate(path, data);

        Assert.True(result);
    }

    [Fact]
    public void Evaluate_DeepNestedDictionaryPath_FromJsonParsedData_ReturnsTrue()
    {
        // Test with JSON-parsed data structure (Dictionary<string, object> at all levels)
        string json = @"{
            ""config"": {
                ""options"": {
                    ""items"": {
                        ""feature1"": {
                            ""responses"": {
                                ""options"": {
                                    ""items"": {
                                        ""optionA"": {
                                            ""enabled"": true
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }";

        Dictionary<string, object> data = JsonDataParser.ParseJsonToDataDictionary(json);

        string path = "config.options.items.feature1.responses.options.items.optionA.enabled";

        bool result = Evaluate(path, data);

        Assert.True(result);
    }

    [Fact]
    public void Evaluate_DeepNestedPath_InLoopContext_WhenItemHasSamePath_ReturnsTrue()
    {
        // This test simulates the scenario where:
        // 1. Root data has a deeply nested config path
        // 2. We're iterating over data.assets.items
        // 3. Each loop item ALSO has the same nested config structure
        // 4. The conditional should resolve correctly from the loop item context

        string json = @"{
            ""config"": {
                ""options"": {
                    ""items"": {
                        ""feature1"": {
                            ""responses"": {
                                ""options"": {
                                    ""items"": {
                                        ""optionA"": {
                                            ""enabled"": true
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            },
            ""data"": {
                ""assets"": {
                    ""items"": [
                        {
                            ""name"": ""TestAsset"",
                            ""config"": {
                                ""options"": {
                                    ""items"": {
                                        ""feature1"": {
                                            ""responses"": {
                                                ""options"": {
                                                    ""items"": {
                                                        ""optionA"": {
                                                            ""enabled"": true
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    ]
                }
            }
        }";

        Dictionary<string, object> data = JsonDataParser.ParseJsonToDataDictionary(json);

        // Get the first asset item
        var dataObj = (Dictionary<string, object>)data["data"];
        var assets = (Dictionary<string, object>)dataObj["assets"];
        var items = (List<object>)assets["items"];
        var firstItem = (Dictionary<string, object>)items[0];

        // Create the evaluation context chain (simulating loop context)
        GlobalEvaluationContext globalContext = new GlobalEvaluationContext(data);

        // Create LoopContext and LoopEvaluationContext using reflection
        Type loopContextType = typeof(DocumentTemplateProcessor).Assembly
            .GetType("TriasDev.Templify.Loops.LoopContext")!;
        Type loopEvalContextType = typeof(DocumentTemplateProcessor).Assembly
            .GetType("TriasDev.Templify.Loops.LoopEvaluationContext")!;

        object loopContext = Activator.CreateInstance(
            loopContextType,
            new object[] { firstItem, 0, 1, "data.assets.items", null!, null! })!;

        IEvaluationContext loopEvalContext = (IEvaluationContext)Activator.CreateInstance(
            loopEvalContextType,
            new object[] { loopContext, globalContext })!;

        // Now evaluate the condition using LoopEvaluationContext
        string path = "config.options.items.feature1.responses.options.items.optionA.enabled";
        bool result = (bool)_evaluateMethod.Invoke(_evaluator, new object[] { path, loopEvalContext })!;

        Assert.True(result);
    }

    #endregion
}
