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
    private static readonly Type _loopContextType = typeof(DocumentTemplateProcessor).Assembly
        .GetType("TriasDev.Templify.Loops.LoopContext")!;

    private static readonly MethodInfo _createContextsMethod = _loopContextType
        .GetMethod("CreateContexts", BindingFlags.Static | BindingFlags.Public)!;

    private static readonly PropertyInfo _currentItemProp = _loopContextType.GetProperty("CurrentItem")!;
    private static readonly PropertyInfo _indexProp = _loopContextType.GetProperty("Index")!;
    private static readonly PropertyInfo _countProp = _loopContextType.GetProperty("Count")!;
    private static readonly PropertyInfo _isFirstProp = _loopContextType.GetProperty("IsFirst")!;
    private static readonly PropertyInfo _isLastProp = _loopContextType.GetProperty("IsLast")!;
    private static readonly PropertyInfo _collectionNameProp = _loopContextType.GetProperty("CollectionName")!;

    private static readonly MethodInfo _tryResolveVariableMethod = _loopContextType
        .GetMethod("TryResolveVariable", BindingFlags.Public | BindingFlags.Instance)!;

    [Fact]
    public void CreateContexts_WithSimpleList_CreatesCorrectContexts()
    {
        // Arrange
        List<string> items = new List<string> { "First", "Second", "Third" };
        string collectionName = "Items";

        // Act
        object result = _createContextsMethod.Invoke(null, new object[] { items, collectionName, null! })!;
        IList contexts = (IList)result;

        // Assert
        Assert.Equal(3, contexts.Count);

        // Check first context
        object firstContext = contexts[0]!;
        Assert.Equal("First", _currentItemProp.GetValue(firstContext));
        Assert.Equal(0, _indexProp.GetValue(firstContext));
        Assert.Equal(3, _countProp.GetValue(firstContext));
        Assert.True((bool)_isFirstProp.GetValue(firstContext)!);
        Assert.False((bool)_isLastProp.GetValue(firstContext)!);
        Assert.Equal("Items", _collectionNameProp.GetValue(firstContext));

        // Check last context
        object lastContext = contexts[2]!;
        Assert.Equal("Third", _currentItemProp.GetValue(lastContext));
        Assert.Equal(2, _indexProp.GetValue(lastContext));
        Assert.False((bool)_isFirstProp.GetValue(lastContext)!);
        Assert.True((bool)_isLastProp.GetValue(lastContext)!);
    }

    [Fact]
    public void CreateContexts_WithEmptyCollection_ReturnsEmptyList()
    {
        // Arrange
        List<string> items = new List<string>();
        string collectionName = "Items";

        // Act
        object result = _createContextsMethod.Invoke(null, new object[] { items, collectionName, null! })!;
        IList contexts = (IList)result;

        // Assert
        Assert.Empty(contexts);
    }

    [Fact]
    public void TryResolveVariable_WithMetadata_ReturnsCorrectValues()
    {
        // Arrange
        List<string> items = new List<string> { "First", "Second" };
        object result = _createContextsMethod.Invoke(null, new object[] { items, "Items", null! })!;
        IList contexts = (IList)result;
        object context = contexts[0]!;

        // Act & Assert - @index
        object[] parameters = new object[] { "@index", null! };
        bool indexResult = (bool)_tryResolveVariableMethod.Invoke(context, parameters)!;
        Assert.True(indexResult);
        Assert.Equal(0, parameters[1]);

        // Act & Assert - @first
        parameters = new object[] { "@first", null! };
        bool firstResult = (bool)_tryResolveVariableMethod.Invoke(context, parameters)!;
        Assert.True(firstResult);
        Assert.True((bool)parameters[1]!);

        // Act & Assert - @last
        parameters = new object[] { "@last", null! };
        bool lastResult = (bool)_tryResolveVariableMethod.Invoke(context, parameters)!;
        Assert.True(lastResult);
        Assert.False((bool)parameters[1]!);

        // Act & Assert - @count
        parameters = new object[] { "@count", null! };
        bool countResult = (bool)_tryResolveVariableMethod.Invoke(context, parameters)!;
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
        object result = _createContextsMethod.Invoke(null, new object[] { items, "Items", null! })!;
        IList contexts = (IList)result;
        object context = contexts[0]!;

        // Act
        object[] parameters = new object[] { "Name", null! };
        bool success = (bool)_tryResolveVariableMethod.Invoke(context, parameters)!;

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
        object result = _createContextsMethod.Invoke(null, new object[] { items, "Customers", null! })!;
        IList contexts = (IList)result;
        object context = contexts[0]!;

        // Act
        object[] parameters = new object[] { "Address.City", null! };
        bool success = (bool)_tryResolveVariableMethod.Invoke(context, parameters)!;

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
        object result = _createContextsMethod.Invoke(null, new object[] { items, "Items", null! })!;
        IList contexts = (IList)result;
        object context = contexts[0]!;

        // Act
        object[] parameters = new object[] { "NonExistentProperty", null! };
        bool success = (bool)_tryResolveVariableMethod.Invoke(context, parameters)!;

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
