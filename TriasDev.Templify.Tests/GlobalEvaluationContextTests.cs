// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using TriasDev.Templify.Core;

namespace TriasDev.Templify.Tests;

public class GlobalEvaluationContextTests
{
    [Fact]
    public void Constructor_WithValidData_Succeeds()
    {
        // Arrange
        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Name"] = "Test"
        };

        // Act
        GlobalEvaluationContext context = new GlobalEvaluationContext(data);

        // Assert
        Assert.NotNull(context);
    }

    [Fact]
    public void Constructor_WithNullData_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new GlobalEvaluationContext(null!));
    }

    [Fact]
    public void TryResolveVariable_WithSimpleVariable_ReturnsTrue()
    {
        // Arrange
        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Name"] = "John Doe",
            ["Age"] = 30
        };
        GlobalEvaluationContext context = new GlobalEvaluationContext(data);

        // Act
        bool result = context.TryResolveVariable("Name", out object? value);

        // Assert
        Assert.True(result);
        Assert.Equal("John Doe", value);
    }

    [Fact]
    public void TryResolveVariable_WithNestedProperty_ReturnsTrue()
    {
        // Arrange
        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Customer"] = new
            {
                Name = "John Doe",
                Address = new
                {
                    City = "New York",
                    ZipCode = "10001"
                }
            }
        };
        GlobalEvaluationContext context = new GlobalEvaluationContext(data);

        // Act
        bool result = context.TryResolveVariable("Customer.Address.City", out object? value);

        // Assert
        Assert.True(result);
        Assert.Equal("New York", value);
    }

    [Fact]
    public void TryResolveVariable_WithArrayIndex_ReturnsTrue()
    {
        // Arrange
        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Items"] = new List<string> { "Apple", "Banana", "Cherry" }
        };
        GlobalEvaluationContext context = new GlobalEvaluationContext(data);

        // Act
        bool result = context.TryResolveVariable("Items[1]", out object? value);

        // Assert
        Assert.True(result);
        Assert.Equal("Banana", value);
    }

    [Fact]
    public void TryResolveVariable_WithDictionaryKey_ReturnsTrue()
    {
        // Arrange
        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Settings"] = new Dictionary<string, object>
            {
                ["Theme"] = "Dark",
                ["Language"] = "English"
            }
        };
        GlobalEvaluationContext context = new GlobalEvaluationContext(data);

        // Act
        bool result = context.TryResolveVariable("Settings[Theme]", out object? value);

        // Assert
        Assert.True(result);
        Assert.Equal("Dark", value);
    }

    [Fact]
    public void TryResolveVariable_WithMissingVariable_ReturnsFalse()
    {
        // Arrange
        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Name"] = "John Doe"
        };
        GlobalEvaluationContext context = new GlobalEvaluationContext(data);

        // Act
        bool result = context.TryResolveVariable("NonExistent", out object? value);

        // Assert
        Assert.False(result);
        Assert.Null(value);
    }

    [Fact]
    public void TryResolveVariable_WithMissingNestedProperty_ReturnsFalse()
    {
        // Arrange
        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Customer"] = new
            {
                Name = "John Doe"
            }
        };
        GlobalEvaluationContext context = new GlobalEvaluationContext(data);

        // Act
        bool result = context.TryResolveVariable("Customer.Address.City", out object? value);

        // Assert
        Assert.False(result);
        Assert.Null(value);
    }

    [Fact]
    public void Parent_IsAlwaysNull()
    {
        // Arrange
        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Name"] = "Test"
        };
        GlobalEvaluationContext context = new GlobalEvaluationContext(data);

        // Act & Assert
        Assert.Null(context.Parent);
    }

    [Fact]
    public void RootData_ReturnsOriginalData()
    {
        // Arrange
        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Name"] = "Test",
            ["Age"] = 30
        };
        GlobalEvaluationContext context = new GlobalEvaluationContext(data);

        // Act
        IReadOnlyDictionary<string, object> rootData = context.RootData;

        // Assert
        Assert.NotNull(rootData);
        Assert.Equal(2, rootData.Count);
        Assert.Equal("Test", rootData["Name"]);
        Assert.Equal(30, rootData["Age"]);
    }

    [Fact]
    public void TryResolveVariable_WithComplexMixedPath_ReturnsTrue()
    {
        // Arrange
        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Company"] = new
            {
                Departments = new List<object>
                {
                    new
                    {
                        Name = "Engineering",
                        Employees = new List<object>
                        {
                            new { Name = "Alice", Age = 30 },
                            new { Name = "Bob", Age = 25 }
                        }
                    }
                }
            }
        };
        GlobalEvaluationContext context = new GlobalEvaluationContext(data);

        // Act
        bool result = context.TryResolveVariable("Company.Departments[0].Employees[1].Name", out object? value);

        // Assert
        Assert.True(result);
        Assert.Equal("Bob", value);
    }
}
