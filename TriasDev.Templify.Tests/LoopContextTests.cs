// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using TriasDev.Templify.Core;
using TriasDev.Templify.Conditionals;
using TriasDev.Templify.Loops;
using TriasDev.Templify.Placeholders;
using TriasDev.Templify.PropertyPaths;
using TriasDev.Templify.Utilities;
using System.Collections;
using System.Reflection;

namespace TriasDev.Templify.Tests;

public class LoopContextTests
{
    private static readonly Type LoopContextType = typeof(DocumentTemplateProcessor).Assembly
        .GetType("TriasDev.Templify.Loops.LoopContext")!;

    private static readonly MethodInfo CreateContextsMethod = LoopContextType
        .GetMethod("CreateContexts", BindingFlags.Static | BindingFlags.Public)!;

    private static readonly PropertyInfo CurrentItemProp = LoopContextType.GetProperty("CurrentItem")!;
    private static readonly PropertyInfo IndexProp = LoopContextType.GetProperty("Index")!;
    private static readonly PropertyInfo CountProp = LoopContextType.GetProperty("Count")!;
    private static readonly PropertyInfo IsFirstProp = LoopContextType.GetProperty("IsFirst")!;
    private static readonly PropertyInfo IsLastProp = LoopContextType.GetProperty("IsLast")!;
    private static readonly PropertyInfo CollectionNameProp = LoopContextType.GetProperty("CollectionName")!;

    private static readonly MethodInfo TryResolveVariableMethod = LoopContextType
        .GetMethod("TryResolveVariable", BindingFlags.Public | BindingFlags.Instance)!;

    [Fact]
    public void CreateContexts_WithSimpleList_CreatesCorrectContexts()
    {
        // Arrange
        List<string> items = new List<string> { "First", "Second", "Third" };
        string collectionName = "Items";

        // Act
        object result = CreateContextsMethod.Invoke(null, new object[] { items, collectionName, null })!;
        IList contexts = (IList)result;

        // Assert
        Assert.Equal(3, contexts.Count);

        // Check first context
        object firstContext = contexts[0]!;
        Assert.Equal("First", CurrentItemProp.GetValue(firstContext));
        Assert.Equal(0, IndexProp.GetValue(firstContext));
        Assert.Equal(3, CountProp.GetValue(firstContext));
        Assert.True((bool)IsFirstProp.GetValue(firstContext)!);
        Assert.False((bool)IsLastProp.GetValue(firstContext)!);
        Assert.Equal("Items", CollectionNameProp.GetValue(firstContext));

        // Check last context
        object lastContext = contexts[2]!;
        Assert.Equal("Third", CurrentItemProp.GetValue(lastContext));
        Assert.Equal(2, IndexProp.GetValue(lastContext));
        Assert.False((bool)IsFirstProp.GetValue(lastContext)!);
        Assert.True((bool)IsLastProp.GetValue(lastContext)!);
    }

    [Fact]
    public void CreateContexts_WithEmptyCollection_ReturnsEmptyList()
    {
        // Arrange
        List<string> items = new List<string>();
        string collectionName = "Items";

        // Act
        object result = CreateContextsMethod.Invoke(null, new object[] { items, collectionName, null })!;
        IList contexts = (IList)result;

        // Assert
        Assert.Empty(contexts);
    }

    [Fact]
    public void TryResolveVariable_WithMetadata_ReturnsCorrectValues()
    {
        // Arrange
        List<string> items = new List<string> { "First", "Second" };
        object result = CreateContextsMethod.Invoke(null, new object[] { items, "Items", null })!;
        IList contexts = (IList)result;
        object context = contexts[0]!;

        // Act & Assert - @index
        object[] parameters = new object[] { "@index", null! };
        bool indexResult = (bool)TryResolveVariableMethod.Invoke(context, parameters)!;
        Assert.True(indexResult);
        Assert.Equal(0, parameters[1]);

        // Act & Assert - @first
        parameters = new object[] { "@first", null! };
        bool firstResult = (bool)TryResolveVariableMethod.Invoke(context, parameters)!;
        Assert.True(firstResult);
        Assert.True((bool)parameters[1]!);

        // Act & Assert - @last
        parameters = new object[] { "@last", null! };
        bool lastResult = (bool)TryResolveVariableMethod.Invoke(context, parameters)!;
        Assert.True(lastResult);
        Assert.False((bool)parameters[1]!);

        // Act & Assert - @count
        parameters = new object[] { "@count", null! };
        bool countResult = (bool)TryResolveVariableMethod.Invoke(context, parameters)!;
        Assert.True(countResult);
        Assert.Equal(2, parameters[1]);
    }

    [Fact]
    public void TryResolveVariable_WithSimpleProperty_ReturnsValue()
    {
        // Arrange
        List<TestItem> items = new List<TestItem>
        {
            new TestItem { Name = "Item1", Value = 100 }
        };
        object result = CreateContextsMethod.Invoke(null, new object[] { items, "Items", null })!;
        IList contexts = (IList)result;
        object context = contexts[0]!;

        // Act
        object[] parameters = new object[] { "Name", null! };
        bool success = (bool)TryResolveVariableMethod.Invoke(context, parameters)!;

        // Assert
        Assert.True(success);
        Assert.Equal("Item1", parameters[1]);
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
        object result = CreateContextsMethod.Invoke(null, new object[] { items, "Customers", null })!;
        IList contexts = (IList)result;
        object context = contexts[0]!;

        // Act
        object[] parameters = new object[] { "Address.City", null! };
        bool success = (bool)TryResolveVariableMethod.Invoke(context, parameters)!;

        // Assert
        Assert.True(success);
        Assert.Equal("Munich", parameters[1]);
    }

    [Fact]
    public void TryResolveVariable_WithInvalidProperty_ReturnsFalse()
    {
        // Arrange
        List<TestItem> items = new List<TestItem>
        {
            new TestItem { Name = "Item1", Value = 100 }
        };
        object result = CreateContextsMethod.Invoke(null, new object[] { items, "Items", null })!;
        IList contexts = (IList)result;
        object context = contexts[0]!;

        // Act
        object[] parameters = new object[] { "NonExistentProperty", null! };
        bool success = (bool)TryResolveVariableMethod.Invoke(context, parameters)!;

        // Assert
        Assert.False(success);
        Assert.Null(parameters[1]);
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
