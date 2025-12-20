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

public class PropertyPathResolverTests
{
    private static readonly Type _propertyPathType = typeof(DocumentTemplateProcessor).Assembly
        .GetType("TriasDev.Templify.PropertyPaths.PropertyPath")!;

    private static readonly Type _propertyPathResolverType = typeof(DocumentTemplateProcessor).Assembly
        .GetType("TriasDev.Templify.PropertyPaths.PropertyPathResolver")!;

    private static readonly MethodInfo _parseMethod = _propertyPathType
        .GetMethod("Parse", BindingFlags.Static | BindingFlags.Public)!;

    private static readonly MethodInfo _resolvePathMethod = _propertyPathResolverType
        .GetMethod("ResolvePath", BindingFlags.Public | BindingFlags.Static)!;

    private static readonly MethodInfo _tryResolvePathMethod = _propertyPathResolverType
        .GetMethod("TryResolvePath", BindingFlags.Public | BindingFlags.Static)!;

    private object? ResolvePath(object root, string pathString)
    {
        object path = _parseMethod.Invoke(null, new object[] { pathString })!;
        return _resolvePathMethod.Invoke(null, new[] { root, path });
    }

    private bool TryResolvePath(object? root, string pathString, out object? value)
    {
        object path = _parseMethod.Invoke(null, new object[] { pathString })!;
        object[] parameters = new object?[] { root, path, null };
        bool result = (bool)_tryResolvePathMethod.Invoke(null, parameters)!;
        value = parameters[2];
        return result;
    }

    [Fact]
    public void ResolvePath_WithNestedObject_ReturnsCorrectValue()
    {
        // Arrange
        Customer root = new Customer
        {
            Name = "John Doe",
            Address = new Address
            {
                Street = "Main St",
                City = "Berlin"
            }
        };

        // Act
        object? result = ResolvePath(root, "Address.City");

        // Assert
        Assert.Equal("Berlin", result);
    }

    [Fact]
    public void ResolvePath_WithArrayIndex_ReturnsCorrectValue()
    {
        // Arrange
        Order root = new Order
        {
            Items = new List<string> { "Item1", "Item2", "Item3" }
        };

        // Act
        object? result = ResolvePath(root, "Items[1]");

        // Assert
        Assert.Equal("Item2", result);
    }

    [Fact]
    public void ResolvePath_WithDictionaryKey_ReturnsCorrectValue()
    {
        // Arrange
        Settings root = new Settings
        {
            Values = new Dictionary<string, string>
            {
                ["Theme"] = "Dark",
                ["Language"] = "English"
            }
        };

        // Act
        object? result = ResolvePath(root, "Values[Theme]");

        // Assert
        Assert.Equal("Dark", result);
    }

    [Fact]
    public void ResolvePath_WithMixedPath_ReturnsCorrectValue()
    {
        // Arrange
        Company root = new Company
        {
            Orders = new List<Order>
            {
                new Order { Number = "ORD-001", Customer = new Customer { Name = "Alice" } },
                new Order { Number = "ORD-002", Customer = new Customer { Name = "Bob" } }
            }
        };

        // Act
        object? result = ResolvePath(root, "Orders[1].Customer.Name");

        // Assert
        Assert.Equal("Bob", result);
    }

    [Fact]
    public void ResolvePath_WithNullInChain_ReturnsNull()
    {
        // Arrange
        Customer root = new Customer
        {
            Name = "John",
            Address = null  // Null in chain
        };

        // Act
        object? result = ResolvePath(root, "Address.City");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void ResolvePath_WithInvalidProperty_ReturnsNull()
    {
        // Arrange
        Customer root = new Customer { Name = "John" };

        // Act
        object? result = ResolvePath(root, "NonExistentProperty");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void ResolvePath_WithOutOfRangeIndex_ReturnsNull()
    {
        // Arrange
        Order root = new Order
        {
            Items = new List<string> { "Item1", "Item2" }
        };

        // Act
        object? result = ResolvePath(root, "Items[5]");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void ResolvePath_WithDictionaryDotNotation_ReturnsCorrectValue()
    {
        // Arrange
        Settings root = new Settings
        {
            Values = new Dictionary<string, string>
            {
                ["Theme"] = "Light"
            }
        };

        // Act
        object? result = ResolvePath(root, "Values.Theme");

        // Assert
        Assert.Equal("Light", result);
    }

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
        public List<string> Items { get; set; } = new();
    }

    public class Company
    {
        public List<Order> Orders { get; set; } = new();
    }

    public class Settings
    {
        public Dictionary<string, string> Values { get; set; } = new();
    }

    #region TryResolvePath Tests - Null vs Missing Distinction

    [Fact]
    public void TryResolvePath_WithNullRoot_ReturnsTrueWithNullValue()
    {
        // Arrange - root is null
        object? root = null;

        // Act
        bool result = TryResolvePath(root, "AnyPath", out object? value);

        // Assert
        // Null root is treated as "path exists but value is null"
        // This is consistent with null-in-chain behavior
        Assert.True(result);
        Assert.Null(value);
    }

    [Fact]
    public void TryResolvePath_DictionaryKeyExistsWithNullValue_ReturnsTrue()
    {
        // Arrange - key exists but value is null
        Dictionary<string, object?> data = new Dictionary<string, object?>
        {
            ["street2"] = null
        };

        // Act
        bool result = TryResolvePath(data, "street2", out object? value);

        // Assert
        Assert.True(result);   // Key EXISTS
        Assert.Null(value);    // But value is null
    }

    [Fact]
    public void TryResolvePath_DictionaryKeyDoesNotExist_ReturnsFalse()
    {
        // Arrange - key does not exist
        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["street1"] = "Main St"
        };

        // Act
        bool result = TryResolvePath(data, "street2", out object? value);

        // Assert
        Assert.False(result);  // Key does NOT exist
        Assert.Null(value);
    }

    [Fact]
    public void TryResolvePath_NestedPropertyExistsWithNullValue_ReturnsTrue()
    {
        // Arrange - nested property exists but is null
        Customer root = new Customer
        {
            Name = "John",
            Address = null  // Property exists but value is null
        };

        // Act
        bool result = TryResolvePath(root, "Address", out object? value);

        // Assert
        Assert.True(result);   // Property EXISTS
        Assert.Null(value);    // But value is null
    }

    [Fact]
    public void TryResolvePath_NestedPathWithNullInChain_ReturnsTrue()
    {
        // Arrange - Address is null, so we can't traverse further
        // But the path is valid up to Address
        Customer root = new Customer
        {
            Name = "John",
            Address = null
        };

        // Act
        bool result = TryResolvePath(root, "Address.City", out object? value);

        // Assert
        // Path exists but Address is null, so we can't get City
        // This should return true (path is valid) with null value
        Assert.True(result);
        Assert.Null(value);
    }

    [Fact]
    public void TryResolvePath_PropertyDoesNotExist_ReturnsFalse()
    {
        // Arrange
        Customer root = new Customer { Name = "John" };

        // Act
        bool result = TryResolvePath(root, "NonExistentProperty", out object? value);

        // Assert
        Assert.False(result);  // Property does NOT exist
        Assert.Null(value);
    }

    [Fact]
    public void TryResolvePath_ValidPathWithValue_ReturnsTrueWithValue()
    {
        // Arrange
        Customer root = new Customer
        {
            Name = "John",
            Address = new Address { City = "Berlin", Street = "Main St" }
        };

        // Act
        bool result = TryResolvePath(root, "Address.City", out object? value);

        // Assert
        Assert.True(result);
        Assert.Equal("Berlin", value);
    }

    #endregion
}
