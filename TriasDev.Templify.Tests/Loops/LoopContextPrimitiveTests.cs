// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using TriasDev.Templify.Core;
using TriasDev.Templify.Conditionals;
using TriasDev.Templify.Loops;
using TriasDev.Templify.Placeholders;
using TriasDev.Templify.PropertyPaths;
using TriasDev.Templify.Utilities;
using System.Collections;

namespace TriasDev.Templify.Tests.Loops;

public class LoopContextPrimitiveTests
{
    [Fact]
    public void TryResolveVariable_WithDot_ReturnsPrimitiveValue()
    {
        // Arrange
        List<string> items = new List<string> { "Item One", "Item Two", "Item Three" };
        IReadOnlyList<LoopContext> contexts = LoopContext.CreateContexts(items, "Items");
        LoopContext firstContext = contexts[0];

        // Act
        bool success = firstContext.TryResolveVariable(".", out object? value);

        // Assert
        Assert.True(success);
        Assert.Equal("Item One", value);

        // Check second item
        LoopContext secondContext = contexts[1];
        success = secondContext.TryResolveVariable(".", out value);
        Assert.True(success);
        Assert.Equal("Item Two", value);
    }

    [Fact]
    public void TryResolveVariable_WithThis_ReturnsPrimitiveValue()
    {
        // Arrange
        List<int> items = new List<int> { 10, 20, 30 };
        IReadOnlyList<LoopContext> contexts = LoopContext.CreateContexts(items, "Numbers");
        LoopContext firstContext = contexts[0];

        // Act
        bool success = firstContext.TryResolveVariable("this", out object? value);

        // Assert
        Assert.True(success);
        Assert.Equal(10, value);
    }

    [Fact]
    public void TryResolveVariable_WithDot_WorksWithDecimals()
    {
        // Arrange
        List<decimal> items = new List<decimal> { 99.99m, 149.99m, 249.99m };
        IReadOnlyList<LoopContext> contexts = LoopContext.CreateContexts(items, "Prices");
        LoopContext lastContext = contexts[2];

        // Act
        bool success = lastContext.TryResolveVariable(".", out object? value);

        // Assert
        Assert.True(success);
        Assert.Equal(249.99m, value);
    }
}
