// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using TriasDev.Templify.Core;
using TriasDev.Templify.Conditionals;
using TriasDev.Templify.Loops;
using TriasDev.Templify.Placeholders;
using TriasDev.Templify.PropertyPaths;
using TriasDev.Templify.Utilities;
using System.Reflection;

namespace TriasDev.Templify.Tests;

public class ValueResolverTests
{
    private static readonly Type _valueResolverType = typeof(DocumentTemplateProcessor).Assembly
        .GetType("TriasDev.Templify.Placeholders.ValueResolver")!;

    private readonly object _resolver;
    private readonly MethodInfo _tryResolveMethod;

    public ValueResolverTests()
    {
        _resolver = Activator.CreateInstance(_valueResolverType)!;
        _tryResolveMethod = _valueResolverType.GetMethod("TryResolveValue", BindingFlags.Public | BindingFlags.Instance)!;
    }

    private bool TryResolveValue(Dictionary<string, object> data, string variablePath, out object? value)
    {
        object[] parameters = new object[] { data, variablePath, null! };
        bool result = (bool)_tryResolveMethod.Invoke(_resolver, parameters)!;
        value = parameters[2];
        return result;
    }

    [Fact]
    public void TryResolveValue_WithSimpleVariable_ReturnsTrue()
    {
        // Arrange
        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Name"] = "John Doe"
        };

        // Act
        bool result = TryResolveValue(data, "Name", out object? value);

        // Assert
        Assert.True(result);
        Assert.Equal("John Doe", value);
    }

    [Fact]
    public void TryResolveValue_WithNestedPath_ReturnsTrue()
    {
        // Arrange
        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Customer"] = new Customer
            {
                Name = "Alice",
                Address = new Address { City = "Munich" }
            }
        };

        // Act
        bool result = TryResolveValue(data, "Customer.Address.City", out object? value);

        // Assert
        Assert.True(result);
        Assert.Equal("Munich", value);
    }

    [Fact]
    public void TryResolveValue_WithArrayIndexer_ReturnsTrue()
    {
        // Arrange
        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Items"] = new List<string> { "First", "Second", "Third" }
        };

        // Act
        bool result = TryResolveValue(data, "Items[1]", out object? value);

        // Assert
        Assert.True(result);
        Assert.Equal("Second", value);
    }

    [Fact]
    public void TryResolveValue_WithMissingVariable_ReturnsFalse()
    {
        // Arrange
        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Name"] = "John"
        };

        // Act
        bool result = TryResolveValue(data, "Age", out object? value);

        // Assert
        Assert.False(result);
        Assert.Null(value);
    }

    [Fact]
    public void TryResolveValue_WithMissingNestedProperty_ReturnsFalse()
    {
        // Arrange
        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Customer"] = new Customer { Name = "Alice" }
        };

        // Act
        bool result = TryResolveValue(data, "Customer.NonExistent", out object? value);

        // Assert
        Assert.False(result);
        Assert.Null(value);
    }

    [Fact]
    public void TryResolveValue_WithNullInChain_ReturnsTrue()
    {
        // Arrange - Address is null but path is valid
        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Customer"] = new Customer { Name = "Alice", Address = null }
        };

        // Act
        bool result = TryResolveValue(data, "Customer.Address.City", out object? value);

        // Assert
        // The path exists (Customer.Address exists), but Address is null
        // This should return TRUE (path is valid) with null value
        Assert.True(result);
        Assert.Null(value);
    }

    [Fact]
    public void TryResolveValue_BackwardCompatibility_SimpleLookupStillWorks()
    {
        // Arrange - Test that direct dictionary keys take precedence
        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Customer.Name"] = "Direct Value",  // This should be found first
            ["Customer"] = new Customer { Name = "Nested Value" }
        };

        // Act
        bool result = TryResolveValue(data, "Customer.Name", out object? value);

        // Assert
        Assert.True(result);
        Assert.Equal("Direct Value", value);  // Direct key lookup wins
    }

    [Fact]
    public void TryResolveValue_WithDictionary_ReturnsCorrectValue()
    {
        // Arrange
        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Settings"] = new Dictionary<string, string>
            {
                ["Theme"] = "Dark",
                ["Language"] = "de-DE"
            }
        };

        // Act
        bool resultBracket = TryResolveValue(data, "Settings[Theme]", out object? valueBracket);
        bool resultDot = TryResolveValue(data, "Settings.Theme", out object? valueDot);

        // Assert
        Assert.True(resultBracket);
        Assert.Equal("Dark", valueBracket);
        Assert.True(resultDot);
        Assert.Equal("Dark", valueDot);
    }

    [Fact]
    public void TryResolveValue_WithComplexMixedPath_ReturnsCorrectValue()
    {
        // Arrange
        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Company"] = new Company
            {
                Orders = new List<Order>
                {
                    new Order
                    {
                        Number = "ORD-001",
                        Customer = new Customer
                        {
                            Name = "Bob",
                            Address = new Address { City = "Berlin", Street = "Main St" }
                        }
                    }
                }
            }
        };

        // Act
        bool result = TryResolveValue(data, "Company.Orders[0].Customer.Address.City", out object? value);

        // Assert
        Assert.True(result);
        Assert.Equal("Berlin", value);
    }

    #region Null Value vs Missing Variable Tests

    [Fact]
    public void TryResolveValue_WithDirectNullValue_ReturnsTrue()
    {
        // Arrange - key exists with null value
        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["street2"] = null!
        };

        // Act
        bool result = TryResolveValue(data, "street2", out object? value);

        // Assert
        // Variable EXISTS (even though value is null)
        Assert.True(result);
        Assert.Null(value);
    }

    [Fact]
    public void TryResolveValue_WithNestedNullValue_ReturnsTrue()
    {
        // Arrange - nested path where the value is null
        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["address"] = new Dictionary<string, object?>
            {
                ["street1"] = "Main St",
                ["street2"] = null
            }
        };

        // Act
        bool result = TryResolveValue(data, "address.street2", out object? value);

        // Assert
        // Path exists, value is null - should return TRUE
        Assert.True(result);
        Assert.Null(value);
    }

    [Fact]
    public void TryResolveValue_WithMissingNestedKey_ReturnsFalse()
    {
        // Arrange - nested key does NOT exist
        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["address"] = new Dictionary<string, object>
            {
                ["street1"] = "Main St"
                // street2 does NOT exist
            }
        };

        // Act
        bool result = TryResolveValue(data, "address.street2", out object? value);

        // Assert
        // Key does NOT exist - should return FALSE
        Assert.False(result);
        Assert.Null(value);
    }

    #endregion

    // Test classes
    public class Customer
    {
        public string Name { get; set; } = string.Empty;
        public Address? Address { get; set; }
    }

    public class Address
    {
        public string Street { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
    }

    public class Order
    {
        public string Number { get; set; } = string.Empty;
        public Customer? Customer { get; set; }
    }

    public class Company
    {
        public List<Order> Orders { get; set; } = new();
    }
}
