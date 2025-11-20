// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using TriasDev.Templify.Core;
using System.Reflection;

namespace TriasDev.Templify.Tests;

public class LoopEvaluationContextTests
{
    [Fact]
    public void Constructor_WithValidParameters_Succeeds()
    {
        // Arrange
        Dictionary<string, object> data = new Dictionary<string, object>();
        GlobalEvaluationContext globalContext = new GlobalEvaluationContext(data);
        object loopContext = CreateLoopContext("Item1", 0, 3, "Items");

        // Act
        IEvaluationContext context = CreateLoopEvaluationContext(loopContext, globalContext);

        // Assert
        Assert.NotNull(context);
    }

    [Fact]
    public void Constructor_WithNullLoopContext_ThrowsArgumentNullException()
    {
        // Arrange
        Dictionary<string, object> data = new Dictionary<string, object>();
        GlobalEvaluationContext globalContext = new GlobalEvaluationContext(data);

        // Act & Assert
        Assert.Throws<TargetInvocationException>(() => CreateLoopEvaluationContext(null!, globalContext));
    }

    [Fact]
    public void Constructor_WithNullParent_ThrowsArgumentNullException()
    {
        // Arrange
        object loopContext = CreateLoopContext("Item1", 0, 3, "Items");

        // Act & Assert
        Assert.Throws<TargetInvocationException>(() => CreateLoopEvaluationContext(loopContext, null!));
    }

    [Fact]
    public void TryResolveVariable_WithLoopMetadataIndex_ReturnsTrue()
    {
        // Arrange
        Dictionary<string, object> data = new Dictionary<string, object>();
        GlobalEvaluationContext globalContext = new GlobalEvaluationContext(data);
        object loopContext = CreateLoopContext("Item1", 2, 5, "Items");
        IEvaluationContext context = CreateLoopEvaluationContext(loopContext, globalContext);

        // Act
        bool result = context.TryResolveVariable("@index", out object? value);

        // Assert
        Assert.True(result);
        Assert.Equal(2, value);
    }

    [Fact]
    public void TryResolveVariable_WithLoopMetadataFirst_ReturnsTrue()
    {
        // Arrange
        Dictionary<string, object> data = new Dictionary<string, object>();
        GlobalEvaluationContext globalContext = new GlobalEvaluationContext(data);
        object loopContext = CreateLoopContext("Item1", 0, 5, "Items");
        IEvaluationContext context = CreateLoopEvaluationContext(loopContext, globalContext);

        // Act
        bool result = context.TryResolveVariable("@first", out object? value);

        // Assert
        Assert.True(result);
        Assert.Equal(true, value);
    }

    [Fact]
    public void TryResolveVariable_WithLoopMetadataLast_ReturnsTrue()
    {
        // Arrange
        Dictionary<string, object> data = new Dictionary<string, object>();
        GlobalEvaluationContext globalContext = new GlobalEvaluationContext(data);
        object loopContext = CreateLoopContext("Item1", 4, 5, "Items");
        IEvaluationContext context = CreateLoopEvaluationContext(loopContext, globalContext);

        // Act
        bool result = context.TryResolveVariable("@last", out object? value);

        // Assert
        Assert.True(result);
        Assert.Equal(true, value);
    }

    [Fact]
    public void TryResolveVariable_WithLoopMetadataCount_ReturnsTrue()
    {
        // Arrange
        Dictionary<string, object> data = new Dictionary<string, object>();
        GlobalEvaluationContext globalContext = new GlobalEvaluationContext(data);
        object loopContext = CreateLoopContext("Item1", 2, 5, "Items");
        IEvaluationContext context = CreateLoopEvaluationContext(loopContext, globalContext);

        // Act
        bool result = context.TryResolveVariable("@count", out object? value);

        // Assert
        Assert.True(result);
        Assert.Equal(5, value);
    }

    [Fact]
    public void TryResolveVariable_WithCurrentItemDot_ReturnsTrue()
    {
        // Arrange
        Dictionary<string, object> data = new Dictionary<string, object>();
        GlobalEvaluationContext globalContext = new GlobalEvaluationContext(data);
        object loopContext = CreateLoopContext("Apple", 0, 3, "Items");
        IEvaluationContext context = CreateLoopEvaluationContext(loopContext, globalContext);

        // Act
        bool result = context.TryResolveVariable(".", out object? value);

        // Assert
        Assert.True(result);
        Assert.Equal("Apple", value);
    }

    [Fact]
    public void TryResolveVariable_WithCurrentItemProperty_ReturnsTrue()
    {
        // Arrange
        Dictionary<string, object> data = new Dictionary<string, object>();
        GlobalEvaluationContext globalContext = new GlobalEvaluationContext(data);
        object currentItem = new
        {
            Name = "Product A",
            Price = 99.99m
        };
        object loopContext = CreateLoopContext(currentItem, 0, 3, "Products");
        IEvaluationContext context = CreateLoopEvaluationContext(loopContext, globalContext);

        // Act
        bool result = context.TryResolveVariable("Name", out object? value);

        // Assert
        Assert.True(result);
        Assert.Equal("Product A", value);
    }

    [Fact]
    public void TryResolveVariable_WithGlobalVariable_FallsBackToParent()
    {
        // Arrange
        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["CompanyName"] = "Acme Corp",
            ["Year"] = 2025
        };
        GlobalEvaluationContext globalContext = new GlobalEvaluationContext(data);
        object loopContext = CreateLoopContext("Item1", 0, 3, "Items");
        IEvaluationContext context = CreateLoopEvaluationContext(loopContext, globalContext);

        // Act
        bool result = context.TryResolveVariable("CompanyName", out object? value);

        // Assert
        Assert.True(result);
        Assert.Equal("Acme Corp", value);
    }

    [Fact]
    public void TryResolveVariable_WithNestedGlobalProperty_FallsBackToParent()
    {
        // Arrange
        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Company"] = new
            {
                Name = "Acme Corp",
                Address = new
                {
                    City = "New York"
                }
            }
        };
        GlobalEvaluationContext globalContext = new GlobalEvaluationContext(data);
        object loopContext = CreateLoopContext("Item1", 0, 3, "Items");
        IEvaluationContext context = CreateLoopEvaluationContext(loopContext, globalContext);

        // Act
        bool result = context.TryResolveVariable("Company.Address.City", out object? value);

        // Assert
        Assert.True(result);
        Assert.Equal("New York", value);
    }

    [Fact]
    public void TryResolveVariable_WithMissingVariable_ReturnsFalse()
    {
        // Arrange
        Dictionary<string, object> data = new Dictionary<string, object>();
        GlobalEvaluationContext globalContext = new GlobalEvaluationContext(data);
        object loopContext = CreateLoopContext("Item1", 0, 3, "Items");
        IEvaluationContext context = CreateLoopEvaluationContext(loopContext, globalContext);

        // Act
        bool result = context.TryResolveVariable("NonExistent", out object? value);

        // Assert
        Assert.False(result);
        Assert.Null(value);
    }

    [Fact]
    public void Parent_ReturnsGlobalContext()
    {
        // Arrange
        Dictionary<string, object> data = new Dictionary<string, object>();
        GlobalEvaluationContext globalContext = new GlobalEvaluationContext(data);
        object loopContext = CreateLoopContext("Item1", 0, 3, "Items");
        IEvaluationContext context = CreateLoopEvaluationContext(loopContext, globalContext);

        // Act & Assert
        Assert.NotNull(context.Parent);
        Assert.Same(globalContext, context.Parent);
    }

    [Fact]
    public void RootData_DelegatesToParent()
    {
        // Arrange
        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Name"] = "Test",
            ["Age"] = 30
        };
        GlobalEvaluationContext globalContext = new GlobalEvaluationContext(data);
        object loopContext = CreateLoopContext("Item1", 0, 3, "Items");
        IEvaluationContext context = CreateLoopEvaluationContext(loopContext, globalContext);

        // Act
        IReadOnlyDictionary<string, object> rootData = context.RootData;

        // Assert
        Assert.NotNull(rootData);
        Assert.Equal(2, rootData.Count);
        Assert.Equal("Test", rootData["Name"]);
        Assert.Equal(30, rootData["Age"]);
    }

    [Fact]
    public void TryResolveVariable_WithNestedLoops_ResolvesHierarchically()
    {
        // Arrange
        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["GlobalVar"] = "Global Value"
        };
        GlobalEvaluationContext globalContext = new GlobalEvaluationContext(data);

        // Outer loop context
        object outerItem = new { OuterProp = "Outer Value" };
        object outerLoopContext = CreateLoopContext(outerItem, 0, 2, "OuterItems");
        IEvaluationContext outerContext = CreateLoopEvaluationContext(outerLoopContext, globalContext);

        // Inner loop context
        object innerItem = new { InnerProp = "Inner Value" };
        object innerLoopContext = CreateLoopContext(innerItem, 1, 3, "InnerItems", outerLoopContext);
        IEvaluationContext innerContext = CreateLoopEvaluationContext(innerLoopContext, outerContext);

        // Act & Assert - Inner property
        Assert.True(innerContext.TryResolveVariable("InnerProp", out object? innerValue));
        Assert.Equal("Inner Value", innerValue);

        // Act & Assert - Outer property (from parent loop)
        Assert.True(innerContext.TryResolveVariable("OuterProp", out object? outerValue));
        Assert.Equal("Outer Value", outerValue);

        // Act & Assert - Global variable (from root)
        Assert.True(innerContext.TryResolveVariable("GlobalVar", out object? globalValue));
        Assert.Equal("Global Value", globalValue);
    }

    // Use reflection to access internal LoopContext and LoopEvaluationContext types
    private static readonly Type _loopContextType = typeof(DocumentTemplateProcessor).Assembly
        .GetType("TriasDev.Templify.Loops.LoopContext")!;

    private static readonly Type _loopEvaluationContextType = typeof(DocumentTemplateProcessor).Assembly
        .GetType("TriasDev.Templify.Loops.LoopEvaluationContext")!;

    // Helper method to create LoopContext instances using reflection
    private static object CreateLoopContext(
        object currentItem,
        int index,
        int count,
        string collectionName,
        object? parent = null)
    {
        return Activator.CreateInstance(
            _loopContextType,
            currentItem,
            index,
            count,
            collectionName,
            parent)!;
    }

    // Helper method to create LoopEvaluationContext instances using reflection
    private static IEvaluationContext CreateLoopEvaluationContext(
        object loopContext,
        IEvaluationContext parent)
    {
        return (IEvaluationContext)Activator.CreateInstance(
            _loopEvaluationContextType,
            loopContext,
            parent)!;
    }
}
