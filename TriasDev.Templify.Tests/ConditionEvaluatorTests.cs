// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Text.Json;
using TriasDev.Templify.Conditionals;
using TriasDev.Templify.Core;

namespace TriasDev.Templify.Tests;

/// <summary>
/// Tests for the public <see cref="ConditionEvaluator"/> class.
/// </summary>
public class ConditionEvaluatorTests
{
    private readonly ConditionEvaluator _evaluator = new();

    #region Evaluate with Dictionary

    [Fact]
    public void Evaluate_WithTrueBoolean_ReturnsTrue()
    {
        Dictionary<string, object> data = new() { ["IsActive"] = true };

        bool result = _evaluator.Evaluate("IsActive", data);

        Assert.True(result);
    }

    [Fact]
    public void Evaluate_WithFalseBoolean_ReturnsFalse()
    {
        Dictionary<string, object> data = new() { ["IsActive"] = false };

        bool result = _evaluator.Evaluate("IsActive", data);

        Assert.False(result);
    }

    [Fact]
    public void Evaluate_WithMissingVariable_ReturnsFalse()
    {
        Dictionary<string, object> data = new();

        bool result = _evaluator.Evaluate("MissingVar", data);

        Assert.False(result);
    }

    [Fact]
    public void Evaluate_WithComparison_ReturnsCorrectResult()
    {
        Dictionary<string, object> data = new() { ["Count"] = 5 };

        Assert.True(_evaluator.Evaluate("Count > 3", data));
        Assert.True(_evaluator.Evaluate("Count = 5", data));
        Assert.False(_evaluator.Evaluate("Count < 3", data));
    }

    [Fact]
    public void Evaluate_WithStringComparison_ReturnsCorrectResult()
    {
        Dictionary<string, object> data = new() { ["Status"] = "Active" };

        bool result = _evaluator.Evaluate("Status = \"Active\"", data);

        Assert.True(result);
    }

    [Fact]
    public void Evaluate_WithLogicalOperators_ReturnsCorrectResult()
    {
        Dictionary<string, object> data = new()
        {
            ["IsEnabled"] = true,
            ["HasAccess"] = true
        };

        Assert.True(_evaluator.Evaluate("IsEnabled and HasAccess", data));
        Assert.True(_evaluator.Evaluate("IsEnabled or HasAccess", data));
    }

    [Fact]
    public void Evaluate_WithNegation_ReturnsCorrectResult()
    {
        Dictionary<string, object> data = new() { ["IsDisabled"] = false };

        bool result = _evaluator.Evaluate("not IsDisabled", data);

        Assert.True(result);
    }

    [Fact]
    public void Evaluate_WithNestedPath_ReturnsCorrectResult()
    {
        Dictionary<string, object> data = new()
        {
            ["Customer"] = new Dictionary<string, object>
            {
                ["Name"] = "John",
                ["IsActive"] = true
            }
        };

        Assert.True(_evaluator.Evaluate("Customer.IsActive", data));
        Assert.True(_evaluator.Evaluate("Customer.Name = \"John\"", data));
    }

    #endregion

    #region Evaluate with JSON

    [Fact]
    public void Evaluate_WithJsonData_ReturnsCorrectResult()
    {
        string json = """{"IsActive": true, "Count": 5}""";

        Assert.True(_evaluator.Evaluate("IsActive", json));
        Assert.True(_evaluator.Evaluate("Count > 3", json));
    }

    [Fact]
    public void Evaluate_WithComplexJsonData_ReturnsCorrectResult()
    {
        string json = """
            {
                "Customer": {
                    "Name": "John",
                    "IsActive": true
                },
                "Status": "Active"
            }
            """;

        Assert.True(_evaluator.Evaluate("Customer.IsActive", json));
        Assert.True(_evaluator.Evaluate("Status = \"Active\"", json));
    }

    [Fact]
    public void Evaluate_WithInvalidJson_ThrowsJsonException()
    {
        string invalidJson = "{ invalid json }";

        Assert.Throws<JsonException>(() => _evaluator.Evaluate("IsActive", invalidJson));
    }

    [Fact]
    public void Evaluate_WithJsonArray_ThrowsJsonException()
    {
        string jsonArray = "[1, 2, 3]";

        Assert.Throws<JsonException>(() => _evaluator.Evaluate("IsActive", jsonArray));
    }

    #endregion

    #region EvaluateAsync

    [Fact]
    public async Task EvaluateAsync_WithDictionary_ReturnsCorrectResult()
    {
        Dictionary<string, object> data = new() { ["IsActive"] = true };

        bool result = await _evaluator.EvaluateAsync("IsActive", data);

        Assert.True(result);
    }

    [Fact]
    public async Task EvaluateAsync_WithJson_ReturnsCorrectResult()
    {
        string json = """{"IsActive": true}""";

        bool result = await _evaluator.EvaluateAsync("IsActive", json);

        Assert.True(result);
    }

    [Fact]
    public async Task EvaluateAsync_WithContext_ReturnsCorrectResult()
    {
        Dictionary<string, object> data = new() { ["IsActive"] = true };
        IEvaluationContext context = _evaluator.CreateContext(data);

        bool result = await _evaluator.EvaluateAsync("IsActive", context);

        Assert.True(result);
    }

    #endregion

    #region CreateContext

    [Fact]
    public void CreateContext_WithDictionary_ReturnsValidContext()
    {
        Dictionary<string, object> data = new()
        {
            ["Name"] = "Test",
            ["Count"] = 42
        };

        IEvaluationContext context = _evaluator.CreateContext(data);

        Assert.NotNull(context);
        Assert.True(context.TryResolveVariable("Name", out object? name));
        Assert.Equal("Test", name);
        Assert.True(context.TryResolveVariable("Count", out object? count));
        Assert.Equal(42, count);
    }

    [Fact]
    public void CreateContext_WithJson_ReturnsValidContext()
    {
        string json = """{"Name": "Test", "Count": 42}""";

        IEvaluationContext context = _evaluator.CreateContext(json);

        Assert.NotNull(context);
        Assert.True(context.TryResolveVariable("Name", out object? name));
        Assert.Equal("Test", name);
    }

    [Fact]
    public void CreateContext_AllowsBatchEvaluation()
    {
        Dictionary<string, object> data = new()
        {
            ["IsActive"] = true,
            ["Count"] = 5,
            ["Status"] = "Active"
        };

        IEvaluationContext context = _evaluator.CreateContext(data);

        Assert.True(_evaluator.Evaluate("IsActive", context));
        Assert.True(_evaluator.Evaluate("Count > 3", context));
        Assert.True(_evaluator.Evaluate("Status = \"Active\"", context));
    }

    #endregion

    #region Evaluate with Context

    [Fact]
    public void Evaluate_WithContext_ReturnsCorrectResult()
    {
        Dictionary<string, object> data = new()
        {
            ["IsEnabled"] = true,
            ["Count"] = 10
        };
        IEvaluationContext context = _evaluator.CreateContext(data);

        Assert.True(_evaluator.Evaluate("IsEnabled", context));
        Assert.True(_evaluator.Evaluate("Count > 5", context));
        Assert.True(_evaluator.Evaluate("IsEnabled and Count > 5", context));
    }

    #endregion

    #region Interface Implementation

    [Fact]
    public void ConditionEvaluator_ImplementsIConditionEvaluator()
    {
        IConditionEvaluator evaluator = new ConditionEvaluator();

        Assert.NotNull(evaluator);
    }

    #endregion

    #region Null Parameter Validation

    [Fact]
    public void Evaluate_WithNullExpression_ThrowsArgumentNullException()
    {
        Dictionary<string, object> data = new() { ["Key"] = "Value" };

        Assert.Throws<ArgumentNullException>(() => _evaluator.Evaluate(null!, data));
    }

    [Fact]
    public void Evaluate_WithNullDictionary_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _evaluator.Evaluate("IsActive", (Dictionary<string, object>)null!));
    }

    [Fact]
    public void Evaluate_WithNullJsonData_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _evaluator.Evaluate("IsActive", (string)null!));
    }

    [Fact]
    public void Evaluate_WithNullContext_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _evaluator.Evaluate("IsActive", (IEvaluationContext)null!));
    }

    [Fact]
    public void CreateContext_WithNullDictionary_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _evaluator.CreateContext((Dictionary<string, object>)null!));
    }

    [Fact]
    public void CreateContext_WithNullJsonData_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _evaluator.CreateContext((string)null!));
    }

    #endregion
}
