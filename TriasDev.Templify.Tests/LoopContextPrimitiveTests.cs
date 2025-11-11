using TriasDev.Templify.Core;
using TriasDev.Templify.Conditionals;
using TriasDev.Templify.Loops;
using TriasDev.Templify.Placeholders;
using TriasDev.Templify.PropertyPaths;
using TriasDev.Templify.Utilities;
using System.Collections;
using System.Reflection;

namespace TriasDev.Templify.Tests;

public class LoopContextPrimitiveTests
{
    private static readonly Type LoopContextType = typeof(DocumentTemplateProcessor).Assembly
        .GetType("TriasDev.Templify.Loops.LoopContext")!;

    private static readonly MethodInfo CreateContextsMethod = LoopContextType
        .GetMethod("CreateContexts", BindingFlags.Static | BindingFlags.Public)!;

    private static readonly MethodInfo TryResolveVariableMethod = LoopContextType
        .GetMethod("TryResolveVariable", BindingFlags.Public | BindingFlags.Instance)!;

    [Fact]
    public void TryResolveVariable_WithDot_ReturnsPrimitiveValue()
    {
        // Arrange
        List<string> items = new List<string> { "Item One", "Item Two", "Item Three" };
        object result = CreateContextsMethod.Invoke(null, new object[] { items, "Items", null })!;
        IList contexts = (IList)result;
        object firstContext = contexts[0]!;

        // Act
        object[] parameters = new object[] { ".", null! };
        bool success = (bool)TryResolveVariableMethod.Invoke(firstContext, parameters)!;

        // Assert
        Assert.True(success);
        Assert.Equal("Item One", parameters[1]);

        // Check second item
        object secondContext = contexts[1]!;
        parameters = new object[] { ".", null! };
        success = (bool)TryResolveVariableMethod.Invoke(secondContext, parameters)!;
        Assert.True(success);
        Assert.Equal("Item Two", parameters[1]);
    }

    [Fact]
    public void TryResolveVariable_WithThis_ReturnsPrimitiveValue()
    {
        // Arrange
        List<int> items = new List<int> { 10, 20, 30 };
        object result = CreateContextsMethod.Invoke(null, new object[] { items, "Numbers", null })!;
        IList contexts = (IList)result;
        object firstContext = contexts[0]!;

        // Act
        object[] parameters = new object[] { "this", null! };
        bool success = (bool)TryResolveVariableMethod.Invoke(firstContext, parameters)!;

        // Assert
        Assert.True(success);
        Assert.Equal(10, parameters[1]);
    }

    [Fact]
    public void TryResolveVariable_WithDot_WorksWithDecimals()
    {
        // Arrange
        List<decimal> items = new List<decimal> { 99.99m, 149.99m, 249.99m };
        object result = CreateContextsMethod.Invoke(null, new object[] { items, "Prices", null })!;
        IList contexts = (IList)result;
        object lastContext = contexts[2]!;

        // Act
        object[] parameters = new object[] { ".", null! };
        bool success = (bool)TryResolveVariableMethod.Invoke(lastContext, parameters)!;

        // Assert
        Assert.True(success);
        Assert.Equal(249.99m, parameters[1]);
    }
}
