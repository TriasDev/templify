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

public class LoopContextTests
{
    [Fact]
    public void CreateContexts_WithSimpleList_CreatesCorrectContexts()
    {
        // Arrange
        List<string> items = new List<string> { "First", "Second", "Third" };
        string collectionName = "Items";

        // Act
        IReadOnlyList<LoopContext> contexts = LoopContext.CreateContexts(items, collectionName);

        // Assert
        Assert.Equal(3, contexts.Count);

        // Check first context
        LoopContext firstContext = contexts[0];
        Assert.Equal("First", firstContext.CurrentItem);
        Assert.Equal(0, firstContext.Index);
        Assert.Equal(3, firstContext.Count);
        Assert.True(firstContext.IsFirst);
        Assert.False(firstContext.IsLast);
        Assert.Equal("Items", firstContext.CollectionName);

        // Check last context
        LoopContext lastContext = contexts[2];
        Assert.Equal("Third", lastContext.CurrentItem);
        Assert.Equal(2, lastContext.Index);
        Assert.False(lastContext.IsFirst);
        Assert.True(lastContext.IsLast);
    }

    [Fact]
    public void CreateContexts_WithEmptyCollection_ReturnsEmptyList()
    {
        // Arrange
        List<string> items = new List<string>();
        string collectionName = "Items";

        // Act
        IReadOnlyList<LoopContext> contexts = LoopContext.CreateContexts(items, collectionName);

        // Assert
        Assert.Empty(contexts);
    }

    [Fact]
    public void TryResolveVariable_WithMetadata_ReturnsCorrectValues()
    {
        // Arrange
        List<string> items = new List<string> { "First", "Second" };
        IReadOnlyList<LoopContext> contexts = LoopContext.CreateContexts(items, "Items");
        LoopContext context = contexts[0];

        // Act & Assert - @index
        bool indexResult = context.TryResolveVariable("@index", out object? value);
        Assert.True(indexResult);
        Assert.Equal(0, value);

        // Act & Assert - @first
        bool firstResult = context.TryResolveVariable("@first", out value);
        Assert.True(firstResult);
        Assert.True((bool)value!);

        // Act & Assert - @last
        bool lastResult = context.TryResolveVariable("@last", out value);
        Assert.True(lastResult);
        Assert.False((bool)value!);

        // Act & Assert - @count
        bool countResult = context.TryResolveVariable("@count", out value);
        Assert.True(countResult);
        Assert.Equal(2, value);
    }

    [Fact]
    public void TryResolveVariable_WithSimpleProperty_ReturnsValue()
    {
        // Arrange
        List<TestItem> items = new List<TestItem>
        {
            new TestItem { Name = "Item1", Value = 100 }
        };
        IReadOnlyList<LoopContext> contexts = LoopContext.CreateContexts(items, "Items");
        LoopContext context = contexts[0];

        // Act
        bool success = context.TryResolveVariable("Name", out object? value);

        // Assert
        Assert.True(success);
        Assert.Equal("Item1", value);
    }

    [Fact]
    public void TryResolveVariable_WithNestedProperty_ReturnsValue()
    {
        // Arrange
        List<Customer> items = new List<Customer>
        {
            new Customer
            {
                Name = "John Doe",
                Address = new Address { City = "Munich" }
            }
        };
        IReadOnlyList<LoopContext> contexts = LoopContext.CreateContexts(items, "Customers");
        LoopContext context = contexts[0];

        // Act
        bool success = context.TryResolveVariable("Address.City", out object? value);

        // Assert
        Assert.True(success);
        Assert.Equal("Munich", value);
    }

    [Fact]
    public void TryResolveVariable_WithInvalidProperty_ReturnsFalse()
    {
        // Arrange
        List<TestItem> items = new List<TestItem>
        {
            new TestItem { Name = "Item1", Value = 100 }
        };
        IReadOnlyList<LoopContext> contexts = LoopContext.CreateContexts(items, "Items");
        LoopContext context = contexts[0];

        // Act
        bool success = context.TryResolveVariable("NonExistentProperty", out object? value);

        // Assert
        Assert.False(success);
        Assert.Null(value);
    }

    [Fact]
    public void CreateContexts_WithNullItem_CreatesContextWithNullCurrentItem()
    {
        // Arrange
        List<string?> items = new List<string?> { "a", null };

        // Act
        IReadOnlyList<LoopContext> contexts = LoopContext.CreateContexts(items, "Items");

        // Assert
        Assert.Equal(2, contexts.Count);
        Assert.Null(contexts[1].CurrentItem);
        Assert.Equal(1, contexts[1].Index);
    }

    [Fact]
    public void TryResolveVariable_NullItem_DotResolvesToNull()
    {
        // Arrange
        IReadOnlyList<LoopContext> contexts = LoopContext.CreateContexts(new List<string?> { null }, "Items");

        // Act
        bool success = contexts[0].TryResolveVariable(".", out object? value);

        // Assert
        Assert.True(success);
        Assert.Null(value);
    }

    [Fact]
    public void TryResolveVariable_NullItem_ImplicitPropertyIsNotResolved()
    {
        // Arrange: a null item has no properties, so implicit names are left to the parent scope
        IReadOnlyList<LoopContext> contexts = LoopContext.CreateContexts(new List<TestItem?> { null }, "Items");

        // Act
        bool success = contexts[0].TryResolveVariable("Name", out object? value);

        // Assert
        Assert.False(success);
    }

    [Fact]
    public void TryResolveVariable_NullItem_NamedVariablePropertyResolvesToNull()
    {
        // Arrange
        IReadOnlyList<LoopContext> contexts = LoopContext.CreateContexts(new List<TestItem?> { null }, "Items", "item");

        // Act
        bool success = contexts[0].TryResolveVariable("item.Name", out object? value);

        // Assert
        Assert.True(success);
        Assert.Null(value);
    }

    [Fact]
    public void TryResolveVariable_PropertyWithNullValue_ReturnsTrueWithNull()
    {
        // Arrange
        List<Customer> items = new List<Customer> { new Customer { Name = "A", Address = null } };
        IReadOnlyList<LoopContext> contexts = LoopContext.CreateContexts(items, "Customers");

        // Act
        bool success = contexts[0].TryResolveVariable("Address", out object? value);

        // Assert
        Assert.True(success);
        Assert.Null(value);
    }

    [Fact]
    public void TryResolveVariable_DictionaryItemWithNullValue_ReturnsTrueWithNull()
    {
        // Arrange
        List<Dictionary<string, object?>> items = new List<Dictionary<string, object?>>
        {
            new Dictionary<string, object?> { ["Notes"] = null }
        };
        IReadOnlyList<LoopContext> contexts = LoopContext.CreateContexts(items, "Items");

        // Act
        bool success = contexts[0].TryResolveVariable("Notes", out object? value);

        // Assert
        Assert.True(success);
        Assert.Null(value);
    }

    private class TestItem
    {
        public string Name { get; set; } = string.Empty;
        public int Value { get; set; }
    }

    private class Customer
    {
        public string Name { get; set; } = string.Empty;
        public Address? Address { get; set; }
    }

    private class Address
    {
        public string City { get; set; } = string.Empty;
    }
}
