// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using TriasDev.Templify.Core;
using TriasDev.Templify.Conditionals;
using TriasDev.Templify.Loops;
using TriasDev.Templify.Placeholders;
using TriasDev.Templify.PropertyPaths;
using TriasDev.Templify.Utilities;
using System.Dynamic;
using System.Reflection;
using System.Text.Json;

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
        object?[] parameters = new object?[] { root, path, null };
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

    #region Dictionary Shapes and Key Precedence (#146)

    [Fact]
    public void TryResolvePath_DictionaryKeyNamedCount_ReturnsKeyValue()
    {
        // Arrange
        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Order"] = new Dictionary<string, object> { ["Count"] = 42, ["A"] = 1 }
        };

        // Act
        bool result = new ValueResolver().TryResolveValue(data, "Order.Count", out object? value);

        // Assert
        Assert.True(result);
        Assert.Equal(42, value);
    }

    [Fact]
    public void TryResolvePath_DictionaryWithoutCountKey_ReturnsEntryCount()
    {
        // Arrange - no "Count" key: falls back to the dictionary's Count property (backward compatible)
        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Settings"] = new Dictionary<string, object> { ["A"] = 1, ["B"] = 2, ["C"] = 3 }
        };

        // Act
        bool result = new ValueResolver().TryResolveValue(data, "Settings.Count", out object? value);

        // Assert
        Assert.True(result);
        Assert.Equal(3, value);
    }

    [Fact]
    public void TryResolvePath_JsonKeyNamedValues_ReturnsKeyValue()
    {
        // Arrange
        Dictionary<string, object> data = JsonDataParser.ParseJsonToDataDictionary(
            "{\"Stats\": {\"Values\": \"abc\", \"Keys\": \"k\", \"Comparer\": \"c\"}}");
        ValueResolver resolver = new ValueResolver();

        // Act & Assert
        Assert.True(resolver.TryResolveValue(data, "Stats.Values", out object? values));
        Assert.Equal("abc", values);
        Assert.True(resolver.TryResolveValue(data, "Stats.Keys", out object? keys));
        Assert.Equal("k", keys);
        Assert.True(resolver.TryResolveValue(data, "Stats.Comparer", out object? comparer));
        Assert.Equal("c", comparer);
    }

    [Fact]
    public void TryResolvePath_ExpandoObject_Nested()
    {
        // Arrange
        dynamic customer = new ExpandoObject();
        customer.Name = "Alice";
        dynamic address = new ExpandoObject();
        address.City = "Berlin";
        customer.Address = address;
        Dictionary<string, object> data = new Dictionary<string, object> { ["Customer"] = customer };
        ValueResolver resolver = new ValueResolver();

        // Act & Assert
        Assert.True(resolver.TryResolveValue(data, "Customer.Name", out object? name));
        Assert.Equal("Alice", name);
        Assert.True(resolver.TryResolveValue(data, "Customer.Address.City", out object? city));
        Assert.Equal("Berlin", city);
        Assert.True(resolver.TryResolveValue(data, "Customer[Name]", out object? indexed));
        Assert.Equal("Alice", indexed);
        Assert.False(resolver.TryResolveValue(data, "Customer.Missing", out _));
    }

    [Fact]
    public void TryResolvePath_IReadOnlyDictionaryOnly_ResolvesKey()
    {
        // Arrange
        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["M"] = new ReadOnlyOnlyDictionary(new Dictionary<string, object> { ["X"] = "y", ["Count"] = "key" })
        };
        ValueResolver resolver = new ValueResolver();

        // Act & Assert
        Assert.True(resolver.TryResolveValue(data, "M.X", out object? x));
        Assert.Equal("y", x);
        Assert.True(resolver.TryResolveValue(data, "M[X]", out object? indexed));
        Assert.Equal("y", indexed);
        Assert.True(resolver.TryResolveValue(data, "M.Count", out object? count));
        Assert.Equal("key", count);
        Assert.False(resolver.TryResolveValue(data, "M.Missing", out _));
    }

    [Fact]
    public void TryResolvePath_IntKeyedDictionary_Indexer()
    {
        // Arrange
        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Map"] = new Dictionary<int, string> { [1] = "one", [2] = "two" }
        };
        ValueResolver resolver = new ValueResolver();

        // Act & Assert
        Assert.True(resolver.TryResolveValue(data, "Map[1]", out object? one));
        Assert.Equal("one", one);
        Assert.False(resolver.TryResolveValue(data, "Map[3]", out _));
        Assert.False(resolver.TryResolveValue(data, "Map[abc]", out _));
    }

    [Fact]
    public void TryResolvePath_EnumKeyedDictionary_Indexer()
    {
        // Arrange
        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Days"] = new Dictionary<DayOfWeek, string> { [DayOfWeek.Monday] = "Mo" }
        };

        // Act
        bool result = new ValueResolver().TryResolveValue(data, "Days[Monday]", out object? value);

        // Assert
        Assert.True(result);
        Assert.Equal("Mo", value);
    }

    [Fact]
    public void TryResolvePath_ExpandoKeyWithNullValue_ReturnsTrueWithNull()
    {
        // Arrange
        dynamic expando = new ExpandoObject();
        expando.Street2 = null;

        // Act
        bool result = TryResolvePath((object)expando, "Street2", out object? value);

        // Assert
        Assert.True(result);
        Assert.Null(value);
    }

    [Fact]
    public void TryResolvePath_PublicField_ReturnsFieldValue()
    {
        // Arrange
        FieldHolder root = new FieldHolder { Title = "Hello" };

        // Act
        bool result = TryResolvePath(root, "Title", out object? value);

        // Assert
        Assert.True(result);
        Assert.Equal("Hello", value);
    }

    [Fact]
    public void TryResolvePath_JsonElementFromDeserialize_ResolvesNestedPaths()
    {
        // Arrange
        Dictionary<string, object> data = JsonSerializer.Deserialize<Dictionary<string, object>>(
            "{\"Customer\": {\"Name\": \"Alice\", \"Age\": 30, \"Vip\": true, \"Note\": null, " +
            "\"Orders\": [{\"Id\": \"A-1\"}, {\"Id\": \"A-2\"}]}}")!;
        ValueResolver resolver = new ValueResolver();

        // Act & Assert
        Assert.True(resolver.TryResolveValue(data, "Customer.Name", out object? name));
        Assert.Equal("Alice", name);
        Assert.True(resolver.TryResolveValue(data, "Customer.Age", out object? age));
        Assert.Equal(30, age);
        Assert.True(resolver.TryResolveValue(data, "Customer.Vip", out object? vip));
        Assert.Equal(true, vip);
        Assert.True(resolver.TryResolveValue(data, "Customer.Note", out object? note));
        Assert.Null(note);
        Assert.True(resolver.TryResolveValue(data, "Customer.Orders[1].Id", out object? id));
        Assert.Equal("A-2", id);
        Assert.False(resolver.TryResolveValue(data, "Customer.Missing", out _));
        Assert.False(resolver.TryResolveValue(data, "Customer.Orders[5]", out _));

        Assert.True(resolver.TryResolveValue(data, "Customer.Orders", out object? orders));
        List<object> orderList = Assert.IsType<List<object>>(orders);
        Assert.Equal(2, orderList.Count);
    }

    public class FieldHolder
    {
        public string Title = string.Empty;
    }

    private sealed class ReadOnlyOnlyDictionary : IReadOnlyDictionary<string, object>
    {
        private readonly Dictionary<string, object> _inner;

        public ReadOnlyOnlyDictionary(Dictionary<string, object> inner) => _inner = inner;

        public object this[string key] => _inner[key];
        public IEnumerable<string> Keys => _inner.Keys;
        public IEnumerable<object> Values => _inner.Values;
        public int Count => _inner.Count;
        public bool ContainsKey(string key) => _inner.ContainsKey(key);
        public bool TryGetValue(string key, out object value) => _inner.TryGetValue(key, out value!);
        public IEnumerator<KeyValuePair<string, object>> GetEnumerator() => _inner.GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => _inner.GetEnumerator();
    }

    #endregion
}
