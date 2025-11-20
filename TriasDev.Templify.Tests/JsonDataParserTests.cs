// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Text.Json;
using TriasDev.Templify.Utilities;

namespace TriasDev.Templify.Tests;

public class JsonDataParserTests
{
    [Fact]
    public void ParseJsonToDataDictionary_WithSimpleObject_ReturnsDictionary()
    {
        // Arrange
        string json = """
            {
                "Name": "John Doe",
                "Age": 30,
                "IsActive": true
            }
            """;

        // Act
        Dictionary<string, object> result = JsonDataParser.ParseJsonToDataDictionary(json);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Count);
        Assert.Equal("John Doe", result["Name"]);
        Assert.Equal(30, result["Age"]);
        Assert.Equal(true, result["IsActive"]);
    }

    [Fact]
    public void ParseJsonToDataDictionary_WithNestedObject_ReturnsDictionaryWithNestedDictionary()
    {
        // Arrange
        string json = """
            {
                "Customer": {
                    "Name": "Alice",
                    "Address": {
                        "City": "Munich",
                        "ZipCode": "80331"
                    }
                }
            }
            """;

        // Act
        Dictionary<string, object> result = JsonDataParser.ParseJsonToDataDictionary(json);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);

        Dictionary<string, object> customer = (Dictionary<string, object>)result["Customer"];
        Assert.Equal("Alice", customer["Name"]);

        Dictionary<string, object> address = (Dictionary<string, object>)customer["Address"];
        Assert.Equal("Munich", address["City"]);
        Assert.Equal("80331", address["ZipCode"]);
    }

    [Fact]
    public void ParseJsonToDataDictionary_WithArray_ReturnsDictionaryWithList()
    {
        // Arrange
        string json = """
            {
                "Items": ["Apple", "Banana", "Cherry"]
            }
            """;

        // Act
        Dictionary<string, object> result = JsonDataParser.ParseJsonToDataDictionary(json);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);

        List<object> items = (List<object>)result["Items"];
        Assert.Equal(3, items.Count);
        Assert.Equal("Apple", items[0]);
        Assert.Equal("Banana", items[1]);
        Assert.Equal("Cherry", items[2]);
    }

    [Fact]
    public void ParseJsonToDataDictionary_WithArrayOfObjects_ReturnsDictionaryWithListOfDictionaries()
    {
        // Arrange
        string json = """
            {
                "LineItems": [
                    { "Product": "Widget", "Quantity": 2 },
                    { "Product": "Gadget", "Quantity": 5 }
                ]
            }
            """;

        // Act
        Dictionary<string, object> result = JsonDataParser.ParseJsonToDataDictionary(json);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);

        List<object> lineItems = (List<object>)result["LineItems"];
        Assert.Equal(2, lineItems.Count);

        Dictionary<string, object> firstItem = (Dictionary<string, object>)lineItems[0];
        Assert.Equal("Widget", firstItem["Product"]);
        Assert.Equal(2, firstItem["Quantity"]);

        Dictionary<string, object> secondItem = (Dictionary<string, object>)lineItems[1];
        Assert.Equal("Gadget", secondItem["Product"]);
        Assert.Equal(5, secondItem["Quantity"]);
    }

    [Fact]
    public void ParseJsonToDataDictionary_WithNull_ReturnsNullValue()
    {
        // Arrange
        string json = """
            {
                "Value": null
            }
            """;

        // Act
        Dictionary<string, object> result = JsonDataParser.ParseJsonToDataDictionary(json);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Null(result["Value"]);
    }

    [Fact]
    public void ParseJsonToDataDictionary_WithNumericTypes_PreservesNumericTypes()
    {
        // Arrange
        string json = """
            {
                "Integer": 42,
                "Long": 9876543210,
                "Decimal": 123.45,
                "Double": 3.14159
            }
            """;

        // Act
        Dictionary<string, object> result = JsonDataParser.ParseJsonToDataDictionary(json);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(4, result.Count);

        // Integer should be parsed as int
        Assert.IsType<int>(result["Integer"]);
        Assert.Equal(42, result["Integer"]);

        // Long should be parsed as long
        Assert.IsType<long>(result["Long"]);
        Assert.Equal(9876543210L, result["Long"]);

        // Decimal numbers should preserve precision
        Assert.True(result["Decimal"] is decimal || result["Decimal"] is double);
        Assert.True(result["Double"] is decimal || result["Double"] is double);
    }

    [Fact]
    public void ParseJsonToDataDictionary_WithBooleanValues_ReturnsBoolean()
    {
        // Arrange
        string json = """
            {
                "IsActive": true,
                "IsDeleted": false
            }
            """;

        // Act
        Dictionary<string, object> result = JsonDataParser.ParseJsonToDataDictionary(json);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.True((bool)result["IsActive"]);
        Assert.False((bool)result["IsDeleted"]);
    }

    [Fact]
    public void ParseJsonToDataDictionary_WithEmptyObject_ReturnsEmptyDictionary()
    {
        // Arrange
        string json = "{}";

        // Act
        Dictionary<string, object> result = JsonDataParser.ParseJsonToDataDictionary(json);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public void ParseJsonToDataDictionary_WithNullString_ThrowsArgumentNullException()
    {
        // Arrange
        string? json = null;

        // Act & Assert
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() =>
            JsonDataParser.ParseJsonToDataDictionary(json!));

        Assert.Equal("jsonString", exception.ParamName);
    }

    [Fact]
    public void ParseJsonToDataDictionary_WithEmptyString_ThrowsArgumentException()
    {
        // Arrange
        string json = "";

        // Act & Assert
        ArgumentException exception = Assert.Throws<ArgumentException>(() =>
            JsonDataParser.ParseJsonToDataDictionary(json));

        Assert.Equal("jsonString", exception.ParamName);
    }

    [Fact]
    public void ParseJsonToDataDictionary_WithWhitespaceString_ThrowsArgumentException()
    {
        // Arrange
        string json = "   ";

        // Act & Assert
        ArgumentException exception = Assert.Throws<ArgumentException>(() =>
            JsonDataParser.ParseJsonToDataDictionary(json));

        Assert.Equal("jsonString", exception.ParamName);
    }

    [Fact]
    public void ParseJsonToDataDictionary_WithInvalidJson_ThrowsJsonException()
    {
        // Arrange
        string json = "{invalid json}";

        // Act & Assert
        Assert.Throws<JsonException>(() =>
            JsonDataParser.ParseJsonToDataDictionary(json));
    }

    [Fact]
    public void ParseJsonToDataDictionary_WithRootArray_ThrowsJsonException()
    {
        // Arrange
        string json = """
            ["item1", "item2", "item3"]
            """;

        // Act & Assert
        JsonException exception = Assert.Throws<JsonException>(() =>
            JsonDataParser.ParseJsonToDataDictionary(json));

        Assert.Contains("JSON root must be an object", exception.Message);
        Assert.Contains("Array", exception.Message);
    }

    [Fact]
    public void ParseJsonToDataDictionary_WithRootString_ThrowsJsonException()
    {
        // Arrange
        string json = "\"just a string\"";

        // Act & Assert
        JsonException exception = Assert.Throws<JsonException>(() =>
            JsonDataParser.ParseJsonToDataDictionary(json));

        Assert.Contains("JSON root must be an object", exception.Message);
    }

    [Fact]
    public void ParseJsonToDataDictionary_WithRootNumber_ThrowsJsonException()
    {
        // Arrange
        string json = "42";

        // Act & Assert
        JsonException exception = Assert.Throws<JsonException>(() =>
            JsonDataParser.ParseJsonToDataDictionary(json));

        Assert.Contains("JSON root must be an object", exception.Message);
    }

    [Fact]
    public void ParseJsonToDataDictionary_WithRootBoolean_ThrowsJsonException()
    {
        // Arrange
        string json = "true";

        // Act & Assert
        JsonException exception = Assert.Throws<JsonException>(() =>
            JsonDataParser.ParseJsonToDataDictionary(json));

        Assert.Contains("JSON root must be an object", exception.Message);
    }

    [Fact]
    public void ParseJsonToDataDictionary_WithComplexNestedStructure_ParsesCorrectly()
    {
        // Arrange
        string json = """
            {
                "Company": {
                    "Name": "TriasDev GmbH & Co. KG",
                    "Employees": [
                        {
                            "Name": "Alice",
                            "Skills": ["C#", "Angular", "SQL"]
                        },
                        {
                            "Name": "Bob",
                            "Skills": ["Python", "Docker"]
                        }
                    ],
                    "Settings": {
                        "AllowRemote": true,
                        "MaxVacationDays": 30
                    }
                }
            }
            """;

        // Act
        Dictionary<string, object> result = JsonDataParser.ParseJsonToDataDictionary(json);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);

        Dictionary<string, object> company = (Dictionary<string, object>)result["Company"];
        Assert.Equal("TriasDev GmbH & Co. KG", company["Name"]);

        List<object> employees = (List<object>)company["Employees"];
        Assert.Equal(2, employees.Count);

        Dictionary<string, object> alice = (Dictionary<string, object>)employees[0];
        Assert.Equal("Alice", alice["Name"]);

        List<object> aliceSkills = (List<object>)alice["Skills"];
        Assert.Equal(3, aliceSkills.Count);
        Assert.Equal("C#", aliceSkills[0]);

        Dictionary<string, object> settings = (Dictionary<string, object>)company["Settings"];
        Assert.True((bool)settings["AllowRemote"]);
        Assert.Equal(30, settings["MaxVacationDays"]);
    }
}
