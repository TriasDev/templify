// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Dynamic;
using System.Globalization;
using System.Text.Json;
using TriasDev.Templify.Placeholders;

namespace TriasDev.Templify.Tests.Placeholders;

public sealed class JoinFormatTests
{
    [Theory]
    [InlineData("join", null, ", ")]
    [InlineData("JOIN", null, ", ")]
    [InlineData("join:Name", "Name", ", ")]
    [InlineData("join:Owner.Name", "Owner.Name", ", ")]
    [InlineData("join:\" / \"", null, " / ")]
    [InlineData("join:\"\"", null, "")]
    [InlineData("join:Name:\"; \"", "Name", "; ")]
    [InlineData("join:„ und “", null, " und ")]
    [InlineData("join:\"a\\\"b\\\\\"", null, "a\"b\\")]
    public void TryParse_ValidFormat_ReturnsPathAndSeparator(string format, string? path, string separator)
    {
        Assert.True(JoinFormat.TryParse(format, out JoinFormat? join));
        Assert.Equal(path, join.ItemPath);
        Assert.Equal(separator, join.Separator);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("joined")]
    [InlineData("number:N2")]
    [InlineData("join:\"open")]
    [InlineData("join:Name:x")]
    [InlineData("join:Na me")]
    [InlineData("join:Name:\"a\":\"b\"")]
    public void TryParse_InvalidFormat_ReturnsFalse(string? format)
    {
        Assert.False(JoinFormat.TryParse(format, out _));
    }

    public static TheoryData<object?, bool> CollectionCandidates => new()
    {
        { new List<string> { "a" }, true },
        { new[] { "a" }, true },
        { JsonDocument.Parse("[1]").RootElement, true },
        { "abc", false },
        { new Dictionary<string, object> { ["a"] = 1 }, false },
        { new ExpandoObject(), false },
        { JsonDocument.Parse("{\"a\":1}").RootElement, false },
        { 42, false },
        { null, false },
    };

    [Theory]
    [MemberData(nameof(CollectionCandidates))]
    public void IsCollection_ReturnsExpected(object? value, bool expected)
    {
        Assert.Equal(expected, JoinFormat.IsCollection(value));
    }

    [Fact]
    public void Join_Strings_DefaultSeparator() =>
        Assert.Equal("a, b, c", Join("join", new List<string> { "a", "b", "c" }));

    [Fact]
    public void Join_EmptySeparator() =>
        Assert.Equal("ABC", Join("join:\"\"", new List<string> { "A", "B", "C" }));

    [Fact]
    public void Join_SkipsNullAndEmpty() =>
        Assert.Equal("a, b", Join("join", new List<string?> { "a", null, "", "b" }));

    [Fact]
    public void Join_Property_FromDictionaries() =>
        Assert.Equal("A, B", Join("join:Name", new List<Dictionary<string, object>> { new() { ["Name"] = "A" }, new() { ["Name"] = "B" } }));

    [Fact]
    public void Join_NestedProperty_FromPocos() =>
        Assert.Equal("X, Y", Join("join:Owner.Name", new List<Asset> { new(new Owner("X")), new(new Owner("Y")) }));

    [Fact]
    public void Join_PropertyMissingOnSomeItems_SkipsThem() =>
        Assert.Equal("A, B", Join("join:Name", new List<Dictionary<string, object>>
        {
            new() { ["Name"] = "A" },
            new() { ["Other"] = 1 },
            new() { ["Name"] = "B" },
        }));

    [Fact]
    public void Join_Numbers_UseCulture() =>
        Assert.Equal("1,5; 2,25", Join("join:\"; \"", new List<decimal> { 1.5m, 2.25m }, new CultureInfo("de-DE")));

    [Fact]
    public void Join_JsonElementArray() =>
        Assert.Equal("x, y", Join("join", JsonDocument.Parse("[\"x\",\"y\"]").RootElement));

    [Fact]
    public void Join_EmptyCollection() =>
        Assert.Equal(string.Empty, Join("join", new List<string>()));

    [Fact]
    public void TryGetMissingItemVariable_PropertyOnNoItem_ReturnsQualifiedName()
    {
        List<Dictionary<string, object>> items = [new() { ["Nme"] = "A" }];

        Assert.True(JoinFormat.TryGetMissingItemVariable("Assets", items, "join:Name", out string? missing));
        Assert.Equal("Assets.Name", missing);
    }

    public static TheoryData<object?, string> NotMissing => new()
    {
        { new List<Dictionary<string, object>> { new() { ["Name"] = "A" }, new() { ["Other"] = 1 } }, "join:Name" },
        { new List<object>(), "join:Name" },
        { new List<object?> { null }, "join:Name" },
        { new List<Dictionary<string, object>> { new() { ["Nme"] = "A" } }, "join" },
        { "abc", "join:Name" },
    };

    [Theory]
    [MemberData(nameof(NotMissing))]
    public void TryGetMissingItemVariable_NotMissing_ReturnsFalse(object? value, string format)
    {
        Assert.False(JoinFormat.TryGetMissingItemVariable("Assets", value, format, out _));
    }

    private static string Join(string format, object collection, CultureInfo? culture = null)
    {
        Assert.True(JoinFormat.TryParse(format, out JoinFormat? join));
        return join.Join(collection, culture ?? CultureInfo.InvariantCulture);
    }

    public sealed record Owner(string Name);

    public sealed record Asset(Owner Owner);
}
