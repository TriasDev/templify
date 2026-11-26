// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using TriasDev.Templify.Builder.Json;

namespace TriasDev.Templify.Builder.Tests;

/// <summary>The width-based layout of exported JSON definitions.</summary>
public sealed class JsonLayoutTests
{
    [Fact]
    public void FitsOnOneLine_WhenItFits()
    {
        Assert.Equal("{\"a\": 1, \"b\": [\"x\", \"y\"]}\n", JsonLayout.Format("""{"a":1,"b":["x","y"]}"""));
    }

    [Fact]
    public void Width_CountsTheKey_NotTheIndentation()
    {
        // The member's key "\"k\": " is 5 characters; a 95-character value makes 100 exactly. The indentation (2 or 4
        // spaces) does not count, so a member reads the same at every depth.
        string fits = "[\"" + new string('x', 91) + "\"]";
        Assert.Equal(95, fits.Length);
        string tooLong = "[\"" + new string('x', 92) + "\"]";
        string Wrap(string value) => "{\"k\":" + value + ",\"other\":" + new string('1', 95) + "}";
        string Nested(string value) => "{\"a\":" + Wrap(value) + "}";

        Assert.Contains("  \"k\": " + fits + ",\n", JsonLayout.Format(Wrap(fits)), StringComparison.Ordinal);
        Assert.Contains("    \"k\": " + fits + ",\n", JsonLayout.Format(Nested(fits)), StringComparison.Ordinal);
        Assert.Contains("  \"k\": [\n    \"" + new string('x', 92) + "\"\n  ],\n", JsonLayout.Format(Wrap(tooLong)), StringComparison.Ordinal);
    }

    [Fact]
    public void Width_ArrayItems_CountOnlyThemselves()
    {
        string fits = "{\"p\":\"" + new string('y', 91) + "\"}";
        Assert.Equal(100, "{\"p\": \"".Length + 91 + 2);
        string json = "[[" + fits + ",\"" + new string('z', 100) + "\"]]";

        Assert.Contains("    {\"p\": \"" + new string('y', 91) + "\"},\n", JsonLayout.Format(json), StringComparison.Ordinal);
    }

    [Fact]
    public void Nested_OuterBreaks_InnerFits()
    {
        string json = "{\"content\":[{\"style\":\"Heading\",\"paragraph\":\"Title\"},\"" + new string('t', 100) + "\"]}";

        Assert.Equal(
            "{\n  \"content\": [\n    {\"style\": \"Heading\", \"paragraph\": \"Title\"},\n    \"" + new string('t', 100) + "\"\n  ]\n}\n",
            JsonLayout.Format(json));
    }

    [Fact]
    public void LongStrings_AreNotWrapped_EscapingIsKept()
    {
        string json = "{\"a\":\"" + new string('a', 120) + "\",\"q\":\"say \\\"ü\\\" \\\\ {{x}}\"}";

        string formatted = JsonLayout.Format(json);

        Assert.Contains("  \"a\": \"" + new string('a', 120) + "\",\n", formatted, StringComparison.Ordinal);
        Assert.Contains("  \"q\": \"say \\\"ü\\\" \\\\ {{x}}\"\n", formatted, StringComparison.Ordinal);
    }

    [Fact]
    public void EmptyContainers_AreInline()
    {
        string json = "{\"a\":[],\"b\":{},\"c\":\"" + new string('c', 100) + "\"}";

        Assert.Equal("{\n  \"a\": [],\n  \"b\": {},\n  \"c\": \"" + new string('c', 100) + "\"\n}\n", JsonLayout.Format(json));
    }

    [Fact]
    public void Format_IsIdempotent()
    {
        string json = "{\"content\":[{\"if\":\"a\",\"then\":[\"" + new string('x', 120) + "\"]},\"short\"],\"n\":1.5,\"t\":true,\"z\":null}";

        string once = JsonLayout.Format(json);

        Assert.Equal(once, JsonLayout.Format(once));
        Assert.Equal(once, JsonLayout.Format(json));
    }
}
