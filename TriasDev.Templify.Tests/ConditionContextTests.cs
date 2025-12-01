// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Text.Json;
using TriasDev.Templify.Conditionals;

namespace TriasDev.Templify.Tests;

/// <summary>
/// Tests for the public <see cref="IConditionContext"/> interface and <see cref="ConditionContext"/> class.
/// </summary>
public class ConditionContextTests
{
    private readonly ConditionEvaluator _evaluator = new();

    #region CreateConditionContext with Dictionary

    [Fact]
    public void CreateConditionContext_WithDictionary_ReturnsValidContext()
    {
        Dictionary<string, object> data = new()
        {
            ["IsActive"] = true,
            ["Count"] = 5
        };

        IConditionContext context = _evaluator.CreateConditionContext(data);

        Assert.NotNull(context);
    }

    [Fact]
    public void CreateConditionContext_WithNullDictionary_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _evaluator.CreateConditionContext((Dictionary<string, object>)null!));
    }

    #endregion

    #region CreateConditionContext with JSON

    [Fact]
    public void CreateConditionContext_WithJson_ReturnsValidContext()
    {
        string json = """{"IsActive": true, "Count": 5}""";

        IConditionContext context = _evaluator.CreateConditionContext(json);

        Assert.NotNull(context);
    }

    [Fact]
    public void CreateConditionContext_WithNullJson_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _evaluator.CreateConditionContext((string)null!));
    }

    [Fact]
    public void CreateConditionContext_WithInvalidJson_ThrowsJsonException()
    {
        string invalidJson = "{ invalid json }";

        Assert.Throws<JsonException>(() => _evaluator.CreateConditionContext(invalidJson));
    }

    #endregion

    #region Evaluate

    [Fact]
    public void Evaluate_WithTrueBoolean_ReturnsTrue()
    {
        Dictionary<string, object> data = new() { ["IsActive"] = true };
        IConditionContext context = _evaluator.CreateConditionContext(data);

        bool result = context.Evaluate("IsActive");

        Assert.True(result);
    }

    [Fact]
    public void Evaluate_WithFalseBoolean_ReturnsFalse()
    {
        Dictionary<string, object> data = new() { ["IsActive"] = false };
        IConditionContext context = _evaluator.CreateConditionContext(data);

        bool result = context.Evaluate("IsActive");

        Assert.False(result);
    }

    [Fact]
    public void Evaluate_WithMissingVariable_ReturnsFalse()
    {
        Dictionary<string, object> data = new();
        IConditionContext context = _evaluator.CreateConditionContext(data);

        bool result = context.Evaluate("MissingVar");

        Assert.False(result);
    }

    [Fact]
    public void Evaluate_WithComparison_ReturnsCorrectResult()
    {
        Dictionary<string, object> data = new() { ["Count"] = 5 };
        IConditionContext context = _evaluator.CreateConditionContext(data);

        Assert.True(context.Evaluate("Count > 3"));
        Assert.True(context.Evaluate("Count = 5"));
        Assert.False(context.Evaluate("Count < 3"));
    }

    [Fact]
    public void Evaluate_WithStringComparison_ReturnsCorrectResult()
    {
        Dictionary<string, object> data = new() { ["Status"] = "Active" };
        IConditionContext context = _evaluator.CreateConditionContext(data);

        bool result = context.Evaluate("Status = \"Active\"");

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
        IConditionContext context = _evaluator.CreateConditionContext(data);

        Assert.True(context.Evaluate("IsEnabled and HasAccess"));
        Assert.True(context.Evaluate("IsEnabled or HasAccess"));
    }

    [Fact]
    public void Evaluate_WithNegation_ReturnsCorrectResult()
    {
        Dictionary<string, object> data = new() { ["IsDisabled"] = false };
        IConditionContext context = _evaluator.CreateConditionContext(data);

        bool result = context.Evaluate("not IsDisabled");

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
        IConditionContext context = _evaluator.CreateConditionContext(data);

        Assert.True(context.Evaluate("Customer.IsActive"));
        Assert.True(context.Evaluate("Customer.Name = \"John\""));
    }

    [Fact]
    public void Evaluate_WithNullExpression_ThrowsArgumentNullException()
    {
        Dictionary<string, object> data = new() { ["Key"] = "Value" };
        IConditionContext context = _evaluator.CreateConditionContext(data);

        Assert.Throws<ArgumentNullException>(() => context.Evaluate(null!));
    }

    [Fact]
    public void Evaluate_BatchEvaluation_ReusesContext()
    {
        Dictionary<string, object> data = new()
        {
            ["IsActive"] = true,
            ["Count"] = 5,
            ["Status"] = "Active"
        };
        IConditionContext context = _evaluator.CreateConditionContext(data);

        // Evaluate multiple expressions against the same context
        Assert.True(context.Evaluate("IsActive"));
        Assert.True(context.Evaluate("Count > 3"));
        Assert.True(context.Evaluate("Status = \"Active\""));
        Assert.True(context.Evaluate("IsActive and Count > 0"));
    }

    #endregion

    #region EvaluateAsync

    [Fact]
    public async Task EvaluateAsync_WithTrueBoolean_ReturnsTrue()
    {
        Dictionary<string, object> data = new() { ["IsActive"] = true };
        IConditionContext context = _evaluator.CreateConditionContext(data);

        bool result = await context.EvaluateAsync("IsActive");

        Assert.True(result);
    }

    [Fact]
    public async Task EvaluateAsync_WithComparison_ReturnsCorrectResult()
    {
        Dictionary<string, object> data = new() { ["Count"] = 10 };
        IConditionContext context = _evaluator.CreateConditionContext(data);

        Assert.True(await context.EvaluateAsync("Count > 5"));
        Assert.False(await context.EvaluateAsync("Count < 5"));
    }

    [Fact]
    public async Task EvaluateAsync_WithNullExpression_ThrowsArgumentNullException()
    {
        Dictionary<string, object> data = new() { ["Key"] = "Value" };
        IConditionContext context = _evaluator.CreateConditionContext(data);

        await Assert.ThrowsAsync<ArgumentNullException>(() => context.EvaluateAsync(null!));
    }

    [Fact]
    public async Task EvaluateAsync_WithCancelledToken_ThrowsOperationCanceledException()
    {
        Dictionary<string, object> data = new() { ["IsActive"] = true };
        IConditionContext context = _evaluator.CreateConditionContext(data);
        CancellationToken cancelledToken = new(canceled: true);

        await Assert.ThrowsAsync<OperationCanceledException>(() => context.EvaluateAsync("IsActive", cancelledToken));
    }

    #endregion

    #region JSON Data

    [Fact]
    public void Evaluate_WithJsonData_ReturnsCorrectResult()
    {
        string json = """{"IsActive": true, "Count": 5}""";
        IConditionContext context = _evaluator.CreateConditionContext(json);

        Assert.True(context.Evaluate("IsActive"));
        Assert.True(context.Evaluate("Count > 3"));
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
        IConditionContext context = _evaluator.CreateConditionContext(json);

        Assert.True(context.Evaluate("Customer.IsActive"));
        Assert.True(context.Evaluate("Status = \"Active\""));
    }

    #endregion

    #region Interface Implementation

    [Fact]
    public void ConditionContext_ImplementsIConditionContext()
    {
        Dictionary<string, object> data = new() { ["Key"] = "Value" };
        IConditionContext context = _evaluator.CreateConditionContext(data);

        Assert.IsAssignableFrom<IConditionContext>(context);
    }

    #endregion
}
