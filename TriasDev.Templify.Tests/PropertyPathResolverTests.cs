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
    private static readonly Type PropertyPathType = typeof(DocumentTemplateProcessor).Assembly
        .GetType("TriasDev.Templify.PropertyPaths.PropertyPath")!;

    private static readonly Type PropertyPathResolverType = typeof(DocumentTemplateProcessor).Assembly
        .GetType("TriasDev.Templify.PropertyPaths.PropertyPathResolver")!;

    private static readonly MethodInfo ParseMethod = PropertyPathType
        .GetMethod("Parse", BindingFlags.Static | BindingFlags.Public)!;

    private static readonly MethodInfo ResolvePathMethod = PropertyPathResolverType
        .GetMethod("ResolvePath", BindingFlags.Public | BindingFlags.Static)!;

    private object? ResolvePath(object root, string pathString)
    {
        object path = ParseMethod.Invoke(null, new object[] { pathString })!;
        return ResolvePathMethod.Invoke(null, new[] { root, path });
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
}
